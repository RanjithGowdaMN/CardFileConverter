// Rebuild the original application artwork without external graphics packages.
// csc /r:System.Drawing.dll /out:CreateIcon.exe Tools\CreateIcon.cs
// CreateIcon.exe CardFileConverter\Assets
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class CreateIcon
{
    private static GraphicsPath Rounded(float x, float y, float w, float h, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(x, y, d, d, 180, 90); path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90); path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }

    private static Bitmap Draw(int size)
    {
        using (var large = new Bitmap(size * 4, size * 4, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(large))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(size * 4f / 256, size * 4f / 256);
                using (var background = new LinearGradientBrush(new Point(0, 0), new Point(256, 256), Color.FromArgb(35, 60, 86), Color.FromArgb(17, 32, 52)))
                using (var shape = Rounded(8, 8, 240, 240, 52)) g.FillPath(background, shape);
                using (var document = new GraphicsPath())
                {
                    document.AddPolygon(new[] { new PointF(64, 43), new PointF(141, 43), new PointF(183, 85), new PointF(183, 210), new PointF(64, 210) });
                    using (var white = new SolidBrush(Color.FromArgb(243, 249, 253))) g.FillPath(white, document);
                }
                using (var fold = new SolidBrush(Color.FromArgb(181, 204, 221)))
                    g.FillPolygon(fold, new[] { new PointF(141, 43), new PointF(141, 85), new PointF(183, 85) });
                using (var line = new Pen(Color.FromArgb(150, 176, 195), 8))
                { line.StartCap = LineCap.Round; line.EndCap = LineCap.Round; g.DrawLine(line, 85, 71, 119, 71); g.DrawLine(line, 85, 93, 119, 93); }
                using (var teal = new SolidBrush(Color.FromArgb(0, 153, 155)))
                {
                    g.FillPolygon(teal, new[] { new PointF(85, 118), new PointF(169, 118), new PointF(169, 101), new PointF(205, 135), new PointF(169, 169), new PointF(169, 152), new PointF(85, 152) });
                }
                using (var navy = new SolidBrush(Color.FromArgb(25, 61, 86)))
                {
                    g.FillPolygon(navy, new[] { new PointF(162, 172), new PointF(106, 172), new PointF(106, 158), new PointF(76, 185), new PointF(106, 212), new PointF(106, 198), new PointF(162, 198) });
                }
            }
            var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(large, 0, 0, size, size); }
            return result;
        }
    }

    private static byte[] Dib(Bitmap bitmap)
    {
        int n = bitmap.Width, maskStride = ((n + 31) / 32) * 4;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(40); writer.Write(n); writer.Write(n * 2); writer.Write((short)1); writer.Write((short)32);
            writer.Write(0); writer.Write(n * n * 4 + maskStride * n);
            writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for (int y = n - 1; y >= 0; y--)
                for (int x = 0; x < n; x++)
                { var c = bitmap.GetPixel(x, y); writer.Write(c.B); writer.Write(c.G); writer.Write(c.R); writer.Write(c.A); }
            for (int y = n - 1; y >= 0; y--)
            {
                var mask = new byte[maskStride];
                for (int x = 0; x < n; x++) if (bitmap.GetPixel(x, y).A == 0) mask[x / 8] |= (byte)(128 >> (x % 8));
                writer.Write(mask);
            }
            return stream.ToArray();
        }
    }

    private static void Main(string[] args)
    {
        Directory.CreateDirectory(args[0]);
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var frames = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++) using (var bitmap = Draw(sizes[i])) frames[i] = Dib(bitmap);
        using (var writer = new BinaryWriter(File.Create(Path.Combine(args[0], "AppIcon.ico"))))
        {
            writer.Write((short)0); writer.Write((short)1); writer.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((short)1); writer.Write((short)32);
                writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
            }
            foreach (var frame in frames) writer.Write(frame);
        }
        using (var preview = Draw(256)) preview.Save(Path.Combine(args[0], "AppIcon.png"), ImageFormat.Png);
    }
}
