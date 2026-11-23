using System.Net;
using System.Security.Claims;
using Dayleaf.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Dayleaf.Services;

/// <summary>Single-user password gate (plan §10, Phase 9). Off unless Dayleaf:Auth:PasswordHash is set.</summary>
public sealed class PasswordGate(IOptions<DayleafOptions> options)
{
    private static readonly PasswordHasher<string> Hasher = new();
    public const string User = "dayleaf";

    public bool Enabled => options.Value.Auth.Enabled;

    public static string Hash(string password) => Hasher.HashPassword(User, password);

    // Simple brute-force brake: at most 5 failed attempts per 5 minutes (single-user app, in-memory is enough).
    private static readonly Queue<DateTimeOffset> Failures = new();
    private static readonly Lock FailuresLock = new();

    public bool IsThrottled(DateTimeOffset now)
    {
        lock (FailuresLock)
        {
            while (Failures.Count > 0 && now - Failures.Peek() > TimeSpan.FromMinutes(5)) Failures.Dequeue();
            return Failures.Count >= 5;
        }
    }

    public async Task<bool> VerifyAsync(string? password, DateTimeOffset now)
    {
        var hash = options.Value.Auth.PasswordHash;
        if (string.IsNullOrEmpty(hash) || IsThrottled(now)) return false;
        var ok = !string.IsNullOrEmpty(password) && Hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed;
        if (!ok)
        {
            lock (FailuresLock) Failures.Enqueue(now);
            await Task.Delay(800);
        }
        return ok;
    }

    public async Task SignInAsync(HttpContext http, bool remember)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, User)], CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IsPersistent = remember,
            ExpiresUtc = remember ? DateTimeOffset.UtcNow.AddDays(Math.Max(1, options.Value.Auth.SessionDays)) : null,
        });
    }

    /// <summary>Without a password, only requests from this machine are served.</summary>
    public static bool IsLocal(HttpContext http)
    {
        var remote = http.Connection.RemoteIpAddress;
        if (remote is null) return true; // in-process test server
        if (IPAddress.IsLoopback(remote)) return true;
        return remote.Equals(http.Connection.LocalIpAddress);
    }
}
