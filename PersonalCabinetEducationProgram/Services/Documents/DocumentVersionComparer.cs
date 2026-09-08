using System.Text.RegularExpressions;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services.Documents;

public static class DocumentVersionComparer
{
    public static List<DocumentPageChange> Compare(IReadOnlyList<PdfPageText> before, IReadOnlyList<PdfPageText> after)
    {
        // LCS aligns unchanged pages, so inserting a page doesn't mark the entire PDF changed.
        var a = before.Select(p => DocumentRules.Normalize(p.Text)).ToArray();
        var b = after.Select(p => DocumentRules.Normalize(p.Text)).ToArray();
        var lengths = new int[a.Length + 1, b.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
            for (var j = b.Length - 1; j >= 0; j--)
                lengths[i, j] = a[i] == b[j] ? 1 + lengths[i + 1, j + 1] : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
        var changes = new List<DocumentPageChange>();
        var removed = new List<PdfPageText>();
        var added = new List<PdfPageText>();
        int x = 0, y = 0;
        while (x < a.Length || y < b.Length)
        {
            if (x < a.Length && y < b.Length && a[x] == b[y]) { Flush(); x++; y++; }
            else if (x < a.Length && (y == b.Length || lengths[x + 1, y] >= lengths[x, y + 1])) removed.Add(before[x++]);
            else added.Add(after[y++]);
        }
        Flush();
        return changes;

        void Flush()
        {
            for (var i = 0; i < Math.Max(removed.Count, added.Count); i++)
            {
                var oldPage = removed.ElementAtOrDefault(i);
                var newPage = added.ElementAtOrDefault(i);
                var oldText = oldPage?.Text ?? "";
                var newText = newPage?.Text ?? "";
                var prefix = 0;
                while (prefix < Math.Min(oldText.Length, newText.Length) && oldText[prefix] == newText[prefix]) prefix++;
                var start = Math.Max(0, prefix - 60);
                changes.Add(new() { OldPageNumber = oldPage?.Number, NewPageNumber = newPage?.Number,
                    Kind = oldPage == null ? "Added" : newPage == null ? "Removed" : "Changed",
                    BeforeQuote = DocumentRules.Excerpt(oldText[start..]), AfterQuote = DocumentRules.Excerpt(newText[start..]),
                    Description = Describe(oldText, newText) });
            }
            removed.Clear(); added.Clear();
        }
    }

    private static string Describe(string before, string after)
    {
        var categories = new List<string> { "текст/структура страницы" };
        foreach (var (name, pattern) in new[] { ("даты", @"\b\d{2}\.\d{2}\.\d{4}\b"), ("шифры", @"\b\d{2}\.\d{2}\.\d{2}\b"),
                     ("часы/числа и строки таблиц", @"\d+(?:[,.]\d+)?"), ("заголовки/реквизиты", @"(?m)^[^\r\n]{1,100}[:\n]") })
        {
            string Values(string text) => string.Join("|", Regex.Matches(text, pattern, RegexOptions.None, DocumentRules.RegexTimeout).Select(m => m.Value));
            if (Values(before) != Values(after)) categories.Add(name);
        }
        return "Изменены: " + string.Join(", ", categories) + ". Сопоставление текстовое; оформление и ячейки таблиц проверьте в PDF.";
    }
}
