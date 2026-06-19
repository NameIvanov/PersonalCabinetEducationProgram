using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PersonalCabinetEducationProgram.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "personal_cabinet");

            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code_department = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "facultys",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facultys", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    concurrency_stamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    full_name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    post = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approval_status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rejection_reason = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    username = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_username = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    email_confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    password_hash = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    security_stamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    concurrency_stamp = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone_number = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone_confirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    access_failed_count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    role_id = table.Column<int>(type: "int", nullable: false),
                    claim_type = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    claim_value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "fk_rc_role",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "approver_assignments",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    approver_user_id = table.Column<int>(type: "int", nullable: false),
                    faculty_id = table.Column<int>(type: "int", nullable: true),
                    department_id = table.Column<int>(type: "int", nullable: true),
                    assigned_by_user_id = table.Column<int>(type: "int", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approver_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "fk_appr_by",
                        column: x => x.assigned_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_appr_dept",
                        column: x => x.department_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_appr_fac",
                        column: x => x.faculty_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "facultys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_appr_user",
                        column: x => x.approver_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "educational_programs",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code_referral = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    educational_level = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    year_approvals = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_educational_programs", x => x.Id);
                    table.ForeignKey(
                        name: "fk_prog_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    claim_type = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    claim_value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "fk_uc_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_key = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_ul_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    role_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_ur_role",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ur_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    login_provider = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    value = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_ut_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "educational_program_assignments",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    educational_program_id = table.Column<int>(type: "int", nullable: false),
                    department_id = table.Column<int>(type: "int", nullable: false),
                    faculty_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_educational_program_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "fk_epa_dept",
                        column: x => x.department_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_epa_fac",
                        column: x => x.faculty_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "facultys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_epa_prog",
                        column: x => x.educational_program_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "educational_program_elements",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    educational_program_id = table.Column<int>(type: "int", nullable: false),
                    type_element = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    upload_date = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status_approvals = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_path = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_educational_program_elements", x => x.Id);
                    table.ForeignKey(
                        name: "fk_elem_prog",
                        column: x => x.educational_program_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "educational_program_managers",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    educational_program_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    assigned_by_user_id = table.Column<int>(type: "int", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_educational_program_managers", x => x.Id);
                    table.ForeignKey(
                        name: "fk_mgr_by",
                        column: x => x.assigned_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_mgr_prog",
                        column: x => x.educational_program_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mgr_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "comments_educational_program_element",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    educational_program_element_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    date_time_comment = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    comment_content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments_educational_program_element", x => x.Id);
                    table.ForeignKey(
                        name: "fk_comm_elem",
                        column: x => x.educational_program_element_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_program_elements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_comm_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "element_status_history",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    educational_program_element_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    old_status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    new_status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    change_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    comment = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_element_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "fk_hist_elem",
                        column: x => x.educational_program_element_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_program_elements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_hist_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                schema: "personal_cabinet",
                table: "departments",
                columns: new[] { "Id", "code_department", "Name" },
                values: new object[,]
                {
                    { 1, "Каф.ПМИ", "Кафедра прикладной математики и информатики" },
                    { 2, "Каф.ИВТ", "Кафедра информационных вычислительных технологий" },
                    { 3, "Каф.МАТЕМ", "Кафедра математического анализа" }
                });

            migrationBuilder.InsertData(
                schema: "personal_cabinet",
                table: "facultys",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Факультет информационных технологий" },
                    { 2, "Факультет математики и механики" },
                    { 3, "Факультет педагогического образования" }
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "Id", "concurrency_stamp", "Description", "Name", "normalized_name" },
                values: new object[,]
                {
                    { 1, "role-manager", "Руководитель ОПОП", "Manager", "MANAGER" },
                    { 2, "role-approver", "Согласующий", "Approver", "APPROVER" },
                    { 3, "role-moderator", "Модератор", "Moderator", "MODERATOR" },
                    { 4, "role-admin", "Администратор", "Admin", "ADMIN" }
                });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "Id", "access_failed_count", "approval_status", "concurrency_stamp", "email", "email_confirmed", "full_name", "lockout_enabled", "lockout_end", "normalized_email", "normalized_username", "password_hash", "phone_number", "phone_confirmed", "post", "rejection_reason", "security_stamp", "two_factor_enabled", "username" },
                values: new object[,]
                {
                    { 1, 0, "Approved", "user-1", null, false, "Иванов Иван Иванович", true, null, null, "MANAGER", "866485796cfa8d7c0cf7111640205b83076433547577511d81f8030ae99ecea5", null, false, "Заведующий кафедрой", null, "security-1", false, "manager" },
                    { 2, 0, "Approved", "user-2", null, false, "Петрова Анна Сергеевна", true, null, null, "APPROVER", "1c391319644c0c6e9f5955e44e55862a8fd27b3b9d9863456500096ccf512db3", null, false, "Декан факультета", null, "security-2", false, "approver" },
                    { 3, 0, "Approved", "user-3", null, false, "Сидоров Петр Алексеевич", true, null, null, "MODERATOR", "4c8425b174053ea6935b29c2b0e0aa4e2eab1a01b784e6ac91b8bdce9c26235a", null, false, "Модератор", null, "security-3", false, "moderator" },
                    { 4, 0, "Approved", "user-4", null, false, "Козлова Мария Ивановна", true, null, null, "ADMIN", "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9", null, false, "Администратор", null, "security-4", false, "admin" }
                });

            migrationBuilder.InsertData(
                schema: "personal_cabinet",
                table: "educational_programs",
                columns: new[] { "Id", "code_referral", "educational_level", "Name", "Status", "user_id", "year_approvals" },
                values: new object[,]
                {
                    { 1, "01.03.02", "Бакалавриат", "Прикладная математика и информатика", "Разрабатывается", 1, null },
                    { 2, "09.03.01", "Бакалавриат", "Информатика и вычислительная техника", "Разрабатывается", 1, null },
                    { 3, "44.03.05", "Бакалавриат", "Педагогическое образование (Математика. Информатика)", "Разрабатывается", 1, null }
                });

            migrationBuilder.InsertData(
                table: "user_roles",
                columns: new[] { "role_id", "user_id" },
                values: new object[,]
                {
                    { 1, 1 },
                    { 2, 2 },
                    { 3, 3 },
                    { 4, 4 }
                });

            migrationBuilder.InsertData(
                schema: "personal_cabinet",
                table: "educational_program_assignments",
                columns: new[] { "Id", "department_id", "educational_program_id", "faculty_id" },
                values: new object[,]
                {
                    { 1, 1, 1, 1 },
                    { 2, 2, 2, 1 },
                    { 3, 1, 3, 3 }
                });

            migrationBuilder.InsertData(
                schema: "personal_cabinet",
                table: "educational_program_elements",
                columns: new[] { "Id", "Description", "educational_program_id", "file_name", "file_path", "Name", "status_approvals", "type_element", "upload_date" },
                values: new object[,]
                {
                    { 1, "Основной учебный план", 1, null, null, "Учебный план (очный)", "", "Main", null },
                    { 2, "Общая информация", 1, null, null, "Пояснительная записка", "На доработку", "Main", null },
                    { 3, "График обучения", 1, null, null, "Календарный учебный график", "", "Main", null },
                    { 4, "Воспитательная программа", 1, null, null, "Программа воспитательной работы", "", "Main", null },
                    { 5, "Календарный план", 1, null, null, "Календарный план воспитательной работы", "", "Main", null },
                    { 6, "Б1.О.01", 1, null, null, "Философия", "Согласовано", "Discipline", null },
                    { 7, "Б1.О.02", 1, null, null, "Математический анализ", "На согласовании", "Discipline", null },
                    { 8, "Б1.О.03", 1, null, null, "Линейная алгебра", "", "Discipline", null },
                    { 9, "Б1.О.04", 1, null, null, "Программирование", "Согласовано", "Discipline", null },
                    { 10, "Б1.О.05", 1, null, null, "Базы данных", "", "Discipline", null },
                    { 11, "Практика 1", 1, null, null, "Учебная практика", "", "Practice", null },
                    { 12, "Практика 2", 1, null, null, "Производственная практика", "", "Practice", null },
                    { 13, "ГИА", 1, null, null, "Государственный экзамен", "", "GIA", null },
                    { 14, "ВКР", 1, null, null, "Выпускная квалификационная работа", "", "GIA", null }
                });

            migrationBuilder.InsertData(
                schema: "personal_cabinet",
                table: "educational_program_managers",
                columns: new[] { "Id", "assigned_at", "assigned_by_user_id", "educational_program_id", "user_id" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 5, 30, 10, 0, 0, 0, DateTimeKind.Unspecified), 4, 1, 1 },
                    { 2, new DateTime(2026, 5, 30, 10, 5, 0, 0, DateTimeKind.Unspecified), 4, 2, 1 },
                    { 3, new DateTime(2026, 5, 30, 10, 10, 0, 0, DateTimeKind.Unspecified), 4, 3, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_appr_by",
                schema: "personal_cabinet",
                table: "approver_assignments",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_appr_dept",
                schema: "personal_cabinet",
                table: "approver_assignments",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_appr_fac",
                schema: "personal_cabinet",
                table: "approver_assignments",
                column: "faculty_id");

            migrationBuilder.CreateIndex(
                name: "ix_appr_user",
                schema: "personal_cabinet",
                table: "approver_assignments",
                column: "approver_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_comm_elem",
                schema: "personal_cabinet",
                table: "comments_educational_program_element",
                column: "educational_program_element_id");

            migrationBuilder.CreateIndex(
                name: "ix_comm_user",
                schema: "personal_cabinet",
                table: "comments_educational_program_element",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_epa_dept",
                schema: "personal_cabinet",
                table: "educational_program_assignments",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_epa_fac",
                schema: "personal_cabinet",
                table: "educational_program_assignments",
                column: "faculty_id");

            migrationBuilder.CreateIndex(
                name: "ix_epa_prog",
                schema: "personal_cabinet",
                table: "educational_program_assignments",
                column: "educational_program_id");

            migrationBuilder.CreateIndex(
                name: "ix_elem_prog",
                schema: "personal_cabinet",
                table: "educational_program_elements",
                column: "educational_program_id");

            migrationBuilder.CreateIndex(
                name: "ix_mgr_by",
                schema: "personal_cabinet",
                table: "educational_program_managers",
                column: "assigned_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mgr_prog",
                schema: "personal_cabinet",
                table: "educational_program_managers",
                column: "educational_program_id");

            migrationBuilder.CreateIndex(
                name: "ix_mgr_user",
                schema: "personal_cabinet",
                table: "educational_program_managers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_prog_user",
                schema: "personal_cabinet",
                table: "educational_programs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_hist_elem",
                schema: "personal_cabinet",
                table: "element_status_history",
                column: "educational_program_element_id");

            migrationBuilder.CreateIndex(
                name: "ix_hist_user",
                schema: "personal_cabinet",
                table: "element_status_history",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rc_role",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_roles_name",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_uc_user",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ul_user",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ur_role",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ux_users_name",
                table: "users",
                column: "normalized_username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approver_assignments",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "comments_educational_program_element",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "educational_program_assignments",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "educational_program_managers",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "element_status_history",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "departments",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "facultys",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "educational_program_elements",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "educational_programs",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
