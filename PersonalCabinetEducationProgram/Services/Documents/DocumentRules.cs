using System.Globalization;
using System.Text.RegularExpressions;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services.Documents;

public static class DocumentRules
{
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
    public static List<DocumentFinding> Check(IReadOnlyList<PdfPageText> pages,
        EducationalProgram program, DocumentRequirementTemplate? template)
    {
        var findings = new List<DocumentFinding>();
        var readable = pages.Where(p => !string.IsNullOrWhiteSpace(p.Text) && !p.LowReadability).ToList();
        if (readable.Count == 0) return findings;
        var complete = pages.All(p => p.IsBlank || (!string.IsNullOrWhiteSpace(p.Text) && !p.LowReadability));
        var allText = string.Join("\n", readable.Select(p => p.Text));
        if (template != null && complete)
        {
            foreach (var item in template.RequiredSections.Concat(template.RequiredFields))
            {
                if (Normalize(allText).Contains(Normalize(item), StringComparison.OrdinalIgnoreCase)) continue;
                var anchor = readable[0];
                Add("Не найден обязательный раздел или реквизит", $"По шаблону «{template.Id}», версия {template.Version}, требуется «{item}». Точное название не найдено в извлечённом тексте. Цитата — начало документа, а не доказательство отсутствия; проверьте альтернативное название.",
                    "RequiredContent", anchor, Excerpt(anchor.Text), .75, $"Проверьте наличие «{item}» и соответствие шаблону организации.");
            }
        }
        foreach (var page in readable)
        {
            foreach (Match match in Regex.Matches(page.Text, @"\b\d{2}\.\d{2}\.\d{4}\b", RegexOptions.None, RegexTimeout))
                if (!DateOnly.TryParseExact(match.Value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    Add("Некорректная календарная дата", "Дата не существует в календаре.", "InvalidDate", page, match.Value, 1, "Уточните день, месяц и год по исходным данным.");
            foreach (Match match in Regex.Matches(page.Text, @"\b\d{2}\.\d{2}\.\d{2}\b", RegexOptions.None, RegexTimeout))
                if (!string.IsNullOrWhiteSpace(program.CodeReferral) && match.Value != program.CodeReferral)
                    Add("Шифр отличается от карточки ОПОП", $"В карточке программы указан шифр {program.CodeReferral}. Возможно, в PDF приведён связанный шифр — проверьте контекст.",
                        "ProgramCode", page, match.Value, .85, "Сверьте шифр направления с карточкой ОПОП.");

            foreach (Match match in Regex.Matches(page.Text, @"(?im)^\s*(?:Название программы|Наименование программы)\s*[:—-]\s*(?<value>[^\r\n]{3,250})", RegexOptions.None, RegexTimeout))
                if (Normalize(match.Groups["value"].Value) != Normalize(program.Name))
                    Add("Название программы отличается от карточки ОПОП", $"В карточке указано: {program.Name}. Проверьте, относится ли строка к этой программе.",
                        "ProgramName", page, match.Value, .85, "Согласуйте название программы в PDF и карточке ОПОП.");

            foreach (Match match in Regex.Matches(page.Text, @"(?<a>\d{1,7})\s*\+\s*(?<b>\d{1,7})\s*=\s*(?<total>\d{1,8})(?:\s*(?:ч\.|час\w*))?", RegexOptions.None, RegexTimeout))
                if (long.Parse(match.Groups["a"].Value) + long.Parse(match.Groups["b"].Value) != long.Parse(match.Groups["total"].Value))
                    Add("Арифметическая сумма не сходится", "Сумма двух явно указанных слагаемых отличается от результата после знака равенства.",
                        "Arithmetic", page, match.Value, 1, "Пересчитайте сумму и проверьте исходные значения.");
        }
        // Only compare repeated, explicit labels. Different unlabeled dates/hours aren't necessarily contradictory.
        var values = readable.SelectMany(p => Regex.Matches(p.Text,
                @"(?im)^\s*(?<label>Дата реализации программы|Год начала реализации|Всего часов|Общая трудоёмкость|Итого часов)\s*[:—-]\s*(?<value>[^\r\n]{1,150})",
                RegexOptions.None, RegexTimeout).Select(m => new { Page = p, Match = m, Label = Normalize(m.Groups["label"].Value), Value = Normalize(m.Groups["value"].Value) }));
        foreach (var group in values.GroupBy(x => x.Label).Where(g => g.Select(x => x.Value).Distinct().Count() > 1))
        {
            var first = group.First();
            foreach (var other in group.Where(x => x.Value != first.Value))
                Add("Противоречащие значения одного реквизита", $"Реквизит «{group.Key}» отличается от страницы {first.Page.Number}: «{Excerpt(first.Match.Value)}». Уточните контекст и единицы измерения.",
                    "ConflictingField", other.Page, other.Match.Value, .9, "Проверьте оба значения и приведите одинаковый реквизит к согласованному значению.");
        }
        return findings;

        void Add(string title, string description, string type, PdfPageText page, string quote, double confidence, string suggestion)
        {
            findings.Add(new() { Title = title, Description = description, Type = type, PageNumber = page.Number,
                Quote = Excerpt(quote), Confidence = page.FromOcr ? Math.Min(confidence, .7) : confidence,
                FromOcr = page.FromOcr, Suggestion = suggestion });
        }
    }

    public static bool HasValidEvidence(DocumentFinding finding, IReadOnlyList<PdfPageText> pages) =>
        finding.Title is { Length: > 0 and <= 200 } && finding.Description is { Length: > 0 and <= 1500 } &&
        finding.Suggestion is { Length: <= 1500 } && finding.Type is { Length: > 0 and <= 100 } &&
        finding.Quote is { Length: > 0 and <= 600 } && !string.IsNullOrWhiteSpace(finding.Quote) &&
        double.IsFinite(finding.Confidence) && finding.Confidence is >= 0 and <= 1 &&
        pages.Any(p => p.Number == finding.PageNumber && !p.LowReadability && p.Text.Contains(finding.Quote, StringComparison.Ordinal));

    public static string Excerpt(string text) => text[..Math.Min(600, text.Length)];
    public static string Normalize(string text) => Regex.Replace(text, @"\s+", " ", RegexOptions.None, RegexTimeout).Trim().ToLowerInvariant();
}
