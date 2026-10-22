using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace ReStreetAssetScan;

// Converts the game's textures to PNG for viewing: plain DDS (the imgassets folders) and
// Xbox 360 XPR2 packages (the textures inside each UI .big), which are big-endian and tiled.
internal static class Textures
{
    // Returns one PNG per texture found, or nothing when the data is not a supported texture.
    internal static IEnumerable<(string Name, byte[] Png)> ToPng(byte[] b)
    {
        if (b.Length >= 128 && Encoding.ASCII.GetString(b, 0, 4) == "DDS ")
        {
            var png = FromDds(b);
            if (png is not null) yield return ("", png);
        }
        else if (b.Length >= 16 && Encoding.ASCII.GetString(b, 0, 4) == "XPR2")
        {
            foreach (var item in FromXpr2(b)) yield return item;
        }
    }

    // ---- DDS ----------------------------------------------------------------------------------

    static byte[]? FromDds(byte[] b)
    {
        var image = DecodeDds(b);
        return image is null ? null : Png(image.Value.Rgba, image.Value.Width, image.Value.Height);
    }

    internal static (byte[] Rgba, int Width, int Height)? DecodeDds(byte[] b)
    {
        if (b.Length < 128 || Encoding.ASCII.GetString(b, 0, 4) != "DDS ") return null;
        int height = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(12)), width = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(16));
        int pfFlags = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(80));
        string fourcc = Encoding.ASCII.GetString(b, 84, 4);
        if (width is <= 0 or > 8192 || height is <= 0 or > 8192) return null;
        var data = b.AsSpan(128);
        byte[]? rgba = (pfFlags & 4) != 0
            ? fourcc switch { "DXT1" => Dxt(data, width, height, 1), "DXT3" => Dxt(data, width, height, 3), "DXT5" => Dxt(data, width, height, 5), _ => null }
            : BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(88)) == 32 && data.Length >= width * height * 4 ? Bgra(data, width, height) : null;
        return rgba is null ? null : (rgba, width, height);
    }

    static byte[] Bgra(ReadOnlySpan<byte> s, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        { rgba[i * 4] = s[i * 4 + 2]; rgba[i * 4 + 1] = s[i * 4 + 1]; rgba[i * 4 + 2] = s[i * 4]; rgba[i * 4 + 3] = s[i * 4 + 3]; }
        return rgba;
    }

    // ---- XPR2 ---------------------------------------------------------------------------------

    static IEnumerable<(string, byte[])> FromXpr2(byte[] b)
    {
        uint headerSize = BE(b, 4), count = BE(b, 12);
        long dataStart = headerSize + 12;
        for (int i = 0; i < count && 16 + i * 16 + 16 <= b.Length; i++)
        {
            int r = 16 + i * 16;
            if (Encoding.ASCII.GetString(b, r, 4) != "TX2D") continue;
            int header = (int)BE(b, r + 4) + 12, nameAt = (int)BE(b, r + 12) + 12;
            string name = nameAt < b.Length ? ReadName(b, nameAt) : $"tex{i}";
            byte[]? png = null;
            try
            {
                var texture = XenosTexture.Parse(b, header + 28, dataStart);
                if (texture is not null) png = Png(texture.Read(b), texture.Width, texture.Height);
            }
            catch (Exception) { }
            if (png is not null) yield return (count > 1 ? "_" + Safe(name) : "", png);
        }
    }

    // The first TX2D texture of an XPR2 package (every UI texture in the game has exactly one).
    internal static XenosTexture? FirstXpr2Texture(byte[] b)
    {
        if (b.Length < 32 || Encoding.ASCII.GetString(b, 0, 4) != "XPR2" || Encoding.ASCII.GetString(b, 16, 4) != "TX2D") return null;
        return XenosTexture.Parse(b, (int)BE(b, 20) + 12 + 28, BE(b, 4) + 12);
    }

    static string ReadName(byte[] b, int at) { int end = Array.IndexOf(b, (byte)0, at); return Encoding.ASCII.GetString(b, at, (end < 0 ? b.Length : end) - at); }
    static string Safe(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_'));

    // A GPU texture fetch constant (6 dwords) describes the base mip level.
    internal sealed record XenosTexture(long Offset, int Width, int Height, int Format, int Endian, bool Tiled, int BlocksW, int BlocksH, int AlignedW, int Bytes)
    {
        internal static XenosTexture? Parse(byte[] b, int fetch, long dataStart)
        {
            uint d0 = BE(b, fetch), d1 = BE(b, fetch + 4), d2 = BE(b, fetch + 8);
            int format = (int)(d1 & 63);
            int width = (int)(d2 & 0x1FFF) + 1, height = (int)((d2 >> 13) & 0x1FFF) + 1;
            (int blockSize, int bytes) = format switch { 0x12 => (4, 8), 0x13 or 0x14 => (4, 16), 0x06 => (1, 4), 0x02 => (1, 1), _ => (0, 0) };
            if (blockSize == 0) return null;
            int blocksW = (width + blockSize - 1) / blockSize, blocksH = (height + blockSize - 1) / blockSize;
            var texture = new XenosTexture(dataStart + ((d1 >> 12) << 12), width, height, format, (int)((d1 >> 6) & 3), (d0 >> 31) != 0,
                blocksW, blocksH, (blocksW + 31) & ~31, bytes);
            return texture.Block(blocksW - 1, blocksH - 1) + bytes <= b.Length ? texture : null;
        }

        long Block(int x, int y) => Offset + (Tiled ? TiledOffset(x, y, AlignedW, Bytes) : (long)y * AlignedW + x) * Bytes;

        // Untiles into a linear block array, undoes the GPU byte order and decodes to RGBA.
        internal byte[] Read(byte[] b)
        {
            var linear = new byte[BlocksW * BlocksH * Bytes];
            for (int y = 0; y < BlocksH; y++)
                for (int x = 0; x < BlocksW; x++)
                    Buffer.BlockCopy(b, (int)Block(x, y), linear, (y * BlocksW + x) * Bytes, Bytes);
            Swap(linear, Endian);
            return Format switch
            {
                0x12 => Dxt(linear, Width, Height, 1, BlocksW),
                0x13 => Dxt(linear, Width, Height, 3, BlocksW),
                0x14 => Dxt(linear, Width, Height, 5, BlocksW),
                0x06 => Argb(linear, Width, Height),
                _ => Gray(linear, Width, Height),
            };
        }

        // Encodes RGBA back into the same place, format and byte order (DXT5 only, the format of all UI art).
        internal void Write(byte[] b, byte[] rgba)
        {
            if (Format != 0x14) throw new NotSupportedException($"Writing texture format 0x{Format:x} is not supported.");
            byte[] linear = EncodeDxt5(rgba, Width, Height);
            Swap(linear, Endian);
            for (int y = 0; y < BlocksH; y++)
                for (int x = 0; x < BlocksW; x++)
                    Buffer.BlockCopy(linear, (y * BlocksW + x) * Bytes, b, (int)Block(x, y), Bytes);
        }
    }

    // XGAddress2DTiledOffset: element index of block (x, y) in a tiled Xenos surface.
    static long TiledOffset(int x, int y, int alignedWidth, int bytesPerBlock)
    {
        int logBpp = (bytesPerBlock >> 2) + ((bytesPerBlock >> 1) >> (bytesPerBlock >> 2));
        int macro = ((x >> 5) + (y >> 5) * (alignedWidth >> 5)) << (logBpp + 7);
        int micro = ((x & 7) + ((y & 6) << 2)) << logBpp;
        int offset = macro + ((micro & ~15) << 1) + (micro & 15) + ((y & 8) << (3 + logBpp)) + ((y & 1) << 4);
        return (((offset & ~511) << 3) + ((offset & 448) << 2) + (offset & 63) + ((y & 16) << 7) + (((((y & 8) >> 2) + (x >> 3)) & 3) << 6)) >> logBpp;
    }

    static void Swap(byte[] d, int endian)
    {
        if (endian == 1) for (int i = 0; i + 1 < d.Length; i += 2) (d[i], d[i + 1]) = (d[i + 1], d[i]);
        else if (endian == 2) for (int i = 0; i + 3 < d.Length; i += 4) { (d[i], d[i + 3]) = (d[i + 3], d[i]); (d[i + 1], d[i + 2]) = (d[i + 2], d[i + 1]); }
        else if (endian == 3) for (int i = 0; i + 3 < d.Length; i += 4) { (d[i], d[i + 2]) = (d[i + 2], d[i]); (d[i + 1], d[i + 3]) = (d[i + 3], d[i + 1]); }
    }

    static byte[] Argb(byte[] s, int width, int height)
    {
        // After the byte swap 8_8_8_8 is stored B, G, R, A in memory.
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
        { rgba[i * 4] = s[i * 4 + 2]; rgba[i * 4 + 1] = s[i * 4 + 1]; rgba[i * 4 + 2] = s[i * 4]; rgba[i * 4 + 3] = s[i * 4 + 3]; }
        return rgba;
    }

    static byte[] Gray(byte[] s, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++) { rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = 255; rgba[i * 4 + 3] = s[i]; }
        return rgba;
    }

    // ---- DXT ----------------------------------------------------------------------------------

    static byte[] Dxt(ReadOnlySpan<byte> s, int width, int height, int kind, int blocksPerRow = 0)
    {
        int bw = (width + 3) / 4, bh = (height + 3) / 4, size = kind == 1 ? 8 : 16;
        if (blocksPerRow == 0) blocksPerRow = bw;
        var rgba = new byte[width * height * 4];
        Span<byte> colors = stackalloc byte[16], alpha = stackalloc byte[16];
        for (int by = 0; by < bh; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                int at = (by * blocksPerRow + bx) * size;
                if (at + size > s.Length) continue;
                var block = s.Slice(at, size);
                alpha.Fill(255);
                if (kind == 3) for (int i = 0; i < 16; i++) alpha[i] = (byte)(((block[i / 2] >> (i % 2 * 4)) & 15) * 17);
                if (kind == 5)
                {
                    int a0 = block[0], a1 = block[1]; ulong bits = 0;
                    for (int i = 0; i < 6; i++) bits |= (ulong)block[2 + i] << (8 * i);
                    for (int i = 0; i < 16; i++)
                    {
                        int code = (int)((bits >> (3 * i)) & 7);
                        alpha[i] = (byte)(code == 0 ? a0 : code == 1 ? a1 : a0 > a1 ? ((8 - code) * a0 + (code - 1) * a1) / 7
                            : code == 6 ? 0 : code == 7 ? 255 : ((6 - code) * a0 + (code - 1) * a1) / 5);
                    }
                }
                var c = block.Slice(kind == 1 ? 0 : 8, 8);
                int c0 = c[0] | (c[1] << 8), c1 = c[2] | (c[3] << 8);
                Rgb565(c0, colors[..4]); Rgb565(c1, colors.Slice(4, 4));
                bool four = kind != 1 || c0 > c1;
                for (int k = 0; k < 3; k++)
                {
                    colors[8 + k] = (byte)(four ? (2 * colors[k] + colors[4 + k]) / 3 : (colors[k] + colors[4 + k]) / 2);
                    colors[12 + k] = (byte)(four ? (colors[k] + 2 * colors[4 + k]) / 3 : 0);
                }
                colors[11] = 255; colors[15] = (byte)(four ? 255 : 0);
                uint idx = (uint)(c[4] | (c[5] << 8) | (c[6] << 16) | (c[7] << 24));
                for (int i = 0; i < 16; i++)
                {
                    int x = bx * 4 + i % 4, y = by * 4 + i / 4;
                    if (x >= width || y >= height) continue;
                    int code = (int)((idx >> (2 * i)) & 3), o = (y * width + x) * 4;
                    rgba[o] = colors[code * 4]; rgba[o + 1] = colors[code * 4 + 1]; rgba[o + 2] = colors[code * 4 + 2];
                    rgba[o + 3] = kind == 1 ? colors[code * 4 + 3] : alpha[i];
                }
            }
        return rgba;
    }

    // Bounding-box DXT5 encoder: good for the game's flat, mostly two-tone UI art.
    internal static byte[] EncodeDxt5(byte[] rgba, int width, int height)
    {
        int bw = (width + 3) / 4, bh = (height + 3) / 4;
        var output = new byte[bw * bh * 16];
        Span<int> px = stackalloc int[64];
        Span<int> palette = stackalloc int[12];
        Span<int> alphas = stackalloc int[8];
        Span<byte> e = stackalloc byte[8];
        Span<double> e0 = stackalloc double[3], e1 = stackalloc double[3], sx = stackalloc double[3], sy = stackalloc double[3];
        for (int by = 0; by < bh; by++)
            for (int bx = 0; bx < bw; bx++)
            {
                for (int i = 0; i < 16; i++)
                {
                    int x = Math.Min(bx * 4 + i % 4, width - 1), y = Math.Min(by * 4 + i / 4, height - 1), o = (y * width + x) * 4;
                    for (int k = 0; k < 4; k++) px[i * 4 + k] = rgba[o + k];
                }
                var block = output.AsSpan((by * bw + bx) * 16, 16);

                int a0 = 0, a1 = 255;
                for (int i = 0; i < 16; i++) { a0 = Math.Max(a0, px[i * 4 + 3]); a1 = Math.Min(a1, px[i * 4 + 3]); }
                block[0] = (byte)a0; block[1] = (byte)a1;
                alphas[0] = a0; alphas[1] = a1;
                for (int i = 1; i <= 6; i++) alphas[i + 1] = ((7 - i) * a0 + i * a1) / 7;
                ulong abits = 0;
                for (int i = 0; i < 16; i++)
                {
                    int best = 0;
                    for (int c = 1; c < 8 && a0 != a1; c++) if (Math.Abs(alphas[c] - px[i * 4 + 3]) < Math.Abs(alphas[best] - px[i * 4 + 3])) best = c;
                    abits |= (ulong)best << (3 * i);
                }
                for (int i = 0; i < 6; i++) block[2 + i] = (byte)(abits >> (8 * i));

                // Colour endpoints: the darkest and brightest visible texels.
                int lo = -1, hi = -1;
                for (int i = 0; i < 16; i++)
                {
                    if (px[i * 4 + 3] == 0 && a0 > 0) continue;
                    int l = px[i * 4] * 3 + px[i * 4 + 1] * 6 + px[i * 4 + 2];
                    if (lo < 0 || l < px[lo * 4] * 3 + px[lo * 4 + 1] * 6 + px[lo * 4 + 2]) lo = i;
                    if (hi < 0 || l > px[hi * 4] * 3 + px[hi * 4 + 1] * 6 + px[hi * 4 + 2]) hi = i;
                }
                if (lo < 0) lo = hi = 0;

                // Start from the darkest and brightest texels, then refine both endpoints by least
                // squares a few times and keep the best block, so small coloured details survive.
                e0[0] = px[hi * 4]; e0[1] = px[hi * 4 + 1]; e0[2] = px[hi * 4 + 2];
                e1[0] = px[lo * 4]; e1[1] = px[lo * 4 + 1]; e1[2] = px[lo * 4 + 2];
                long bestError = long.MaxValue; int bestC0 = 0, bestC1 = 0; uint bestBits = 0;
                for (int pass = 0; pass < 4; pass++)
                {
                    int c0 = To565((int)Math.Round(e0[0]), (int)Math.Round(e0[1]), (int)Math.Round(e0[2]));
                    int c1 = To565((int)Math.Round(e1[0]), (int)Math.Round(e1[1]), (int)Math.Round(e1[2]));
                    if (c0 < c1) (c0, c1) = (c1, c0);
                    Rgb565(c0, e[..4]); Rgb565(c1, e.Slice(4, 4));
                    for (int k = 0; k < 3; k++)
                    {
                        palette[k] = e[k]; palette[3 + k] = e[4 + k];
                        palette[6 + k] = (2 * e[k] + e[4 + k]) / 3; palette[9 + k] = (e[k] + 2 * e[4 + k]) / 3;
                    }
                    uint cbits = 0; long error = 0;
                    double sa = 0, sb = 0, sc = 0;
                    sx.Clear(); sy.Clear();
                    for (int i = 0; i < 16; i++)
                    {
                        int best = 0, bestDistance = int.MaxValue;
                        for (int c = 0; c < (c0 == c1 ? 1 : 4); c++)
                        {
                            int dr = palette[c * 3] - px[i * 4], dg = palette[c * 3 + 1] - px[i * 4 + 1], db = palette[c * 3 + 2] - px[i * 4 + 2];
                            int distance = dr * dr + dg * dg + db * db;
                            if (distance < bestDistance) { best = c; bestDistance = distance; }
                        }
                        cbits |= (uint)best << (2 * i);
                        if (px[i * 4 + 3] == 0 && a0 > 0) continue;   // invisible texels do not count
                        error += bestDistance;
                        double w = best switch { 0 => 1, 1 => 0, 2 => 2 / 3.0, _ => 1 / 3.0 };
                        sa += w * w; sb += w * (1 - w); sc += (1 - w) * (1 - w);
                        for (int k = 0; k < 3; k++) { sx[k] += w * px[i * 4 + k]; sy[k] += (1 - w) * px[i * 4 + k]; }
                    }
                    if (error < bestError) { bestError = error; bestC0 = c0; bestC1 = c1; bestBits = cbits; }
                    double det = sa * sc - sb * sb;
                    if (error == 0 || Math.Abs(det) < 1e-6) break;
                    for (int k = 0; k < 3; k++)
                    {
                        e0[k] = Math.Clamp((sc * sx[k] - sb * sy[k]) / det, 0, 255);
                        e1[k] = Math.Clamp((sa * sy[k] - sb * sx[k]) / det, 0, 255);
                    }
                }
                block[8] = (byte)bestC0; block[9] = (byte)(bestC0 >> 8); block[10] = (byte)bestC1; block[11] = (byte)(bestC1 >> 8);
                block[12] = (byte)bestBits; block[13] = (byte)(bestBits >> 8); block[14] = (byte)(bestBits >> 16); block[15] = (byte)(bestBits >> 24);
            }
        return output;
    }

    static int To565(int r, int g, int b) => ((r * 31 + 127) / 255 << 11) | ((g * 63 + 127) / 255 << 5) | ((b * 31 + 127) / 255);

    // Minimal PNG reader for the patcher's own glyphs (8-bit grey/RGB/RGBA, not interlaced).
    internal static (byte[] Rgba, int Width, int Height) ReadPng(byte[] png)
    {
        int width = 0, height = 0, type = 0, p = 8;
        using var idat = new MemoryStream();
        while (p + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p)); string chunk = Encoding.ASCII.GetString(png, p + 4, 4);
            if (chunk == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p + 8)); height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p + 12));
                if (png[p + 16] != 8 || png[p + 20] != 0) throw new InvalidDataException("Only 8-bit, non-interlaced PNG glyphs are supported.");
                type = png[p + 17];
            }
            else if (chunk == "IDAT") idat.Write(png, p + 8, length);
            p += 12 + length;
        }
        int channels = type switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => throw new InvalidDataException("Unsupported PNG colour type.") };
        using var z = new ZLibStream(new MemoryStream(idat.ToArray()), CompressionMode.Decompress);
        using var raw = new MemoryStream(); z.CopyTo(raw); byte[] data = raw.ToArray();
        int stride = width * channels; var previous = new byte[stride]; var row = new byte[stride]; var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            int filter = data[y * (stride + 1)]; Buffer.BlockCopy(data, y * (stride + 1) + 1, row, 0, stride);
            for (int i = 0; i < stride; i++)
            {
                int a = i >= channels ? row[i - channels] : 0, b = previous[i], c = i >= channels ? previous[i - channels] : 0;
                int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - 2 * c);
                row[i] = (byte)(row[i] + filter switch { 1 => a, 2 => b, 3 => (a + b) / 2, 4 => pa <= pb && pa <= pc ? a : pb <= pc ? b : c, _ => 0 });
            }
            for (int x = 0; x < width; x++)
            {
                int o = (y * width + x) * 4, s = x * channels;
                (rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]) = channels switch
                {
                    1 => (row[s], row[s], row[s], (byte)255),
                    2 => (row[s], row[s], row[s], row[s + 1]),
                    3 => (row[s], row[s + 1], row[s + 2], (byte)255),
                    _ => (row[s], row[s + 1], row[s + 2], row[s + 3]),
                };
            }
            (previous, row) = (row, previous);
        }
        return (rgba, width, height);
    }

    internal static byte[] ToPng(byte[] rgba, int width, int height) => Png(rgba, width, height);

    static void Rgb565(int v, Span<byte> o)
    {
        o[0] = (byte)(((v >> 11) & 31) * 255 / 31); o[1] = (byte)(((v >> 5) & 63) * 255 / 63); o[2] = (byte)((v & 31) * 255 / 31); o[3] = 255;
    }

    // ---- PNG ----------------------------------------------------------------------------------

    static byte[] Png(byte[] rgba, int width, int height)
    {
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            for (int y = 0; y < height; y++) { z.WriteByte(0); z.Write(rgba, y * width * 4, width * 4); }
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width); BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; ihdr[9] = 6;
        Chunk(png, "IHDR", ihdr); Chunk(png, "IDAT", raw.ToArray()); Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var head = new byte[8]; BinaryPrimitives.WriteInt32BigEndian(head, data.Length); Encoding.ASCII.GetBytes(type).CopyTo(head, 4);
        s.Write(head); s.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFF, head.AsSpan(4)), data) ^ 0xFFFFFFFF;
        var tail = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(tail, crc); s.Write(tail);
    }

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte v in data) crc = CrcTable[(crc ^ v) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    static uint BE(byte[] b, int p) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(p, 4));
}
