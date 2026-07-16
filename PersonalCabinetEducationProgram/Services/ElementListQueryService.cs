using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;
using PersonalCabinetEducationProgram.ViewModels;

namespace PersonalCabinetEducationProgram.Services
{
    public class ElementListQueryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ElementFilterService _filterService;

        public ElementListQueryService(ApplicationDbContext context, ElementFilterService filterService)
        {
            _context = context;
            _filterService = filterService;
        }

        public async Task<ElementListPageViewModel> GetAsync(
            int? programId, string tab, int page, string sort, string direction,
            ElementListFiltersViewModel filters)
        {
            if (!programId.HasValue)
                return new ElementListPageViewModel
                {
                    Page = 1,
                    TotalPages = 1,
                    Sort = sort,
                    Direction = direction,
                    Filters = filters
                };

            const int pageSize = 25;
            page = Math.Max(1, page);
            var tabType = tab switch
            {
                "practices" => EducationalProgramElementTypes.Practice,
                "gia" => EducationalProgramElementTypes.Gia,
                _ => EducationalProgramElementTypes.Discipline
            };

            var baseQuery = _context.EducationalProgramElements
                .Where(e => e.EducationalProgramId == programId && !e.IsArchived && !e.EducationalProgram.IsArchived);
            var statuses = await baseQuery.Select(e => e.StatusApprovals).ToListAsync();
            var mainQuery = ApplySort(
                _filterService.ApplyDatabaseFilters(
                    baseQuery.Where(e => e.TypeElement == EducationalProgramElementTypes.Main),
                    filters.Main),
                sort,
                direction);
            var tabQuery = ApplySort(
                baseQuery.Where(e => e.TypeElement == tabType),
                sort,
                direction)
                .Include(e => e.EducationalProgram)
                .Include(e => e.Files.Where(f => f.IsCurrent));

            var mainCandidates = await mainQuery
                .Include(e => e.EducationalProgram)
                .Include(e => e.Files.Where(f => f.IsCurrent))
                .ToListAsync();
            var mainElements = _filterService.ApplyTextFilters(mainCandidates, filters.Main).ToList();
            var tabResult = await _filterService.FilterAndPageAsync(tabQuery, filters.Tab, page, pageSize);

            return new ElementListPageViewModel
            {
                Elements = mainElements.Concat(tabResult.Items).ToList(),
                Statuses = statuses,
                Page = page,
                TotalPages = Math.Max(1, (int)Math.Ceiling(tabResult.TotalCount / (double)pageSize)),
                Sort = sort,
                Direction = direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc",
                Filters = filters
            };
        }

        private static IQueryable<EducationalProgramElement> ApplySort(
            IQueryable<EducationalProgramElement> query, string sort, string direction)
        {
            var descending = direction.Equals("desc", StringComparison.OrdinalIgnoreCase);
            return sort switch
            {
                "description" => descending ? query.OrderByDescending(e => e.Description) : query.OrderBy(e => e.Description),
                "status" => descending ? query.OrderByDescending(e => e.StatusApprovals) : query.OrderBy(e => e.StatusApprovals),
                "date" => descending ? query.OrderByDescending(e => e.UploadDate) : query.OrderBy(e => e.UploadDate),
                _ => descending ? query.OrderByDescending(e => e.Name) : query.OrderBy(e => e.Name)
            };
        }
    }
}
