using PersonalCabinetEducationProgram.Services;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Tests;

public class ListFilterMatcherTests
{
    [Fact]
    public void AnyText_SearchesAcrossAllValuesWithTypos()
    {
        Assert.True(ListFilterMatcher.AnyText(
            ["09.03.01", "Прикладная информатика"],
            "приклодная"));
    }

    [Fact]
    public void Exact_IgnoresCaseButDoesNotUseFuzzyMatching()
    {
        Assert.True(ListFilterMatcher.Exact("Approved", "approved"));
        Assert.False(ListFilterMatcher.Exact("Approved", "Aproved"));
    }

    [Theory]
    [InlineData("2026-07-16", "2026-07-15", "2026-07-17", true)]
    [InlineData("2026-07-15", "2026-07-15", "2026-07-15", true)]
    [InlineData("2026-07-14", "2026-07-15", "2026-07-17", false)]
    public void Date_AppliesInclusiveRange(string value, string from, string to, bool expected)
    {
        Assert.Equal(expected, ListFilterMatcher.Date(
            DateOnly.Parse(value),
            DateOnly.Parse(from),
            DateOnly.Parse(to)));
    }

    [Fact]
    public void RouteData_PreservesActiveFiltersAndVisibleState()
    {
        var filters = new ProgramListFiltersViewModel
        {
            Code = "09.03",
            Status = "Согласована"
        };

        var routeData = filters.ToRouteData();

        Assert.Equal("09.03", routeData[nameof(filters.Code)]);
        Assert.Equal("Согласована", routeData[nameof(filters.Status)]);
        Assert.Equal("true", routeData[nameof(filters.ShowFilters)]);
        Assert.DoesNotContain(nameof(filters.Year), routeData.Keys);
    }
}
