using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21))));

builder.Services.Configure<FileStorageSettings>(builder.Configuration.GetSection("FileStorageSettings"));
builder.Services.AddScoped<IFileStorageService, FileSystemStorageService>();
builder.Services.AddScoped<ElementWorkflowService>();

var app = builder.Build();

await EnsureDatabaseCompatibilityAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

static async Task EnsureDatabaseCompatibilityAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var connection = context.Database.GetDbConnection();

    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using var columnCommand = connection.CreateCommand();
    columnCommand.CommandText = """
        SELECT COUNT(*)
        FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'users'
          AND column_name = 'role_id';
        """;

    var columnExists = Convert.ToInt32(await columnCommand.ExecuteScalarAsync()) > 0;
    if (!columnExists)
    {
        await ExecuteNonQueryAsync(connection, "ALTER TABLE users ADD COLUMN role_id INT NOT NULL DEFAULT 1;");
    }

    var managerAssignedByExists = await ColumnExistsAsync(connection, "educational_program_managers", "assigned_by_user_id");
    if (!managerAssignedByExists)
    {
        await ExecuteNonQueryAsync(connection, "ALTER TABLE educational_program_managers ADD COLUMN assigned_by_user_id INT NULL;");
    }

    var managerAssignedAtExists = await ColumnExistsAsync(connection, "educational_program_managers", "assigned_at");
    if (!managerAssignedAtExists)
    {
        await ExecuteNonQueryAsync(connection, "ALTER TABLE educational_program_managers ADD COLUMN assigned_at DATETIME NULL;");
    }

    await ExecuteNonQueryAsync(connection, """
        UPDATE users
        SET role_id = CASE link_role
            WHEN 'Manager' THEN 1
            WHEN 'Approver' THEN 2
            WHEN 'Moderator' THEN 3
            WHEN 'Admin' THEN 4
            ELSE 1
        END
        WHERE role_id IS NULL OR role_id = 0 OR role_id = 1;
        """);

    await ExecuteNonQueryAsync(connection, """
        UPDATE educational_program_managers
        SET assigned_by_user_id = COALESCE(assigned_by_user_id, 4),
            assigned_at = COALESCE(assigned_at, NOW())
        WHERE assigned_by_user_id IS NULL OR assigned_at IS NULL;
        """);
}

static async Task ExecuteNonQueryAsync(System.Data.Common.DbConnection connection, string commandText)
{
    await using var command = connection.CreateCommand();
    command.CommandText = commandText;
    await command.ExecuteNonQueryAsync();
}

static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string tableName, string columnName)
{
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT COUNT(*)
        FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = @tableName
          AND column_name = @columnName;
        """;

    var tableParam = command.CreateParameter();
    tableParam.ParameterName = "@tableName";
    tableParam.Value = tableName;
    command.Parameters.Add(tableParam);

    var columnParam = command.CreateParameter();
    columnParam.ParameterName = "@columnName";
    columnParam.Value = columnName;
    command.Parameters.Add(columnParam);

    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
}
