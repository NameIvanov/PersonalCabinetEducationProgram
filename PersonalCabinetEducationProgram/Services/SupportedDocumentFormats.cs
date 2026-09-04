namespace PersonalCabinetEducationProgram.Services;

public static class SupportedDocumentFormats
{
    public const string PdfContentType = "application/pdf";

    public static bool IsSupported(string? fileName) =>
        string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase);
}
