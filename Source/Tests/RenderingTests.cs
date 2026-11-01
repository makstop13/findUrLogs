using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FindUrLogs;

internal static class RenderingTests
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool verify = args.Length > 0 && args[0] == "--verify";
        MethodInfo paint = typeof(AuroraCanvas).GetMethod("OnPaintBackground", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo cache = typeof(AuroraCanvas).GetField("backdrop", BindingFlags.NonPublic | BindingFlags.Instance);
        Size[] sizes = { new Size(1320, 930), new Size(2560, 1350), new Size(1180, 880), new Size(1920, 1080), new Size(1600, 950), new Size(2400, 1300) };
        int builds = 0;
        using (AuroraCanvas canvas = new AuroraCanvas())
        using (Bitmap output = new Bitmap(2560, 1440))
        using (Graphics graphics = Graphics.FromImage(output))
        {
            canvas.Size = sizes[0];
            paint.Invoke(canvas, new object[] { new PaintEventArgs(graphics, new Rectangle(Point.Empty, canvas.Size)) });
            object last = cache.GetValue(canvas);
            Stopwatch watch = Stopwatch.StartNew();
            for (int frame = 0; frame < 36; frame++)
            {
                canvas.Size = sizes[frame % sizes.Length];
                graphics.SetClip(new Rectangle(Point.Empty, canvas.Size));
                paint.Invoke(canvas, new object[] { new PaintEventArgs(graphics, new Rectangle(Point.Empty, canvas.Size)) });
                object next = cache.GetValue(canvas);
                if (!object.ReferenceEquals(last, next)) builds++;
                last = next;
            }
            watch.Stop();
            Bitmap bitmap = (Bitmap)cache.GetValue(canvas);
            Console.WriteLine("36 resize/background frames: " + watch.Elapsed.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                + " ms; background rebuilds on resize: " + builds + "; cache: " + bitmap.Width + "x" + bitmap.Height);
            if (verify)
            {
                if (builds != 0) throw new Exception("Background rebuilt while resizing.");
                if (bitmap.Width > 960 || bitmap.Height > 600) throw new Exception("Background cache exceeds render budget.");
                Theme.Current = ThemePalette.Presets()[3]; canvas.ClearBackdrop();
                paint.Invoke(canvas, new object[] { new PaintEventArgs(graphics, new Rectangle(Point.Empty, canvas.Size)) });
                if (object.ReferenceEquals(last, cache.GetValue(canvas))) throw new Exception("Theme change did not refresh backdrop.");
                if (output.GetPixel(5, 5).GetBrightness() > .5) throw new Exception("Dark theme not rendered after cache refresh.");
                Console.WriteLine("PASS: resize cache reuse, bounded bitmap, theme refresh, dark rendering.");
            }
        }
    }
}
