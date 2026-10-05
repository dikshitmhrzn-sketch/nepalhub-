namespace NepalHub.Api.Data.Entities;

/// <summary>
/// One staff action that changed something belonging to another account or to the
/// moderation queue.
///
/// <para>
/// This table exists because <c>docs/architecture.md</c> states the rule — admin actions
/// that change other users' data are written to an audit log — and because the row has to
/// exist before the actions it records start happening: an audit trail written after the
/// fact is a reconstruction, not a record.
/// </para>
///
/// <para>
/// The scope is deliberate. Moderation and user administration write here; the seven module
/// content controllers do not, because their subject is the site's own catalogue rather
/// than somebody else's account. Widening the coverage later means calling the same service
/// from more places, not changing this table.
/// </para>
///
/// <para>
/// <c>before</c> and <c>after</c> are JSON snapshots rather than column diffs, so a reader
/// of the log sees what the row was and what it became without the log needing a schema for
/// every subject it will ever record. Rows are appended and never updated: this table is
/// written once and read, never edited.
/// </para>
/// </summary>
public sealed class AdminAuditLog
{
    public Guid Id { get; set; }

    /// <summary>Who acted. A plain id rather than a name, so a renamed account stays truthful.</summary>
    public Guid AdminUserId { get; set; }

    public ApplicationUser? AdminUser { get; set; }

    /// <summary>Dotted action name, e.g. <c>user.deactivated</c> or <c>report.resolved</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>What kind of thing was acted on, e.g. <c>report</c> or <c>user</c>.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>The id of the thing acted on, stored as text so the table needs no polymorphic key.</summary>
    public string TargetId { get; set; } = string.Empty;

    public string? Before { get; set; }

    public string? After { get; set; }

    /// <summary>The address the action came from, or null when the request did not carry one.</summary>
    public string? Ip { get; set; }

    public DateTime CreatedAt { get; set; }
}
