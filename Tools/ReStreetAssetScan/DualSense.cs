using System.Buffers.Binary;
using System.Text;

namespace ReStreetAssetScan;

// Swaps FIFA Street's Xbox 360 button art for DualSense art, inside the user's own game files.
//
//   preview <game folder> <output> --controller ps5   writes before/after PNGs, touches nothing
//   apply   <game folder> --controller ps5            patches data0.big and data1.big (and their .bh indexes)
//   revert  <game folder>                             puts both archives back exactly as they were
//   status  <game folder>                             says which controller art is applied
//   --art <folder>                                    use the PNGs found there instead of the bundled ones
//
// The controller is one byte (Controller below); it is stored in the backup so the install
// records what was applied. Only the PS5 DualSense art exists so far.
//
// Menu and HUD prompts: the game's own texture is kept (outline, shading, padding) and only the
// symbol inside its black face is repainted with a glyph from the DualSense folder.
// Skill-move pictures: the game ships a PS3 set next to the Xbox one, so the Xbox entries are
// pointed at the PS3 data, and the in-match copy (proxy/art/tricks.big) is rebuilt from them.
//
// Changed entries are appended to the archive and the directory is rewritten, as the installer's
// credit patch does. Before the first write the original directory, index and file length are
// saved to "<archive>.dualsense-backup", which is all "revert" needs.
internal static class DualSense
{
    // Which PlayStation pad the art is for. The value is the byte stored in the backup.
    internal enum Controller : byte { PS4 = 0x04, PS5 = 0x05 }

    static readonly Controller[] Available = { Controller.PS5 };

    static Controller ParseController(string? value)
    {
        Program.Require(value is not null, "Pass --controller ps5 (the PS5 DualSense art).");
        Controller controller = value!.Trim().ToLowerInvariant() switch
        {
            "ps5" or "dualsense" or "5" or "0x05" => Controller.PS5,
            "ps4" or "dualshock4" or "ds4" or "4" or "0x04" => Controller.PS4,
            _ => throw new InvalidDataException($"Unknown controller \"{value}\"; use ps5."),
        };
        Program.Require(Available.Contains(controller), $"{Describe(controller)} art is not available yet; use --controller ps5.");
        return controller;
    }

    internal static string Describe(Controller controller) => controller switch
    {
        Controller.PS5 => "PS5 DualSense",
        Controller.PS4 => "PS4 DualShock 4",
        _ => $"controller 0x{(byte)controller:x2}",
    };

    const string Art = "data/ui/external/ion_sports07/platform/proxy/art/";
    const string Tricks = "data/ui/imgassets/tricks/";

    // (archive inside data0.big, texture entry, glyph)
    static readonly (string Big, string Entry, string Glyph)[] Prompts =
    {
        ("a.big", "2", "cross"), ("b.big", "2", "circle"), ("x.big", "2", "square"), ("y.big", "2", "triangle"),
        ("lb.big", "2", "l1"), ("rb.big", "2", "r1"), ("lt.big", "2", "l2"), ("rt.big", "2", "r2"),
        ("lbrb.big", "2", "l1"), ("lbrb.big", "5", "r1"), ("ltrt.big", "2", "l2"), ("ltrt.big", "5", "r2"),
        ("l3.big", "2", "l3"), ("r3.big", "2", "r3"),
        ("las.big", "2", "l"), ("las_all.big", "2", "l"), ("las_d.big", "2", "l"), ("las_l.big", "2", "l"), ("las_lr.big", "2", "l"),
        ("las_none.big", "2", "l"), ("las_r.big", "2", "l"), ("las_u.big", "2", "l"), ("las_ud.big", "2", "l"),
        ("ras.big", "2", "r"), ("ras_all.big", "2", "r"), ("ras_d.big", "2", "r"), ("ras_l.big", "2", "r"), ("ras_lr.big", "2", "r"),
        ("ras_none.big", "2", "r"), ("ras_r.big", "2", "r"), ("ras_u.big", "2", "r"), ("ras_ud.big", "2", "r"),
        ("start.big", "2", "options"), ("select.big", "2", "create"), ("hud.big", "2", "ps"),
    };

