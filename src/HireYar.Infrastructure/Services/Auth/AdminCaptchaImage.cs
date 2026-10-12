using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
namespace HireYar.Infrastructure.Auth;

// Small grayscale PNG rasterizer using only the BCL, portable to the Linux API host.
internal static class AdminCaptchaImage
{
    private const int Width = 240, Height = 80;
    private static readonly string[] Digits = ["abcdef", "bc", "abdeg", "abcdg", "bcfg", "acdfg", "acdefg", "abc", "abcdefg", "abcdfg"];
    private static readonly (int X1, int Y1, int X2, int Y2)[] Segments = [(3, 1, 17, 1), (19, 3, 19, 17), (19, 23, 19, 37), (3, 39, 17, 39), (1, 23, 1, 37), (1, 3, 1, 17), (3, 20, 17, 20)];
    public static byte[] Render(string answer)
    {
        var pixels = Enumerable.Repeat((byte)240, Width * Height).ToArray();
        void Line(int x1, int y1, int x2, int y2, byte shade, int radius)
        {
            var steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
            for (var step = 0; step <= steps; step++)
            {
                var fraction = steps == 0 ? 0 : (double)step / steps;
                var x = (int)Math.Round(x1 + (x2 - x1) * fraction); var y = (int)Math.Round(y1 + (y2 - y1) * fraction);
                for (var dx = -radius; dx <= radius; dx++) for (var dy = -radius; dy <= radius; dy++)
                    if (x + dx >= 0 && x + dx < Width && y + dy >= 0 && y + dy < Height) pixels[(y + dy) * Width + x + dx] = shade;
            }
        }
        for (var i = 0; i < 12; i++) Line(RandomNumberGenerator.GetInt32(Width), RandomNumberGenerator.GetInt32(Height), RandomNumberGenerator.GetInt32(Width), RandomNumberGenerator.GetInt32(Height), 160, 0);
        for (var i = 0; i < answer.Length; i++)
        {
            var angle = RandomNumberGenerator.GetInt32(-12, 13) * Math.PI / 180; var yOffset = 18 + RandomNumberGenerator.GetInt32(-5, 6);
            (int X, int Y) Transform(int x, int y) => ((int)Math.Round(28 + 36 * i + (x - 10) * Math.Cos(angle) - (y - 20) * Math.Sin(angle)),
                (int)Math.Round(yOffset + 20 + (x - 10) * Math.Sin(angle) + (y - 20) * Math.Cos(angle)));
            foreach (var segment in Digits[answer[i] - '0'])
            {
                var s = Segments[segment - 'a']; var from = Transform(s.X1, s.Y1); var to = Transform(s.X2, s.Y2);
                Line(from.X, from.Y, to.X, to.Y, 20, 1);
            }
        }
        var raw = new byte[(Width + 1) * Height];
        for (var y = 0; y < Height; y++) pixels.AsSpan(y * Width, Width).CopyTo(raw.AsSpan(y * (Width + 1) + 1, Width));
        using var compressed = new MemoryStream();
        using (var zip = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) zip.Write(raw);
        using var png = new MemoryStream(); png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header, Width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height); header[8] = 8;
        Chunk(png, "IHDR", header); Chunk(png, "IDAT", compressed.ToArray()); Chunk(png, "IEND", []);
        return png.ToArray();
    }
    private static void Chunk(Stream output, string type, byte[] data)
    {
        var name = Encoding.ASCII.GetBytes(type); Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length); output.Write(number); output.Write(name); output.Write(data);
        uint crc = 0xffffffff;
        foreach (var value in name.Concat(data))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc); output.Write(number);
    }
}
