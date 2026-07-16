using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Services
{
    public class ElementFilterService
    {
        public IQueryable<EducationalProgramElement> ApplyDatabaseFilters(
            IQueryable<EducationalProgramElement> query,
            ElementColumnFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.TypeElement))
                query = query.Where(e => e.TypeElement == filter.TypeElement);

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                if (filter.Status == ElementListFiltersViewModel.NotUploadedFilterValue)
                {
                    query = query.Where(e => e.StatusApprovals == null || e.StatusApprovals == string.Empty);
                }
                else
                {
                    var status = ElementApprovalStatus.Normalize(filter.Status);
                    query = status switch
                    {
                        ElementApprovalStatus.OnApproval =>
                            query.Where(e => e.StatusApprovals == ElementApprovalStatus.OnApproval || e.StatusApprovals == "На рассмотрении"),
                        ElementApprovalStatus.RevisionRequired =>
                            query.Where(e => e.StatusApprovals == ElementApprovalStatus.RevisionRequired || e.StatusApprovals == "Отклонено"),
                        _ => query.Where(e => e.StatusApprovals == status)
                    };
                }
            }

            if (filter.DateFrom.HasValue)
                query = query.Where(e => e.UploadDate >= filter.DateFrom.Value);
            if (filter.DateTo.HasValue)
                query = query.Where(e => e.UploadDate <= filter.DateTo.Value);

            return query;
        }

        public IEnumerable<EducationalProgramElement> ApplyTextFilters(
            IEnumerable<EducationalProgramElement> elements,
            ElementColumnFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                elements = elements.Where(e =>
                    FuzzyTextMatcher.IsMatch(e.Name, filter.SearchText) ||
                    FuzzyTextMatcher.IsMatch(e.Description, filter.SearchText));
            }
            if (!string.IsNullOrWhiteSpace(filter.Description))
                elements = elements.Where(e => FuzzyTextMatcher.IsMatch(e.Description, filter.Description));
            if (!string.IsNullOrWhiteSpace(filter.Name))
                elements = elements.Where(e => FuzzyTextMatcher.IsMatch(e.Name, filter.Name));

            return elements;
        }

        public async Task<(List<EducationalProgramElement> Items, int TotalCount)> FilterAndPageAsync(
            IQueryable<EducationalProgramElement> query,
            ElementColumnFilter filter,
            int page,
            int pageSize)
        {
            query = ApplyDatabaseFilters(query, filter);

            if (!filter.HasTextFilter)
            {
                var totalCount = await query.CountAsync();
                var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                return (items, totalCount);
            }

            var candidates = await query.ToListAsync();
            var filtered = ApplyTextFilters(candidates, filter).ToList();
            return (filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), filtered.Count);
        }
    }
}
