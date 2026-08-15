using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<DownloadQuotaFilter>();
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = FileUploadLimits.MaxRequestSizeBytes);
builder.Services.Configure<IISServerOptions>(options =>
    options.MaxRequestBodySize = FileUploadLimits.MaxRequestSizeBytes);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = FileUploadLimits.MaxRequestSizeBytes);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = Math.Max(1, builder.Configuration.GetValue<int?>("TrustedProxies:ForwardLimit") ?? 1);
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
    foreach (var value in builder.Configuration.GetSection("TrustedProxies:KnownProxies").Get<string[]>() ?? [])
    {
        if (System.Net.IPAddress.TryParse(value, out var address))
            options.KnownProxies.Add(address);
    }
});
builder.Services.AddRateLimiter(AppRateLimiterConfiguration.Configure);
builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) && builder.Environment.IsEnvironment("Testing"))
    connectionString = "Server=localhost;Database=integration_test;User Id=test;Password=test;";
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 21)),
        mySqlOptions => mySqlOptions.SchemaBehavior(MySqlSchemaBehavior.Ignore)));

builder.Services
    .AddIdentity<User, Role>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnRedirectToAccessDenied = context =>
    {
        // Cookie authentication converts Forbid() into a 302 redirect. Record the
        // denial here because the request logger otherwise only sees the redirect.
        if (!ObjectAuthorizationIncidentService.WasRecorded(context.HttpContext))
        {
            var securityEvents = context.HttpContext.RequestServices
                .GetRequiredService<SecurityEventService>();
            securityEvents.Record(
                SecurityEventTypes.AccessDenied,
                SecurityEventSeverities.High,
                "Отказ в доступе",
                $"Доступ к {context.Request.Path}{context.Request.QueryString} запрещён политикой авторизации.");
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, ApplicationClaimsPrincipalFactory>();
builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection("FileStorageSettings"));
builder.Services.Configure<SecurityMonitoringOptions>(builder.Configuration.GetSection(SecurityMonitoringOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IIpGeolocationService, IpGeolocationService>();
builder.Services.AddScoped<IFileStorageService, FileSystemStorageService>();
builder.Services.AddScoped<ElementWorkflowService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ElementAccessService>();
builder.Services.AddScoped<ObjectAuthorizationIncidentService>();
builder.Services.AddScoped<ProtectedObjectProbeDetector>();
builder.Services.AddScoped<IpAddressSecurityService>();
builder.Services.AddScoped<ElementListQueryService>();
builder.Services.AddScoped<ElementFilterService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SecurityEventService>();
builder.Services.AddScoped<AccountSecurityService>();
builder.Services.AddScoped<LoginSecurityService>();
builder.Services.AddSingleton<IIpNetworkService, IpNetworkService>();
builder.Services.AddScoped<SystemHealthService>();
builder.Services.AddScoped<StorageHealthService>();
builder.Services.AddScoped<PlxParserService>();
builder.Services.AddScoped<PlxImportStorageService>();
builder.Services.AddScoped<CurriculumImportService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SystemLogQueue>();
builder.Services.AddSingleton<RequestActivityTracker>();
builder.Services.AddSingleton<SuspiciousActivityMonitor>();
builder.Services.AddSingleton<SecurityBlockedAccountRegistry>();
builder.Services.AddSingleton<IpAddressBlockRegistry>();
builder.Services.AddHostedService<SystemLogWriterService>();
builder.Services.AddHostedService<LogRetentionService>();
builder.Services.Configure<DownloadQuotaOptions>(_ => { });
builder.Services.AddSingleton<DownloadQuotaService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (dbContext.Database.IsRelational())
        await dbContext.Database.MigrateAsync();
    else
        await dbContext.Database.EnsureCreatedAsync();

    var blockedAccounts = scope.ServiceProvider.GetRequiredService<SecurityBlockedAccountRegistry>();
    var blockedUserIds = await dbContext.Users
        .Where(user => user.SecurityBlockedAtUtc != null)
        .Select(user => user.Id)
        .ToListAsync();
    foreach (var userId in blockedUserIds)
        blockedAccounts.Block(userId);

    var blockedIpAddresses = scope.ServiceProvider.GetRequiredService<IpAddressBlockRegistry>();
    var nowUtc = DateTime.UtcNow;
    var ipBlocks = await dbContext.IpAddressSecurityStates
        .Where(state => state.IsPermanentlyBlocked || state.BlockedUntilUtc > nowUtc)
        .Select(state => new
        {
            state.IpAddress,
            state.IsPermanentlyBlocked,
            state.BlockedUntilUtc,
            state.EscalationLevel
        })
        .ToListAsync();
    foreach (var state in ipBlocks)
    {
        blockedIpAddresses.Set(
            state.IpAddress,
            state.IsPermanentlyBlocked,
            state.BlockedUntilUtc,
            state.EscalationLevel);
    }
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<IpAddressSecurityMiddleware>();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseStaticFiles();
app.UseMiddleware<UserLoginSessionMiddleware>();
app.UseMiddleware<SecurityBlockedAccountMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();

public partial class Program;
