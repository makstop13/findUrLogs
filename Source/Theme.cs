using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FindUrLogs
{
    internal sealed class ThemePalette
    {
        internal string Name;
        internal bool Dark;
        internal Color Top, Bottom, GlowA, GlowB, GlowC, GlowD, Accent;
        internal ThemePalette Clone() { return (ThemePalette)MemberwiseClone(); }
        internal Color Text { get { return Dark ? Color.FromArgb(237, 242, 255) : Color.FromArgb(32, 38, 72); } }
        internal Color Muted { get { return Dark ? Color.FromArgb(174, 188, 212) : Color.FromArgb(91, 101, 133); } }
        internal Color Surface { get { return Dark ? Mix(Color.FromArgb(26, 33, 51), Bottom, .07) : Color.FromArgb(245, 247, 255); } }
        internal Color Input { get { return Dark ? Mix(Color.FromArgb(20, 27, 43), Bottom, .05) : Color.FromArgb(250, 251, 255); } }
        internal Color Header { get { return Dark ? Color.FromArgb(36, 45, 66) : Color.FromArgb(235, 237, 252); } }
        internal Color Border { get { return Dark ? Color.FromArgb(70, 83, 110) : Color.FromArgb(235, 239, 255); } }
        internal Color Selection { get { return Mix(Surface, Accent, Dark ? .29 : .18); } }
        internal Color AccentEnd
        {
            get
            {
                Color end = Mix(Accent, GlowA, .30);
                for (int n = 0; n < 30; n++)
                {
                    Color text = BestText(Accent, end);
                    if (Math.Min(Contrast(text, Accent), Contrast(text, end)) >= 4.5) return end;
                    end = Mix(end, Accent, .25);
                }
                return Accent;
            }
        }
        internal Color AccentText { get { return BestText(Accent, AccentEnd); } }
        internal Color Disabled { get { return Dark ? Color.FromArgb(131, 145, 166) : Color.FromArgb(136, 145, 168); } }

        internal static Color Mix(Color a, Color b, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb((int)Math.Round(a.R * (1 - amount) + b.R * amount),
                (int)Math.Round(a.G * (1 - amount) + b.G * amount), (int)Math.Round(a.B * (1 - amount) + b.B * amount));
        }
        private static double Linear(byte c) { double x = c / 255.0; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
        internal static double Luminance(Color c) { return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B); }
        internal static double Contrast(Color a, Color b)
        { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
        internal static Color BestText(Color a, Color b)
        {
            double white = Math.Min(Contrast(Color.White, a), Contrast(Color.White, b));
            double black = Math.Min(Contrast(Color.Black, a), Contrast(Color.Black, b));
            return white >= black ? Color.White : Color.Black;
        }
        internal Color CanvasColor(Color color)
        {
            // Keep arbitrary user colors legible behind the window heading.
            for (int n = 0; n < 40; n++)
            {
                double l = Luminance(color);
                if (Dark ? l <= .075 : l >= .42) return color;
                color = Mix(color, Dark ? Color.FromArgb(8, 12, 24) : Color.White, .12);
            }
            return color;
        }
        internal static Color Hex(string text)
        {
            int rgb;
            if (text == null || text.Length != 7 || text[0] != '#' || !int.TryParse(text.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
                throw new FormatException("Неверный формат цвета.");
            return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }
        internal static string Hex(Color color) { return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2"); }
        internal static ThemePalette[] Presets()
        {
            return new[] {
                Make("Аврора", false, "#E3ECFF", "#F7E6F7", "#7CC9FF", "#AB87FF", "#FF99CA", "#6FE1D3", "#6C5BEB"),
                Make("Персик", false, "#FFF1E7", "#FFE6EE", "#FFD09A", "#FFB4C4", "#E0BBFA", "#F7DBAF", "#B85459"),
                Make("Мята", false, "#E5FAF5", "#E7F0FC", "#83DCCB", "#B0D6FF", "#BFDDD3", "#CBE9B4", "#207C76"),
                Make("Полночь", true, "#10192C", "#191B35", "#25588A", "#59429D", "#753C70", "#1D686B", "#7A91F4"),
                Make("Аметист", true, "#21172F", "#171C31", "#573D82", "#733F77", "#873C5E", "#284A76", "#B28DF1"),
                Make("Графит", true, "#181D26", "#20262E", "#3C5262", "#414C6A", "#59505F", "#2D5C59", "#69C6BC")
            };
        }
        private static ThemePalette Make(string name, bool dark, params string[] colors)
        {
            return new ThemePalette { Name = name, Dark = dark, Top = Hex(colors[0]), Bottom = Hex(colors[1]), GlowA = Hex(colors[2]),
                GlowB = Hex(colors[3]), GlowC = Hex(colors[4]), GlowD = Hex(colors[5]), Accent = Hex(colors[6]) };
        }
    }

    internal static class ThemeStore
    {
        internal static string DefaultPath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "findUrLogs.theme"); } }
        internal static ThemePalette Load(string path, out string warning)
        {
            warning = null;
            try
            {
                if (!File.Exists(path)) return ThemePalette.Presets()[0];
                using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (file.Length > 4096) throw new FormatException("Слишком большой файл оформления.");
                    using (StreamReader reader = new StreamReader(file, Encoding.UTF8, true)) return Parse(reader.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                if (!(e is IOException) && !(e is UnauthorizedAccessException) && !(e is FormatException) && !(e is System.Security.SecurityException)) throw;
                warning = "Не удалось загрузить оформление. Используется тема «Аврора».";
                return ThemePalette.Presets()[0];
            }
        }
        internal static ThemePalette Parse(string text)
        {
            if (text == null || text.Length > 4096) throw new FormatException("Неверный файл оформления.");
            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim(); if (line.Length == 0) continue;
                int equals = line.IndexOf('=');
                if (equals <= 0 || data.ContainsKey(line.Substring(0, equals))) throw new FormatException("Неверные параметры оформления.");
                data.Add(line.Substring(0, equals), line.Substring(equals + 1));
            }
            string[] keys = { "Version", "Name", "Dark", "Top", "Bottom", "GlowA", "GlowB", "GlowC", "GlowD", "Accent" };
            if (data.Count != keys.Length || keys.Any(x => !data.ContainsKey(x)) || data["Version"] != "1") throw new FormatException("Неподдерживаемый формат оформления.");
            bool dark;
            if (!bool.TryParse(data["Dark"], out dark) || data["Name"].Length < 1 || data["Name"].Length > 60 || data["Name"].Any(char.IsControl)) throw new FormatException("Неверные параметры оформления.");
            return new ThemePalette { Name = data["Name"], Dark = dark, Top = ThemePalette.Hex(data["Top"]), Bottom = ThemePalette.Hex(data["Bottom"]),
                GlowA = ThemePalette.Hex(data["GlowA"]), GlowB = ThemePalette.Hex(data["GlowB"]), GlowC = ThemePalette.Hex(data["GlowC"]),
                GlowD = ThemePalette.Hex(data["GlowD"]), Accent = ThemePalette.Hex(data["Accent"]) };
        }
        internal static string Serialize(ThemePalette p)
        {
            return "Version=1\nName=" + p.Name + "\nDark=" + p.Dark + "\nTop=" + ThemePalette.Hex(p.Top) + "\nBottom=" + ThemePalette.Hex(p.Bottom)
                + "\nGlowA=" + ThemePalette.Hex(p.GlowA) + "\nGlowB=" + ThemePalette.Hex(p.GlowB) + "\nGlowC=" + ThemePalette.Hex(p.GlowC)
                + "\nGlowD=" + ThemePalette.Hex(p.GlowD) + "\nAccent=" + ThemePalette.Hex(p.Accent) + "\n";
        }
        internal static void Save(string path, ThemePalette palette)
        {
            string content = Serialize(palette); Parse(content);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, content, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    internal static class Theme
    {
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        internal static ThemePalette Current = ThemePalette.Presets()[0];
        internal static void Apply(Control root)
        {
            ThemePalette p = Current;
            root.SuspendLayout();
            ApplyControl(root, p);
            root.ResumeLayout(true); root.Invalidate(true);
        }
        private static void ApplyControl(Control c, ThemePalette p)
        {
            AuroraCanvas canvas = c as AuroraCanvas;
            if (canvas != null) canvas.ClearBackdrop();
            Form form = c as Form;
            if (form != null)
            {
                form.BackColor = p.Surface; form.ForeColor = p.Text;
                if (form.IsHandleCreated)
                {
                    int dark = p.Dark ? 1 : 0;
                    try { DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int)); }
                    catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
                }
            }
            Label label = c as Label;
            if (label != null) label.ForeColor = label.Font.Size >= 12 ? p.Text : p.Muted;
            TextBox box = c as TextBox;
            if (box != null) { box.BackColor = p.Input; box.ForeColor = box.ReadOnly ? p.Muted : p.Text; }
            ComboBox combo = c as ComboBox;
            if (combo != null) { combo.BackColor = p.Input; combo.ForeColor = p.Text; }
            DataGridView grid = c as DataGridView;
            if (grid != null)
            {
                grid.BackgroundColor = p.Input;
                grid.ColumnHeadersDefaultCellStyle.BackColor = p.Header; grid.ColumnHeadersDefaultCellStyle.ForeColor = p.Muted;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.Header;
                grid.DefaultCellStyle.BackColor = p.Input; grid.DefaultCellStyle.ForeColor = p.Text;
                grid.DefaultCellStyle.SelectionBackColor = p.Selection; grid.DefaultCellStyle.SelectionForeColor = p.Text;
                grid.AlternatingRowsDefaultCellStyle.BackColor = ThemePalette.Mix(p.Input, p.Header, .24);
                grid.GridColor = p.Dark ? p.Surface : Color.FromArgb(233, 235, 248);
                foreach (DataGridViewRow row in grid.Rows)
                {
                    Finding f = row.Tag as Finding;
                    if (f != null) ColorFinding(row, f);
                }
            }
            foreach (Control child in c.Controls) ApplyControl(child, p);
        }
        internal static void ColorFinding(DataGridViewRow row, Finding f)
        {
            Color color = f.IsEvent ? Color.FromArgb(132, 82, 179) : f.Kind == "Папка" ? Color.FromArgb(175, 108, 43) : Color.FromArgb(36, 120, 146);
            if (Current.Dark) color = ThemePalette.Mix(color, Color.White, .48);
            row.Cells[0].Style.ForeColor = color; row.Cells[0].Style.SelectionForeColor = color;
        }
    }
}
