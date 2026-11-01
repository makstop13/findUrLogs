using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using FindUrLogs;

internal static class ScannerTests
{
    private static int assertions;
    private static void Check(bool success, string message) { if (!success) throw new Exception(message); assertions++; }
    private static SearchOptions Options(string root)
    {
        SearchOptions o = new SearchOptions { Terms = SearchOptions.ParseTerms("ExampleGame; ИграТест"), ReadEvents = false };
        o.Roots.Add(new SearchRoot(root, "Fixture")); return o;
    }
    private static ScanReport Run(SearchOptions o, out List<Finding> list)
    {
        List<Finding> result = new List<Finding>();
        ScanReport report = new Scanner(o, CancellationToken.None, result.Add, delegate {}).Run();
        list = result; return report;
    }
    private static string Hash(string path)
    {
        using (SHA256 hash = SHA256.Create()) using (FileStream f = File.OpenRead(path)) return Convert.ToBase64String(hash.ComputeHash(f));
    }
    private static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "ExampleGame Saves"));
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        File.WriteAllText(Path.Combine(root, "ExampleGame.log"), "name match");
        File.WriteAllText(Path.Combine(root, "runtime.log"), "something EXAMPLEGAME.exe something");
        File.WriteAllText(Path.Combine(root, "unicode.wer"), "Ошибка ИграТест", Encoding.Unicode);
        File.WriteAllText(Path.Combine(root, "ansi.log"), "ExampleGame", Encoding.Default);
        File.WriteAllText(Path.Combine(root, "ignored.txt"), "ExampleGame");
        File.WriteAllText(Path.Combine(root, "unrelated.log"), "no matching terms");
        File.WriteAllText(Path.Combine(root, "old-ExampleGame.sav"), "precious save");
        File.SetLastWriteTimeUtc(Path.Combine(root, "old-ExampleGame.sav"), DateTime.UtcNow.AddDays(-90));
        File.WriteAllText(Path.Combine(root, "nested", "ExampleGame.cfg"), "setting=1");
        File.WriteAllText(Path.Combine(root, "node_modules", "ExampleGame.log"), "ignored");
        File.WriteAllText(Path.Combine(root, "big.log"), "ExampleGame" + new string('x', 2 * 1024 * 1024));
        string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        Dictionary<string, string> before = files.ToDictionary(x => x, Hash);
        List<Finding> items;
        SearchOptions o = Options(root);
        ScanReport r = Run(o, out items);
        Check(items.Any(x => x.Name == "ExampleGame.log"), "name match missing");
        Check(items.Any(x => x.Name == "runtime.log" && x.Reason.StartsWith("Текст")), "case insensitive log match missing");
        Check(items.Any(x => x.Name == "unicode.wer"), "UTF16 match missing");
        Check(items.Any(x => x.Name == "ansi.log"), "ANSI match missing");
        Check(items.Any(x => x.Kind == "Папка"), "folder match missing");
        Check(items.Any(x => x.Name == "ExampleGame.cfg"), "nested result missing");
        Check(!items.Any(x => x.Name == "ignored.txt" || x.Name == "unrelated.log" || x.Name == "big.log"), "unsafe content scope");
        Check(!items.Any(x => x.Location.Contains("node_modules")), "ignored dir traversed");
        Check(r.Notes.Any(x => x.Contains("2 МБ")), "large log skipped silently");
        Check(r.Results == items.Count, "count mismatch");
        o = Options(root); o.SinceUtc = DateTime.UtcNow.AddDays(-1);
        Run(o, out items);
        Check(!items.Any(x => x.Name == "old-ExampleGame.sav"), "date filter failed");
        o = Options(root); o.ReadLogText = false; Run(o, out items);
        Check(!items.Any(x => x.Name == "runtime.log"), "content mode ignored");
        Check(items.Any(x => x.Name == "ExampleGame.log"), "name scan disabled");
        o = Options(root); o.MaxResults = 2; r = Run(o, out items);
        Check(items.Count == 2 && r.Notes.Any(x => x.Contains("предел результатов")), "result limit failed");
        o = Options(root); o.MaxFiles = 1; r = Run(o, out items);
        Check(r.Files == 1 && r.Notes.Any(x => x.Contains("предел файлов")), "file limit failed");
        o = Options(root); o.MaxTextBytes = 0; r = Run(o, out items);
        Check(!items.Any(x => x.Name == "runtime.log") && r.Notes.Any(x => x.Contains("Лимит чтения")), "text byte budget failed");
        o = Options(root); o.ExcludedDirectory = root; Run(o, out items);
        Check(items.Count == 0, "excluded dir scanned");
        o = Options(root); o.Roots.Add(new SearchRoot(root, "Duplicate")); r = Run(o, out items);
        Check(items.Select(x => x.Location).Distinct().Count() == items.Count, "duplicates found");
        using (CancellationTokenSource c = new CancellationTokenSource())
        {
            c.Cancel(); r = new Scanner(Options(root), c.Token, delegate { throw new Exception("cancelled callback"); }, delegate {}).Run();
            Check(r.Cancelled && r.Files == 0, "cancellation failed");
        }
        Check(SearchOptions.ParseTerms("One; one; Two").Length == 2, "term dedup failed");
        bool rejected = false; try { SearchOptions.ParseTerms("ab"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "short term accepted");
        foreach (string f in files) Check(File.Exists(f) && before[f] == Hash(f), "scanner changed file: " + f);
        Check(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length == files.Length, "scanner wrote files");
        o = new SearchOptions { Terms = new[] { "Microsoft", "Windows", "Service" }, ReadEvents = true, MaxResults = 2, MaxSeconds = 30 };
        r = Run(o, out items);
        Console.WriteLine("Event smoke: " + r.Summary());
        foreach (string note in r.Notes) Console.WriteLine(note);
        if (r.Events == 0)
        {
            Check(r.Skipped == 2, "event permission failure not counted");
            Check(r.Notes.Count(x => x.StartsWith("Не удалось прочитать журнал")) == 2, "event permission failures hidden");
            Check(items.Count == 0, "unexpected event results after access denial");
        }
        else
        {
            Check(r.Events > 0, "event reader did not read events");
            Check(items.Count > 0 && items.All(x => x.IsEvent), "event matching failed");
            Check(items.All(x => x.EventRecordId.HasValue && !string.IsNullOrEmpty(x.Details)), "event details missing");
        }
        Console.WriteLine("PASS: " + assertions + " assertions; file contents and counts unchanged.");
    }
}
