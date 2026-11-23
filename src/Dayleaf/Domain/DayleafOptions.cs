namespace Dayleaf.Domain;

public sealed class DayleafOptions
{
    public const string SectionName = "Dayleaf";

    /// <summary>Where dayleaf.db and media/ live. Empty = %LOCALAPPDATA%/Dayleaf (Windows) or ./App_Data.</summary>
    public string? DataDirectory { get; set; }
    public double EditWindowHours { get; set; } = 6;
    /// <summary>Drafts auto-submit this many hours after creation. 0 disables auto-submit.</summary>
    public double AutoSubmitDraftAfterHours { get; set; } = 24;
    public int MaxUploadMb { get; set; } = 20;
    public int KeepBackups { get; set; } = 10;

    /// <summary>Optional single-user password gate. Required before hosting Dayleaf anywhere but localhost.</summary>
    public DayleafAuthOptions Auth { get; set; } = new();

    public string ResolveDataDirectory(string contentRoot)
    {
        if (!string.IsNullOrWhiteSpace(DataDirectory))
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(DataDirectory), contentRoot);
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dayleaf");
        return Path.Combine(contentRoot, "App_Data");
    }
}

public sealed class DayleafAuthOptions
{
    /// <summary>PBKDF2 hash made with `dotnet run -- --hash-password`. Empty = no password (localhost only).</summary>
    public string? PasswordHash { get; set; }
    public int SessionDays { get; set; } = 30;

    public bool Enabled => !string.IsNullOrWhiteSpace(PasswordHash);
}

/// <summary>Resolved runtime paths.</summary>
public sealed class DayleafPaths(string dataDirectory)
{
    public string DataDirectory { get; } = dataDirectory;
    public string DatabasePath => Path.Combine(DataDirectory, "dayleaf.db");
    public string MediaDirectory => Path.Combine(DataDirectory, "media");
    public string BackupDirectory => Path.Combine(DataDirectory, "backups");
    public string TempDirectory => Path.Combine(DataDirectory, "tmp");
}
