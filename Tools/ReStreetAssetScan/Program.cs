using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Content;

namespace ReStreetAssetScan;

// Explorer for FIFA Street's EA BIG archives, and the DualSense patcher. Only the patch commands
// ("install"/"uninstall", "dualsense apply/revert") write to the game folder.
//   (no arguments), install, uninstall, status [ReStreet folder]   player front end (EasyInstall.cs)
//   list    <game folder> [--filter regex]             every entry, nested BIGs included
//   extract <game folder> <output> [--filter regex] [--png]   decoded copies of the matching entries
//   buttons <game folder> <output>                     controller-icon candidates as PNG + report
internal static class Program
{
    internal sealed record Entry(string Name, int Offset, int Length, int TablePosition);
    sealed record Asset(string Path, byte[] Data, int StoredLength);

    // Where the controller art lives. The menus load the proxy art by path
    // ("external/ion_sports07/platform/proxy/art/A" and so on); the tricks folder has an xenon
    // and a ps3 copy of every skill-move picture.
    static readonly (string Group, string Prefix)[] ButtonGroups =
    {
        ("Button prompts (menus and HUD)", "data/ui/external/ion_sports07/platform/proxy/art/"),
        ("Skill-move pictures", "data/ui/imgassets/tricks/"),
        ("Controller pictures", "data/ui/artassets/controllerimage/"),
        ("Controller pictures", "data/ui/artassets/matchsettingicons/controller.big"),
    };
    const string ProxyArtReference = "external/ion_sports07/platform/proxy/art/";

