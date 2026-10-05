using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NepalHub.Api.Common;
using NepalHub.Api.Data;
using NepalHub.Api.Data.Entities;

namespace NepalHub.Api.Features.Admin;

/// <summary>
/// Appends audit entries to the request's own context and reads the trail back for the
/// admin screen. See <see cref="IAuditLog"/> for why the write does not save on its own.
/// </summary>
public sealed class AuditLogService(AppDbContext dbContext) : IAuditLog
{
    /// <summary>
    /// The same shape the API writes over the wire, so a before/after pair in the log
    /// reads like the JSON of the thing it describes.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Record(
        Guid adminUserId,
        string action,
        string targetType,
        string targetId,
        object? before = null,
        object? after = null,
        string? ip = null)
    {
        dbContext.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Before = before is null ? null : JsonSerializer.Serialize(before, Json),
            After = after is null ? null : JsonSerializer.Serialize(after, Json),
            Ip = ip,
        });
    }

    public async Task<PagedResult<AuditLogResponse>> ListAsync(
        PageRequest paging,
        string? action,
        string? targetType,
        CancellationToken cancellationToken)
    {
        var matching =
            from entry in dbContext.AdminAuditLogs
            join admin in dbContext.Users on entry.AdminUserId equals admin.Id
            where (action == null || entry.Action == action)
                && (targetType == null || entry.TargetType == targetType)
            orderby entry.CreatedAt descending
            select new
            {
                entry.Id,
                entry.Action,
                entry.TargetType,
                entry.TargetId,
                AdminName = admin.FullName,
                entry.Before,
                entry.After,
                entry.Ip,
                entry.CreatedAt,
            };

        var total = await matching.CountAsync(cancellationToken);
        var clamped = paging.ClampTo(total);

        var rows = await matching
            .Skip(clamped.Skip)
            .Take(clamped.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AuditLogResponse(
                row.Id,
                row.Action,
                row.TargetType,
                row.TargetId,
                row.AdminName,
                row.Before,
                row.After,
                row.Ip,
                row.CreatedAt))
            .ToList();

        return new PagedResult<AuditLogResponse>(items, clamped.Page, clamped.PageSize, total);
    }
}
