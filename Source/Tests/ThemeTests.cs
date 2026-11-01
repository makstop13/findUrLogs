using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using FindUrLogs;

internal static class ThemeTests
{
    private static int count;
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
    private static void Reject(string text)
    { bool rejected = false; try { ThemeStore.Parse(text); } catch (FormatException) { rejected = true; } Check(rejected, "invalid theme accepted"); }
    private static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        string path = Path.Combine(root, "test.theme");
        ThemePalette[] all = ThemePalette.Presets();
        Check(all.Length == 6, "preset count"); Check(all.Count(p => p.Dark) == 3, "dark count"); Check(all.Count(p => !p.Dark) == 3, "light count");
        Check(all.Select(p => p.Name).Distinct().Count() == 6, "duplicate preset");
        foreach (ThemePalette p in all)
        {
            string serialized = ThemeStore.Serialize(p);
            Check(ThemeStore.Serialize(ThemeStore.Parse(serialized)) == serialized, "round trip " + p.Name);
            Check(ThemePalette.Contrast(p.Text, p.Surface) >= 4.5, "surface contrast " + p.Name);
            Check(ThemePalette.Contrast(p.Muted, p.Surface) >= 4.5, "muted contrast " + p.Name);
            Check(ThemePalette.Contrast(p.Text, p.Input) >= 4.5, "input contrast " + p.Name);
            Check(ThemePalette.Contrast(p.AccentText, p.Accent) >= 4.5, "button contrast " + p.Name);
            foreach (Color color in new[] { Color.Black, Color.White, Color.Red, Color.Blue, Color.Lime, p.Top, p.GlowB })
            {
                Color bg = p.CanvasColor(color);
                Check(p.Dark ? ThemePalette.Luminance(bg) <= .075 : ThemePalette.Luminance(bg) >= .42, "canvas luminance " + p.Name);
                Check(ThemePalette.Contrast(p.Text, bg) >= 4.5, "canvas contrast " + p.Name);
            }
            ThemePalette clone = p.Clone(); clone.Name = "Changed";
            Check(p.Name != clone.Name, "clone aliasing");
        }
        string warning;
        ThemePalette loaded = ThemeStore.Load(Path.Combine(root, "missing.theme"), out warning);
        Check(warning == null && loaded.Name == "Аврора", "first run");
        ThemeStore.Save(path, all[3]); loaded = ThemeStore.Load(path, out warning);
        Check(warning == null && ThemeStore.Serialize(loaded) == ThemeStore.Serialize(all[3]), "save and load");
        ThemePalette custom = all[5].Clone(); custom.Name = "Своя палитра"; custom.Accent = Color.FromArgb(17, 93, 211);
        ThemeStore.Save(path, custom); loaded = ThemeStore.Load(path, out warning);
        Check(warning == null && ThemeStore.Serialize(loaded) == ThemeStore.Serialize(custom), "atomic replace custom theme");
        string good = ThemeStore.Serialize(all[0]);
        Reject(good.Replace("Version=1", "Version=2")); Reject(good + "Name=duplicate\n"); Reject(good.Replace("Dark=False", "Dark=maybe"));
        Reject(good.Replace("Accent=#6C5BEB", "Accent=#GGGGGG")); Reject(good.Replace("Name=Аврора", "Name=")); Reject(good + "Extra=1\n");
        Reject("Version=1\n"); Reject(new string('x', 5000)); Reject(good.Replace("Name=Аврора", "Name=Bad\tName"));
        File.WriteAllText(path, "malformed"); loaded = ThemeStore.Load(path, out warning);
        Check(loaded.Name == "Аврора" && warning != null, "malformed fallback");
        Check(File.ReadAllText(path) == "malformed", "bad settings overwritten on load");
        File.WriteAllText(path, new string('x', 5000)); ThemeStore.Load(path, out warning); Check(warning != null, "oversize fallback");
        string directoryTarget = Path.Combine(root, "directory-target"); Directory.CreateDirectory(directoryTarget);
        bool failed = false; try { ThemeStore.Save(directoryTarget, custom); } catch (IOException) { failed = true; }
        Check(failed, "save error not reported"); Check(Directory.GetFiles(root, "*.tmp").Length == 0, "temporary settings leaked");
        Check(ThemePalette.Hex(ThemePalette.Hex("#1193D3")) == "#1193D3", "hex round trip");
        foreach (Color accent in new[] { Color.Black, Color.White, Color.Red, Color.Lime, Color.Blue, Color.Gray, Color.Yellow, Color.Cyan })
        {
            ThemePalette p = all[0].Clone(); p.Accent = accent; p.GlowA = Color.White;
            Check(ThemePalette.Contrast(p.AccentText, p.Accent) >= 4.5, "custom accent contrast");
            Check(ThemePalette.Contrast(p.AccentText, p.AccentEnd) >= 4.5, "custom gradient contrast");
        }
        using (Panel panel = new Panel())
        using (TextBox box = new TextBox { Text = "retained query" })
        using (DataGridView grid = new DataGridView { AllowUserToAddRows = false })
        {
            grid.Columns.Add("name", "Name"); int row = grid.Rows.Add("retained result");
            grid.Rows[row].Tag = new Finding { IsEvent = true }; grid.CurrentCell = grid.Rows[row].Cells[0];
            panel.Controls.Add(box); panel.Controls.Add(grid);
            Theme.Current = all[3].Clone(); Theme.Apply(panel);
            Check(box.Text == "retained query", "theme changed query");
            Check(grid.Rows.Count == 1 && (string)grid.Rows[0].Cells[0].Value == "retained result", "theme changed results");
            Check(grid.CurrentCell == grid.Rows[0].Cells[0], "theme changed selection");
            Check(box.BackColor == Theme.Current.Input && grid.BackgroundColor == Theme.Current.Input, "dark input colors not applied");
            Theme.Current = all[0].Clone(); Theme.Apply(panel);
            Check(grid.Rows.Count == 1 && box.Text == "retained query", "light restore lost state");
        }
        Console.WriteLine("PASS: " + count + " theme assertions.");
    }
}
