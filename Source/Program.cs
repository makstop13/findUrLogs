using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FindUrLogs
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try { Native.SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal static class Native
    {
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint flags, out uint attrs);
        [DllImport("shell32.dll")] private static extern int SHOpenFolderAndSelectItems(IntPtr pidl, uint count, IntPtr children, uint flags);

        internal static void Reveal(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) throw new IOException("Объект уже удалён, перемещён или его том отключён. Повторите поиск после подключения тома.");
            IntPtr pidl;
            uint attrs;
            Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out attrs));
            try { Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(pidl, 0, IntPtr.Zero, 0)); }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
    }

    public sealed partial class MainForm : Form
    {
        private readonly Color ink = Color.FromArgb(27, 38, 59);
        private readonly Color blue = Color.FromArgb(37, 99, 235);
        private TextBox terms, folder, filter, detail;
        private ComboBox period;
        private CheckBox appdata, docs, temp, steam, content, events, onlyFolder;
        private Button search, stop, pickExe, pickFolder, reveal, openEvent, copy;
        private Label status, count;
        private ProgressBar bar;
        private DataGridView grid;
        private List<Finding> findings = new List<Finding>();
        private List<Control> settings = new List<Control>();
        private CancellationTokenSource cancellation;
        private ScanReport lastReport;
        private bool busy;

        public MainForm()
        {
            string themeWarning;
            Theme.Current = ThemeStore.Load(ThemeStore.DefaultPath, out themeWarning);
            Text = "findUrLogs — поиск данных приложения";
            Font = new Font("Segoe UI", 10F);
            ForeColor = ink;
            BackColor = Color.FromArgb(234, 239, 253);
            ClientSize = new Size(1320, 930);
            MinimumSize = new Size(1180, 880);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = DesignAssets.MakeIcon();
            BuildUi();
            Theme.Apply(this);
            if (themeWarning != null) status.Text = themeWarning;
            Shown += delegate { Theme.Apply(this); terms.Focus(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (busy) { cancellation.Cancel(); e.Cancel = true; status.Text = "Останавливаю поиск. После остановки окно можно закрыть."; }
            };
        }

        private void AddColumn(string caption, float weight)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = caption, FillWeight = weight, MinimumWidth = 55,
                SortMode = DataGridViewColumnSortMode.Automatic });
        }

        private Finding Selected() { return grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].Tag as Finding; }

        private void UpdateActions()
        {
            Finding f = Selected(); reveal.Enabled = f != null && !f.IsEvent; copy.Enabled = f != null; openEvent.Enabled = f != null && f.IsEvent;
        }

        private void ShowSelection()
        {
            Finding f = Selected(); UpdateActions();
            if (f == null) return;
            detail.Text = f.Location + "\r\n" + f.Reason + "\r\n" + f.Details;
            detail.SelectionStart = 0; detail.SelectionLength = 0;
        }

        private void RevealSelected()
        {
            Finding f = Selected(); if (f == null || f.IsEvent) return;
            TryAction(delegate { Native.Reveal(f.Location); });
        }

        private void TryAction(Action action)
        {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Действие недоступно", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        private void PickExe()
        {
            using (OpenFileDialog dlg = new OpenFileDialog { Filter = "Программы (*.exe)|*.exe", CheckFileExists = true, Title = "Выбрать программу — файл не будет запущен" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string name = Path.GetFileNameWithoutExtension(dlg.FileName);
                string proposed = string.IsNullOrWhiteSpace(terms.Text) ? name : terms.Text.Trim() + "; " + name;
                try { terms.Text = string.Join("; ", SearchOptions.ParseTerms(proposed)); }
                catch (ArgumentException ex) { MessageBox.Show(this, ex.Message, "Название для поиска"); }
            }
        }

        private SearchOptions GetOptions()
        {
            SearchOptions o = new SearchOptions { Terms = SearchOptions.ParseTerms(terms.Text), ReadEvents = events.Checked,
                ReadLogText = content.Checked, ExcludedDirectory = AppDomain.CurrentDomain.BaseDirectory };
            int[] days = { 1, 7, 30, 0 };
            if (days[period.SelectedIndex] > 0) o.SinceUtc = DateTime.UtcNow.AddDays(-days[period.SelectedIndex]);
            string custom = folder.Text.Trim();
            if (custom.Length > 0)
            {
                custom = Path.GetFullPath(custom);
                if (custom.StartsWith("\\\\", StringComparison.Ordinal)) throw new ArgumentException("Выберите локальную папку: сетевые пути не поддерживаются.");
                if (!Directory.Exists(custom)) throw new ArgumentException("Дополнительная папка не найдена. Подключите том или выберите другой путь.");
                if ((File.GetAttributes(custom) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Выберите саму папку, а не ссылку или точку подключения.");
                if (string.Equals(custom.TrimEnd('\\'), Path.GetPathRoot(custom).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Выберите конкретную папку вместо целого диска.");
                o.Roots.Add(new SearchRoot(custom, "Выбранная папка"));
            }
            else if (onlyFolder.Checked) throw new ArgumentException("Выберите папку для поиска.");
            if (!onlyFolder.Checked)
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (appdata.Checked)
                {
                    o.Roots.Add(new SearchRoot(local, "AppData / Local"));
                    o.Roots.Add(new SearchRoot(roaming, "AppData / Roaming"));
                    o.Roots.Add(new SearchRoot(Path.Combine(Path.GetDirectoryName(local), "LocalLow"), "AppData / LocalLow"));
                }
                if (docs.Checked)
                {
                    o.Roots.Add(new SearchRoot(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Документы"));
                    o.Roots.Add(new SearchRoot(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Saved Games"), "Сохранённые игры"));
                }
                if (temp.Checked) o.Roots.Add(new SearchRoot(Path.GetTempPath(), "Временные файлы"));
                if (steam.Checked)
                {
                    string steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                    if (!string.IsNullOrEmpty(steamPath)) o.Roots.Add(new SearchRoot(Path.Combine(steamPath, "logs"), "Логи Steam"));
                }
            }
            if (o.Roots.Count == 0 && !o.ReadEvents) throw new ArgumentException("Выберите хотя бы одну область поиска.");
            // Prioritize narrow roots before the broad AppData scan.
            o.Roots = o.Roots.OrderBy(x => x.Label.StartsWith("AppData", StringComparison.Ordinal) ? 1 : 0)
                .GroupBy(x => x.Path.TrimEnd('\\'), StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
            return o;
        }

        private async Task StartSearch()
        {
            if (busy) return;
            SearchOptions o;
            try { o = GetOptions(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Проверьте параметры", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            findings.Clear(); grid.Rows.Clear(); filter.Clear(); lastReport = null;
            detail.Text = "Поиск выполняется. Совпадения появятся в списке. Найденные файлы не изменяются.";
            busy = true; cancellation = new CancellationTokenSource(); UpdateEmptyState();
            foreach (Control c in settings) c.Enabled = false;
            search.Enabled = false; stop.Enabled = true; bar.Visible = true;
            count.Text = "Совпадения · 0"; status.Text = "Начинаю поиск…";
            Progress<Finding> results = new Progress<Finding>(delegate(Finding item) {
                findings.Add(item);
                if (VisibleFinding(item)) AppendRow(item);
                count.Text = "Совпадения · " + findings.Count + (filter.Text.Length > 0 ? " / показано " + grid.Rows.Count : "");
            });
            Progress<string> progress = new Progress<string>(delegate(string value) { status.Text = value; });
            try
            {
                lastReport = await Task.Run(delegate {
                    return new Scanner(o, cancellation.Token, ((IProgress<Finding>)results).Report, ((IProgress<string>)progress).Report).Run();
                });
                // Progress callbacks use the UI context; refresh from the accumulated list after completion.
                status.Text = (lastReport.Cancelled ? "Остановлено. " : "Готово. ") + lastReport.Summary();
                if (lastReport.Notes.Count > 0) status.Text += " · Есть ограничения: см. сводку";
                if (findings.Count == 0) detail.Text = "Совпадений не найдено в проверенных областях.\r\nПопробуйте другое имя, выберите .exe, расширьте период или укажите папку.\r\n\r\nЭто не доказывает отсутствие данных о запуске. Ограничения и пропуски перечислены в сводке поиска.";
            }
            catch (Exception ex)
            {
                status.Text = "Поиск завершился с ошибкой. Уже найденные результаты сохранены в списке.";
                MessageBox.Show(this, ex.Message, "Не удалось завершить поиск", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                busy = false; foreach (Control c in settings) c.Enabled = true;
                search.Enabled = true; stop.Enabled = false; bar.Visible = false; UpdateEmptyState();
                cancellation.Dispose(); cancellation = null; ShowSelection(); UpdateActions();
            }
        }

        private bool VisibleFinding(Finding f)
        {
            string text = filter.Text.Trim();
            return text.Length == 0 || (f.Name + " " + f.Location + " " + f.Kind + " " + f.Reason).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AppendRow(Finding f)
        {
            int index = grid.Rows.Add(f.Kind, f.Name, f.Reason, f.Modified.HasValue ? f.Modified.Value.ToString("dd.MM.yy HH:mm") : "—", FormatBytes(f.Bytes), f.Location);
            grid.Rows[index].Tag = f;
            Theme.ColorFinding(grid.Rows[index], f);
            foreach (DataGridViewCell cell in grid.Rows[index].Cells) cell.ToolTipText = f.Location + "\r\n" + f.Reason;
            if (grid.CurrentRow == grid.Rows[index]) ShowSelection();
        }

        private void RefreshRows()
        {
            grid.Rows.Clear();
            foreach (Finding f in findings) if (VisibleFinding(f)) AppendRow(f);
            count.Text = "Совпадения · " + findings.Count + (filter.Text.Length > 0 ? " / показано " + grid.Rows.Count : "");
            if (grid.Rows.Count == 0) detail.Text = findings.Count == 0 ? "Результатов пока нет." : "Нет результатов, подходящих под фильтр.";
            UpdateEmptyState();
            UpdateActions();
        }

        private void UpdateEmptyState()
        {
            FinderGrid view = grid as FinderGrid;
            if (view == null) return;
            if (filter.Text.Trim().Length > 0)
            { view.EmptyTitle = "По фильтру ничего не найдено"; view.EmptyDescription = "Измените или очистите фильтр, чтобы увидеть результаты."; }
            else if (busy)
            { view.EmptyTitle = "Ищем совпадения…"; view.EmptyDescription = "Можно продолжить работу с окном или остановить поиск."; }
            else if (lastReport != null)
            { view.EmptyTitle = "Совпадений не найдено"; view.EmptyDescription = "Попробуйте другое имя, период или папку.\nГраницы поиска указаны в сводке."; }
            else
            { view.EmptyTitle = "Здесь появятся совпадения"; view.EmptyDescription = "Введите название приложения и начните поиск.\nКаждый результат можно будет проверить вручную."; }
            view.Invalidate();
        }

        private static string FormatBytes(long? size)
        {
            if (!size.HasValue) return "—";
            if (size.Value < 1024) return size + " Б";
            if (size.Value < 1024 * 1024) return (size.Value / 1024.0).ToString("0.#") + " КБ";
            return (size.Value / (1024.0 * 1024)).ToString("0.#") + " МБ";
        }

        private void ShowSummary()
        {
            StringBuilder text = new StringBuilder();
            if (lastReport != null)
            {
                text.AppendLine(lastReport.Summary()); text.AppendLine();
                foreach (string note in lastReport.Notes) text.AppendLine("• " + note);
            }
            else text.AppendLine(busy ? "Поиск ещё выполняется." : "Поиск ещё не запускался.");
            text.AppendLine();
            text.AppendLine("Область: выбранные пользовательские папки и логи Steam; события только Application и System. До 3 000 последних событий каждого журнала.");
            text.AppendLine("До 500 результатов, 100 000 файлов, 30 000 папок, глубина 12 уровней, время 120 секунд.");
            text.AppendLine("Текст: только .log и .wer до 2 МБ, суммарно до 100 МБ. Кодировки UTF-8, UTF-16 с BOM и системная ANSI. Другие файлы — по имени.");
            text.AppendLine("Ссылки и точки повторной обработки, служебные папки разработки и папка самой утилиты пропускаются. Сетевые пути не поддерживаются.");
            text.AppendLine("Нет автоматического определения псевдонимов и Steam App ID. Общие кэши GPU, содержимое ярлыков, реестр, Security и прочие журналы не исследуются.");
            text.AppendLine("Результат — совпадение текста, а не доказательство принадлежности. Утилита не удаляет данные и не обещает полного обнаружения истории запусков.");
            text.AppendLine("Собственный отчёт на диск не записывается, сетевых запросов нет. Сам запуск и чтение файлов могут учитываться Windows и другим ПО.");
            text.AppendLine("По кнопке «Применить» оформление сохраняется в findUrLogs.theme рядом с программой. Запросы и результаты в этот файл не записываются.");
            using (Form dialog = new Form { Text = "Сводка и границы поиска", Size = new Size(790, 570), MinimumSize = new Size(580, 400), StartPosition = FormStartPosition.CenterParent, Font = Font, MinimizeBox = false, MaximizeBox = false })
            {
                dialog.Controls.Add(new TextBox { Text = text.ToString(), ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.None, Margin = new Padding(15) });
                dialog.Padding = new Padding(18); Theme.Apply(dialog); dialog.ShowDialog(this);
            }
        }
    }
}
