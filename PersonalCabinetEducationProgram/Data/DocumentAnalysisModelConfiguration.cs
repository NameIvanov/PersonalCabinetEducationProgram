using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Data;

public static class DocumentAnalysisModelConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<DocumentAnalysis>(b =>
        {
            b.ToTable("document_analyses");
            b.Property(x => x.TemplateSnapshot).HasColumnType("longtext");
            b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PreviousFile).WithMany().HasForeignKey(x => x.PreviousFileId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.QueueSlot).IsUnique();
            b.HasIndex(x => x.ActiveKey).IsUnique();
            b.HasIndex(x => x.UserQueueKey).IsUnique();
            b.HasIndex(x => new { x.FileId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.Status, x.DeadlineUtc });
        });
        model.Entity<DocumentFinding>(b =>
        {
            b.ToTable("document_findings");
            b.HasOne(x => x.Analysis).WithMany(x => x.Findings).HasForeignKey(x => x.DocumentAnalysisId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<DocumentFindingFeedback>(b =>
        {
            b.ToTable("document_finding_feedback");
            b.HasOne(x => x.Finding).WithMany(x => x.Feedback).HasForeignKey(x => x.DocumentFindingId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
        model.Entity<DocumentPageDiagnostic>(b =>
        {
            b.ToTable("document_page_diagnostics");
            b.HasOne(x => x.Analysis).WithMany(x => x.Pages).HasForeignKey(x => x.DocumentAnalysisId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.DocumentAnalysisId, x.PageNumber }).IsUnique();
        });
        model.Entity<DocumentPageChange>(b =>
        {
            b.ToTable("document_page_changes");
            b.HasOne(x => x.Analysis).WithMany(x => x.Changes).HasForeignKey(x => x.DocumentAnalysisId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