    // Select Sides controller art: (archive inside data1.big, texture entry -> PNG in DualSense/).
    static readonly (string Big, Dictionary<string, string> Textures)[] Controllers =
    {
        (Art + "controllers.big", new() { ["5"] = "controller.png", ["2"] = "controller_bar.png" }),
        ("data/ui/game/components/fifasideselector.big",
            new() { ["33"] = "sideselector.png", ["30"] = "sideselector_bar.png", ["8"] = "sideselector_silhouette.png" }),
    };

    // Screens that draw the controller with call-out lines (archives inside data1.big). Each has an
    // Xbox and a PS3 layout chosen by PLATFORM == "PS3"; pointing their "PS3" constant at "XENON"
    // makes them take the PS3 one (DualShock picture and its lines), which matches a DualSense.
    static readonly string[] PlayStationLayoutScreens =
    {
        "data/ui/game/screens/settings/controllersetup.big",
        "data/ui/game/screens/settings/controllerconfiguration.big",
        // These embed both pictures (Xbox 360 and DualShock 3) instead of loading them by path.
        "data/ui/game/overlays/overlay_79.big",       // loading screen: controls + tips
        "data/ui/game/overlays/overlay_87.big",       // loading screen: controls + tips (second variant)
        "data/ui/game/screens/misc/ingamemanual.big", // pause menu > Game Manual
    };

    // The DualShock 3 photo the PlayStation layout's lines point at: (archive in data1.big, texture
    // entry, art). Built by controls_from_image.py; without that art the photo stays.
    static readonly (string Big, string Entry, string Art)[] ControlsPictures =
    {
        ("data/ui/game/overlays/overlay_79.big", "74", "controls.png"),
        ("data/ui/game/overlays/overlay_87.big", "80", "controls.png"),
        ("data/ui/game/screens/misc/ingamemanual.big", "17", "controls.png"),
        ("data/ui/artassets/controllerimage/controller_ps3.big", "2", "controls_large.png"),
    };

    // How much of the black face a glyph may fill.
    static double Fill(string glyph) => glyph switch
    {
        "cross" or "circle" or "square" or "triangle" => 0.6,
        "options" or "create" => 0.5,
        "l3" or "r3" => 0.56,
        "l1" or "r1" or "l2" or "r2" => 0.64,
        _ => 0.72,
    };

    internal static int Run(string mode, string game, string? output, string? artFolder, string? controllerFlag)
    {
        try
        {
            Program.Require(Directory.Exists(game), $"Folder not found: {game}");
            string data0 = Path.Combine(game, "data0.big"), data1 = Path.Combine(game, "data1.big");
            Program.Require(File.Exists(data0) && File.Exists(data1), "data0.big and data1.big were not found; pass the ReStreet Game folder.");
            switch (mode)
            {
                case "preview":
                    Program.Require(output is not null, "Missing output folder.");
                    var previewed = ParseController(controllerFlag);
                    Directory.CreateDirectory(output!);
                    Patch(data0, data1, output, write: false, artFolder, previewed);
                    return 0;
                case "apply":
                    var controller = ParseController(controllerFlag);
                    if (Archive.HasBackup(data0) || Archive.HasBackup(data1))
                    {
                        var before = Archive.AppliedController(data0);
                        Console.WriteLine($"{(before is null ? "PlayStation" : Describe(before.Value))} art is already applied; restoring the originals first.");
                        Archive.Revert(data0); Archive.Revert(data1);
                    }
                    Patch(data0, data1, null, write: true, artFolder, controller);
                    Console.WriteLine($"{Describe(controller)} art applied (controller byte 0x{(byte)controller:x2}). Undo with: dualsense revert <game folder>");
                    return 0;
                case "status":
                    if (!Archive.HasBackup(data0) && !Archive.HasBackup(data1)) { Console.WriteLine("Original Xbox art (nothing applied)."); return 0; }
                    var applied = Archive.AppliedController(data0) ?? Archive.AppliedController(data1);
                    Console.WriteLine(applied is null ? "PlayStation art applied by an older patcher (controller not recorded)."
                        : $"{Describe(applied.Value)} art applied (controller byte 0x{(byte)applied.Value:x2}).");
                    return 0;
                case "revert":
                    bool any = Archive.Revert(data0) | Archive.Revert(data1);
                    Console.WriteLine(any ? "Original Xbox art restored." : "Nothing to revert.");
                    return 0;
                default:
                    Console.Error.WriteLine("dualsense preview|apply|revert|status <game folder> [output] --controller ps5");
                    return 2;
            }
        }
        catch (Exception e) { Console.Error.WriteLine("error: " + e.Message); return 1; }
    }

