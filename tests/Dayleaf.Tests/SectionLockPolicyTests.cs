using Dayleaf.Domain;

namespace Dayleaf.Tests;

public class SectionLockPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private readonly SectionLockPolicy _policy = new(TimeSpan.FromHours(6), TimeSpan.FromHours(24));

    private static Section Draft() => new() { Label = "Morning", Seq = 1, CreatedAt = T0 };

    [Fact]
    public void Draft_is_editable()
    {
        var s = Draft();
        Assert.True(_policy.IsEditable(s, T0.AddHours(1)));
        Assert.Equal(SectionState.Draft, _policy.State(s, T0.AddHours(1)));
        Assert.Null(_policy.LockAt(s, T0.AddHours(1)));
    }

    [Fact]
    public void Submitted_is_editable_at_5h59m()
    {
        var s = Draft();
        s.SubmittedAt = T0.AddHours(1);
        var now = s.SubmittedAt.Value + TimeSpan.FromHours(5) + TimeSpan.FromMinutes(59);
        Assert.True(_policy.IsEditable(s, now));
        Assert.Equal(SectionState.Submitted, _policy.State(s, now));
        Assert.Equal(TimeSpan.FromMinutes(1), _policy.TimeLeft(s, now));
    }

    [Fact]
    public void Submitted_locks_at_exactly_6h()
    {
        var s = Draft();
        s.SubmittedAt = T0.AddHours(1);
        var now = s.SubmittedAt.Value + TimeSpan.FromHours(6);
        Assert.True(_policy.IsLocked(s, now));
        Assert.Equal(SectionState.Locked, _policy.State(s, now));
        Assert.Throws<SectionLockedException>(() => _policy.EnsureEditable(s, now));
    }

    [Fact]
    public void Draft_auto_submits_at_24h_and_locks_at_30h()
    {
        var s = Draft();
        Assert.True(_policy.IsDraft(s, T0.AddHours(23.99)));
        Assert.Equal(T0.AddHours(24), _policy.EffectiveSubmittedAt(s, T0.AddHours(24)));
        Assert.Equal(SectionState.Submitted, _policy.State(s, T0.AddHours(24)));
        Assert.True(_policy.IsEditable(s, T0.AddHours(29.99)));
        Assert.True(_policy.IsLocked(s, T0.AddHours(30)));
        Assert.Equal(T0.AddHours(30), _policy.LockAt(s, T0.AddHours(31)));
    }

    [Fact]
    public void AutoSubmit_zero_keeps_draft_editable_forever()
    {
        var policy = new SectionLockPolicy(TimeSpan.FromHours(6), TimeSpan.Zero);
        var s = Draft();
        Assert.True(policy.IsEditable(s, T0.AddYears(5)));
        Assert.Equal(SectionState.Draft, policy.State(s, T0.AddYears(5)));
        Assert.Null(policy.AutoSubmitsAt(s, T0));
    }

    [Fact]
    public void Meta_lock_follows_first_section()
    {
        var first = Draft();
        first.SubmittedAt = T0;
        var second = new Section { Label = "Evening", Seq = 2, CreatedAt = T0.AddHours(10) };
        var sections = new[] { second, first };

        Assert.True(_policy.IsMetaEditable(sections, T0.AddHours(5)));
        Assert.False(_policy.IsMetaEditable(sections, T0.AddHours(6)));
        // Second section is still a draft, but meta is locked anyway.
        Assert.True(_policy.IsEditable(second, T0.AddHours(6)));
        Assert.Throws<SectionLockedException>(() => _policy.EnsureMetaEditable(sections, T0.AddHours(6)));
    }

    [Fact]
    public void Explicit_submit_wins_over_auto_submit()
    {
        var s = Draft();
        s.SubmittedAt = T0.AddHours(2);
        Assert.Equal(T0.AddHours(8), _policy.LockAt(s, T0.AddHours(30)));
    }

    [Theory]
    [InlineData(0.5, "less than a minute")]
    [InlineData(45, "45m")]
    [InlineData(312, "5h 12m")]
    public void FormatSpan_is_friendly(double minutes, string expected) =>
        Assert.Equal(expected, SectionLockPolicy.FormatSpan(TimeSpan.FromMinutes(minutes)));
}
