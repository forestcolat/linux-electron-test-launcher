using System.Diagnostics;
using System.Text.Json;

namespace ReStreetAssetScan;

// Player-facing front end for the DualSense patch: finds the ReStreet install by itself, checks the
// game is closed and runs apply / revert / status. Started with no arguments it shows a menu, so the
// published exe (and "Instalar DualSense.cmd" / "Remover DualSense.cmd") work with a double click.
//
//   install   [ReStreet folder]   apply the PS5 DualSense art (art from the DualSense folder next to the exe)
//   uninstall [ReStreet folder]   restore the original Xbox art
//   status    [ReStreet folder]
internal static class EasyInstall
{
    internal static int Run(string[] args)
    {
        int result;
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (Exception) { }
        try
        {
            string mode = args.Length > 0 ? args[0] : Menu();
            if (mode == "") return 0;
            string? folder = args.Length > 1 ? args[1] : null;
            string game = FindGame(folder);
            Console.WriteLine($"ReStreet encontrado: {game}");
            Console.WriteLine();
            if (mode != "status" && Process.GetProcessesByName("fifastreet").Length > 0)
                throw new InvalidOperationException("O jogo está aberto. Feche o FIFA Street (o launcher pode ficar aberto) e tente de novo.");
            result = mode switch
            {
                "install" => DualSense.Run("apply", game, null, null, "ps5"),
                "uninstall" => DualSense.Run("revert", game, null, null, null),
                "status" => DualSense.Run("status", game, null, null, null),
                _ => throw new ArgumentException($"Opção desconhecida: {mode}"),
            };
            Console.WriteLine();
            Console.WriteLine(result == 0
                ? mode switch
                {
                    "install" => "Pronto: o DualSense (PS5) foi aplicado. Antes de atualizar o ReStreet, use \"Remover DualSense\" e depois instale de novo.",
                    "uninstall" => "Pronto: o jogo voltou à arte original do Xbox.",
                    _ => "",
                }
                : "Não deu certo; veja a mensagem acima. Nada foi alterado se o erro apareceu antes de gravar.");
        }
        catch (Exception e)
        {
            Console.WriteLine("Erro: " + e.Message);
            result = 1;
        }
        Pause();
        return result;
    }

    static string Menu()
    {
        Console.WriteLine("ReStreet - botões de PlayStation 5 (DualSense)");
        Console.WriteLine();
        Console.WriteLine("  1  Instalar DualSense");
        Console.WriteLine("  2  Remover DualSense (voltar ao Xbox)");
        Console.WriteLine("  3  Ver o que está aplicado");
        Console.WriteLine();
        Console.Write("Escolha 1, 2 ou 3: ");
        string? answer = Console.ReadLine()?.Trim();
        Console.WriteLine();
        return answer switch { "1" => "install", "2" => "uninstall", "3" => "status", _ => "" };
    }

    static void Pause()
    {
        if (Console.IsInputRedirected) return;
        Console.WriteLine();
        Console.Write("Pressione Enter para fechar.");
        Console.ReadLine();
    }

    // ---- Finding the install ----------------------------------------------------------------

    // The Game folder (the one with data0.big and data1.big) of a ReStreet install.
    static string FindGame(string? given)
    {
        if (given is not null)
        {
            string path = Path.GetFullPath(given.Trim('"'));
            return GameFolder(path) ?? throw new DirectoryNotFoundException($"\"{path}\" não parece ser uma pasta do ReStreet (não achei Game\\data0.big).");
        }

        var found = new List<string>();
        void Add(string? root)
        {
            if (root is null) return;
            string? game = GameFolder(root);
            if (game is not null && !found.Contains(game, StringComparer.OrdinalIgnoreCase)) found.Add(game);
        }

        // 1. The exe sits inside the install (or one of its folders).
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent) Add(dir.FullName);
        // 2. The launcher shortcut the installer creates.
        foreach (string lnk in Shortcuts()) Add(Path.GetDirectoryName(ShortcutTarget(lnk)));
        if (found.Count == 1) return found[0];
        // 3. A shallow look at every fixed drive.
        if (found.Count == 0)
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
                foreach (string root in Folders(drive.RootDirectory.FullName, depth: 3)) Add(root);

