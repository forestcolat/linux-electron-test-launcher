using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FifaStreetLauncher.Linux;

internal static class InstallFolder
{
    // The ReStreet folder holds Binaries/fifastreet.exe and Game/default.xex. The Linux launcher is published to
    // <ReStreet>/Linux, so look beside it, then one level up, then in the current folder.
    public static string? Find()
    {
        foreach (string dir in new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, ".."), Environment.CurrentDirectory })
        {
            string full = Path.GetFullPath(dir);
            if (IsInstall(full)) return full;
        }
        return null;
    }

    public static bool IsInstall(string dir) => File.Exists(Path.Combine(dir, "Binaries", "fifastreet.exe"));

    // Wine maps the Linux root to Z:.
    public static string WindowsPath(string linuxPath) => "Z:" + Path.GetFullPath(linuxPath).Replace('/', '\\');
}

// Windows programs (the game, the setup) run through Proton. Two ways, best first:
//  - umu-run (umu-launcher), when it is installed: any Proton build, inside the Steam Linux Runtime;
//  - the Proton that Steam already has, run directly (inside Steam's own Linux Runtime when it is there). This needs
//    nothing installed, so it is the way on SteamOS, whose system is read-only.
internal static partial class Proton
{
    // RESTREET_NO_UMU=1 skips umu-run (a broken umu install, or testing the direct way).
    public static string? UmuRun() => Environment.GetEnvironmentVariable("RESTREET_NO_UMU") == "1" ? null :
        new[] { "/usr/bin/umu-run", "/usr/local/bin/umu-run", Path.Combine(Home, ".local/bin/umu-run") }.FirstOrDefault(File.Exists);

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // Steam's own folder (native install, the Deck, or the Flatpak).
    private static string? SteamRoot() =>
        new[]
        {
            Path.Combine(Home, ".local/share/Steam"), Path.Combine(Home, ".steam/steam"), Path.Combine(Home, ".steam/root"),
            Path.Combine(Home, ".var/app/com.valvesoftware.Steam/.local/share/Steam"),
        }.FirstOrDefault(d => Directory.Exists(Path.Combine(d, "steamapps")) || Directory.Exists(Path.Combine(d, "compatibilitytools.d")));

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPath();

    // Every steamapps/common folder: the main one and the other libraries (SD card on the Deck) in libraryfolders.vdf.
    private static IEnumerable<string> SteamCommonFolders()
    {
        string? root = SteamRoot();
        if (root == null) yield break;
        var seen = new HashSet<string>();
        var libraries = new List<string> { root };
        try
        {
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                libraries.AddRange(LibraryPath().Matches(File.ReadAllText(vdf)).Select(m => m.Groups[1].Value.Replace("\\\\", "\\")));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Debug.WriteLine(ex.Message); }
        foreach (string lib in libraries)
        {
            string common = Path.Combine(lib, "steamapps", "common");
            if (Directory.Exists(common) && seen.Add(Path.GetFullPath(common))) yield return common;
        }
    }

    // Proton builds the player has installed, best first: CachyOS and GE builds, then Valve's own. With umu-run and
    // none installed, PROTONPATH stays unset and umu-run fetches its default (UMU-Proton).
    public static List<string> Installed()
    {
        var found = new List<string>();
        var folders = new List<string> { "/usr/share/steam/compatibilitytools.d" };
        if (SteamRoot() is { } root) folders.Insert(0, Path.Combine(root, "compatibilitytools.d"));
        folders.AddRange(SteamCommonFolders());
        foreach (string dir in folders)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (string tool in Directory.GetDirectories(dir))
                if (File.Exists(Path.Combine(tool, "proton")) && !found.Any(f => Path.GetFileName(f) == Path.GetFileName(tool)))
                    found.Add(tool);
        }
        static int Rank(string p)
        {
            string n = Path.GetFileName(p).ToLowerInvariant();
            return n.Contains("cachyos") ? 0 : n.Contains("ge-proton") || n.Contains("proton-ge") ? 1 : n.Contains("experimental") ? 2 : 3;
        }
        return found.OrderBy(Rank).ThenByDescending(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Steam's container for Proton (what Steam itself uses for Proton 8 and newer). Without it Proton runs on the host
    // libraries, which works on SteamOS and on most desktops.
    private static string? SniperEntryPoint() =>
        SteamCommonFolders().Select(c => Path.Combine(c, "SteamLinuxRuntime_sniper", "_v2-entry-point")).FirstOrDefault(File.Exists);

    // What the launcher can run Windows programs with, or why it can't (shown on the page).
    public static string? Missing(List<string> protons) =>
        UmuRun() != null || protons.Count > 0 ? null
            : "Proton was not found. In Steam, install \"Proton Experimental\" (Library → Tools), or install umu-launcher.";

    public static string Describe(List<string> protons) =>
        (protons.Count > 0 ? Path.GetFileName(protons[0]) : "UMU-Proton") + (UmuRun() != null ? " (umu)" : "");

    public static string DefaultPrefix(string root)
    {
        // A prefix beside the install (how the first CachyOS installs were made) is kept; otherwise one of our own.
        string beside = Path.GetFullPath(Path.Combine(root, "..", "prefix-restreet"));
        if (Directory.Exists(beside)) return beside;
        return Path.Combine(Home, ".local/share/restreet/prefix");
    }

    // A Windows program started through Proton; the process ends with it ("waitforexitandrun").
    public static ProcessStartInfo StartInfo(string exe, string prefix, string? protonPath)
    {
        Directory.CreateDirectory(prefix);
        // umu-run and Proton use the same layout: the compat data folder with the Wine prefix in pfx/. umu points pfx/
        // at the folder itself, so a prefix made by either works with both.
        string pfx = Path.Combine(prefix, "pfx");
        if (!Directory.Exists(pfx) && !File.Exists(pfx)) File.CreateSymbolicLink(pfx, ".");

        ProcessStartInfo info;
        string? umu = UmuRun();
        if (umu != null)
        {
            info = new ProcessStartInfo(umu);
            info.ArgumentList.Add(exe);
            info.Environment["GAMEID"] = "umu-restreet";
            info.Environment["STORE"] = "none";
            if (!string.IsNullOrEmpty(protonPath)) info.Environment["PROTONPATH"] = protonPath;
        }
        else
        {
            if (string.IsNullOrEmpty(protonPath)) throw new InvalidOperationException(Missing(new List<string>()));
            string proton = Path.Combine(protonPath, "proton");
            string? sniper = SniperEntryPoint();
            if (sniper != null)
            {
                info = new ProcessStartInfo(sniper);
                foreach (string a in new[] { "--verb=waitforexitandrun", "--", proton, "waitforexitandrun", exe }) info.ArgumentList.Add(a);
            }
            else
            {
                info = new ProcessStartInfo(proton);
                info.ArgumentList.Add("waitforexitandrun");
                info.ArgumentList.Add(exe);
            }
            info.Environment["STEAM_COMPAT_DATA_PATH"] = prefix;
            info.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = SteamRoot() ?? Path.Combine(Home, ".local/share/Steam");
            info.Environment["SteamAppId"] = "0";
            info.Environment["SteamGameId"] = "0";
        }
        info.WorkingDirectory = Path.GetDirectoryName(exe)!;
        info.UseShellExecute = false;
        info.Environment["WINEPREFIX"] = prefix;
        return info;
    }
}
