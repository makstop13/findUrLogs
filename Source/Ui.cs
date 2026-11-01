using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FindUrLogs
{
    public sealed partial class MainForm
    {
        private Label LabelOf(string text, float size, bool bold)
        {
            return new Label { Text = text, AutoSize = true, BackColor = Color.Transparent, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = DesignAssets.Ink, Anchor = AnchorStyles.Left, Margin = new Padding(0, 2, 10, 2) };
        }

        private Button ButtonOf(string text, bool primary)
        {
            return new GlassButton(primary) { Text = text, AutoSize = true, Height = 40, MinimumSize = new Size(76, 36),
                Font = new Font("Segoe UI", 9, primary ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 0, 8, 0) };
        }

        private CheckBox Check(string text, bool value)
        {
            GlassCheck c = new GlassCheck { Text = text, Checked = value, Font = new Font("Segoe UI", 9), Margin = new Padding(0, 0, 0, 5) };
            settings.Add(c); return c;
        }

        private TableLayoutPanel Table(int columns, int rows)
        {
            return new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty, BackColor = Color.Transparent };
        }

        private GlassPanel Card(int padding)
        {
            return new GlassPanel { Dock = DockStyle.Fill, Padding = new Padding(padding), Margin = Padding.Empty };
        }

        private Control Field(TextBox input)
        {
            GlassPanel shell = new GlassPanel { Dock = DockStyle.Fill, Radius = 12, WhiteAlpha = 195, Padding = new Padding(13, 11, 13, 8), Margin = new Padding(0, 0, 10, 0) };
            input.BorderStyle = BorderStyle.None; input.BackColor = Color.FromArgb(250, 251, 255); input.ForeColor = DesignAssets.Ink;
            input.Dock = DockStyle.Fill; input.Margin = Padding.Empty;
            shell.Controls.Add(input); return shell;
        }

        private void Section(FlowLayoutPanel parent, string text)
        {
            Label l = LabelOf(text, 8, true); l.ForeColor = DesignAssets.Muted; l.Margin = new Padding(3, 10, 0, 7); parent.Controls.Add(l);
        }

        private void SidebarCheck(FlowLayoutPanel parent, CheckBox check, Color accent)
        {
            ((GlassCheck)check).Accent = accent; check.AutoSize = false; check.Size = new Size(254, 33); parent.Controls.Add(check);
        }

        private void BuildUi()
        {
            AuroraCanvas canvas = new AuroraCanvas { Dock = DockStyle.Fill };
            Controls.Add(canvas);
            TableLayoutPanel page = Table(1, 4); page.Padding = new Padding(25, 18, 25, 12);
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 144));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
            canvas.Controls.Add(page);

            TableLayoutPanel header = Table(3, 1);
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 289));
            BrandMark mark = new BrandMark { Size = new Size(59, 59), Anchor = AnchorStyles.Left, Margin = new Padding(2, 0, 0, 8) };
            header.Controls.Add(mark, 0, 0);
            Panel wordmark = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            Label name = LabelOf("findUrLogs", 29, true); name.Anchor = AnchorStyles.Top | AnchorStyles.Left; name.Location = new Point(0, 0);
            Label tagline = LabelOf("Находите нужное. Решайте сами.", 10, false); tagline.Anchor = AnchorStyles.Top | AnchorStyles.Left; tagline.ForeColor = DesignAssets.Muted; tagline.Location = new Point(3, 51);
            wordmark.Controls.Add(name); wordmark.Controls.Add(tagline); header.Controls.Add(wordmark, 1, 0);
            TableLayoutPanel tools = Table(1, 2); tools.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); tools.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label edition = LabelOf("ЛОКАЛЬНЫЙ ПОИСК  /  v1.2.1", 9, true); edition.Anchor = AnchorStyles.Right;
            tools.Controls.Add(edition, 0, 0);
            Button appearance = ButtonOf("Оформление…", false); appearance.Anchor = AnchorStyles.Right; appearance.Margin = new Padding(0, 0, 0, 8);
            appearance.Click += delegate { using (AppearanceDialog dialog = new AppearanceDialog(this)) dialog.ShowDialog(this); };
            tools.Controls.Add(appearance, 0, 1); header.Controls.Add(tools, 2, 0); page.Controls.Add(header, 0, 0);

            GlassPanel searchCard = Card(19); searchCard.Margin = new Padding(0, 0, 0, 12);
            TableLayoutPanel query = Table(5, 3);
            query.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); query.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 149));
            query.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148)); query.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108)); query.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            query.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); query.RowStyles.Add(new RowStyle(SizeType.Absolute, 43)); query.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label searchLabel = LabelOf("01  /  ЧТО НАЙТИ", 8, true); searchLabel.ForeColor = Color.FromArgb(98, 83, 174);
            query.Controls.Add(searchLabel, 0, 0); query.Controls.Add(LabelOf("Период изменений", 8, true), 1, 0);
            terms = new TextBox { Font = new Font("Segoe UI", 11), MaxLength = 1000, AccessibleName = "Название приложения" };
            query.Controls.Add(Field(terms), 0, 1);
            period = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(249, 250, 255), ForeColor = DesignAssets.Ink, Margin = new Padding(0, 8, 10, 0), AccessibleName = "Период поиска" };
            period.Items.AddRange(new object[] { "За 24 часа", "За 7 дней", "За 30 дней", "За всё время" }); period.SelectedIndex = 1;
            pickExe = ButtonOf("Выбрать .exe…", false); pickExe.Dock = DockStyle.Fill;
            search = ButtonOf("Найти →", true); search.Dock = DockStyle.Fill; search.AccessibleName = "Найти";
            stop = ButtonOf("Стоп", false); stop.Dock = DockStyle.Fill; stop.Enabled = false;
            query.Controls.Add(period, 1, 1); query.Controls.Add(pickExe, 2, 1); query.Controls.Add(search, 3, 1); query.Controls.Add(stop, 4, 1);
            Label hint = LabelOf("Название или несколько вариантов через ;    Например: Example Game; ExampleGame.exe", 9, false);
            hint.ForeColor = DesignAssets.Muted; hint.Margin = new Padding(3, 7, 0, 0); query.Controls.Add(hint, 0, 2); query.SetColumnSpan(hint, 5);
            settings.AddRange(new Control[] { terms, period, pickExe }); searchCard.Controls.Add(query); page.Controls.Add(searchCard, 0, 1);

            TableLayoutPanel workspace = Table(2, 1); workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 308)); workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            GlassPanel sideCard = Card(20); sideCard.Margin = new Padding(0, 0, 12, 0);
            FlowLayoutPanel sidebar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent, Margin = Padding.Empty };
            sidebar.Controls.Add(LabelOf("Область поиска", 15, true));
            Label sideHint = LabelOf("Выберите, где искать совпадения.", 9, false); sideHint.ForeColor = DesignAssets.Muted; sideHint.Margin = new Padding(0, 2, 0, 0); sidebar.Controls.Add(sideHint);
            Section(sidebar, "ПАПКИ ПОЛЬЗОВАТЕЛЯ");
            appdata = Check("AppData", true); docs = Check("Документы / сохранения", true); temp = Check("Временные файлы", true); steam = Check("Логи Steam", true);
            SidebarCheck(sidebar, appdata, Color.FromArgb(89, 116, 224)); SidebarCheck(sidebar, docs, Color.FromArgb(151, 91, 196));
            SidebarCheck(sidebar, temp, Color.FromArgb(29, 146, 137)); SidebarCheck(sidebar, steam, Color.FromArgb(206, 113, 66));
            Section(sidebar, "ДОПОЛНИТЕЛЬНО ПРОВЕРЯТЬ");
            content = Check("Текст логов .log / .wer", true); events = Check("События Windows", true);
            SidebarCheck(sidebar, content, Color.FromArgb(83, 128, 189)); SidebarCheck(sidebar, events, Color.FromArgb(122, 98, 208));
            Label eventNote = LabelOf("Application и System · только просмотр", 8, false); eventNote.ForeColor = DesignAssets.Muted; eventNote.Margin = new Padding(4, 0, 0, 0); sidebar.Controls.Add(eventNote);
            Section(sidebar, "ДОПОЛНИТЕЛЬНАЯ ПАПКА");
            folder = new TextBox { Font = new Font("Segoe UI", 9), AccessibleName = "Дополнительная папка" };
            Control folderField = Field(folder); folderField.Dock = DockStyle.None; folderField.Size = new Size(254, 40); folderField.Margin = new Padding(0, 0, 0, 5); sidebar.Controls.Add(folderField);
            pickFolder = ButtonOf("Выбрать папку…", false); pickFolder.AutoSize = false; pickFolder.Size = new Size(254, 38); pickFolder.Margin = new Padding(0, 0, 0, 5); sidebar.Controls.Add(pickFolder);
            onlyFolder = Check("Файлы: только эта папка", false); SidebarCheck(sidebar, onlyFolder, Color.FromArgb(89, 116, 224));
            settings.Add(folder); settings.Add(pickFolder);
            Label note = LabelOf("Период ограничивает файлы и события.\nПапки ищутся по имени за всё время.", 8, false); note.ForeColor = DesignAssets.Muted; note.Margin = new Padding(4, 9, 0, 3); sidebar.Controls.Add(note);
            sideCard.Controls.Add(sidebar); workspace.Controls.Add(sideCard, 0, 0);

            GlassPanel resultsCard = Card(20);
            TableLayoutPanel resultsLayout = Table(1, 4); resultsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            resultsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); resultsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            resultsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 51)); resultsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
            TableLayoutPanel resultHeader = Table(3, 1); resultHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60)); resultHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 219));
            count = LabelOf("Совпадения · 0", 15, true);
            filter = new TextBox { Font = new Font("Segoe UI", 9), AccessibleName = "Фильтр результатов" };
            Control filterShell = Field(filter); filterShell.Margin = new Padding(0, 4, 0, 8);
            resultHeader.Controls.Add(count, 0, 0); resultHeader.Controls.Add(LabelOf("Фильтр", 9, false), 1, 0); resultHeader.Controls.Add(filterShell, 2, 0);
            resultsLayout.Controls.Add(resultHeader, 0, 0);

            grid = new FinderGrid { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false, BackgroundColor = Color.FromArgb(248, 250, 255), BorderStyle = BorderStyle.None, AutoGenerateColumns = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false, Margin = Padding.Empty,
                AccessibleName = "Найденные файлы и события", RowTemplate = { Height = 36 }, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 237, 252); grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(84, 91, 128);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold); grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(7, 0, 0, 0);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 237, 252);
            grid.ColumnHeadersHeight = 36; grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9); grid.DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 255);
            grid.DefaultCellStyle.ForeColor = DesignAssets.Ink; grid.DefaultCellStyle.Padding = new Padding(7, 0, 0, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 253);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 223, 255); grid.DefaultCellStyle.SelectionForeColor = DesignAssets.Ink;
            grid.GridColor = Color.FromArgb(233, 235, 248);
            AddColumn("Тип", 95); AddColumn("Имя / источник", 150); AddColumn("Почему найдено", 165); AddColumn("Изменено", 105); AddColumn("Размер", 60); AddColumn("Расположение", 200);
            resultsLayout.Controls.Add(grid, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = new Padding(0, 8, 0, 0) };
            reveal = ButtonOf("Показать в папке", false); openEvent = ButtonOf("Журналы Windows", false); copy = ButtonOf("Копировать путь", false);
            Button summary = ButtonOf("Сводка поиска", false);
            actions.Controls.AddRange(new Control[] { reveal, openEvent, copy, summary }); resultsLayout.Controls.Add(actions, 0, 2);

            GlassPanel detailsCard = Card(13); detailsCard.Radius = 16; detailsCard.WhiteAlpha = 175;
            TableLayoutPanel detailLayout = Table(1, 2); detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Label detailsCaption = LabelOf("ПОДРОБНОСТИ  /  РУЧНАЯ ПРОВЕРКА", 8, true); detailsCaption.ForeColor = Color.FromArgb(109, 99, 161); detailLayout.Controls.Add(detailsCaption, 0, 0);
            detail = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(250, 251, 255), ForeColor = DesignAssets.Muted, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9),
                Text = "Выберите результат, чтобы увидеть полный путь и причину совпадения.\r\nУтилита открывает расположение для ручной проверки. Автоматического удаления нет.",
                Margin = new Padding(1, 0, 1, 0), AccessibleName = "Подробности результата" };
            detailLayout.Controls.Add(detail, 0, 1); detailsCard.Controls.Add(detailLayout); resultsLayout.Controls.Add(detailsCard, 0, 3);
            resultsCard.Controls.Add(resultsLayout); workspace.Controls.Add(resultsCard, 1, 0); page.Controls.Add(workspace, 0, 2);

            TableLayoutPanel footer = Table(2, 1); footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            status = LabelOf("Готово к поиску · файлы и журналы не изменяются", 9, false); status.AutoSize = false; status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleLeft;
            status.ForeColor = DesignAssets.Muted; status.AutoEllipsis = true;
            bar = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, Visible = false, Margin = new Padding(0, 11, 0, 10) };
            footer.Controls.Add(status, 0, 0); footer.Controls.Add(bar, 1, 0); page.Controls.Add(footer, 0, 3);

            search.Click += async delegate { await StartSearch(); };
            stop.Click += delegate { if (cancellation != null) { cancellation.Cancel(); stop.Enabled = false; status.Text = "Останавливаю поиск…"; } };
            pickExe.Click += delegate { PickExe(); };
            pickFolder.Click += delegate { using (FolderBrowserDialog dlg = new FolderBrowserDialog { Description = "Выберите дополнительную область поиска", ShowNewFolderButton = false }) { if (dlg.ShowDialog(this) == DialogResult.OK) folder.Text = dlg.SelectedPath; } };
            filter.TextChanged += delegate { RefreshRows(); };
            grid.SelectionChanged += delegate { ShowSelection(); };
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0 && Selected() != null && !Selected().IsEvent) RevealSelected(); };
            reveal.Click += delegate { RevealSelected(); };
            copy.Click += delegate { Finding f = Selected(); if (f != null) TryAction(delegate { Clipboard.SetText(f.Location); }); };
            openEvent.Click += delegate { TryAction(delegate { Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "eventvwr.msc")) { UseShellExecute = true }); }); };
            summary.Click += delegate { ShowSummary(); };
            terms.KeyDown += async delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && !busy) { e.SuppressKeyPress = true; await StartSearch(); } };
            UpdateActions();
        }
    }
}
