using Microsoft.Net.Http.Headers;

namespace PersonalCabinetEducationProgram.Services;

public static class FileContentDisposition
{
    public static void SetInline(HttpResponse response, string? fileName)
    {
        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(NormalizeFileName(fileName));
        response.Headers.ContentDisposition = disposition.ToString();
    }

    private static string NormalizeFileName(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        name = string.Concat(name.Where(character => !char.IsControl(character)));
        return string.IsNullOrWhiteSpace(name) ? "preview" : name;
    }
}
