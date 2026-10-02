using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;

namespace PoultryOS.Services;

public class SalesHistoryService : ISalesHistoryService
{
    private const int MaxPageSize = 200;

    private readonly AppDbContext _db;

    public SalesHistoryService(AppDbContext db) => _db = db;

    public async Task<SalesHistoryPageViewModel> GetPageAsync(SalesHistoryFilter filter)
    {
        // ---------- Normalise the filter ----------
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 || filter.PageSize > MaxPageSize
            ? 50
            : filter.PageSize;

        // If the user picked "to = 2026-09-30", they mean end of that day.
        // We add one day and use "<" so the full day is included.
        var fromDate = filter.From?.Date;
        var toDateExclusive = filter.To?.Date.AddDays(1);

        // ---------- Build the query with filters ----------
        var query = _db.Sales
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.User)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(s => s.SoldAt >= fromDate.Value);

        if (toDateExclusive.HasValue)
            query = query.Where(s => s.SoldAt < toDateExclusive.Value);

        if (filter.ProductId.HasValue)
            query = query.Where(s => s.ProductId == filter.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(filter.EnteredBy))
            query = query.Where(s => s.User.Username == filter.EnteredBy);

        // ---------- Count BEFORE paging ----------
        var totalCount = await query.CountAsync();

        // ---------- Fetch just one page, newest first ----------
        var rows = await query
            .OrderByDescending(s => s.SoldAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SalesHistoryRowViewModel
            {
                Id = s.Id,
                SoldAt = s.SoldAt,
                ProductName = s.Product.Name,
                Units = s.Product.Units,
                Quantity = s.Quantity,
                EnteredBy = s.User.Username,
                Notes = s.Notes ?? string.Empty
            })
            .ToListAsync();

        // ---------- Dropdown options ----------
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProductOption { Id = p.Id, Name = p.Name })
            .ToListAsync();

        var users = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new UserOption { Username = u.Username })
            .ToListAsync();

        // ---------- Assemble the page ----------
        return new SalesHistoryPageViewModel
        {
            Rows = rows,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Products = products,
            Users = users
        };
    }
}