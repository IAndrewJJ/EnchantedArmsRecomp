using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EnchantedArmsPC
{
    static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(9, 18, 31);
        internal static readonly Color Panel = Color.FromArgb(16, 31, 48);
        internal static readonly Color Text = Color.FromArgb(231, 239, 240);
        internal static readonly Color Muted = Color.FromArgb(139, 165, 181);
        internal static readonly Color Accent = Color.FromArgb(121, 220, 223);
        internal static readonly Color Gold = Color.FromArgb(196, 174, 126);
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value,
                                                int size);
        internal static void Window(Form form)
        {
            var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon != null)
            {
                form.Icon = icon;
                form.Disposed += (s, e) => icon.Dispose();
            }
            form.Font = new Font("Segoe UI", 10);
            form.BackColor = Background;
            form.ForeColor = Text;
            form.StartPosition = FormStartPosition.CenterParent;
            form.HandleCreated += (s, e) =>
            {
                try
                {
                    int dark = 1;
                    if (DwmSetWindowAttribute(form.Handle, 20, ref dark, 4) != 0)
                        DwmSetWindowAttribute(form.Handle, 19, ref dark, 4);
                }
                catch (DllNotFoundException)
                {
                }
            };
        }
        internal static Label Label(string text, int width)
        {
            return new Label { Text = text,
                               ForeColor = Muted,
                               BackColor = Color.Transparent,
                               AutoSize = true,
                               MaximumSize = new Size(width, 0),
                               Margin = new Padding(0, 8, 0, 8) };
        }
        internal static EtherButton Button(string text, int width, bool primary = false)
        {
            return new EtherButton { Text = text, Width = width, Height = 42, Primary = primary,
                                     Margin = new Padding(0, 5, 10, 5) };
        }
        internal static ComboBox Choice(int width, params string[] values)
        {
            var choice = new EtherCombo { DropDownStyle = ComboBoxStyle.DropDownList,
                                          Width = width,
                                          ItemHeight = 25,
                                          DrawMode = DrawMode.OwnerDrawFixed,
                                          BackColor = Panel,
                                          ForeColor = Text,
                                          Margin = new Padding(0, 4, 0, 4) };
            choice.Items.AddRange(values);
            choice.DrawItem += (s, e) =>
            {
                if (e.Index < 0)
                    return;
                using (var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0
                                                     ? Color.FromArgb(33, 78, 96)
                                                     : Panel))
                    e.Graphics.FillRectangle(fill, e.Bounds);
                TextRenderer.DrawText(
                    e.Graphics, choice.Items[e.Index].ToString(), choice.Font,
                    new Rectangle(e.Bounds.X + 7, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height),
                    Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            };
            return choice;
        }
        internal static void Tracked(Graphics g, string text, Font font, Brush brush, float x,
                                     float y, float tracking)
        {
            foreach (char letter in text)
            {
                string part = letter.ToString();
                g.DrawString(part, font, brush, x, y, StringFormat.GenericTypographic);
                x += g.MeasureString(part, font, PointF.Empty, StringFormat.GenericTypographic)
                         .Width +
                     tracking;
            }
        }
    }

    sealed class EtherButton : Button
    {
        public bool Primary;
        public bool Selected;
        bool hover;
        public EtherButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer,
                     true);
            MouseEnter += (s, e) =>
            {
                hover = true;
                Invalidate();
            };
            MouseLeave += (s, e) =>
            {
                hover = false;
                Invalidate();
            };
            GotFocus += (s, e) => Invalidate();
            LostFocus += (s, e) => Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Background);
            Color top = Primary    ? Color.FromArgb(53, 130, 144)
                        : Selected ? Color.FromArgb(27, 61, 77)
                                   : Theme.Panel;
            Color bottom = Primary ? Color.FromArgb(23, 75, 98) : Color.FromArgb(12, 25, 41);
            if (hover && Enabled)
                top = Primary ? Color.FromArgb(70, 157, 166) : Color.FromArgb(32, 65, 82);
            if (!Enabled)
            {
                top = Color.FromArgb(23, 38, 49);
                bottom = Theme.Background;
            }
            var bounds = new Rectangle(1, 1, Width - 3, Height - 3);
            using (var path = new GraphicsPath())
            {
                float cut = Primary ? 10 : 6;
                path.AddPolygon(new[] { new PointF(bounds.Left + cut, bounds.Top),
                                        new PointF(bounds.Right, bounds.Top),
                                        new PointF(bounds.Right, bounds.Bottom - cut),
                                        new PointF(bounds.Right - cut, bounds.Bottom),
                                        new PointF(bounds.Left, bounds.Bottom),
                                        new PointF(bounds.Left, bounds.Top + cut) });
                using (var fill = new LinearGradientBrush(bounds, top, bottom, 90))
                    g.FillPath(fill, path);
                using (var stroke =
                           new Pen(!Enabled                         ? Color.FromArgb(46, 65, 77)
                                   : Primary || Selected || Focused ? Theme.Accent
                                                                    : Color.FromArgb(54, 83, 100),
                                   Focused ? 2 : 1)) g.DrawPath(stroke, path);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(10, 0, Width - 20, Height),
                                  Enabled ? Theme.Text : Theme.Muted,
                                  TextFormatFlags.HorizontalCenter |
                                      TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    sealed class EtherCombo : ComboBox
    {
        public EtherCombo()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer,
                     true);
        }
        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }
        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }
        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Panel);
            using (var border = new Pen(Focused ? Theme.Accent : Color.FromArgb(57, 84, 102)))
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, SelectedItem == null ? "" : SelectedItem.ToString(),
                                  Font, new Rectangle(9, 0, Width - 37, Height), Theme.Text,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                      TextFormatFlags.EndEllipsis);
            using (var arrow = new Pen(Theme.Accent, 1.4f))
            {
                int x = Width - 20, y = Height / 2;
                e.Graphics.DrawLines(arrow, new[] { new Point(x - 4, y - 2), new Point(x, y + 2),
                                                    new Point(x + 4, y - 2) });
            }
        }
    }

    // Original vector artwork: no game textures, logos or downloaded art.
    sealed class EtherBackdrop : Panel
    {
        Bitmap artwork;
        public EtherBackdrop()
        {
            DoubleBuffered = true;
            BackColor = Theme.Background;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width < 1 || Height < 1)
                return;
            if (artwork == null || artwork.Size != ClientSize)
            {
                if (artwork != null)
                    artwork.Dispose();
                artwork = new Bitmap(Width, Height);
                using (var g = Graphics.FromImage(artwork)) DrawArtwork(g);
            }
            e.Graphics.DrawImageUnscaled(artwork, 0, 0);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && artwork != null)
            {
                artwork.Dispose();
                artwork = null;
            }
            base.Dispose(disposing);
        }
        void DrawArtwork(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var old = g.Save();
            g.ScaleTransform(Width / 1000f, Height / 620f);
            using (var bg = new LinearGradientBrush(
                       new Rectangle(0, 0, 1000, 620), Color.FromArgb(7, 16, 29),
                       Color.FromArgb(17, 43, 63), 24)) g.FillRectangle(bg, 0, 0, 1000, 620);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(440, -50, 570, 640);
                using (var glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = Color.FromArgb(58, 43, 140, 160);
                    glow.SurroundColors = new[] { Color.FromArgb(0, 10, 30, 46) };
                    g.FillPath(glow, path);
                }
            }
            using (var grid = new Pen(Color.FromArgb(13, 130, 204, 216))) for (int i = -620;
                                                                               i < 1400; i += 44)
                g.DrawLine(grid, i, 620, i + 620, 0);
            g.TranslateTransform(752, 293);
            using (var ink = new Pen(Color.FromArgb(45, 142, 218, 219), 1))
            {
                foreach (int r in new[] { 133, 151, 184, 212, 225 })
                    g.DrawEllipse(ink, -r, -r, r * 2, r * 2);
                for (int i = 0; i < 96; i++)
                {
                    double a = i * Math.PI / 48;
                    float r = i % 8 == 0 ? 199 : 207;
                    g.DrawLine(ink, (float)Math.Cos(a) * r, (float)Math.Sin(a) * r,
                               (float)Math.Cos(a) * 218, (float)Math.Sin(a) * 218);
                }
            }
            using (var gold = new Pen(Color.FromArgb(128, 196, 174, 126), 2))
            {
                g.DrawArc(gold, -184, -184, 368, 368, 215, 85);
                g.DrawArc(gold, -184, -184, 368, 368, 35, 40);
            }
            for (int i = 0; i < 8; i++)
            {
                var saved = g.Save();
                g.RotateTransform(i * 45 + 22.5f);
                using (var pen = new Pen(Color.FromArgb(75, Theme.Accent), 1))
                    g.DrawPolygon(pen, new[] { new PointF(0, -166), new PointF(7, -153),
                                               new PointF(0, -140), new PointF(-7, -153) });
                g.Restore(saved);
            }
            PointF top = new PointF(0, -132), left = new PointF(-70, -9),
                   right = new PointF(61, 12), bottom = new PointF(-6, 130),
                   center = new PointF(-5, -7);
            using (var facet = new LinearGradientBrush(new Rectangle(-80, -140, 160, 280),
                                                       Color.FromArgb(160, 226, 233),
                                                       Color.FromArgb(25, 80, 117), 18))
                g.FillPolygon(facet, new[] { top, left, bottom, right });
            using (var light = new SolidBrush(Color.FromArgb(192, 190, 241, 241)))
                g.FillPolygon(light, new[] { top, left, center });
            using (var dark = new SolidBrush(Color.FromArgb(170, 15, 61, 96)))
                g.FillPolygon(dark, new[] { center, left, bottom });
            using (var light = new SolidBrush(Color.FromArgb(90, 216, 255, 251)))
                g.FillPolygon(light, new[] { center, right, bottom });
            using (var edge = new Pen(Color.FromArgb(190, 186, 246, 243), 1))
            {
                g.DrawPolygon(edge, new[] { top, left, bottom, right });
                g.DrawLine(edge, top, center);
                g.DrawLine(edge, center, bottom);
                g.DrawLine(edge, left, right);
            }
            g.ResetTransform();
            g.ScaleTransform(Width / 1000f, Height / 620f);
            using (var fade = new LinearGradientBrush(
                       new Rectangle(0, 0, 1000, 620), Color.FromArgb(245, 8, 18, 31),
                       Color.FromArgb(0, 8, 18, 31), 0f)) g.FillRectangle(fade, 0, 0, 1000, 620);
            using (var rim = new Pen(Color.FromArgb(44, Theme.Gold)))
            {
                g.DrawLine(rim, 40, 32, 960, 32);
                g.DrawLine(rim, 40, 570, 960, 570);
            }
            using (var silver = new LinearGradientBrush(new Rectangle(50, 100, 440, 190),
                                                        Color.FromArgb(246, 245, 226),
                                                        Color.FromArgb(133, 190, 206), 90))
            {
                using (var enchanted =
                           new Font("Palatino Linotype", 34, FontStyle.Regular, GraphicsUnit.Pixel))
                    Theme.Tracked(g, "ENCHANTED", enchanted, silver, 52, 113, 6);
                using (var arms =
                           new Font("Palatino Linotype", 98, FontStyle.Regular, GraphicsUnit.Pixel))
                    Theme.Tracked(g, "ARMS", arms, silver, 47, 147, 12);
            }
            using (var gold = new SolidBrush(Theme.Gold)) using (
                var edition = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                Theme.Tracked(g, "P C   E D I T I O N", edition, gold, 58, 267, 2);
            using (var dot = new SolidBrush(Color.FromArgb(114, Theme.Accent)))
            {
                var random = new Random(5553);
                for (int i = 0; i < 35; i++)
                {
                    float x = random.Next(540, 960), y = random.Next(55, 545);
                    g.FillEllipse(dot, x, y, i % 3 == 0 ? 2 : 1, i % 3 == 0 ? 2 : 1);
                }
            }
            g.Restore(old);
        }
    }
}
