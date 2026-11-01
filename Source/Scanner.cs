using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FindUrLogs
{
    public sealed class SearchOptions
    {
        public string[] Terms;
        public DateTime? SinceUtc;
        public bool ReadLogText = true;
        public bool ReadEvents = true;
        public int MaxResults = 500;
        public int MaxFiles = 100000;
        public int MaxDirectories = 30000;
        public int MaxDepth = 12;
        public int MaxSeconds = 120;
        public long MaxTextBytes = 100L * 1024 * 1024;
        public string ExcludedDirectory;
        public List<SearchRoot> Roots = new List<SearchRoot>();

        public static string[] ParseTerms(string input)
        {
            string[] terms = (input ?? "").Split(';').Select(x => x.Trim()).Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (terms.Length == 0 || terms.Length > 8)
                throw new ArgumentException("Введите от 1 до 8 названий через точку с запятой.");
            if (terms.Any(x => x.Length < 3 || x.Length > 120 || x.Any(char.IsControl)))
                throw new ArgumentException("Каждое название должно содержать от 3 до 120 символов без переводов строк.");
            return terms;
        }
    }

    public sealed class SearchRoot
    {
        public string Path;
        public string Label;
        public SearchRoot(string path, string label) { Path = path; Label = label; }
    }

    public sealed class Finding
    {
        public string Kind;
        public string Name;
        public string Location;
        public string Reason;
        public string Details;
        public DateTime? Modified;
        public long? Bytes;
        public bool IsEvent;
        public string EventChannel;
        public long? EventRecordId;
    }

    public sealed class ScanReport
    {
        public int Files, Directories, TextFiles, Events, Skipped, Results;
        public long TextBytes;
        public bool Cancelled;
        public List<string> Notes = new List<string>();
        public string Summary()
        {
            return string.Format("Найдено: {0} · файлов проверено: {1:N0} · логов прочитано: {2:N0} · событий: {3:N0} · пропусков: {4:N0}",
                Results, Files, TextFiles, Events, Skipped);
        }
    }

    public sealed class Scanner
    {
        private readonly SearchOptions options;
        private readonly CancellationToken token;
        private readonly Action<Finding> found;
        private readonly Action<string> progress;
        private readonly ScanReport report = new ScanReport();
        private readonly HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Stopwatch watch = new Stopwatch();
        private bool filesLimited, stopped;
        private static readonly HashSet<string> IgnoredDirs = new HashSet<string>(
            new[] { ".git", "node_modules", ".venv", "venv", "$Recycle.Bin", "System Volume Information" }, StringComparer.OrdinalIgnoreCase);

        public Scanner(SearchOptions options, CancellationToken token, Action<Finding> found, Action<string> progress)
        { this.options = options; this.token = token; this.found = found; this.progress = progress; }

        public ScanReport Run()
        {
            watch.Start();
            foreach (SearchRoot root in options.Roots)
            {
                if (Stop() || filesLimited) break;
                if (root.Path.StartsWith("\\\\", StringComparison.Ordinal)) { Note("Сетевая папка пропущена: " + root.Label); report.Skipped++; continue; }
                if (Directory.Exists(root.Path)) Walk(root.Path, root.Label, 0);
                else Note("Папка недоступна или отсутствует: " + root.Label);
            }
            if (!Stop() && options.ReadEvents) ReadEventLogs();
            report.Cancelled = token.IsCancellationRequested;
            if (report.Cancelled) Note("Поиск остановлен пользователем. Показаны уже найденные совпадения.");
            if (!options.ReadLogText) Note("Содержимое файлов не проверялось.");
            if (!options.ReadEvents) Note("Журналы Windows не проверялись.");
            return report;
        }

        private void Note(string note) { if (!report.Notes.Contains(note)) report.Notes.Add(note); }

        private bool Stop()
        {
            if (stopped || token.IsCancellationRequested) return true;
            if (report.Results >= options.MaxResults) { Note("Достигнут предел результатов (" + options.MaxResults + "). Уточните название или область поиска."); stopped = true; }
            if (watch.Elapsed.TotalSeconds >= options.MaxSeconds) { Note("Достигнут лимит времени (" + options.MaxSeconds + " с). Поиск неполный; сузьте область."); stopped = true; }
            return stopped;
        }

        private string Match(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (string term in options.Terms)
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return term;
            return null;
        }

        private bool IsExcluded(string path)
        {
            if (string.IsNullOrEmpty(options.ExcludedDirectory)) return false;
            string excluded = options.ExcludedDirectory.TrimEnd('\\', '/');
            return string.Equals(path, excluded, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(excluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private void Add(Finding item)
        {
            string key = item.IsEvent ? item.EventChannel + ":" + item.EventRecordId : item.Location;
            if (!seen.Add(key) || Stop()) return;
            report.Results++;
            found(item);
        }

        private void Walk(string path, string label, int depth)
        {
            if (Stop() || filesLimited || IsExcluded(path) || !visited.Add(path)) return;
            if (report.Directories >= options.MaxDirectories)
            { filesLimited = true; Note("Достигнут предел папок. Поиск файлов неполный."); return; }
            try
            {
                DirectoryInfo dir = new DirectoryInfo(path);
                if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) { report.Skipped++; return; }
                report.Directories++;
                if (report.Directories % 60 == 1) progress("Проверяю: " + label + " · файлов: " + report.Files.ToString("N0"));
                string term = Match(dir.Name);
                if (term != null)
                {
                    Add(new Finding { Kind = "Папка", Name = dir.Name, Location = dir.FullName, Modified = dir.LastWriteTime,
                        Reason = "Название папки: «" + term + "»", Details = "Область: " + label + "\r\nСовпадение только в названии. Папка может содержать сохранения, настройки и другие нужные данные.\r\nПериод поиска не ограничивает папки: дата папки не отражает все изменения внутри." });
                }
                foreach (FileInfo file in dir.EnumerateFiles())
                {
                    if (Stop()) return;
                    if (report.Files >= options.MaxFiles)
                    { filesLimited = true; Note("Достигнут предел файлов (" + options.MaxFiles.ToString("N0") + "). Поиск файлов неполный."); return; }
                    report.Files++;
                    try { InspectFile(file, label); }
                    catch (Exception ex) { if (!IsFileException(ex)) throw; report.Skipped++; }
                }
                if (depth >= options.MaxDepth)
                {
                    if (dir.EnumerateDirectories().Any()) { report.Skipped++; Note("Некоторые папки глубже " + options.MaxDepth + " уровней пропущены."); }
                    return;
                }
                foreach (DirectoryInfo child in dir.EnumerateDirectories())
                {
                    if (Stop() || filesLimited) return;
                    if (IgnoredDirs.Contains(child.Name)) { report.Skipped++; continue; }
                    Walk(child.FullName, label, depth + 1);
                }
            }
            catch (Exception ex) { if (!IsFileException(ex)) throw; report.Skipped++; }
        }

        private static bool IsFileException(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException
                || ex is NotSupportedException || ex is ArgumentException;
        }

        private void InspectFile(FileInfo file, string label)
        {
            if ((file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0) { report.Skipped++; return; }
            if (options.SinceUtc.HasValue && file.LastWriteTimeUtc < options.SinceUtc.Value) return;
            string term = Match(file.Name);
            string reason = term == null ? null : "Имя файла: «" + term + "»";
            string extension = file.Extension.ToLowerInvariant();
            if (reason == null && options.ReadLogText && (extension == ".log" || extension == ".wer"))
            {
                if (file.Length > 2L * 1024 * 1024) { report.Skipped++; Note("Логи больше 2 МБ проверялись только по имени."); }
                else if (report.TextBytes + file.Length > options.MaxTextBytes)
                { report.Skipped++; Note("Лимит чтения логов исчерпан (100 МБ). Остальные файлы проверялись только по имени."); }
                else
                {
                    // Bounded read; no writes, no execution, sharing with running applications.
                    using (FileStream stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        int capacity = (int)Math.Min(file.Length + 1, 2L * 1024 * 1024 + 1);
                        byte[] data = new byte[capacity];
                        int used = 0, count;
                        while (used < data.Length && (count = stream.Read(data, used, data.Length - used)) > 0) used += count;
                        report.TextBytes += used;
                        report.TextFiles++;
                        if (used > 2 * 1024 * 1024) { report.Skipped++; return; }
                        using (MemoryStream memory = new MemoryStream(data, 0, used))
                        using (StreamReader reader = new StreamReader(memory, Encoding.UTF8, true))
                            term = Match(reader.ReadToEnd());
                        // Legacy Windows logs can use the local ANSI code page without a BOM.
                        if (term == null) term = Match(Encoding.Default.GetString(data, 0, used));
                        if (term != null) reason = "Текст лога: «" + term + "»";
                    }
                }
            }
            if (reason == null) return;
            string kind = "Файл";
            if (extension == ".log" || extension == ".wer" || extension == ".dmp") kind = "Лог / отчёт";
            else if (new[] { ".sav", ".save", ".ini", ".cfg", ".json", ".xml", ".vdf", ".db" }.Contains(extension)) kind = "Данные / настройки";
            else if (extension == ".lnk") kind = "Ярлык";
            Add(new Finding { Kind = kind, Name = file.Name, Location = file.FullName, Reason = reason,
                Bytes = file.Length, Modified = file.LastWriteTime, Details = "Область: " + label
                + "\r\nСовпадение не доказывает, что файл принадлежит выбранному приложению или что его можно удалить."
                + "\r\nУтилита открывает расположение, а не запускает найденный файл. Содержимое .lnk и двоичных кэшей не разбирается." });
        }

        private void ReadEventLogs()
        {
            foreach (string channel in new[] { "Application", "System" })
            {
                if (Stop()) return;
                progress("Читаю журнал Windows: " + channel);
                long millis = options.SinceUtc.HasValue ? Math.Max(1L, (long)(DateTime.UtcNow - options.SinceUtc.Value).TotalMilliseconds) : 0;
                string queryText = millis > 0 ? "*[System[TimeCreated[timediff(@SystemTime) <= " + millis + "]]]" : "*";
                EventLogQuery query = new EventLogQuery(channel, PathType.LogName, queryText) { ReverseDirection = true };
                try
                {
                    using (EventLogReader reader = new EventLogReader(query))
                    {
                        int count = 0;
                        while (!Stop())
                        {
                            using (EventRecord record = reader.ReadEvent(TimeSpan.FromMilliseconds(500)))
                            {
                                if (record == null) break;
                                if (count >= 3000) { Note("Журнал " + channel + ": проверены только 3 000 последних событий за выбранный период."); break; }
                                count++; report.Events++;
                                string xml = record.ToXml();
                                string message = "";
                                try { message = record.FormatDescription() ?? ""; }
                                catch (EventLogException) { }
                                string term = Match(message) ?? Match(xml) ?? Match(record.ProviderName);
                                if (term == null) continue;
                                string details = message.Length > 0 ? message : xml;
                                if (details.Length > 12000) details = details.Substring(0, 12000) + "\r\n[Текст сокращён]";
                                Add(new Finding { IsEvent = true, Kind = "Событие Windows", Name = (record.ProviderName ?? channel) + " · ID " + record.Id,
                                    Location = channel + " · запись " + record.RecordId, EventChannel = channel, EventRecordId = record.RecordId,
                                    Modified = record.TimeCreated, Reason = "Текст / источник события: «" + term + "»",
                                    Details = "Только просмотр. Это запись общего журнала, а не отдельный файл.\r\n\r\n" + details });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is EventLogException) && !(ex is UnauthorizedAccessException) && !(ex is System.Security.SecurityException)) throw;
                    report.Skipped++;
                    Note("Не удалось прочитать журнал " + channel + ": " + ex.Message);
                }
            }
        }
    }
}
