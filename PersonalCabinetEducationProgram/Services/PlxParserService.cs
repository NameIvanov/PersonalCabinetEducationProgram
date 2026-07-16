using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed partial class PlxParserService
    {
        public const long MaxPlxFileSizeBytes = 20L * 1024 * 1024;
        private const long MaxXmlCharacters = 40L * 1024 * 1024;

        private static readonly (string Key, string Name)[] MainTemplates =
        [
            ("main:general", "Общая характеристика ОПОП"),
            ("main:curriculum", "Учебный план"),
            ("main:schedule", "Календарный учебный график"),
            ("main:guidelines", "Методические рекомендации"),
            ("main:education-work", "Программа воспитательной работы"),
            ("main:education-calendar", "Календарный план воспитательной работы")
        ];

        public async Task<PlxImportPreview> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null || !stream.CanRead)
                throw new InvalidDataException("Файл PLX недоступен для чтения.");

            var settings = new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                MaxCharactersInDocument = MaxXmlCharacters
            };

            XDocument document;
            try
            {
                using var reader = XmlReader.Create(stream, settings);
                document = await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException("Файл не является корректным учебным планом PLX.", ex);
            }

            var root = document.Root;
            if (root == null || !root.Name.LocalName.Equals("Документ", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Корневой элемент PLX «Документ» не найден.");

            var warnings = new List<string>();
            var oop = FindElements(document, "ООП").FirstOrDefault();
            var plan = FindElements(document, "Планы").FirstOrDefault();
            var planCode = Attribute(oop, "Шифр");
            var planName = FirstNotEmpty(
                Attribute(plan, "Титул"),
                Attribute(oop, "Название"),
                Attribute(plan, "ИмяФайла"),
                Attribute(root, "LastName"));

            if (string.IsNullOrWhiteSpace(planCode))
                warnings.Add("В файле не найден шифр образовательной программы.");
            if (string.IsNullOrWhiteSpace(planName))
                warnings.Add("В файле не найдено наименование учебного плана.");

            var objectTypeNames = FindElements(document, "СправочникТипОбъекта")
                .Where(element => !string.IsNullOrWhiteSpace(Attribute(element, "Код")))
                .GroupBy(element => Attribute(element, "Код"))
                .ToDictionary(group => group.Key, group => Attribute(group.First(), "Название"));

            var workTypes = FindElements(document, "СправочникВидыРабот")
                .Where(element => !string.IsNullOrWhiteSpace(Attribute(element, "Код")))
                .GroupBy(element => Attribute(element, "Код"))
                .ToDictionary(
                    group => group.Key,
                    group => FirstNotEmpty(Attribute(group.First(), "Название"), Attribute(group.First(), "Наименование"), Attribute(group.First(), "Аббревиатура")));

            var elements = MainTemplates.Select(template => new PlxElementCandidate
            {
                ExternalKey = template.Key,
                TypeElement = EducationalProgramElementTypes.Main,
                Name = template.Name
            }).ToList();

            var rowsByObjectId = new Dictionary<string, PlxElementCandidate>(StringComparer.OrdinalIgnoreCase);
            var usedKeys = new HashSet<string>(elements.Select(element => element.ExternalKey), StringComparer.OrdinalIgnoreCase);
            var unknownTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var excludedRows = 0;

            foreach (var row in FindElements(document, "ПланыСтроки"))
            {
                if (IsFalse(Attribute(row, "СчитатьВПлане")))
                {
                    excludedRows++;
                    continue;
                }

                var name = Attribute(row, "Дисциплина").Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    excludedRows++;
                    continue;
                }

                var objectTypeCode = Attribute(row, "ТипОбъекта");
                objectTypeNames.TryGetValue(objectTypeCode, out var objectTypeName);
                var code = Attribute(row, "ДисциплинаКод").Trim();
                var type = ClassifyElementType(objectTypeCode, objectTypeName, code, name);
                if (type == null)
                {
                    unknownTypes.Add(string.IsNullOrWhiteSpace(objectTypeName)
                        ? $"код {objectTypeCode}"
                        : $"{objectTypeName} (код {objectTypeCode})");
                    excludedRows++;
                    continue;
                }

                var sourceObjectId = Attribute(row, "Код");
                var stablePart = !string.IsNullOrWhiteSpace(code)
                    ? code
                    : FirstNotEmpty(sourceObjectId, name);
                var externalKey = MakeUniqueKey(
                    $"row:{type.ToLowerInvariant()}:{NormalizeKeyPart(stablePart)}",
                    sourceObjectId,
                    usedKeys);

                var candidate = new PlxElementCandidate
                {
                    ExternalKey = externalKey,
                    TypeElement = type,
                    Code = code,
                    Name = name,
                    Details = BuildWorkloadDetails(row),
                    SourceObjectId = sourceObjectId
                };
                elements.Add(candidate);

                if (!string.IsNullOrWhiteSpace(sourceObjectId))
                    rowsByObjectId[sourceObjectId] = candidate;
            }

            var semestersPerCourse = ParseNullableInt(FirstNotEmpty(
                Attribute(plan, "СеместровНаКурсе"),
                Attribute(root, "СеместровНаКурсе"))) ?? 2;
            var courseworkCount = AddCourseworkElements(
                document,
                workTypes,
                rowsByObjectId,
                usedKeys,
                elements,
                semestersPerCourse);
            if (courseworkCount == 0)
                warnings.Add("Отдельные курсовые работы и проекты в плане не обнаружены.");
            if (unknownTypes.Count > 0)
                warnings.Add($"Пропущены неизвестные типы объектов: {string.Join(", ", unknownTypes.OrderBy(value => value))}.");
            if (!elements.Any(element => element.TypeElement == EducationalProgramElementTypes.Discipline))
                warnings.Add("В файле не обнаружены дисциплины.");
            if (excludedRows > 0)
                warnings.Add($"Не включено строк, не учитываемых в плане или не содержащих наименование: {excludedRows}.");

            return new PlxImportPreview
            {
                PlanCode = planCode,
                PlanName = planName,
                EducationalLevel = DetectEducationalLevel(oop, plan, root),
                EducationForm = DetectEducationForm(document, plan, root),
                AdmissionYear = ParseNullableInt(Attribute(plan, "ГодНачалаПодготовки")),
                CoursesCount = ParseNullableInt(FirstNotEmpty(Attribute(plan, "ЧислоКурсов"), Attribute(root, "ЧислоКурсов"))),
                PlanKind = Attribute(root, "Тип"),
                SourceAppVersion = FirstNotEmpty(Attribute(root, "AppVersion"), Attribute(plan, "НомерВерсииПриложения")),
                Elements = elements
                    .OrderBy(element => ElementTypeOrder(element.TypeElement))
                    .ThenBy(element => element.Code, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(element => element.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
                Warnings = warnings,
                ExcludedRowsCount = excludedRows
            };
        }

        private static int AddCourseworkElements(
            XDocument document,
            IReadOnlyDictionary<string, string> workTypes,
            IReadOnlyDictionary<string, PlxElementCandidate> rowsByObjectId,
            HashSet<string> usedKeys,
            List<PlxElementCandidate> elements,
            int semestersPerCourse)
        {
            var count = 0;
            foreach (var hoursRow in FindElements(document, "ПланыНовыеЧасы"))
            {
                var workCode = Attribute(hoursRow, "КодВидаРаботы");
                if (!workTypes.TryGetValue(workCode, out var workName) ||
                    !workName.Contains("курс", StringComparison.CurrentCultureIgnoreCase))
                    continue;

                var objectId = Attribute(hoursRow, "КодОбъекта");
                if (!rowsByObjectId.TryGetValue(objectId, out var parent))
                    continue;

                var amount = Attribute(hoursRow, "Количество");
                if (decimal.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericAmount) && numericAmount <= 0)
                    continue;

                var course = ParseNullableInt(Attribute(hoursRow, "Курс"));
                var semesterInCourse = ParseNullableInt(Attribute(hoursRow, "Семестр"));
                int? semester = course.HasValue && semesterInCourse.HasValue
                    ? ((course.Value - 1) * semestersPerCourse) + semesterInCourse.Value
                    : null;
                var period = semester.HasValue
                    ? $"{semester} семестр"
                    : course.HasValue ? $"{course} курс" : string.Empty;
                var code = string.Join("; ", new[] { parent.Code, period }.Where(value => !string.IsNullOrWhiteSpace(value)));
                var externalKey = MakeUniqueKey(
                    $"coursework:{NormalizeKeyPart(parent.ExternalKey)}:{NormalizeKeyPart(workCode)}:{course}:{semesterInCourse}",
                    objectId,
                    usedKeys);

                elements.Add(new PlxElementCandidate
                {
                    ExternalKey = externalKey,
                    ParentExternalKey = parent.ExternalKey,
                    TypeElement = EducationalProgramElementTypes.Coursework,
                    Code = code,
                    Name = $"{workName}: {parent.Name}",
                    Details = period,
                    SourceObjectId = objectId
                });
                count++;
            }

            return count;
        }

        private static string? ClassifyElementType(string typeCode, string? typeName, string code, string name)
        {
            var reference = typeName ?? string.Empty;
            if (reference.Contains("дисцип", StringComparison.CurrentCultureIgnoreCase) || typeCode == "2")
                return EducationalProgramElementTypes.Discipline;
            if (reference.Contains("практи", StringComparison.CurrentCultureIgnoreCase) ||
                reference.Contains("НИР", StringComparison.CurrentCultureIgnoreCase) || typeCode == "3")
                return EducationalProgramElementTypes.Practice;
            if (reference.Contains("модул", StringComparison.CurrentCultureIgnoreCase) ||
                reference.Contains("блок", StringComparison.CurrentCultureIgnoreCase) || typeCode is "1" or "5")
                return EducationalProgramElementTypes.Module;
            if (reference.Contains("ГИА", StringComparison.CurrentCultureIgnoreCase) || typeCode == "6")
                return EducationalProgramElementTypes.Gia;

            var normalizedCode = code.Replace('B', 'Б').Replace('b', 'Б');
            if (normalizedCode.StartsWith("Б2", StringComparison.CurrentCultureIgnoreCase) ||
                name.Contains("практик", StringComparison.CurrentCultureIgnoreCase))
                return EducationalProgramElementTypes.Practice;
            if (normalizedCode.StartsWith("Б3", StringComparison.CurrentCultureIgnoreCase) ||
                name.Contains("государствен", StringComparison.CurrentCultureIgnoreCase) ||
                name.Contains("итогов", StringComparison.CurrentCultureIgnoreCase))
                return EducationalProgramElementTypes.Gia;

            return null;
        }

        private static string BuildWorkloadDetails(XElement row)
        {
            var parts = new List<string>();
            var credits = FirstNotEmpty(Attribute(row, "ЗЕТфакт"), Attribute(row, "ТрудоемкостьКредитов"));
            var hours = FirstNotEmpty(Attribute(row, "ЧасовПоПлану"), Attribute(row, "ПодлежитИзучениюЧасов"));
            if (!string.IsNullOrWhiteSpace(credits)) parts.Add($"{credits} з.е.");
            if (!string.IsNullOrWhiteSpace(hours)) parts.Add($"{hours} ч.");
            return string.Join("; ", parts);
        }

        private static string DetectEducationalLevel(XElement? oop, XElement? plan, XElement root)
        {
            var value = FirstNotEmpty(Attribute(plan, "Квалификация"), Attribute(oop, "Название"), Attribute(plan, "Титул"));
            if (value.Contains("бакалав", StringComparison.CurrentCultureIgnoreCase)) return "Бакалавриат";
            if (value.Contains("магистр", StringComparison.CurrentCultureIgnoreCase)) return "Магистратура";
            if (value.Contains("ординат", StringComparison.CurrentCultureIgnoreCase)) return "Ординатура";
            if (value.Contains("аспиран", StringComparison.CurrentCultureIgnoreCase)) return "Аспирантура";
            if (value.Contains("специалист", StringComparison.CurrentCultureIgnoreCase)) return "Специалитет";
            if (value.Contains("СПО", StringComparison.CurrentCultureIgnoreCase)) return "СПО";

            return Attribute(root, "КодУровняОбразования") switch
            {
                "1" => "Специалитет",
                "2" => "Бакалавриат",
                "3" => "Магистратура",
                _ => string.Empty
            };
        }

        private static string DetectEducationForm(XDocument document, XElement? plan, XElement root)
        {
            var formCode = FirstNotEmpty(Attribute(plan, "КодФормыОбучения"), Attribute(root, "КодФормыОбучения"));
            var form = FindElements(document, "ФормаОбучения")
                .FirstOrDefault(element => Attribute(element, "Код") == formCode);
            return FirstNotEmpty(Attribute(form, "Название"), Attribute(form, "Наименование"));
        }

        private static IEnumerable<XElement> FindElements(XContainer document, string localName) =>
            document.Descendants().Where(element => element.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

        private static string Attribute(XElement? element, string name) =>
            element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value?.Trim()
            ?? string.Empty;

        private static string FirstNotEmpty(params string[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

        private static bool IsFalse(string value) =>
            value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0";

        private static int? ParseNullableInt(string value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;

        private static int ElementTypeOrder(string type) => type switch
        {
            EducationalProgramElementTypes.Main => 0,
            EducationalProgramElementTypes.Module => 1,
            EducationalProgramElementTypes.Discipline => 2,
            EducationalProgramElementTypes.Practice => 3,
            EducationalProgramElementTypes.Coursework => 4,
            EducationalProgramElementTypes.Gia => 5,
            _ => 6
        };

        private static string MakeUniqueKey(string proposedKey, string fallback, HashSet<string> usedKeys)
        {
            proposedKey = LimitKey(proposedKey);
            if (usedKeys.Add(proposedKey))
                return proposedKey;

            var candidate = LimitKey($"{proposedKey}:{NormalizeKeyPart(fallback)}");
            if (usedKeys.Add(candidate))
                return candidate;

            var suffix = 2;
            while (!usedKeys.Add(candidate = LimitKey($"{proposedKey}:{suffix}")))
                suffix++;
            return candidate;
        }

        private static string NormalizeKeyPart(string value) =>
            WhitespaceRegex().Replace(value.Trim().ToLowerInvariant(), "-");

        private static string LimitKey(string value)
        {
            if (value.Length <= 280)
                return value;

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
            return $"{value[..200]}:{hash}";
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}
