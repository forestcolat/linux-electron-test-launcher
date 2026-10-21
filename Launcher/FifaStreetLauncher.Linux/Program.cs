using System.Diagnostics;
using Photino.NET;

namespace FifaStreetLauncher.Linux;

// ReStreet launcher for Linux: the Windows launcher's page (ui/launcher.html) in a native WebKitGTK window, with the
// game started through Proton. The page talks to this process over 127.0.0.1 exactly as it does with the Windows
// launcher under Wine (PageServer.cs), so the page needs no Linux-specific code. Without a ReStreet folder beside it,
// the launcher first installs ReStreet from the player's ISO (Setup.cs, ui/linux-setup.html).
internal static class Program
{
    private static int Main(string[] args)
    {
        string? root = null;
        bool play = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--root" && i + 1 < args.Length) root = args[++i];
            else if (args[i] == "--play") play = true;
        }
        root ??= InstallFolder.Find();
        string ui = Path.Combine(AppContext.BaseDirectory, "ui");

        if (root == null)
        {
            if (play)
            {
                Console.Error.WriteLine("ReStreet folder not found. Run from the ReStreet folder or pass --root <folder>.");
                return 1;
            }
            var setup = new Setup();
            using var setupServer = new PageServer(ui, setup, "linux-setup.html");
            setup.Page = setupServer;
            bool closed = false;
            ShowPage(setupServer, "ReStreet Setup", 980, 700, window =>
            {
                setup.CloseWindow = () => { closed = true; window?.Invoke(window.Close); };
                setup.PickFile = (title, start) => Pick(window, title, start, false);
                setup.PickFolder = (title, start) => Pick(window, title, start, true);
            }, () => closed || (!setup.Busy && DateTime.UtcNow - setupServer.LastSeen > TimeSpan.FromSeconds(90)));
            return 0;
        }

        var launcher = new Launcher(root);
        // Direct play: no window, the game starts with the saved settings and the launcher exits with it.
        if (play) return launcher.PlayAndWait();

        using var server = new PageServer(ui, launcher);
        launcher.Page = server;
        launcher.StartServices();
        bool launcherClosed = false;
        ShowPage(server, "ReStreet Launcher", 1250, 760,
            window => launcher.CloseWindow = () => { launcherClosed = true; window?.Invoke(window.Close); },
            () => launcherClosed || !launcher.KeepBrowserOpen(server));
        launcher.Shutdown();
        return 0;
    }

    // The page in its own WebKitGTK window, or, without WebKitGTK (or with RESTREET_BROWSER_UI=1), in the system
    // browser, as the Windows launcher does under Wine; then this process lives until done() says so.
    private static void ShowPage(PageServer server, string title, int width, int height, Action<PhotinoWindow?> setUp, Func<bool> done)
    {
        if (Environment.GetEnvironmentVariable("RESTREET_BROWSER_UI") != "1")
        {
            try
            {
                var window = new PhotinoWindow()
                    .SetLogVerbosity(0)
                    .SetTitle(title)
                    .SetUseOsDefaultSize(false)
                    .SetSize(width, height)
                    .SetMinSize(Math.Min(width, 1000), Math.Min(height, 620))
                    .Center()
                    .SetIconFile(Path.Combine(AppContext.BaseDirectory, "restreet.jpg"))
                    .SetContextMenuEnabled(false)
                    .SetDevToolsEnabled(false)
                    .Load(new Uri(server.Url));
                setUp(window);
                window.WaitForClose();
                return;
            }
            catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or EntryPointNotFoundException)
            {
                Console.Error.WriteLine("WebKitGTK is not available (" + ex.Message + "); opening the page in the browser.");
            }
        }
        setUp(null);
        Console.WriteLine("ReStreet page: " + server.Url);
        try { Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { server.Url }, UseShellExecute = false }); }
        catch (System.ComponentModel.Win32Exception) { Console.WriteLine("Open that address in your browser."); }
        while (!done()) Thread.Sleep(1000);
    }

    // A native file or folder dialog: Photino's (GTK) in the window, otherwise zenity or kdialog.
    private static async Task<string?> Pick(PhotinoWindow? window, string title, string start, bool folder)
    {
        if (window != null)
        {
            var result = new TaskCompletionSource<string?>();
            window.Invoke(async () =>
            {
                try
                {
                    string[] chosen = folder
                        ? await window.ShowOpenFolderAsync(title, start, false)
                        : await window.ShowOpenFileAsync(title, start, false, new[] { ("Xbox 360 ISO", new[] { "*.iso", "*.ISO" }), ("All files", new[] { "*" }) });
                    result.TrySetResult(chosen?.FirstOrDefault());
                }
                catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException) { result.TrySetResult(null); }
            });
            return await result.Task;
        }
        foreach (var (tool, toolArgs) in new (string, string[])[]
        {
            ("zenity", folder ? new[] { "--file-selection", "--directory", "--title=" + title, "--filename=" + start + "/" }
                              : new[] { "--file-selection", "--title=" + title, "--filename=" + start + "/", "--file-filter=*.iso *.ISO" }),
            ("kdialog", folder ? new[] { "--getexistingdirectory", start, "--title", title } : new[] { "--getopenfilename", start, "*.iso *.ISO", "--title", title }),
        })
        {
            try
            {
                var info = new ProcessStartInfo(tool) { RedirectStandardOutput = true, UseShellExecute = false };
                foreach (string a in toolArgs) info.ArgumentList.Add(a);
                using var process = Process.Start(info);
                if (process == null) continue;
                string path = (await process.StandardOutput.ReadToEndAsync()).Trim();
                await process.WaitForExitAsync();
                return path.Length > 0 ? path : null;
            }
            catch (System.ComponentModel.Win32Exception) { /* not installed: try the next one */ }
        }
        return null;
    }
}
