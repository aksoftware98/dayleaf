using System.Net;
using Dayleaf.Domain;
using Dayleaf.Services;
using Microsoft.AspNetCore.Http;

namespace Dayleaf.Tests;

public class PasswordGateTests
{
    private static PasswordGate Gate(string? hash) =>
        new(Microsoft.Extensions.Options.Options.Create(new DayleafOptions { Auth = new() { PasswordHash = hash } }));

    [Fact]
    public async Task Hash_verifies_only_the_right_password()
    {
        var gate = Gate(PasswordGate.Hash("correct horse"));
        Assert.True(gate.Enabled);
        var now = DateTimeOffset.UtcNow.AddYears(-1); // isolated from other tests' failure window
        Assert.True(await gate.VerifyAsync("correct horse", now));
        Assert.False(Gate(null).Enabled);
        Assert.False(await Gate(null).VerifyAsync("anything", now));
    }

    [Fact]
    public async Task Five_failures_throttle_further_attempts()
    {
        var gate = Gate(PasswordGate.Hash("right-password"));
        var t = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 5; i++) Assert.False(await gate.VerifyAsync("nope", t));
        Assert.True(gate.IsThrottled(t));
        Assert.False(await gate.VerifyAsync("right-password", t)); // even the right one waits
        Assert.False(gate.IsThrottled(t.AddMinutes(6)));
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("192.168.1.20", false)]
    [InlineData("8.8.8.8", false)]
    public void Only_loopback_counts_as_local(string ip, bool expected)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        ctx.Connection.LocalIpAddress = IPAddress.Parse("10.0.0.5");
        Assert.Equal(expected, PasswordGate.IsLocal(ctx));
    }
}
