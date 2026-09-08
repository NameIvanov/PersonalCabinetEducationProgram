using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace PersonalCabinetEducationProgram.Tests;

public sealed class DocumentAnalysisMigrationTests
{
    [Fact]
    public void MigrationMatchesModel_GeneratesMySqlTablesAndUniqueQueueLimits()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(
            "Server=localhost;Database=design_only;Uid=design;Pwd=design;", new MySqlServerVersion(new Version(8, 0, 21)),
            mysql => mysql.SchemaBehavior(MySqlSchemaBehavior.Ignore)).Options;
        using var db = new ApplicationDbContext(options);
        Assert.Contains("20260907203923_AddDocumentAnalysis", db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript("20260907201000_AddPersistentAiAssistantState", "20260907203923_AddDocumentAnalysis");
        foreach (var table in new[] { "document_analyses", "document_findings", "document_finding_feedback", "document_page_diagnostics", "document_page_changes" })
            Assert.Contains($"CREATE TABLE `{table}`", sql);
        Assert.Contains("`TemplateSnapshot` longtext", sql);
        foreach (var column in new[] { "ActiveKey", "QueueSlot", "UserQueueKey" })
            Assert.Contains($"CREATE UNIQUE INDEX `IX_document_analyses_{column}`", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain("CREATE TABLE `admin_ai", sql);
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(DocumentAnalysis))!.GetProperties(), p => p.Name is "FullText" or "PdfText");
    }
}
