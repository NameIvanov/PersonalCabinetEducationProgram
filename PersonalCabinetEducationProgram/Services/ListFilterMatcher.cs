namespace PersonalCabinetEducationProgram.Services
{
    public static class ListFilterMatcher
    {
        public static bool Text(string? value, string? query) => FuzzyTextMatcher.IsMatch(value, query);

        public static bool AnyText(IEnumerable<string?> values, string? query) =>
            string.IsNullOrWhiteSpace(query) || values.Any(value => Text(value, query));

        public static bool Exact(string? value, string? expected) =>
            string.IsNullOrWhiteSpace(expected) || string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

        public static bool Date(DateTime? value, DateOnly? from, DateOnly? to)
        {
            if (!from.HasValue && !to.HasValue)
                return true;
            if (!value.HasValue)
                return false;

            var date = DateOnly.FromDateTime(value.Value.ToLocalTime());
            return (!from.HasValue || date >= from.Value) && (!to.HasValue || date <= to.Value);
        }

        public static bool Date(DateOnly? value, DateOnly? from, DateOnly? to)
        {
            if (!from.HasValue && !to.HasValue)
                return true;
            if (!value.HasValue)
                return false;

            return (!from.HasValue || value.Value >= from.Value) &&
                (!to.HasValue || value.Value <= to.Value);
        }
    }
}
