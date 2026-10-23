using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WebDeployer.Ui
{
    internal enum ButtonKind { Secondary, Primary, Dark, Link }

    /// <summary>
    /// Pulsante arrotondato disegnato come nel design. Resta un Button, quindi funzionano
    /// tastiera, Invio/Esc dei dialoghi e accessibilità.
    /// </summary>
    internal sealed class FlatButton : Button
    {
        private const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        private bool _hover;
        private bool _pressed;

        public FlatButton(string text, ButtonKind kind = ButtonKind.Secondary, IconKind icon = IconKind.None, int minHeight = 38)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = text;
            Kind = kind;
            Icon = icon;
            MinHeight = kind == ButtonKind.Dark ? 34 : kind == ButtonKind.Link ? 24 : minHeight;
            Font = kind == ButtonKind.Primary ? Theme.Semibold(14) : kind == ButtonKind.Dark ? Theme.Regular(13) : Theme.Regular(14);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, 8, 0);
            UseVisualStyleBackColor = false;
        }

        public ButtonKind Kind { get; }
        public IconKind Icon { get; }
        /// <summary>Altezza minima in px a 96 DPI (nel design 34–44).</summary>
        public int MinHeight { get; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var scale = Theme.Scale(this);
            var text = Text.Length == 0 ? Size.Empty : TextRenderer.MeasureText(Text, Font, Size.Empty, TextFlags);
            var icon = Icon == IconKind.None ? 0 : (int)(16 * scale);
            var gap = icon > 0 && text.Width > 0 ? (int)(8 * scale) : 0;
            var padding = Kind == ButtonKind.Link ? 0 : (int)((Kind == ButtonKind.Dark ? 12 : 16) * scale);
            var height = Math.Max((int)(MinHeight * scale), text.Height + (int)(12 * scale));
            var width = icon + gap + text.Width + padding * 2;
            return new Size(Text.Length == 0 ? Math.Max(width, height) : width, height); // solo icona: quadrato
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _hover = _pressed = false;
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var scale = Theme.Scale(this);
            GetColors(out var fill, out var border, out var fore);

            var frame = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = Theme.RoundedRect(frame, (Kind == ButtonKind.Dark ? 7 : 8) * scale))
            {
                if (fill != Color.Empty)
                    using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (border != Color.Empty)
                    using (var pen = new Pen(border)) g.DrawPath(pen, path);
            }

            // Icona + testo centrati (i link sono allineati a sinistra)
            var text = Text.Length == 0 ? Size.Empty : TextRenderer.MeasureText(g, Text, Font, Size.Empty, TextFlags);
            var icon = Icon == IconKind.None ? 0f : 16 * scale;
            var gap = icon > 0 && text.Width > 0 ? 8 * scale : 0f;
            var x = Kind == ButtonKind.Link ? 0f : (Width - (icon + gap + text.Width)) / 2f;
            if (icon > 0)
                Icons.Draw(g, Icon, new RectangleF(x, (Height - icon) / 2f, icon, icon), fore);
            if (text.Width > 0)
            {
                var origin = new Point((int)(x + icon + gap), (Height - text.Height) / 2);
                var font = Kind == ButtonKind.Link && (_hover || Focused && ShowFocusCues) ? new Font(Font, FontStyle.Underline) : Font;
                TextRenderer.DrawText(g, Text, font, origin, fore, TextFlags);
                if (font != Font) font.Dispose();
            }

            if (Focused && ShowFocusCues && Kind != ButtonKind.Link)
            {
                using (var path = Theme.RoundedRect(new RectangleF(2.5f, 2.5f, Width - 5f, Height - 5f), 6 * scale))
                using (var pen = new Pen(Kind == ButtonKind.Primary ? Color.White : Theme.Accent, 1.5f) { DashStyle = DashStyle.Dot })
                    g.DrawPath(pen, path);
            }
        }

        private void GetColors(out Color fill, out Color border, out Color fore)
        {
            switch (Kind)
            {
                case ButtonKind.Primary:
                    fill = !Enabled ? Theme.AccentDisabled : _pressed ? Theme.AccentPressed : _hover ? Theme.AccentHover : Theme.Accent;
                    border = Color.Empty;
                    fore = Color.White;
                    break;
                case ButtonKind.Dark:
                    fill = !Enabled ? Color.Empty : _hover ? Theme.DarkButtonHover : Theme.DarkButton;
                    border = Enabled ? Theme.DarkButtonBorder : Theme.LogBorder;
                    fore = Enabled ? Theme.LogTitle : Theme.DarkDisabledText;
                    break;
                case ButtonKind.Link:
                    fill = Color.Empty;
                    border = Color.Empty;
                    fore = !Enabled ? Theme.TextDisabled : _hover ? Theme.AccentPressed : Theme.Accent;
                    break;
                default:
                    fill = !Enabled ? Theme.SurfaceMuted : _pressed ? Theme.PressedFill : _hover ? Theme.HoverFill : Theme.Surface;
                    border = Enabled ? Theme.InputBorder : Theme.Border;
                    fore = Enabled ? Theme.Text : Theme.TextDisabled;
                    break;
            }
        }
    }

    /// <summary>
    /// Riquadro con angoli arrotondati e bordo (le "card" del design): una colonna di righe.
    /// È una TableLayoutPanel perché ricalcola bene l'altezza quando un testo va a capo.
    /// </summary>
    internal class Card : TableLayoutPanel
    {
        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            BorderColor = Theme.Border;
            Padding = new Padding(20, 18, 20, 18);
            ColumnCount = 1;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        }

        public Color BorderColor { get; set; }
        public int Radius { get; set; } = 12;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), Radius * Theme.Scale(this)))
            using (var fill = new SolidBrush(BackColor))
            {
                g.FillPath(fill, path);
                if (BorderColor != Color.Empty)
                    using (var pen = new Pen(BorderColor)) g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Pannello con una linea sottile sul bordo alto o basso (intestazione e piè di pagina).</summary>
    internal sealed class EdgePanel : Panel
    {
        public EdgePanel(DockStyle edge, Color line)
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            Edge = edge;
            Line = line;
        }

        public DockStyle Edge { get; }
        public Color Line { get; }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Line))
            {
                var y = Edge == DockStyle.Top ? 0 : Height - 1;
                e.Graphics.DrawLine(pen, 0, y, Width, y);
            }
        }
    }

    /// <summary>Campo di testo con bordo arrotondato; il TextBox vero e proprio è <see cref="Box"/>.</summary>
    internal sealed class InputBox : Panel
    {
        private const int EM_SETCUEBANNER = 0x1501;
        private string _placeholder;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public InputBox(bool readOnly = false, bool password = false)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                ReadOnly = readOnly,
                UseSystemPasswordChar = password,
                Font = Theme.Regular(14),
                BackColor = readOnly ? Theme.SurfaceMuted : Theme.Surface,
                ForeColor = readOnly ? Theme.TextMuted : Theme.Text,
            };
            BackColor = Box.BackColor;
            Controls.Add(Box);
            Box.GotFocus += (s, e) => Invalidate();
            Box.LostFocus += (s, e) => Invalidate();
            Box.HandleCreated += (s, e) => ApplyPlaceholder();
            Cursor = Cursors.IBeam;
            Margin = new Padding(0, 3, 0, 3);
            Height = 38;
        }

        public TextBox Box { get; }

        /// <summary>Testo grigio mostrato quando il campo è vuoto.</summary>
        public string Placeholder
        {
            get => _placeholder;
            set { _placeholder = value; ApplyPlaceholder(); }
        }

        private void ApplyPlaceholder()
        {
            if (Box.IsHandleCreated && _placeholder != null)
                SendMessage(Box.Handle, EM_SETCUEBANNER, (IntPtr)1, _placeholder);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var scale = Theme.Scale(this);
            return new Size((int)(160 * scale), Math.Max((int)(38 * scale), Box.PreferredHeight + (int)(12 * scale)));
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Box.Focus();
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var pad = (int)(12 * Theme.Scale(this));
            Box.SetBounds(pad, (Height - Box.Height) / 2, Math.Max(10, Width - pad * 2), Box.Height);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var border = Box.Focused ? Theme.Accent : Box.ReadOnly ? Theme.Border : Theme.InputBorder;
            using (var path = Theme.RoundedRect(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), 8 * Theme.Scale(this)))
            using (var fill = new SolidBrush(Box.BackColor))
            using (var pen = new Pen(border, Box.Focused ? 1.5f : 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Etichetta a pillola colorata (es. "Git 2.52.0").</summary>
    internal sealed class Pill : Label
    {
        public Pill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = Theme.Regular(12);
            Fill = Theme.SuccessSoft;
            ForeColor = Theme.SuccessText;
            AutoSize = true;
            Margin = new Padding(0, 0, 6, 0);
        }

        public Color Fill { get; set; }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var scale = Theme.Scale(this);
            var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(text.Width + (int)(20 * scale), text.Height + (int)(8 * scale));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRect(new RectangleF(0, 0, Width - 1f, Height - 1f), Height / 2f))
            using (var brush = new SolidBrush(Fill))
                g.FillPath(brush, path);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Pallino colorato seguito da un testo (stato dell'anteprima, barra di stato).</summary>
    internal sealed class StatusDot : Label
    {
        private Color _dot = Theme.Idle;

        public StatusDot()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = Theme.Regular(12);
            ForeColor = Theme.TextMuted;
            AutoSize = true;
        }

        public Color DotColor
        {
            get => _dot;
            set { _dot = value; Invalidate(); }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var scale = Theme.Scale(this);
            var text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(text.Width + (int)(14 * scale), Math.Max(text.Height, (int)(8 * scale)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var scale = Theme.Scale(this);
            var size = 8 * scale;
            using (var brush = new SolidBrush(_dot))
                g.FillEllipse(brush, 0, (Height - size) / 2f, size, size);
            var text = new Rectangle((int)(14 * scale), 0, Width - (int)(14 * scale), Height);
            TextRenderer.DrawText(g, Text, Font, text, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Cerchietto con il numero della sezione.</summary>
    internal sealed class NumberBadge : Control
    {
        public NumberBadge(int number)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Text = number.ToString();
            Font = Theme.Bold(12);
            Size = new Size(24, 24);
            Margin = new Padding(0, 0, 10, 0);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Theme.AccentSoft))
                g.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Theme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Barra di avanzamento sottile, colore principale.</summary>
    internal sealed class ThinProgress : Control
    {
        private int _value;

        public ThinProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(180, 6);
            TabStop = false;
        }

        /// <summary>Da 0 a 100.</summary>
        public int Value
        {
            get => _value;
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new RectangleF(0, 0, Width - 1f, Height - 1f);
            using (var path = Theme.RoundedRect(track, Height / 2f))
            using (var brush = new SolidBrush(Theme.Divider))
                g.FillPath(brush, path);
            if (_value <= 0) return;
            var bar = new RectangleF(0, 0, Math.Max(Height, (Width - 1f) * _value / 100f), Height - 1f);
            using (var path = Theme.RoundedRect(bar, Height / 2f))
            using (var brush = new SolidBrush(Theme.Accent))
                g.FillPath(brush, path);
        }
    }

    /// <summary>Selettore a pillola (es. "Progetto | FTP"), come le schede del design.</summary>
    internal sealed class Segmented : Control
    {
        private string[] _items;
        private int _selected;
        private int _hover = -1;
        private readonly Font _selectedFont = Theme.Semibold(14);

        public Segmented(params string[] items)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            _items = items;
            Font = Theme.Regular(14);
            TabStop = true;
            AutoSize = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PageTabList;
        }

        public event EventHandler SelectedIndexChanged;

        public string[] Items
        {
            get => _items;
            set
            {
                _items = value;
                _selected = Math.Min(_selected, _items.Length - 1);
                Parent?.PerformLayout(this, nameof(Items));
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (value == _selected || value < 0 || value >= _items.Length) return;
                _selected = value;
                AccessibleName = _items[value];
                Parent?.PerformLayout(this, nameof(SelectedIndex)); // il testo in grassetto è più largo
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private int ItemWidth(int index)
        {
            var font = index == _selected ? _selectedFont : Font;
            return TextRenderer.MeasureText(_items[index], font, Size.Empty, TextFormatFlags.NoPadding).Width + (int)(36 * Theme.Scale(this));
        }

        private Rectangle[] ItemBounds()
        {
            var scale = Theme.Scale(this);
            var pad = (int)(4 * scale);
            var x = pad;
            var bounds = new Rectangle[_items.Length];
            for (var i = 0; i < _items.Length; i++)
            {
                var w = ItemWidth(i);
                bounds[i] = new Rectangle(x, pad, w, Height - pad * 2);
                x += w + pad;
            }
            return bounds;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var scale = Theme.Scale(this);
            var width = (int)(4 * scale);
            for (var i = 0; i < _items.Length; i++) width += ItemWidth(i) + (int)(4 * scale);
            return new Size(width, (int)(44 * scale));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var scale = Theme.Scale(this);
            using (var path = Theme.RoundedRect(new RectangleF(0, 0, Width - 1f, Height - 1f), 10 * scale))
            using (var brush = new SolidBrush(Theme.NavBackground))
                g.FillPath(brush, path);

            var bounds = ItemBounds();
            for (var i = 0; i < bounds.Length; i++)
            {
                var r = bounds[i];
                var selected = i == _selected;
                if (selected)
                {
                    using (var shadow = Theme.RoundedRect(new RectangleF(r.X, r.Y + 1, r.Width, r.Height), 7 * scale))
                    using (var brush = new SolidBrush(Color.FromArgb(30, Theme.Text)))
                        g.FillPath(brush, shadow);
                    using (var path = Theme.RoundedRect(r, 7 * scale))
                    using (var brush = new SolidBrush(Theme.Surface))
                        g.FillPath(brush, path);
                    if (Focused && ShowFocusCues)
                        using (var path = Theme.RoundedRect(RectangleF.Inflate(r, -2, -2), 6 * scale))
                        using (var pen = new Pen(Theme.Accent, 1.5f) { DashStyle = DashStyle.Dot })
                            g.DrawPath(pen, path);
                }
                var color = selected || i == _hover ? Theme.Text : Theme.TextMuted;
                TextRenderer.DrawText(g, _items[i], selected ? _selectedFont : Font, r, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private int HitTest(Point p) => Array.FindIndex(ItemBounds(), r => r.Contains(p));

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hit = HitTest(e.Location);
            if (hit != _hover) { _hover = hit; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            var hit = HitTest(e.Location);
            if (hit >= 0) SelectedIndex = hit;
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) SelectedIndex = Math.Max(0, _selected - 1);
            if (e.KeyCode == Keys.Right) SelectedIndex = Math.Min(_items.Length - 1, _selected + 1);
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}