    static void Patch(string data0, string data1, string? preview, bool write, string? artFolder, Controller controller)
    {
        // Art files come from --art when it has them, otherwise from the DualSense folder next to the tool.
        if (artFolder is not null) Program.Require(Directory.Exists(artFolder), $"Art folder not found: {artFolder}");
        string Find(string file) => artFolder is not null && File.Exists(Path.Combine(artFolder, file))
            ? Path.Combine(artFolder, file) : Path.Combine(AppContext.BaseDirectory, "DualSense", file);
        var cache = new Dictionary<string, (byte[] Rgba, int Width, int Height)>();

        using (var archive = Archive.Open(data0, write))
        {
            foreach (var group in Prompts.GroupBy(p => p.Big))
            {
                string name = Art + group.Key;
                byte[] inner = Program.Decode(archive.Read(name));
                var entries = Program.Index(inner, inner.Length).ToDictionary(e => e.Name);
                var replaced = new Dictionary<string, byte[]>();
                foreach (var prompt in group)
                {
                    if (!cache.TryGetValue(prompt.Glyph, out var glyph))
                        cache[prompt.Glyph] = glyph = Textures.ReadPng(File.ReadAllBytes(Find(prompt.Glyph + ".png")));
                    var entry = entries[prompt.Entry];
                    byte[] xpr = Program.Decode(inner.AsSpan(entry.Offset, entry.Length).ToArray());
                    var texture = Textures.FirstXpr2Texture(xpr) ?? throw new InvalidDataException($"{name}!{prompt.Entry} is not a texture.");
                    byte[] before = texture.Read(xpr), after = (byte[])before.Clone();
                    Program.Require(Repaint(after, texture.Width, texture.Height, glyph, Fill(prompt.Glyph)), $"{name}!{prompt.Entry}: no button face found.");
                    texture.Write(xpr, after);
                    replaced[prompt.Entry] = xpr;
                    Preview(preview, $"{Path.GetFileNameWithoutExtension(group.Key)}_{prompt.Entry}", texture, before, texture.Read(xpr));
                }
                archive.Replace(name, Rebuild(inner, replaced));
            }
            if (write) archive.Commit(controller);
        }
        Console.WriteLine($"{Prompts.Length} button prompts repainted.");

        using (var archive = Archive.Open(data1, write))
        {
            // Skill-move list: point every xenon picture at its PS3 twin.
            var pictures = new Dictionary<int, (byte[] Xenon, byte[] Ps3)>();
            int aliased = 0;
            foreach (string xenon in archive.Names.Where(n => n.StartsWith(Tricks + "xenon/", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                string ps3 = Tricks + "ps3/" + xenon[(Tricks.Length + "xenon/".Length)..];
                if (!archive.Contains(ps3)) continue;
                string file = Path.GetFileName(xenon);
                if (file.StartsWith("trick_") && int.TryParse(file[6..file.IndexOf('_', 6)], out int number))
                    pictures[number] = (Program.Decode(archive.Read(xenon)), Program.Decode(archive.Read(ps3)));
                archive.Alias(xenon, ps3); aliased++;
            }

            // In-match hints: texture 2k of tricks.big is skill move 38 + k. Moving the PS3 picture's
            // difference from the Xbox one onto the in-match texture keeps its brighter arrows.
            string name = Art + "tricks.big";
            byte[] inner = Program.Decode(archive.Read(name));
            var replacedTricks = new Dictionary<string, byte[]>();
            foreach (var entry in Program.Index(inner, inner.Length))
            {
                if (!int.TryParse(entry.Name, out int id) || id % 2 != 0 || !pictures.TryGetValue(38 + id / 2, out var picture)) continue;
                byte[] xpr = Program.Decode(inner.AsSpan(entry.Offset, entry.Length).ToArray());
                var texture = Textures.FirstXpr2Texture(xpr);
                var xenon = Textures.DecodeDds(picture.Xenon); var ps3 = Textures.DecodeDds(picture.Ps3);
                if (texture is null || xenon is null || ps3 is null || xenon.Value.Width != texture.Width || xenon.Value.Height != texture.Height
                    || ps3.Value.Width != texture.Width || ps3.Value.Height != texture.Height)
                { Console.Error.WriteLine($"warning: tricks.big!{entry.Name} skipped (size mismatch)"); continue; }
                byte[] before = texture.Read(xpr), after = new byte[before.Length];
                for (int i = 0; i < after.Length; i++) after[i] = (byte)Math.Clamp(before[i] + ps3.Value.Rgba[i] - xenon.Value.Rgba[i], 0, 255);
                texture.Write(xpr, after);
                replacedTricks[entry.Name] = xpr;
                Preview(preview, $"tricks_{entry.Name}", texture, before, texture.Read(xpr));
            }
            archive.Replace(name, Rebuild(inner, replacedTricks));
            Console.WriteLine($"{aliased} skill-move pictures switched to the PS3 set, {replacedTricks.Count} in-match hints rebuilt.");

            // Select Sides: each controller set is a pad, the strip tinted with the player's colour
            // and (offline screen) the empty-slot silhouette, replaced whole with DualSense art.
            foreach (var (big, textures) in Controllers)
            {
                name = big;
                inner = Program.Decode(archive.Read(name));
                var replaced = new Dictionary<string, byte[]>();
                foreach (var entry in Program.Index(inner, inner.Length))
                {
                    if (!textures.TryGetValue(entry.Name, out string? art)) continue;
                    byte[] xpr = Program.Decode(inner.AsSpan(entry.Offset, entry.Length).ToArray());
                    var texture = Textures.FirstXpr2Texture(xpr) ?? throw new InvalidDataException($"{name}!{entry.Name} is not a texture.");
                    var image = Textures.ReadPng(File.ReadAllBytes(Find(art)));
                    Program.Require(image.Width == texture.Width && image.Height == texture.Height, $"{art} must be {texture.Width}x{texture.Height}.");
                    byte[] before = texture.Read(xpr);
                    texture.Write(xpr, image.Rgba);
                    replaced[entry.Name] = xpr;
                    Preview(preview, $"{Path.GetFileNameWithoutExtension(big)}_{entry.Name}", texture, before, texture.Read(xpr));
                }
                Program.Require(replaced.Count == textures.Count, $"{big} does not have the expected textures.");
                archive.Replace(name, Rebuild(inner, replaced));
            }
            Console.WriteLine("Select Sides controller replaced with a DualSense.");

            // Controls screens: PlayStation layout, and the DualShock 3 photo its lines point at
            // replaced with the controls art when there is one (it is made from your own picture).
            bool havePicture = ControlsPictures.All(p => File.Exists(Find(p.Art)));
            int replacedPictures = 0;
            foreach (string screen in PlayStationLayoutScreens.Concat(ControlsPictures.Select(p => p.Big)).Distinct())
            {
                name = screen;
                inner = Program.Decode(archive.Read(name));
                var entries = Program.Index(inner, inner.Length).ToDictionary(e => e.Name);
                var replaced = new Dictionary<string, byte[]>();
                if (PlayStationLayoutScreens.Contains(screen))
                {
                    byte[] table = Program.Decode(inner.AsSpan(entries["1"].Offset, entries["1"].Length).ToArray());
                    replaced["1"] = RepointConstant(table, "PS3", "XENON");
                }
                foreach (var (_, entryName, art) in havePicture ? ControlsPictures.Where(p => p.Big == screen) : Enumerable.Empty<(string, string, string)>())
                {
                    var entry = entries[entryName];
                    byte[] xpr = Program.Decode(inner.AsSpan(entry.Offset, entry.Length).ToArray());
                    var texture = Textures.FirstXpr2Texture(xpr) ?? throw new InvalidDataException($"{name}!{entryName} is not a texture.");
                    var image = Textures.ReadPng(File.ReadAllBytes(Find(art)));
                    Program.Require(image.Width == texture.Width && image.Height == texture.Height, $"{art} must be {texture.Width}x{texture.Height}.");
                    byte[] before = texture.Read(xpr);
                    texture.Write(xpr, image.Rgba);
                    replaced[entryName] = xpr;
                    Preview(preview, $"{Path.GetFileNameWithoutExtension(screen)}_{entryName}", texture, before, texture.Read(xpr));
                    replacedPictures++;
                }
                if (replaced.Count > 0) archive.Replace(name, Rebuild(inner, replaced));
            }
            if (write) archive.Commit(controller);
            Console.WriteLine($"{PlayStationLayoutScreens.Length} controls screens switched to their PlayStation layout" +
                (havePicture ? $", {replacedPictures} controller pictures replaced." : " (no controls art: they keep the DualShock 3 photo)."));
        }
    }

    // Writes a nested BIG again with some entries replaced (stored uncompressed, which the game
    // accepts, as the credit patch shows); the other entries keep their original bytes.
    static byte[] Rebuild(byte[] original, Dictionary<string, byte[]> replaced)
    {
        var entries = Program.Index(original, original.Length);
        int headerLength = 16 + entries.Sum(e => 9 + Encoding.UTF8.GetByteCount(e.Name));
        using var output = new MemoryStream();
        output.SetLength((headerLength + 63) & ~63); output.Position = output.Length;
        var header = new byte[headerLength];
        Buffer.BlockCopy(original, 0, header, 0, 4);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), (uint)entries.Count);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(12), (uint)headerLength);
        int p = 16;
        foreach (var e in entries)
        {
            byte[] data = replaced.TryGetValue(e.Name, out var changed) ? changed : original.AsSpan(e.Offset, e.Length).ToArray();
            byte[] name = Encoding.UTF8.GetBytes(e.Name);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(p), (uint)output.Position);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(p + 4), (uint)data.Length);
            name.CopyTo(header, p + 8); p += 9 + name.Length;
            output.Write(data); output.Write(new byte[(int)((-output.Position) & 63)]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)output.Length);
        output.Position = 0; output.Write(header);
        return output.ToArray();
    }

    // An Apt constant file ("Apt constant file\x1a\0\0", then big-endian data offset, item count and
    // item table offset; items are (type, value) pairs, type 1 a string at offset value). The string
    // the item named `from` points at is replaced by `to`, appended at the end of the file.
    static byte[] RepointConstant(byte[] table, string from, string to)
    {
        Program.Require(table.Length > 32 && Encoding.ASCII.GetString(table, 0, 17) == "Apt constant file", "Not an Apt constant file.");
        int count = (int)Program.U(table, 24), items = (int)Program.U(table, 28);
        string Text(int at) { int end = Array.IndexOf(table, (byte)0, at); return Encoding.Latin1.GetString(table, at, end - at); }
        var matches = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int item = items + 8 * i, value = (int)Program.U(table, item + 4);
            if (Program.U(table, item) == 1 && value < table.Length && Text(value) == from) matches.Add(item);
        }
        Program.Require(matches.Count == 1, $"Expected one \"{from}\" constant, found {matches.Count}.");
        int at = (table.Length + 3) & ~3;
        var result = new byte[at + ((to.Length + 1 + 3) & ~3)];
        table.CopyTo(result, 0);
        Encoding.ASCII.GetBytes(to).CopyTo(result, at);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(matches[0] + 4), (uint)at);
        return result;
    }

    static void Preview(string? folder, string name, Textures.XenosTexture texture, byte[] before, byte[] after)
    {
        if (folder is null) return;
        File.WriteAllBytes(Path.Combine(folder, name + "_before.png"), Textures.ToPng(before, texture.Width, texture.Height));
        File.WriteAllBytes(Path.Combine(folder, name + "_after.png"), Textures.ToPng(after, texture.Width, texture.Height));
    }

    // ---- Repainting a prompt ----------------------------------------------------------------

    // Finds the black face of the prompt (the dark area enclosed by its light outline), clears the
    // Xbox symbol in it and draws the glyph centred in the face.
    static bool Repaint(byte[] rgba, int width, int height, (byte[] Rgba, int Width, int Height) glyph, double fill)
    {
        int n = width * height;
        bool Dark(int i) => rgba[i * 4 + 3] > 128 && rgba[i * 4] + rgba[i * 4 + 1] + rgba[i * 4 + 2] < 3 * 120;
        bool Bright(int i) => rgba[i * 4 + 3] > 128 && rgba[i * 4] + rgba[i * 4 + 1] + rgba[i * 4 + 2] > 3 * 150;

        // Everything reachable from the border without crossing a dark texel is outside the face.
        var outside = new bool[n];
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
            if ((i % width == 0 || i % width == width - 1 || i / width == 0 || i / width == height - 1) && !Dark(i)) { outside[i] = true; queue.Enqueue(i); }
        Flood(queue, width, height, j => !outside[j] && !Dark(j), j => outside[j] = true);

        // The enclosed area holding the most symbol texels is the face to repaint.
        var component = new int[n]; Array.Fill(component, -1);
        int best = -1, bestScore = 0, count = 0;
        for (int i = 0; i < n; i++)
        {
            if (outside[i] || component[i] >= 0) continue;
            int id = count++, score = 0;
            component[i] = id; queue.Enqueue(i);
            Flood(queue, width, height, j => !outside[j] && component[j] < 0, j => component[j] = id);
            for (int j = 0; j < n; j++) if (component[j] == id && Bright(j)) score++;
            if (score > bestScore) { best = id; bestScore = score; }
        }
        if (best < 0)
        {
            // The symbol touches the outline (the Guide button's logo): use the disc that fits the dark texels.
            int x0 = width, y0 = height, x1 = -1, y1 = -1;
            for (int i = 0; i < n; i++)
                if (Dark(i)) { x0 = Math.Min(x0, i % width); x1 = Math.Max(x1, i % width); y0 = Math.Min(y0, i / width); y1 = Math.Max(y1, i / width); }
            if (x1 < 0) return false;
            double cx = (x0 + x1 + 1) / 2.0, cy = (y0 + y1 + 1) / 2.0, rx = (x1 - x0 + 1) / 2.0, ry = (y1 - y0 + 1) / 2.0;
            best = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = (i % width + 0.5 - cx) / rx, dy = (i / width + 0.5 - cy) / ry;
                component[i] = dx * dx + dy * dy <= 1 ? 0 : -1;
            }
        }

        int minX = width, minY = height, maxX = -1, maxY = -1; long r = 0, g = 0, b = 0, dark = 0;
        for (int i = 0; i < n; i++)
        {
            if (component[i] != best) continue;
            int x = i % width, y = i / width;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            if (Dark(i)) { r += rgba[i * 4]; g += rgba[i * 4 + 1]; b += rgba[i * 4 + 2]; dark++; }
        }
        byte fr = (byte)(dark > 0 ? r / dark : 0), fg = (byte)(dark > 0 ? g / dark : 0), fb = (byte)(dark > 0 ? b / dark : 0);

        // Glyph fitted into the face, centred.
        double faceW = maxX - minX + 1, faceH = maxY - minY + 1;
        double scale = Math.Min(faceW * fill / glyph.Width, faceH * fill / glyph.Height);
        double left = minX + (faceW - glyph.Width * scale) / 2, top = minY + (faceH - glyph.Height * scale) / 2;
        const int Samples = 4;
        for (int i = 0; i < n; i++)
        {
            if (component[i] != best) continue;
            int x = i % width, y = i / width;
            // Keep a one-texel dark rim so the glyph never touches the outline.
            bool rim = x == 0 || y == 0 || x == width - 1 || y == height - 1
                || component[i - 1] != best || component[i + 1] != best || component[i - width] != best || component[i + width] != best;
            double coverage = 0;
            if (!rim)
                for (int sy = 0; sy < Samples; sy++)
                    for (int sx = 0; sx < Samples; sx++)
                    {
                        int gx = (int)((x + (sx + 0.5) / Samples - left) / scale), gy = (int)((y + (sy + 0.5) / Samples - top) / scale);
                        if (gx >= 0 && gy >= 0 && gx < glyph.Width && gy < glyph.Height) coverage += glyph.Rgba[(gy * glyph.Width + gx) * 4 + 3] / 255.0;
                    }
            coverage /= Samples * Samples;
            rgba[i * 4] = (byte)(fr + (255 - fr) * coverage);
            rgba[i * 4 + 1] = (byte)(fg + (255 - fg) * coverage);
            rgba[i * 4 + 2] = (byte)(fb + (255 - fb) * coverage);
            rgba[i * 4 + 3] = 255;
        }
        return true;
    }

    static void Flood(Queue<int> queue, int width, int height, Func<int, bool> canEnter, Action<int> enter)
    {
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % width, y = i / width;
            foreach (int j in new[] { x > 0 ? i - 1 : -1, x < width - 1 ? i + 1 : -1, y > 0 ? i - width : -1, y < height - 1 ? i + width : -1 })
                if (j >= 0 && canEnter(j)) { enter(j); queue.Enqueue(j); }
        }
    }

    // ---- Archive editing --------------------------------------------------------------------

    sealed class Archive : IDisposable
    {
        readonly string path, bhPath;
        readonly FileStream file;
        readonly byte[] header, bh;
        readonly Dictionary<string, Program.Entry> entries;
        readonly List<(Program.Entry Entry, byte[]? Data, Program.Entry? Target)> changes = new();

        Archive(string path, bool write)
        {
            this.path = path; bhPath = Path.ChangeExtension(path, ".bh");
            file = new FileStream(path, FileMode.Open, write ? FileAccess.ReadWrite : FileAccess.Read, write ? FileShare.None : FileShare.Read);
            var start = new byte[16]; file.ReadExactly(start);
            Program.Require(Program.IsBig(start), $"{Path.GetFileName(path)} is not a BIG archive.");
            int tableEnd = (int)Program.U(start, 12);
            Program.Require(tableEnd <= 8 * 1024 * 1024, "Invalid archive directory size.");
            header = new byte[tableEnd]; start.CopyTo(header, 0); file.ReadExactly(header.AsSpan(16));
            entries = Program.Index(header, file.Length).ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
            bh = File.ReadAllBytes(bhPath);
        }

        internal static Archive Open(string path, bool write) => new(path, write);
        internal IEnumerable<string> Names => entries.Keys;
        internal bool Contains(string name) => entries.ContainsKey(name);

        internal byte[] Read(string name)
        {
            var e = entries.TryGetValue(name, out var found) ? found : throw new InvalidDataException($"{name} was not found in {Path.GetFileName(path)}.");
            var data = new byte[e.Length]; file.Position = e.Offset; file.ReadExactly(data); return data;
        }

        internal void Replace(string name, byte[] data) => changes.Add((entries[name], data, null));
        internal void Alias(string name, string target) => changes.Add((entries[name], null, entries[target]));

        internal static string BackupPath(string path) => path + ".dualsense-backup";
        internal static bool HasBackup(string path) => File.Exists(BackupPath(path));

        // Backup layout: "RSD2", controller byte, original length (i64), directory (i32 length +
        // bytes), index (i32 length + bytes). "RSDS" backups (first patcher) have no controller byte.
        internal void Commit(Controller controller)
        {
            // The backup goes to disk first, so an interrupted patch can always be reverted.
            var newHeader = (byte[])header.Clone(); var newBh = (byte[])bh.Clone();
            long originalLength = file.Length;
            using (var backup = new FileStream(BackupPath(path), FileMode.CreateNew, FileAccess.Write))
            using (var w = new BinaryWriter(backup))
            {
                w.Write(Encoding.ASCII.GetBytes("RSD2")); w.Write((byte)controller); w.Write(originalLength);
                w.Write(header.Length); w.Write(header); w.Write(bh.Length); w.Write(bh);
                w.Flush(); backup.Flush(true);
            }

            file.Position = originalLength;
            foreach (var (entry, data, target) in changes)
            {
                int offset, length;
                if (data is not null)
                {
                    file.Write(new byte[(int)((-file.Position) & 63)]);
                    offset = checked((int)file.Position); length = data.Length; file.Write(data);
                }
                else (offset, length) = (target!.Offset, target.Length);
                int record = FindIndexRecord(entry);
                Put(newHeader, entry.TablePosition, offset); Put(newHeader, entry.TablePosition + 4, length);
                Put(newBh, record, offset); Put(newBh, record + 4, length);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(newHeader.AsSpan(4, 4), checked((uint)file.Length));
            file.Flush(true);
            file.Position = 0; file.Write(newHeader); file.Flush(true);
            File.WriteAllBytes(bhPath, newBh);
        }

        int FindIndexRecord(Program.Entry e)
        {
            var matches = new List<int>();
            for (int i = 16; i + 8 <= bh.Length; i += 20) if (Program.U(bh, i) == e.Offset && Program.U(bh, i + 4) == e.Length) matches.Add(i);
            Program.Require(matches.Count == 1, $"The {Path.GetFileName(bhPath)} index does not match {e.Name}.");
            return matches[0];
        }

        static void Put(byte[] b, int p, int v) => BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(p, 4), checked((uint)v));

        internal static bool Revert(string path)
        {
            string backupPath = BackupPath(path);
            if (!File.Exists(backupPath)) return false;
            using (var r = new BinaryReader(File.OpenRead(backupPath)))
            {
                ReadBackupStart(r, backupPath);
                long length = r.ReadInt64();
                byte[] header = r.ReadBytes(r.ReadInt32()), bh = r.ReadBytes(r.ReadInt32());
                using (var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Program.Require(file.Length >= length, $"{Path.GetFileName(path)} is shorter than before the patch; it was replaced, not reverting.");
                    file.Write(header); file.SetLength(length); file.Flush(true);
                }
                File.WriteAllBytes(Path.ChangeExtension(path, ".bh"), bh);
            }
            File.Delete(backupPath);
            return true;
        }

        // Reads the magic and, for "RSD2", the controller byte; leaves the reader at the length.
        static Controller? ReadBackupStart(BinaryReader r, string backupPath)
        {
            string magic = Encoding.ASCII.GetString(r.ReadBytes(4));
            if (magic == "RSDS") return null;
            Program.Require(magic == "RSD2", $"{Path.GetFileName(backupPath)} is not a DualSense backup.");
            return (Controller)r.ReadByte();
        }

        internal static Controller? AppliedController(string path)
        {
            if (!HasBackup(path)) return null;
            using var r = new BinaryReader(File.OpenRead(BackupPath(path)));
            return ReadBackupStart(r, BackupPath(path));
        }

        public void Dispose() => file.Dispose();
    }
}
