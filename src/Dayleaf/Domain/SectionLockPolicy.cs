using Dayleaf.Localization;
using Microsoft.Extensions.Options;

namespace Dayleaf.Domain;

public enum SectionState { Draft, Submitted, Locked }

public sealed class SectionLockedException(string message) : InvalidOperationException(message);

public sealed class DayleafRuleException(string message) : InvalidOperationException(message);

/// <summary>
/// Single source of truth for the "you can add, but you can't change" rules (plan §0.4).
/// </summary>
public sealed class SectionLockPolicy
{
    public TimeSpan EditWindow { get; }
    public TimeSpan AutoSubmitAfter { get; }

    public SectionLockPolicy(TimeSpan editWindow, TimeSpan autoSubmitAfter)
    {
        EditWindow = editWindow < TimeSpan.Zero ? TimeSpan.Zero : editWindow;
        AutoSubmitAfter = autoSubmitAfter < TimeSpan.Zero ? TimeSpan.Zero : autoSubmitAfter;
    }

    public SectionLockPolicy(IOptions<DayleafOptions> options)
        : this(TimeSpan.FromHours(options.Value.EditWindowHours), TimeSpan.FromHours(options.Value.AutoSubmitDraftAfterHours)) { }

    public DateTimeOffset? EffectiveSubmittedAt(Section s, DateTimeOffset now)
    {
        if (s.SubmittedAt is { } submitted) return submitted;
        if (AutoSubmitAfter > TimeSpan.Zero)
        {
            var auto = s.CreatedAt + AutoSubmitAfter;
            if (now >= auto) return auto;
        }
        return null;
    }

    public DateTimeOffset? LockAt(Section s, DateTimeOffset now) => EffectiveSubmittedAt(s, now) + EditWindow;

    public bool IsLocked(Section s, DateTimeOffset now) => LockAt(s, now) is { } at && now >= at;

    public bool IsEditable(Section s, DateTimeOffset now) => !IsLocked(s, now);

    public bool IsDraft(Section s, DateTimeOffset now) => EffectiveSubmittedAt(s, now) is null;

    public SectionState State(Section s, DateTimeOffset now) =>
        IsDraft(s, now) ? SectionState.Draft : IsLocked(s, now) ? SectionState.Locked : SectionState.Submitted;

    /// <summary>Time left before the section locks (null for drafts and locked sections).</summary>
    public TimeSpan? TimeLeft(Section s, DateTimeOffset now) =>
        LockAt(s, now) is { } at && now < at ? at - now : null;

    /// <summary>When a draft will auto-submit (null if not a draft or auto-submit is off).</summary>
    public DateTimeOffset? AutoSubmitsAt(Section s, DateTimeOffset now) =>
        IsDraft(s, now) && AutoSubmitAfter > TimeSpan.Zero ? s.CreatedAt + AutoSubmitAfter : null;

    public void EnsureEditable(Section s, DateTimeOffset now)
    {
        if (IsLocked(s, now))
        {
            var at = LockAt(s, now)!.Value.ToLocalTime();
            throw new SectionLockedException(Loc.T(
                "The “{0}” section was locked on {1} at {2}. Locked content can't be changed — add a new section instead.",
                s.Label, Dates.Format(at.DateTime, DateStyle.Short), Dates.Time(at)));
        }
    }

    /// <summary>Page metadata (title, date, theme, tags) follows the lock of the page's first section.</summary>
    public bool IsMetaEditable(IEnumerable<Section> sections, DateTimeOffset now)
    {
        var first = sections.OrderBy(s => s.Seq).FirstOrDefault();
        return first is null || IsEditable(first, now);
    }

    public void EnsureMetaEditable(IEnumerable<Section> sections, DateTimeOffset now)
    {
        if (!IsMetaEditable(sections, now))
            throw new SectionLockedException(Loc.T("This page's title, date, theme and tags are locked because its first section is locked."));
    }

    public static string FormatSpan(TimeSpan t)
    {
        if (t.TotalMinutes < 1) return Loc.T("less than a minute");
        if (t.TotalHours < 1) return Loc.T("{0}m", (int)t.TotalMinutes);
        if (t.TotalDays < 1) return Loc.T("{0}h {1}m", (int)t.TotalHours, t.Minutes);
        return Loc.T("{0}d {1}h", (int)t.TotalDays, t.Hours);
    }
}
