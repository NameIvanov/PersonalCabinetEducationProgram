using PersonalCabinetEducationProgram.Services;

namespace PersonalCabinetEducationProgram.Tests;

public class FuzzyTextMatcherTests
{
    [Theory]
    [InlineData("Информатика", "инфарматика")]
    [InlineData("Информационные системы", "инфромационные")]
    [InlineData("Прикладная информатика", "приклодная инфарматика")]
    [InlineData("Учебный план", "учебн план")]
    [InlineData("Фёдоров Иван", "федоров")]
    [InlineData("Календарный учебный график", "кале")]
    public void IsMatch_FindsTextWithTypingErrors(string value, string query)
    {
        Assert.True(FuzzyTextMatcher.IsMatch(value, query));
    }

    [Theory]
    [InlineData("Информатика", "химия")]
    [InlineData("09.03.01", "09.03.02")]
    [InlineData("Петров Иван", "Петров Сидоров")]
    public void IsMatch_RejectsUnrelatedText(string value, string query)
    {
        Assert.False(FuzzyTextMatcher.IsMatch(value, query));
    }

    [Fact]
    public void IsMatch_AllowsExactCodePrefix()
    {
        Assert.True(FuzzyTextMatcher.IsMatch("09.03.01", "09.03"));
    }

    [Fact]
    public void Distance_CountsAdjacentTranspositionAsOneEdit()
    {
        Assert.Equal(1, FuzzyTextMatcher.DamerauLevenshteinDistance("информатика", "инфроматика"));
    }
}
