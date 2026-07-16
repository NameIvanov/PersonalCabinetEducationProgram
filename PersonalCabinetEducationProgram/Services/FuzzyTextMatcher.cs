using System.Globalization;
using System.Text;

namespace PersonalCabinetEducationProgram.Services
{
    public static class FuzzyTextMatcher
    {
        public static bool IsMatch(string? value, string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalizedValue = Normalize(value);
            var normalizedQuery = Normalize(query);
            if (normalizedValue.Contains(normalizedQuery, StringComparison.Ordinal))
                return true;

            if (normalizedQuery.Any(char.IsDigit))
                return false;

            var valueWords = SplitWords(normalizedValue);
            var queryWords = SplitWords(normalizedQuery);
            return queryWords.Count > 0 && queryWords.All(queryWord =>
                valueWords.Any(valueWord => IsWordMatch(valueWord, queryWord)));
        }

        public static int DamerauLevenshteinDistance(string source, string target)
        {
            if (source.Length == 0)
                return target.Length;
            if (target.Length == 0)
                return source.Length;

            var matrix = new int[source.Length + 1, target.Length + 1];
            for (var i = 0; i <= source.Length; i++)
                matrix[i, 0] = i;
            for (var j = 0; j <= target.Length; j++)
                matrix[0, j] = j;

            for (var i = 1; i <= source.Length; i++)
            {
                for (var j = 1; j <= target.Length; j++)
                {
                    var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 &&
                        source[i - 1] == target[j - 2] &&
                        source[i - 2] == target[j - 1])
                    {
                        matrix[i, j] = Math.Min(matrix[i, j], matrix[i - 2, j - 2] + 1);
                    }
                }
            }

            return matrix[source.Length, target.Length];
        }

        private static bool IsWordMatch(string valueWord, string queryWord)
        {
            if (queryWord.Length < 3)
                return valueWord.StartsWith(queryWord, StringComparison.Ordinal);

            var threshold = queryWord.Length switch
            {
                <= 5 => 1,
                <= 10 => 2,
                _ => 3
            };

            if (Math.Abs(valueWord.Length - queryWord.Length) <= threshold &&
                DamerauLevenshteinDistance(valueWord, queryWord) <= threshold)
            {
                return true;
            }

            if (valueWord.Length > queryWord.Length)
            {
                return DamerauLevenshteinDistance(valueWord[..queryWord.Length], queryWord) <= threshold;
            }

            return false;
        }

        private static string Normalize(string value)
        {
            var source = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Replace('ё', 'е');
            var result = new StringBuilder(source.Length);
            var previousWasSpace = false;

            foreach (var character in source)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                var isWordCharacter = char.IsLetterOrDigit(character) || category == UnicodeCategory.NonSpacingMark;
                if (isWordCharacter || character is '.' or '-' or '/')
                {
                    result.Append(character);
                    previousWasSpace = false;
                }
                else if (!previousWasSpace)
                {
                    result.Append(' ');
                    previousWasSpace = true;
                }
            }

            return result.ToString().Trim();
        }

        private static List<string> SplitWords(string value) =>
            value.Split([' ', '.', '-', '/'], StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