    internal static uint U(byte[] b, int p) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(p, 4));
    internal static void Require(bool ok, string message) { if (!ok) throw new InvalidDataException(message); }

    static int Main(string[] args)
    {
        try
        {
            // No arguments (double click) or install / uninstall / status: the player front end.
            if (args.Length == 0 || args[0] is "install" or "uninstall" or "status") return EasyInstall.Run(args);
            if (args.Length >= 3 && args[0] == "dualsense")
                return DualSense.Run(args[1], args[2], args.Length > 3 && !args[3].StartsWith("--") ? args[3] : null,
                    Option(args, "--art"), Option(args, "--controller"));
            if (args.Length < 2) return Usage();
            string game = args[1];
            Require(Directory.Exists(game), $"Folder not found: {game}");
            string? filter = Option(args, "--filter");
            var regex = filter is null ? null : new Regex(filter, RegexOptions.IgnoreCase);
            switch (args[0])
            {
                case "list":
                    foreach (var asset in Walk(game, deep: true))
                        if (regex is null || regex.IsMatch(asset.Path))
                            Console.WriteLine($"{asset.StoredLength,10} {asset.Data.Length,10}  {Kind(asset.Data),-9} {asset.Path}");
                    return 0;
                case "extract":
                    Require(args.Length >= 3, "Missing output folder.");
                    int count = 0; bool png = args.Contains("--png");
                    foreach (var asset in Walk(game, deep: true))
                        if (regex is null || regex.IsMatch(asset.Path)) { Save(args[2], asset, png); count++; }
                    Console.WriteLine($"{count} entries written to {args[2]}");
                    return 0;
                case "buttons":
                    Require(args.Length >= 3, "Missing output folder.");
                    return Buttons(game, args[2]);
                default:
                    return Usage();
            }
        }
        catch (Exception e) { Console.Error.WriteLine("error: " + e.Message); return 1; }
    }

    static int Usage()
    {
        Console.Error.WriteLine("ReStreetAssetScan [install|uninstall|status] [ReStreet folder]");
        Console.Error.WriteLine("ReStreetAssetScan list <game folder> [--filter regex]");
        Console.Error.WriteLine("ReStreetAssetScan extract <game folder> <output> [--filter regex] [--png]");
        Console.Error.WriteLine("ReStreetAssetScan buttons <game folder> <output>");
        Console.Error.WriteLine("ReStreetAssetScan dualsense preview <game folder> <output> --controller ps5 [--art folder]");
        Console.Error.WriteLine("ReStreetAssetScan dualsense apply <game folder> --controller ps5 [--art folder]");
        Console.Error.WriteLine("ReStreetAssetScan dualsense revert|status <game folder>");
        return 2;
    }

    static string? Option(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    // ---- Controller icons -------------------------------------------------------------------

    static int Buttons(string game, string output)
    {
        Directory.CreateDirectory(output);
        var textures = ButtonGroups.Select(g => g.Group).Distinct().ToDictionary(g => g, _ => new List<string>());
        var references = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        int saved = 0;
        // The UI is in data0.big and data1.big; the other archives hold models, kits and audio.
        foreach (var asset in Walk(game, deep: true, archive => Path.GetFileName(archive) is "data0.big" or "data1.big"))
        {
            string inner = asset.Path[(asset.Path.IndexOf('!') + 1)..];
            var group = ButtonGroups.FirstOrDefault(g => inner.StartsWith(g.Prefix, StringComparison.OrdinalIgnoreCase));
            if (group.Group is not null)
            {
                Save(output, asset, png: true); saved++;
                foreach (var (suffix, image) in Textures.ToPng(asset.Data))
                {
                    int w = BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16)), h = BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20));
                    textures[group.Group].Add($"| `{inner}{suffix}` | {Kind(asset.Data)} | {w}x{h} |");
                }
            }
            else if (Kind(asset.Data) == "apt")
            {
                // Which screens draw which prompt: the Apt movie names the proxy art it imports.
                string text = Encoding.Latin1.GetString(asset.Data);
                for (int at = text.IndexOf(ProxyArtReference, StringComparison.OrdinalIgnoreCase); at >= 0;
                     at = text.IndexOf(ProxyArtReference, at + 1, StringComparison.OrdinalIgnoreCase))
                {
                    int end = at + ProxyArtReference.Length;
                    while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
                    string button = text[(at + ProxyArtReference.Length)..end];
                    if (button.Length == 0) continue;
                    string screen = inner.Contains(".big!") ? inner[..(inner.IndexOf(".big!") + 4)] : inner;
                    if (!references.TryGetValue(button.ToUpperInvariant(), out var screens)) references[button.ToUpperInvariant()] = screens = new(StringComparer.OrdinalIgnoreCase);
                    screens.Add(screen);
                }
            }
        }

        var report = new StringBuilder();
        report.AppendLine("# Controller art in FIFA Street");
        report.AppendLine();
        report.AppendLine("Game folder: " + Path.GetFullPath(game));
        report.AppendLine();
        report.AppendLine("Every texture below was saved as a PNG next to its decoded entry in this folder.");
        foreach (var (group, rows) in textures)
        {
            report.AppendLine(); report.AppendLine($"## {group} ({rows.Count})"); report.AppendLine();
            report.AppendLine("| Entry | Format | Size |"); report.AppendLine("|---|---|---|");
            foreach (string row in rows) report.AppendLine(row);
        }
        report.AppendLine(); report.AppendLine($"## Screens that use each button prompt ({references.Count} prompts)"); report.AppendLine();
        report.AppendLine("| Prompt | Screens | Examples |"); report.AppendLine("|---|---|---|");
        foreach (var (button, screens) in references)
            report.AppendLine($"| {button} | {screens.Count} | {string.Join(", ", screens.Take(3).Select(s => $"`{s}`"))} |");
        File.WriteAllText(Path.Combine(output, "REPORT.md"), report.ToString());
        Console.WriteLine($"{saved} entries and {textures.Values.Sum(r => r.Count)} PNGs written to {output} (see REPORT.md)");
        return 0;
    }

    // ---- Archive walking --------------------------------------------------------------------

    static IEnumerable<Asset> Walk(string game, bool deep, Func<string, bool>? archives = null)
    {
        foreach (string file in Directory.EnumerateFiles(game, "*.big", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (archives is not null && !archives(file)) continue;
            string relative = Path.GetRelativePath(game, file).Replace('\\', '/');
            using var input = File.OpenRead(file);
            var header = new byte[16];
            if (input.Read(header) != 16 || !IsBig(header)) continue;
            int tableEnd = (int)U(header, 12);
            Require(tableEnd <= 8 * 1024 * 1024, $"Invalid directory size in {relative}");
            Array.Resize(ref header, tableEnd); input.ReadExactly(header.AsSpan(16));
            foreach (var e in Index(header, input.Length))
            {
                // Huge streamed payloads (audio, movies) are listed but not decoded.
                if (e.Length > 64 * 1024 * 1024) { yield return new Asset($"{relative}!{e.Name}", Array.Empty<byte>(), e.Length); continue; }
                var data = new byte[e.Length]; input.Position = e.Offset; input.ReadExactly(data);
                foreach (var asset in Expand($"{relative}!{e.Name}", data, deep)) yield return asset;
            }
        }
    }

    static IEnumerable<Asset> Expand(string path, byte[] stored, bool deep)
    {
        byte[] data = stored;
        try { data = Decode(stored); }
        catch (Exception e) { Console.Error.WriteLine($"warning: {path}: {e.Message}"); }
        yield return new Asset(path, data, stored.Length);
        if (!deep || !IsBig(data)) yield break;
        List<Entry> entries;
        try { entries = Index(data, data.Length); }
        catch (Exception e) { Console.Error.WriteLine($"warning: {path}: {e.Message}"); yield break; }
        foreach (var e in entries)
            foreach (var asset in Expand($"{path}!{e.Name}", data.AsSpan(e.Offset, e.Length).ToArray(), deep))
                yield return asset;
    }

    internal static bool IsBig(byte[] b) => b.Length >= 16 && Encoding.ASCII.GetString(b, 0, 4) is "BIG4" or "BIGF";

    internal static List<Entry> Index(byte[] b, long fileLength)
    {
        Require(IsBig(b), "Unsupported BIG archive.");
        int count = (int)U(b, 8), end = (int)U(b, 12), p = 16;
        Require(count <= 100000 && end <= b.Length, "Invalid BIG directory.");
        var entries = new List<Entry>();
        for (int i = 0; i < count; i++)
        {
            Require(p + 8 < end, "Truncated BIG directory.");
            int offset = (int)U(b, p), length = (int)U(b, p + 4), zero = Array.IndexOf(b, (byte)0, p + 8, end - p - 8);
            Require(zero >= 0 && (long)offset + length <= fileLength, "Invalid BIG entry.");
            entries.Add(new(Encoding.UTF8.GetString(b, p + 8, zero - p - 8), offset, length, p)); p = zero + 1;
        }
        return entries;
    }

    // ---- Decoding (same formats as Installer/FifaStreetSetupTool/CreditPatch.cs) -------------

    internal static byte[] Decode(byte[] b)
    {
        if (b.Length > 48 && Encoding.ASCII.GetString(b, 0, 8) is "chunklzx" or "chunkzip") b = DecodeChunk(b);
        return DecodeRefPack(b);
    }

    // "chunklzx" holds Xbox LZX chunks (type 3), "chunkzip" raw deflate chunks (type 1); type 4 is stored.
    static byte[] DecodeChunk(byte[] b)
    {
        Require(U(b, 8) == 2, "Unsupported chunk container version.");
        int full = (int)U(b, 12), chunk = (int)U(b, 16), p = 40;
        Require(full <= 256 * 1024 * 1024 && chunk is > 0 and <= 1024 * 1024, "Invalid chunklzx size.");
        using var output = new MemoryStream(full);
        while (output.Length < full)
        {
            p = (p + 7) / 16 * 16 + 8;
            int n = (int)U(b, p), kind = (int)U(b, p + 4); p += 8;
            Require(n > 0 && p + (long)n <= b.Length, "Truncated chunk.");
            int target = Math.Min(chunk, full - (int)output.Length), end = p + n;
            if (kind == 4) { Require(n == target, "Invalid raw chunk."); output.Write(b, p, n); p = end; continue; }
            if (kind == 1)
            {
                long before = output.Length;
                using (var deflate = new System.IO.Compression.DeflateStream(new MemoryStream(b, p, n, false), System.IO.Compression.CompressionMode.Decompress))
                    deflate.CopyTo(output);
                Require(output.Length - before == target, "Invalid deflate chunk."); p = end; continue;
            }
            Require(kind == 3, "Unknown chunk type.");
            var decoder = new LzxDecoder(17); int produced = 0;
            while (p < end && produced < target)
            {
                int outSize = 32768, inSize;
                if (b[p] == 255) { outSize = (b[p + 1] << 8) | b[p + 2]; inSize = (b[p + 3] << 8) | b[p + 4]; p += 5; }
                else { inSize = (b[p] << 8) | b[p + 1]; p += 2; }
                Require(inSize > 0 && p + inSize <= end && outSize > 0 && produced + outSize <= target, "Invalid LZX frame.");
                using var input = new MemoryStream(b, p, inSize, false);
                Require(decoder.Decompress(input, inSize, output, outSize) == 0, "LZX decompression failed.");
                p += inSize; produced += outSize;
            }
            Require(produced == target, "Incomplete chunk."); p = end;
        }
        return output.ToArray();
    }

    static byte[] DecodeRefPack(byte[] b)
    {
        if (b.Length < 5 || b[0] != 0x10 || b[1] != 0xfb) return b;
        int length = (b[2] << 16) | (b[3] << 8) | b[4], p = 5, at = 0;
        Require(length <= 64 * 1024 * 1024, "Invalid RefPack size."); var result = new byte[length];
        while (at < length && p < b.Length)
        {
            int c = b[p++], literals, copies = 0, distance = 0;
            if (c < 0x80) { int d = b[p++]; literals = c & 3; copies = ((c & 0x1c) >> 2) + 3; distance = ((c & 0x60) << 3) + d + 1; }
            else if (c < 0xc0) { int d = b[p++], e = b[p++]; literals = d >> 6; copies = (c & 0x3f) + 4; distance = ((d & 0x3f) << 8) + e + 1; }
            else if (c < 0xe0) { int d = b[p++], e = b[p++], f = b[p++]; literals = c & 3; copies = ((c & 0x0c) << 6) + f + 5; distance = ((c & 0x10) << 12) + (d << 8) + e + 1; }
            else if (c < 0xfc) literals = ((c & 0x1f) << 2) + 4;
            else literals = c & 3;
            Require(p + literals <= b.Length && at + literals + copies <= length, "Invalid RefPack data.");
            Buffer.BlockCopy(b, p, result, at, literals); p += literals; at += literals;
            Require(copies == 0 || distance <= at, "Invalid RefPack reference.");
            for (int i = 0; i < copies; i++) { result[at] = result[at - distance]; at++; }
            if (c >= 0xfc) break;
        }
        Require(at == length, "Incomplete RefPack data."); return result;
    }

    // ---- Output -----------------------------------------------------------------------------

    static string Kind(byte[] b)
    {
        if (b.Length == 0) return "stream";
        if (IsBig(b)) return "big";
        string magic = Encoding.ASCII.GetString(b, 0, Math.Min(4, b.Length));
        return magic switch
        {
            "SHPS" or "SHPX" => "fsh",
            "DDS " => "dds",
            "XPR2" => "xpr2",
            "Apt " => "apt",
            "\x89PNG" => "png",
            "OTTO" => "otf",
            "RIFF" => "riff",
            "FWS" or "CWS" => "swf",
            _ when b.Length >= 4 && BinaryPrimitives.ReadUInt32BigEndian(b) == 0x00010000 => "ttf",
            _ when b.Length >= 3 && Encoding.ASCII.GetString(b, 0, 3) is "FWS" or "CWS" or "GFX" or "CFX" => "swf",
            _ when b.Take(256).All(c => c is 9 or 10 or 13 or (>= 32 and < 127)) => "text",
            _ => "bin:" + Convert.ToHexString(b, 0, Math.Min(4, b.Length)).ToLowerInvariant(),
        };
    }

    static void Save(string output, Asset asset, bool png)
    {
        if (asset.Data.Length == 0) return;
        string relative = string.Join('/', asset.Path.Split('!').Select(part => part.Replace(':', '_')));
        string target = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
        // A nested BIG is saved as "name.big.raw" so its folder of entries can sit next to it.
        if (IsBig(asset.Data)) target += ".raw";
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllBytes(target, asset.Data);
        if (!png) return;
        try { foreach (var (suffix, image) in Textures.ToPng(asset.Data)) File.WriteAllBytes(target + suffix + ".png", image); }
        catch (Exception e) { Console.Error.WriteLine($"warning: {asset.Path}: PNG conversion failed: {e.Message}"); }
    }
}
