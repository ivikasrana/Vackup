using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGen;

static class Program
{
    static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    [STAThread]
    static int Main(string[] args)
    {
        var output = args.Length > 0 ? args[0] : "Vackup.ico";
        var images = Sizes.Select(Render).ToList();

        using var fs = File.Create(output);
        using var w = new BinaryWriter(fs);
        w.Write((short)0);
        w.Write((short)1);
        w.Write((short)images.Count);
        int offset = 6 + 16 * images.Count;
        for (int i = 0; i < images.Count; i++)
        {
            int size = Sizes[i];
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((short)1);
            w.Write((short)32);
            w.Write(images[i].Length);
            w.Write(offset);
            offset += images[i].Length;
        }
        foreach (var png in images)
            w.Write(png);

        Console.WriteLine($"Wrote {output}");
        return 0;
    }

    // Drawn on a 256 x 256 canvas and scaled to each size.
    static byte[] Render(int size)
    {

        var shield = Geometry.Parse("M70,16 L186,16 A54,54 0 0 1 240,70 L240,186 A54,54 0 0 1 186,240 L70,240 A54,54 0 0 1 16,186 L16,70 A54,54 0 0 1 70,16 Z");
        var keyhole = Geometry.Parse("M108,52 L148,52 L148,110 L184,110 L128,166 L72,110 L108,110 Z M64,184 L192,184 L192,206 L64,206 Z");
        var shine = Geometry.Parse("M70,26 L186,26 A44,44 0 0 1 230,70 L230,96 C170,80 90,80 26,110 L26,70 A44,44 0 0 1 70,26 Z");

        var fill =new LinearGradientBrush(Color.FromRgb(0x00, 0x7A, 0xFF), Color.FromRgb(0x89, 0x44, 0xAB), 45);
        var edge = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), 6);
        var glass = new SolidColorBrush(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
            dc.DrawGeometry(fill, size >= 32 ? edge : null, shield);
            if (size >= 32)
                dc.DrawGeometry(glass, null, shine);
            dc.DrawGeometry(Brushes.White, null, keyhole);
            dc.Pop();
        }

        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
