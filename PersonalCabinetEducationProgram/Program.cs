using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = FileUploadLimits.MaxFileSizeBytes);
builder.Services.Configure<IISServerOptions>(options =>
    options.MaxRequestBodySize = FileUploadLimits.MaxRequestSizeBytes);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = FileUploadLimits.MaxRequestSizeBytes);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

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
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, ApplicationClaimsPrincipalFactory>();
builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection("FileStorageSettings"));
builder.Services.AddScoped<IFileStorageService, FileSystemStorageService>();
builder.Services.AddScoped<ElementWorkflowService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