        if (found.Count == 0)
            throw new DirectoryNotFoundException("Não achei o ReStreet. Arraste a pasta do ReStreet (a que tem o FifaStreetLauncher.exe) para cima de \"Instalar DualSense.cmd\".");
        if (found.Count == 1) return found[0];

        Console.WriteLine("Achei mais de uma instalação do ReStreet:");
        for (int i = 0; i < found.Count; i++) Console.WriteLine($"  {i + 1}  {found[i]}");
        if (Console.IsInputRedirected) throw new InvalidOperationException("Mais de uma instalação; passe a pasta do ReStreet como argumento.");
        Console.Write($"Qual delas (1-{found.Count})? ");
        if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= found.Count) { Console.WriteLine(); return found[choice - 1]; }
        throw new InvalidOperationException("Nenhuma instalação escolhida.");
    }

    // Accepts the ReStreet folder (uses the launcher's settings.json when there is one), its Game
    // folder or an older GameData folder.
    static string? GameFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return null;
            string settings = Path.Combine(path, "settings.json");
            if (File.Exists(settings) && File.Exists(Path.Combine(path, "FifaStreetLauncher.exe")))
            {
                using var json = JsonDocument.Parse(File.ReadAllText(settings));
                if (json.RootElement.TryGetProperty("GamePath", out var gamePath) && gamePath.GetString() is string g && HasData(g)) return Path.GetFullPath(g);
            }
            foreach (string candidate in new[] { path, Path.Combine(path, "Game"), Path.Combine(path, "GameData") })
                if (HasData(candidate)) return Path.GetFullPath(candidate);
        }
        catch (Exception) { }
        return null;

        static bool HasData(string folder) =>
            File.Exists(Path.Combine(folder, "data0.big")) && File.Exists(Path.Combine(folder, "data1.big")) && File.Exists(Path.Combine(folder, "data1.bh"));
    }

    static IEnumerable<string> Shortcuts()
    {
        var places = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        };
        foreach (string place in places.Where(p => p.Length > 0 && Directory.Exists(p)))
        {
            IEnumerable<string> links;
            try { links = Directory.EnumerateFiles(place, "*.lnk", SearchOption.AllDirectories).Where(f => Path.GetFileName(f).Contains("ReStreet", StringComparison.OrdinalIgnoreCase)).ToList(); }
            catch (Exception) { continue; }
            foreach (string link in links) yield return link;
        }
    }

    // Target of a .lnk, read from its LinkInfo local base path (MS-SHLLINK); null when absent.
    static string? ShortcutTarget(string lnk)
    {
        try
        {
            byte[] b = File.ReadAllBytes(lnk);
            if (b.Length < 0x4C || BitConverter.ToInt32(b, 0) != 0x4C) return null;
            int flags = BitConverter.ToInt32(b, 0x14), p = 0x4C;
            if ((flags & 0x01) != 0) p += 2 + BitConverter.ToUInt16(b, p);           // LinkTargetIDList
            if ((flags & 0x02) == 0) return null;                                       // no LinkInfo
            int localBase = BitConverter.ToInt32(b, p + 0x10);
            int end = Array.IndexOf(b, (byte)0, p + localBase);
            return System.Text.Encoding.Default.GetString(b, p + localBase, end - p - localBase);
        }
        catch (Exception) { return null; }
    }

    static IEnumerable<string> Folders(string root, int depth)
    {
        if (depth == 0) yield break;
        string[] children;
        try { children = Directory.GetDirectories(root); } catch (Exception) { yield break; }
        foreach (string child in children)
        {
            string name = Path.GetFileName(child);
            if (name.StartsWith('$') || name.Equals("Windows", StringComparison.OrdinalIgnoreCase)) continue;
            yield return child;
            foreach (string deeper in Folders(child, depth - 1)) yield return deeper;
        }
    }
}
