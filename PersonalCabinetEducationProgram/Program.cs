using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IEducationalProgramElementsRepository, EducationalProgramElementsRepository>();
builder.Services.AddScoped<IEducationalProgramRepository, EducationalProgramRepository>();
builder.Services.AddScoped<IFacultysRepository, FacultysRepository>();
builder.Services.AddScoped<IDepartmentsRepository, DepartmentsRepository>();
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddDbContext<ApplicationDbContext>();
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    options.Password.RequiredLength = 4;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var userManager = services.GetRequiredService<UserManager<User>>();

    string[] roleNames = { "РуководительОПОП", "Согласующий", "Модератор", "Администратор" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole<int>(roleName));
    }

    var adminEmail = "admin@edu.ru";
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new User
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Администраторова А.А.",
            Post = "Администратор",
            LinkRole = "4"
        };
        await userManager.CreateAsync(admin, "1234");
        await userManager.AddToRoleAsync(admin, "Администратор");
    }

    var managerEmail = "manager@edu.ru";
    if (await userManager.FindByEmailAsync(managerEmail) == null)
    {
        var manager = new User
        {
            UserName = managerEmail,
            Email = managerEmail,
            FullName = "Иванов Иван Иванович",
            Post = "Заведующий кафедрой",
            LinkRole = "1"
        };
        await userManager.CreateAsync(manager, "1234");
        await userManager.AddToRoleAsync(manager, "РуководительОПОП");
    }

    var approverEmail = "approver@edu.ru";
    if (await userManager.FindByEmailAsync(approverEmail) == null)
    {
        var approver = new User
        {
            UserName = approverEmail,
            Email = approverEmail,
            FullName = "Петров Петр Петрович",
            Post = "Доцент",
            LinkRole = "2"
        };
        await userManager.CreateAsync(approver, "1234");
        await userManager.AddToRoleAsync(approver, "Согласующий");
    }

    var moderatorEmail = "moderator@edu.ru";
    if (await userManager.FindByEmailAsync(moderatorEmail) == null)
    {
        var moderator = new User
        {
            UserName = moderatorEmail,
            Email = moderatorEmail,
            FullName = "Сидорова Анна Сергеевна",
            Post = "Начальник учебного отдела",
            LinkRole = "3"
        };
        await userManager.CreateAsync(moderator, "1234");
        await userManager.AddToRoleAsync(moderator, "Модератор");
    }
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
    pattern: "{controller=ManagerHome}/{action=Index}/{id?}");

app.Run();
