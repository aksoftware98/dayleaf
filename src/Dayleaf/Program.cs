using Dayleaf.Components;
using Dayleaf.Components.Shared;
using Dayleaf.Data;
using Dayleaf.Domain;
using Dayleaf.Endpoints;
using Dayleaf.Services;
using Dayleaf.Services.Export;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Dayleaf.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// `dotnet run -- --hash-password [password]` prints a hash for Dayleaf:Auth:PasswordHash.
if (args.Length > 0 && args[0] == "--hash-password")
{
    var pw = args.Length > 1 ? args[1] : PasswordPrompt.Read();
    if (string.IsNullOrWhiteSpace(pw) || pw.Length < 8)
    {
        Console.Error.WriteLine("Use a password of at least 8 characters.");
        return 1;
    }
    Console.WriteLine(PasswordGate.Hash(pw));
    return 0;
}

var builder = WebApplication.CreateBuilder(args);

// ── options & paths ─────────────────────────────────────────────────────────
builder.Services.Configure<DayleafOptions>(builder.Configuration.GetSection(DayleafOptions.SectionName));
var options = builder.Configuration.GetSection(DayleafOptions.SectionName).Get<DayleafOptions>() ?? new DayleafOptions();
var paths = new DayleafPaths(options.ResolveDataDirectory(builder.Environment.ContentRootPath));
Directory.CreateDirectory(paths.DataDirectory);
Directory.CreateDirectory(paths.MediaDirectory);
builder.Services.AddSingleton(paths);

// ── data & services ─────────────────────────────────────────────────────────
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContextFactory<DayleafDbContext>(o => o.UseSqlite($"Data Source={paths.DatabasePath}"));
builder.Services.AddSingleton<SectionLockPolicy>();
builder.Services.AddSingleton<LayoutService>();
builder.Services.AddSingleton<PasswordGate>();
builder.Services.AddScoped<ISpaceService, SpaceService>();
builder.Services.AddScoped<IPageService, PageService>();
builder.Services.AddScoped<ITimelineService, TimelineService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<DayleafExporter>();
builder.Services.AddScoped<DayleafImporter>();
builder.Services.AddScoped<HtmlPageExporter>();
builder.Services.AddSingleton<PdfRenderer>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<AppEvents>();
builder.Services.AddSingleton<Loc>();

// ── optional password gate ──────────────────────────────────────────────────
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(paths.DataDirectory, "keys")))
    .SetApplicationName("Dayleaf");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.LogoutPath = "/logout";
        o.AccessDeniedPath = "/login";
        o.Cookie.Name = "dayleaf.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.SlidingExpiration = true;
        o.ExpireTimeSpan = TimeSpan.FromDays(Math.Max(1, options.Auth.SessionDays));
    });
builder.Services.AddAuthorization(o =>
{
    // With a password set, everything needs a signed-in user unless marked [AllowAnonymous]
    // (login, public share pages, static assets, media used by shared pages).
    if (options.Auth.Enabled)
        o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment())
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 4 * 1024 * 1024);

var app = builder.Build();

// ── database: back up, then migrate ─────────────────────────────────────────
await DatabaseStartup.RunAsync(app.Services, paths, options, app.Logger);
if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var seeded = await scope.ServiceProvider.GetRequiredService<SeedService>().SeedAsync();
    app.Logger.LogInformation(seeded ? "Sample notebook created." : "Sample notebook already exists.");
}
app.Logger.LogInformation("Dayleaf data directory: {Dir}", paths.DataDirectory);
app.Logger.LogInformation(options.Auth.Enabled
    ? "Password gate is ON."
    : "No password set: Dayleaf only answers requests from this computer. Set Dayleaf:Auth:PasswordHash before hosting it anywhere else.");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Language: the culture cookie (set by /lang/{code}) wins, then the browser's Accept-Language. Default English.
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(Lang.En)
    .AddSupportedCultures(Lang.All)
    .AddSupportedUICultures(Lang.All));

// Without auth, refuse anything that doesn't come from this machine.
if (!options.Auth.Enabled)
{
    app.Use(async (ctx, next) =>
    {
        if (!PasswordGate.IsLocal(ctx))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsync(Loc.T("Dayleaf only accepts connections from this computer until a password is set (Dayleaf:Auth:PasswordHash)."));
            return;
        }
        await next();
    });
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapMediaEndpoints();
app.MapExportEndpoints();
app.MapGet("/logout", async ctx =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    ctx.Response.Redirect("/login");
}).AllowAnonymous();
app.MapGet("/lang/{code}", (string code, string? r, HttpContext ctx) =>
{
    var lang = Lang.Normalize(code);
    ctx.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true });
    // Only ever go back to a local path.
    var back = !string.IsNullOrEmpty(r) && r.StartsWith('/') && !r.StartsWith("//") && !r.StartsWith("/\\") ? r : "/";
    return Results.LocalRedirect(back);
}).AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
return 0;

internal static class DatabaseStartup
{
    public static async Task RunAsync(IServiceProvider services, DayleafPaths paths, DayleafOptions options, ILogger logger)
    {
        if (File.Exists(paths.DatabasePath))
        {
            Directory.CreateDirectory(paths.BackupDirectory);
            var stamp = services.GetRequiredService<TimeProvider>().GetLocalNow().ToString("yyyyMMddHHmmss");
            var target = Path.Combine(paths.BackupDirectory, $"dayleaf-{stamp}.db");
            try
            {
                // VACUUM INTO makes a consistent copy, including anything still in the WAL file.
                await using var conn = new SqliteConnection($"Data Source={paths.DatabasePath}");
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "VACUUM INTO $target";
                cmd.Parameters.AddWithValue("$target", target);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Backup with VACUUM INTO failed; copying the file instead.");
                if (!File.Exists(target)) File.Copy(paths.DatabasePath, target);
            }
            foreach (var old in new DirectoryInfo(paths.BackupDirectory).GetFiles("dayleaf-*.db")
                         .OrderByDescending(f => f.Name).Skip(Math.Max(1, options.KeepBackups)))
                old.Delete();
        }

        var factory = services.GetRequiredService<IDbContextFactory<DayleafDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }
}

internal static class PasswordPrompt
{
    public static string Read()
    {
        Console.Write("Password: ");
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); continue; }
            if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }
}

public partial class Program;
