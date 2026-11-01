using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FindUrLogs
{
    internal static class DesignAssets
    {
        internal static Color Ink { get { return Theme.Current.Text; } }
        internal static Color Muted { get { return Theme.Current.Muted; } }

        internal static GraphicsPath Round(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0) return path;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }

        internal static void PaintMark(Graphics g, RectangleF rect)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath p = Round(rect, rect.Width * .28f))
            using (LinearGradientBrush b = new LinearGradientBrush(rect, Theme.Current.Accent, Theme.Current.AccentEnd, 50))
            { g.FillPath(b, p); using (Pen border = new Pen(Color.FromArgb(190, Color.White), 1.4f)) g.DrawPath(border, p); }
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(rect.X + rect.Width * .25f, rect.Y + rect.Height * .12f, rect.Width * .95f, rect.Height * .7f);
                using (PathGradientBrush glow = new PathGradientBrush(p))
                { glow.CenterColor = Color.FromArgb(130, 162, 255, 227); glow.SurroundColors = new[] { Color.FromArgb(0, 162, 255, 227) }; g.FillPath(glow, p); }
            }
            float x = rect.X, y = rect.Y, w = rect.Width;
            using (Pen pen = new Pen(Theme.Current.AccentText, w * .065f))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, x + w * .24f, y + w * .22f, w * .37f, w * .37f);
                g.DrawLine(pen, x + w * .57f, y + w * .56f, x + w * .76f, y + w * .75f);
            }
            using (Pen shine = new Pen(Color.FromArgb(150, Color.White), w * .02f))
                g.DrawArc(shine, x + w * .09f, y + w * .07f, w * .79f, w * .46f, 210, 100);
        }

        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
        internal static Icon MakeIcon()
        {
            using (Bitmap bitmap = new Bitmap(128, 128))
            {
                using (Graphics g = Graphics.FromImage(bitmap)) { g.Clear(Color.Transparent); PaintMark(g, new RectangleF(3, 3, 122, 122)); }
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }

    internal sealed class AuroraCanvas : Panel
    {
        private const int CacheWidth = 960;
        private const int CacheHeight = 600;
        private Bitmap backdrop;
        internal AuroraCanvas()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnSizeChanged(EventArgs e)
        { base.OnSizeChanged(e); Invalidate(); }
        internal void ClearBackdrop() { if (backdrop != null) { backdrop.Dispose(); backdrop = null; } Invalidate(); }
        private void Glow(Graphics g, float cx, float cy, float radius, Color color)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
                using (PathGradientBrush b = new PathGradientBrush(p))
                { b.CenterColor = color; b.SurroundColors = new[] { Color.FromArgb(0, color) }; g.FillPath(b, p); }
            }
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            if (backdrop == null)
            {
                // Gradients are deliberately soft. Render once per theme, then upscale
                // the small opaque texture instead of rebuilding a screen-sized image
                // on every WM_SIZE (and every transparent child background repaint).
                backdrop = new Bitmap(CacheWidth, CacheHeight, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(backdrop))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    ThemePalette t = Theme.Current;
                    Rectangle cacheRect = new Rectangle(0, 0, CacheWidth, CacheHeight);
                    using (LinearGradientBrush b = new LinearGradientBrush(cacheRect, t.CanvasColor(t.Top), t.CanvasColor(t.Bottom), 35)) g.FillRectangle(b, cacheRect);
                    int alpha = t.Dark ? 180 : 210;
                    Glow(g, CacheWidth * .06f, CacheHeight * .12f, CacheWidth * .51f, Color.FromArgb(alpha, t.CanvasColor(t.GlowA)));
                    Glow(g, CacheWidth * .78f, CacheHeight * .02f, CacheWidth * .49f, Color.FromArgb(alpha, t.CanvasColor(t.GlowB)));
                    Glow(g, CacheWidth * .97f, CacheHeight * .75f, CacheWidth * .39f, Color.FromArgb(alpha, t.CanvasColor(t.GlowC)));
                    Glow(g, CacheWidth * .10f, CacheHeight * .98f, CacheWidth * .47f, Color.FromArgb(alpha, t.CanvasColor(t.GlowD)));
                }
            }
            GraphicsState saved = e.Graphics.Save();
            try
            {
                // Only this already-soft backdrop is upscaled. Fonts, controls and
                // borders keep their normal vector/native rendering and DPI sizes.
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                e.Graphics.CompositingMode = CompositingMode.SourceOver;
                e.Graphics.DrawImage(backdrop, ClientRectangle, 0, 0, CacheWidth, CacheHeight, GraphicsUnit.Pixel);
            }
            finally { e.Graphics.Restore(saved); }
        }
        protected override void Dispose(bool disposing) { if (disposing && backdrop != null) backdrop.Dispose(); base.Dispose(disposing); }
    }

    internal class GlassPanel : Panel
    {
        internal int Radius = 24;
        internal int WhiteAlpha = 154;
        internal GlassPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width < 12 || Height < 12) return;
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            ThemePalette t = Theme.Current;
            RectangleF r = new RectangleF(3, 2, Width - 7, Height - 7);
            using (GraphicsPath shadow = DesignAssets.Round(new RectangleF(3, 5, Width - 7, Height - 7), Radius))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(13, 83, 74, 146))) g.FillPath(b, shadow);
            using (GraphicsPath path = DesignAssets.Round(r, Radius))
            {
                Color top = t.Dark ? Color.FromArgb(236, t.Surface) : Color.FromArgb(Math.Min(240, WhiteAlpha + 45), Color.White);
                Color bottom = t.Dark ? Color.FromArgb(216, t.Surface) : Color.FromArgb(WhiteAlpha, 249, 250, 255);
                using (LinearGradientBrush fill = new LinearGradientBrush(r, top, bottom, 110)) g.FillPath(fill, path);
                using (Pen line = new Pen(t.Dark ? Color.FromArgb(165, t.Border) : Color.FromArgb(220, Color.White), 1.4f)) g.DrawPath(line, path);
            }
        }
    }

    internal sealed class GlassButton : Button
    {
        internal bool Primary;
        private bool hover, pressed;
        internal GlassButton(bool primary)
        {
            Primary = primary;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(245, 247, 255); FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.Opaque, true);
            UseVisualStyleBackColor = false; Cursor = Cursors.Hand;
        }
        public override Size GetPreferredSize(Size proposedSize)
        { return new Size(Math.Max(80, TextRenderer.MeasureText(Text, Font).Width + 34), 40); }
        protected override void OnPaintBackground(PaintEventArgs e)
        { using (SolidBrush b = new SolidBrush(Theme.Current.Surface)) e.Graphics.FillRectangle(b, ClientRectangle); }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width > 3 && Height > 3) using (GraphicsPath p = DesignAssets.Round(new RectangleF(0, 0, Width, Height), 13))
            { Region old = Region; Region = new Region(p); if (old != null) old.Dispose(); }
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            ThemePalette t = Theme.Current;
            using (SolidBrush ground = new SolidBrush(t.Surface)) g.FillRectangle(ground, ClientRectangle);
            Rectangle r = new Rectangle(1, pressed ? 3 : 1, Width - 3, Height - 4);
            if (r.Width <= 0 || r.Height <= 0) return;
            Color a = Primary ? t.Accent : ThemePalette.Mix(t.Surface, t.Dark ? t.Border : Color.White, .45);
            Color b = Primary ? t.AccentEnd : t.Surface;
            if (hover && Enabled)
            {
                Color target = Primary ? (t.AccentText == Color.White ? Color.Black : Color.White) : t.Accent;
                a = ThemePalette.Mix(a, target, Primary ? .05 : .09); b = ThemePalette.Mix(b, target, Primary ? .05 : .08);
            }
            if (!Enabled) { a = t.Header; b = t.Surface; }
            using (GraphicsPath p = DesignAssets.Round(r, 13))
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(r, a, b, 70)) g.FillPath(brush, p);
                using (Pen border = new Pen(t.Dark ? t.Border : Color.FromArgb(225, Color.White), 1.2f)) g.DrawPath(border, p);
            }
            Color text = !Enabled ? t.Disabled : Primary ? t.AccentText : t.Text;
            using (SolidBrush textBrush = new SolidBrush(text))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(Text, Font, textBrush, r, format);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -5, -5), text, Color.Transparent);
        }
    }

    internal sealed class GlassCheck : CheckBox
    {
        internal Color Accent = Color.FromArgb(105, 102, 226);
        private bool hover;
        internal GlassCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(245, 247, 255); Cursor = Cursors.Hand; AutoSize = true;
            SetStyle(ControlStyles.Opaque, true);
        }
        public override Size GetPreferredSize(Size proposedSize)
        { return new Size(TextRenderer.MeasureText(Text, Font).Width + 44, 33); }
        protected override void OnPaintBackground(PaintEventArgs e)
        { using (SolidBrush b = new SolidBrush(Theme.Current.Surface)) e.Graphics.FillRectangle(b, ClientRectangle); }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width > 3 && Height > 3) using (GraphicsPath p = DesignAssets.Round(new RectangleF(0, 0, Width, Height), 12))
            { Region old = Region; Region = new Region(p); if (old != null) old.Dispose(); }
        }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            ThemePalette t = Theme.Current;
            using (SolidBrush ground = new SolidBrush(t.Surface)) g.FillRectangle(ground, ClientRectangle);
            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            if (r.Width < 4 || r.Height < 4) return;
            using (GraphicsPath p = DesignAssets.Round(r, 12))
            {
                using (SolidBrush b = new SolidBrush(Checked ? ThemePalette.Mix(t.Surface, Accent, hover ? .25 : .15) : ThemePalette.Mix(t.Surface, t.Input, .6))) g.FillPath(b, p);
                using (Pen line = new Pen(Checked ? ThemePalette.Mix(t.Surface, Accent, .5) : t.Border)) g.DrawPath(line, p);
            }
            Rectangle circle = new Rectangle(11, (Height - 17) / 2, 17, 17);
            using (SolidBrush b = new SolidBrush(Checked && Enabled ? Accent : ThemePalette.Mix(t.Surface, t.Muted, .32))) g.FillEllipse(b, circle);
            if (Checked)
            {
                using (Pen tick = new Pen(ThemePalette.BestText(Accent, Accent), 1.8f)) { tick.StartCap = LineCap.Round; tick.EndCap = LineCap.Round; g.DrawLines(tick, new[] { new Point(circle.X + 4, circle.Y + 8), new Point(circle.X + 7, circle.Y + 11), new Point(circle.X + 12, circle.Y + 5) }); }
            }
            using (SolidBrush textBrush = new SolidBrush(Enabled ? DesignAssets.Ink : DesignAssets.Muted))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(Text, Font, textBrush, new Rectangle(35, 0, Width - 40, Height), format);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(r, -4, -4));
        }
    }

    internal sealed class BrandMark : Control
    {
        internal BrandMark() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); BackColor = Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e) { DesignAssets.PaintMark(e.Graphics, new RectangleF(1, 1, Width - 3, Height - 3)); }
    }

    internal sealed class FinderGrid : DataGridView
    {
        internal string EmptyTitle = "Здесь появятся совпадения";
        internal string EmptyDescription = "Введите название приложения и начните поиск.\nКаждый результат можно будет проверить вручную.";
        internal FinderGrid() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Rows.Count != 0 || Height < 170) return;
            int y = ColumnHeadersHeight + Math.Max(16, (Height - ColumnHeadersHeight - 145) / 2);
            Rectangle mark = new Rectangle((Width - 52) / 2, y, 52, 52);
            DesignAssets.PaintMark(e.Graphics, mark);
            using (Font f = new Font("Segoe UI", 14, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, EmptyTitle, f, new Rectangle(10, y + 67, Width - 20, 28), DesignAssets.Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
            using (Font f = new Font("Segoe UI", 9))
                TextRenderer.DrawText(e.Graphics, EmptyDescription, f, new Rectangle(12, y + 103, Width - 24, 49), DesignAssets.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
        }
    }
}
