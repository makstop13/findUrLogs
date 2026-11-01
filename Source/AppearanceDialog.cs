using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace FindUrLogs
{
    internal sealed class PresetTile : Button
    {
        internal ThemePalette Palette;
        internal bool Selected;
        internal PresetTile(ThemePalette palette)
        {
            Palette = palette;
            Text = palette.Name + (palette.Dark ? " · тёмная" : " · светлая");
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            Cursor = Cursors.Hand; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.Opaque, true);
            Dock = DockStyle.Fill; Margin = new Padding(4);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width > 4 && Height > 4) using (GraphicsPath p = DesignAssets.Round(new RectangleF(0, 0, Width, Height), 14))
            { Region old = Region; Region = new Region(p); if (old != null) old.Dispose(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush bg = new SolidBrush(Theme.Current.Surface)) g.FillRectangle(bg, ClientRectangle);
            Rectangle r = new Rectangle(2, 2, Width - 5, Height - 5);
            using (GraphicsPath p = DesignAssets.Round(r, 14))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Palette.CanvasColor(Palette.GlowA), Palette.CanvasColor(Palette.GlowB), 20)) g.FillPath(b, p);
                using (SolidBrush blob = new SolidBrush(Color.FromArgb(95, Palette.CanvasColor(Palette.GlowC)))) g.FillEllipse(blob, Width - 100, -20, 140, 105);
                using (SolidBrush bottom = new SolidBrush(Palette.Surface)) g.FillRectangle(bottom, r.Left, Height - 38, r.Width, 35);
                using (Pen line = new Pen(Selected ? Theme.Current.Accent : Theme.Current.Border, Selected ? 2.6f : 1)) g.DrawPath(line, p);
            }
            using (SolidBrush card = new SolidBrush(Color.FromArgb(Palette.Dark ? 175 : 195, Palette.Surface)))
            using (GraphicsPath p = DesignAssets.Round(new Rectangle(20, 16, Width - 43, 35), 9)) g.FillPath(card, p);
            using (SolidBrush dot = new SolidBrush(Palette.Accent)) g.FillEllipse(dot, 29, 25, 15, 15);
            using (Pen line = new Pen(Palette.Muted, 2)) { g.DrawLine(line, 53, 29, Width - 43, 29); g.DrawLine(line, 53, 38, Width - 71, 38); }
            using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
            using (SolidBrush ink = new SolidBrush(Palette.Text))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(Text, font, ink, new Rectangle(14, Height - 38, Width - 28, 31), sf);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -6, -6));
        }
    }

    internal sealed class PaletteChip : Button
    {
        internal string Caption;
        internal Color Swatch;
        internal PaletteChip(string caption)
        {
            Caption = caption;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.Opaque, true);
            Dock = DockStyle.Fill; Margin = new Padding(4);
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width > 4 && Height > 4) using (GraphicsPath p = DesignAssets.Round(new RectangleF(0, 0, Width, Height), 11))
            { Region old = Region; Region = new Region(p); if (old != null) old.Dispose(); }
        }
        internal void SetColor(Color color) { Swatch = color; Text = Caption + " " + ThemePalette.Hex(color); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; ThemePalette t = Theme.Current;
            using (SolidBrush b = new SolidBrush(t.Surface)) g.FillRectangle(b, ClientRectangle);
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath p = DesignAssets.Round(r, 11))
            { using (SolidBrush b = new SolidBrush(t.Input)) g.FillPath(b, p); using (Pen pen = new Pen(t.Border)) g.DrawPath(pen, p); }
            using (SolidBrush text = new SolidBrush(t.Muted)) g.DrawString(Caption, Font, text, new PointF(11, 7));
            using (SolidBrush b = new SolidBrush(Swatch)) g.FillEllipse(b, 12, 33, 21, 21);
            using (Pen pen = new Pen(t.Border)) g.DrawEllipse(pen, 12, 33, 21, 21);
            using (SolidBrush text = new SolidBrush(t.Text)) g.DrawString(ThemePalette.Hex(Swatch), Font, text, new PointF(41, 34));
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -4, -4));
        }
    }

    internal sealed class AppearanceDialog : Form
    {
        private readonly Form owner;
        private readonly ThemePalette original;
        private ThemePalette working;
        private readonly List<PresetTile> presets = new List<PresetTile>();
        private readonly List<PaletteChip> colors = new List<PaletteChip>();
        private ComboBox mode;
        private Label current;
        private bool syncing, committed;

        internal AppearanceDialog(Form owner)
        {
            this.owner = owner; original = Theme.Current.Clone(); working = original.Clone();
            Text = "Оформление — findUrLogs"; Font = new Font("Segoe UI", 9); ClientSize = new Size(756, 730);
            MinimumSize = new Size(735, 755); MaximumSize = new Size(1000, 900);
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi; Icon = DesignAssets.MakeIcon();
            Build(); RefreshPreview();
            Shown += delegate { Theme.Apply(this); };
            FormClosing += delegate {
                if (!committed) { Theme.Current = original.Clone(); Theme.Apply(owner); }
            };
        }
        private Label Caption(string text, float size, bool bold)
        {
            return new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
                BackColor = Color.Transparent, ForeColor = Theme.Current.Text, Anchor = AnchorStyles.Left, Margin = new Padding(4, 0, 0, 0) };
        }
        private GlassButton Action(string text, bool primary)
        { return new GlassButton(primary) { Text = text, AutoSize = true, Font = Font, Margin = new Padding(6, 0, 0, 0), MinimumSize = new Size(105, 40) }; }
        private void Build()
        {
            AuroraCanvas canvas = new AuroraCanvas { Dock = DockStyle.Fill }; Controls.Add(canvas);
            GlassPanel card = new GlassPanel { Dock = DockStyle.Fill, Padding = new Padding(20), Margin = Padding.Empty };
            TableLayoutPanel outside = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Padding = new Padding(14), BackColor = Color.Transparent };
            outside.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); outside.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outside.Controls.Add(card); canvas.Controls.Add(outside);
            TableLayoutPanel page = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, BackColor = Color.Transparent, Margin = Padding.Empty };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int h in new[] { 65, 25, 220, 36, 148, 45, 24 }) page.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); page.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); card.Controls.Add(page);
            Panel title = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            Label heading = Caption("Ваше оформление", 22, true); heading.Anchor = AnchorStyles.Top | AnchorStyles.Left; heading.Location = new Point(0, 0);
            Label sub = Caption("Выберите готовую тему или соберите свою палитру.", 9, false); sub.Anchor = AnchorStyles.Top | AnchorStyles.Left; sub.Location = new Point(2, 43);
            title.Controls.Add(heading); title.Controls.Add(sub); page.Controls.Add(title, 0, 0);
            page.Controls.Add(Caption("ГОТОВЫЕ ТЕМЫ  /  3 СВЕТЛЫЕ + 3 ТЁМНЫЕ", 8, true), 0, 1);
            TableLayoutPanel tiles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
            for (int i = 0; i < 3; i++) tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            for (int i = 0; i < 2; i++) tiles.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            int tileIndex = 0;
            foreach (ThemePalette palette in ThemePalette.Presets())
            {
                PresetTile tile = new PresetTile(palette); presets.Add(tile);
                tile.Click += delegate { working = tile.Palette.Clone(); RefreshPreview(); };
                tiles.Controls.Add(tile, tileIndex % 3, tileIndex / 3); tileIndex++;
            }
            page.Controls.Add(tiles, 0, 2);
            page.Controls.Add(Caption("СВОЯ ПАЛИТРА  /  НАЖМИТЕ НА ЦВЕТ", 8, true), 0, 3);
            TableLayoutPanel paletteGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
            for (int i = 0; i < 4; i++) paletteGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            for (int i = 0; i < 2; i++) paletteGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            string[] captions = { "Фон сверху", "Фон снизу", "Свечение 1", "Свечение 2", "Свечение 3", "Свечение 4", "Акцент кнопок" };
            for (int i = 0; i < captions.Length; i++)
            {
                int index = i; PaletteChip chip = new PaletteChip(captions[i]); colors.Add(chip);
                chip.Click += delegate { PickColor(index); };
                paletteGrid.Controls.Add(chip, i % 4, i / 4);
            }
            TableLayoutPanel modeBox = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(6) };
            modeBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 27)); modeBox.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            modeBox.Controls.Add(Caption("Панели и текст", 9, false), 0, 0);
            mode = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, AccessibleName = "Светлый или тёмный режим" };
            mode.Items.AddRange(new object[] { "Светлые", "Тёмные" });
            mode.SelectedIndexChanged += delegate { if (!syncing) { working.Dark = mode.SelectedIndex == 1; working.Name = "Своя палитра"; RefreshPreview(); } };
            modeBox.Controls.Add(mode, 0, 1); paletteGrid.Controls.Add(modeBox, 3, 1); page.Controls.Add(paletteGrid, 0, 4);
            Label note = Caption("Яркость фона подстраивается под режим для читаемости текста.\nПросмотр сразу в окне; сохранение — только кнопкой «Применить».", 9, false); page.Controls.Add(note, 0, 5);
            current = Caption("", 9, true); page.Controls.Add(current, 0, 6);
            TableLayoutPanel footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 259));
            GlassButton reset = Action("Исходная тема", false); reset.Anchor = AnchorStyles.Left; reset.Margin = new Padding(0);
            reset.Click += delegate { working = ThemePalette.Presets()[0]; RefreshPreview(); }; footer.Controls.Add(reset, 0, 0);
            FlowLayoutPanel right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Color.Transparent, Margin = Padding.Empty };
            GlassButton apply = Action("Применить", true); GlassButton cancel = Action("Отмена", false);
            apply.Click += delegate { Save(); }; cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            right.Controls.Add(apply); right.Controls.Add(cancel); footer.Controls.Add(right, 1, 0); page.Controls.Add(footer, 0, 8);
            AcceptButton = apply; CancelButton = cancel;
        }
        private Color[] PaletteColors() { return new[] { working.Top, working.Bottom, working.GlowA, working.GlowB, working.GlowC, working.GlowD, working.Accent }; }
        private void PickColor(int index)
        {
            Color[] values = PaletteColors();
            using (ColorDialog dialog = new ColorDialog { FullOpen = true, Color = values[index] })
            {
                dialog.CustomColors = Array.ConvertAll(values, x => ColorTranslator.ToOle(x));
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Color color = dialog.Color;
                switch (index)
                {
                    case 0: working.Top = color; break; case 1: working.Bottom = color; break;
                    case 2: working.GlowA = color; break; case 3: working.GlowB = color; break;
                    case 4: working.GlowC = color; break; case 5: working.GlowD = color; break; case 6: working.Accent = color; break;
                }
                working.Name = "Своя палитра"; RefreshPreview();
            }
        }
        private void RefreshPreview()
        {
            syncing = true;
            Theme.Current = working.Clone();
            mode.SelectedIndex = working.Dark ? 1 : 0;
            Color[] values = PaletteColors();
            for (int i = 0; i < colors.Count; i++) colors[i].SetColor(values[i]);
            foreach (PresetTile tile in presets) { tile.Selected = tile.Palette.Name == working.Name; tile.Invalidate(); }
            current.Text = "Сейчас: " + working.Name + (working.Dark ? " · тёмная" : " · светлая");
            Theme.Apply(owner); Theme.Apply(this); syncing = false;
        }
        private void Save()
        {
            try { ThemeStore.Save(ThemeStore.DefaultPath, working); }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is System.Security.SecurityException)) throw;
                MessageBox.Show(this, "Не удалось сохранить оформление рядом с программой. Разместите утилиту в папке, куда у вас есть право записи.\r\n\r\n" + ex.Message,
                    "Оформление не сохранено", MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }
            committed = true; DialogResult = DialogResult.OK; Close();
        }
    }
}
