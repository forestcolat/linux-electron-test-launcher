using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace FifaStreetLauncher;

// Wine/Proton only: if Viewer\ReStreetViewer.exe is not next to the launcher, download ReStreet-Viewer.zip once from the
// fixed "viewer" release of the repository (built by .github/workflows/publish-viewer.yml), check its SHA-256 and unpack it.
// The viewer never changes between launcher versions, so one release serves every launcher version.
// RESTREET_NO_VIEWER_DOWNLOAD=1 turns this off. Any failure only means the browser fallback is used, as before.
public partial class LauncherForm
{
    private const string ViewerZipUrl = "https://github.com/samuelit0davila/fifa-street-pc-port/releases/download/viewer/ReStreet-Viewer.zip";
    private const string ViewerShaUrl = ViewerZipUrl + ".sha256";

    private async Task EnsureViewerAsync()
    {
        if (Environment.GetEnvironmentVariable("RESTREET_NO_VIEWER_DOWNLOAD") == "1") return;
        string root = AppContext.BaseDirectory;
        string viewer = Path.Combine(root, "Viewer");
        if (File.Exists(Path.Combine(viewer, "ReStreetViewer.exe"))) return;
        string temp = Path.Combine(root, "Viewer.download");
        try
        {
            BrowserLog("Viewer missing; downloading " + ViewerZipUrl);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ReStreetLauncher/" + LauncherVersion);
            string expected = (await http.GetStringAsync(ViewerShaUrl)).Trim().Split(' ', '\t')[0].ToLowerInvariant();
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
            Directory.CreateDirectory(temp);
            string zip = Path.Combine(temp, "ReStreet-Viewer.zip");
            using (var response = await http.GetAsync(ViewerZipUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = File.Create(zip);
                await input.CopyToAsync(output);
            }
            string actual;
            await using (var check = File.OpenRead(zip))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(check)).ToLowerInvariant();
            if (actual != expected) { BrowserLog("Viewer download rejected: SHA-256 mismatch."); return; }
            string extracted = Path.Combine(temp, "extracted");
            await Task.Run(() => ZipFile.ExtractToDirectory(zip, extracted));
            // The zip holds a top-level "Viewer" folder (build-viewer.ps1 includes the base directory); accept a flat zip too.
            string unpacked = File.Exists(Path.Combine(extracted, "Viewer", "ReStreetViewer.exe")) ? Path.Combine(extracted, "Viewer") : extracted;
            if (!File.Exists(Path.Combine(unpacked, "ReStreetViewer.exe"))) { BrowserLog("Viewer download rejected: no ReStreetViewer.exe inside."); return; }
            if (Directory.Exists(viewer)) Directory.Delete(viewer, true);
            Directory.Move(unpacked, viewer);
            BrowserLog("Viewer installed at " + viewer);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or TaskCanceledException)
        {
            BrowserLog("Viewer download failed: " + ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Debug.WriteLine(ex.Message); }
        }
    }
}
