using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_analyses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FileId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: true),
                    RequestedByName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    FileRevision = table.Column<int>(type: "int", nullable: false),
                    FileHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviousFileId = table.Column<int>(type: "int", nullable: true),
                    PreviousFileHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeadlineUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    QueueSlot = table.Column<int>(type: "int", nullable: true),
                    ActiveKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserQueueKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyToken = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Message = table.Column<string>(type: "varchar(1500)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Summary = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TemplateSnapshot = table.Column<string>(type: "longtext", maxLength: 16000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EngineVersion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProviderModel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_analyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_analyses_educational_program_element_files_FileId",
                        column: x => x.FileId,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_program_element_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_analyses_educational_program_element_files_Previous~",
                        column: x => x.PreviousFileId,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_program_element_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_analyses_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "document_findings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DocumentAnalysisId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(1500)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    Quote = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Confidence = table.Column<double>(type: "double", nullable: false),
                    FromOcr = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Suggestion = table.Column<string>(type: "varchar(1500)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConcurrencyToken = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_findings_document_analyses_DocumentAnalysisId",
                        column: x => x.DocumentAnalysisId,
                        principalTable: "document_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "document_page_changes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DocumentAnalysisId = table.Column<long>(type: "bigint", nullable: false),
                    OldPageNumber = table.Column<int>(type: "int", nullable: true),
                    NewPageNumber = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BeforeQuote = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AfterQuote = table.Column<string>(type: "varchar(600)", maxLength: 600, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_page_changes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_page_changes_document_analyses_DocumentAnalysisId",
                        column: x => x.DocumentAnalysisId,
                        principalTable: "document_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "document_page_diagnostics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DocumentAnalysisId = table.Column<long>(type: "bigint", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    HasTextLayer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsBlank = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LowReadability = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Rotation = table.Column<int>(type: "int", nullable: false),
                    FromOcr = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Message = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_page_diagnostics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_page_diagnostics_document_analyses_DocumentAnalysis~",
                        column: x => x.DocumentAnalysisId,
                        principalTable: "document_analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "document_finding_feedback",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DocumentFindingId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_finding_feedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_finding_feedback_document_findings_DocumentFindingId",
                        column: x => x.DocumentFindingId,
                        principalTable: "document_findings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_finding_feedback_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_document_analyses_ActiveKey",
                table: "document_analyses",
                column: "ActiveKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_analyses_FileId_CreatedAtUtc",
                table: "document_analyses",
                columns: new[] { "FileId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_document_analyses_PreviousFileId",
                table: "document_analyses",
                column: "PreviousFileId");

            migrationBuilder.CreateIndex(
                name: "IX_document_analyses_QueueSlot",
                table: "document_analyses",
                column: "QueueSlot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_analyses_RequestedByUserId",
                table: "document_analyses",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_document_analyses_UserQueueKey",
                table: "document_analyses",
                column: "UserQueueKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_analyses_Status_DeadlineUtc",
                table: "document_analyses",
                columns: new[] { "Status", "DeadlineUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_document_finding_feedback_DocumentFindingId",
                table: "document_finding_feedback",
                column: "DocumentFindingId");

            migrationBuilder.CreateIndex(
                name: "IX_document_finding_feedback_UserId",
                table: "document_finding_feedback",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_document_findings_DocumentAnalysisId",
                table: "document_findings",
                column: "DocumentAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_document_page_changes_DocumentAnalysisId",
                table: "document_page_changes",
                column: "DocumentAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_document_page_diagnostics_DocumentAnalysisId_PageNumber",
                table: "document_page_diagnostics",
                columns: new[] { "DocumentAnalysisId", "PageNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_finding_feedback");

            migrationBuilder.DropTable(
                name: "document_page_changes");

            migrationBuilder.DropTable(
                name: "document_page_diagnostics");

            migrationBuilder.DropTable(
                name: "document_findings");

            migrationBuilder.DropTable(
                name: "document_analyses");
        }
    }
}
