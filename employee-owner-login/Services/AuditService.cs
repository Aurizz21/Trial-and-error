using Microsoft.EntityFrameworkCore;
using PoultryOS.Data;
using PoultryOS.Models;

namespace PoultryOS.Services;

public class AuditService : IAuditService
{
    private const int MaxPageSize = 200;

    private readonly AppDbContext _db;

    public AuditService(AppDbContext db) => _db = db;

    public async Task<AuditPageViewModel> GetPageAsync(
        string actionTypeFilter,
        string userFilter,
        int page,
        int pageSize)
    {
        // Normalise
        var typeFilter = string.IsNullOrWhiteSpace(actionTypeFilter) ? "All" : actionTypeFilter.Trim();
        var user = string.IsNullOrWhiteSpace(userFilter) ? "All" : userFilter.Trim();
        var p = page < 1 ? 1 : page;
        var size = pageSize < 1 || pageSize > MaxPageSize ? 50 : pageSize;

        // Base query
        var query = _db.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .AsQueryable();

        // Apply filters
        if (typeFilter != "All")
            query = query.Where(a => a.ActionType == typeFilter);

        if (user != "All")
        {
            if (user == "System")
                query = query.Where(a => a.UserId == null);
            else
                query = query.Where(a => a.User != null && a.User.Username == user);
        }

        // Count BEFORE paging
        var totalCount = await query.CountAsync();

        // Fetch one page, newest first
        var rows = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(a => new AuditRowViewModel
            {
                Id = a.Id,
                Timestamp = a.Timestamp,
                ActionType = a.ActionType,
                Username = a.User != null ? a.User.Username : "System",
                Description = a.Description
            })
            .ToListAsync();

        // Dropdown options -- distinct values from the DB
        var actionTypes = await _db.AuditLogs
            .AsNoTracking()
            .Select(a => a.ActionType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();

        var usernames = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => u.Username)
            .ToListAsync();

        return new AuditPageViewModel
        {
            Rows = rows,
            TotalCount = totalCount,
            Page = p,
            PageSize = size,
            ActionTypeFilter = typeFilter,
            UserFilter = user,
            ActionTypes = actionTypes,
            Usernames = usernames
        };
    }
}