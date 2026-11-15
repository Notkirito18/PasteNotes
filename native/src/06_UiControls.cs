using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PasteNotes
{
    internal static class Theme
    {
        public static readonly Color WindowBack = Hex("#1e2330");
        public static readonly Color PanelBack = Hex("#252b3a");
        public static readonly Color InputBack = Hex("#121620");
        public static readonly Color Border = Hex("#343d51");
        public static readonly Color BorderSoft = Hex("#2a3142");
        public static readonly Color RowSelected = Hex("#343d51");
        public static readonly Color Text = Hex("#e8ebf2");
        public static readonly Color Muted = Hex("#8d96a8");
        public static readonly Color Warning = Hex("#d9a33b");
        public static readonly Color Success = Hex("#6fcf97");
        public static readonly Color Danger = Hex("#eb6b6b");

        public static readonly Font BaseFont = new Font("Segoe UI", 9.5f);
        public static readonly Font SmallFont = new Font("Segoe UI", 8.25f);
        public static readonly Font BoldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font HeadingFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font TitleFont = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font PickerTitleFont = new Font("Segoe UI", 9.75f, FontStyle.Bold);

        public static Color Hex(string value)
        {
            return ColorTranslator.FromHtml(value);
        }

        public static Label MakeLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = Text;
            label.BackColor = Color.Transparent;
            label.Margin = new Padding(0, 0, 0, 4);
            return label;
        }

        public static Label MakeHeading(string text)
        {
            Label label = MakeLabel(text);
            label.Font = HeadingFont;
            label.ForeColor = Text;
            return label;
        }

        public static Button MakeButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.MinimumSize = new Size(0, 32);
            button.Padding = new Padding(8, 2, 8, 2);
            button.Margin = new Padding(0, 0, 6, 6);
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = PanelBack;
            button.ForeColor = Text;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = RowSelected;
            button.FlatAppearance.MouseDownBackColor = Border;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            return button;
        }

        public static TextBox MakeInput()
        {
            TextBox box = new TextBox();
            box.BorderStyle = BorderStyle.None;
            box.BackColor = InputBack;
            box.ForeColor = Text;
            box.Font = BaseFont;
            box.Margin = new Padding(0);
            return box;
        }

        public static CheckBox MakeCheckBox(string text)
        {
            CheckBox check = new CheckBox();
            check.Text = text;
            check.AutoSize = true;
            check.ForeColor = Text;
            check.BackColor = Color.Transparent;
            check.FlatStyle = FlatStyle.Flat;
            check.FlatAppearance.BorderColor = Border;
            check.FlatAppearance.CheckedBackColor = WindowBack;
            check.Margin = new Padding(0, 4, 0, 0);
            check.Cursor = Cursors.Hand;
            return check;
        }

        public static void StyleListBox(ListBox list)
        {
            list.BackColor = InputBack;
            list.ForeColor = Text;
            list.BorderStyle = BorderStyle.None;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = UiHelpers.MeasureListHeight(list.Font, 12);
            list.IntegralHeight = false;
            list.Margin = new Padding(0);
        }

        public static void StyleForm(Form form)
        {
            form.BackColor = WindowBack;
            form.ForeColor = Text;
            form.Font = BaseFont;
            form.Icon = IconFactory.ApplicationIcon;
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.AutoScaleDimensions = new SizeF(96f, 96f);
            form.HandleCreated += delegate { NativeMethods.TryEnableDarkTitleBar(form.Handle); };
            if (form.IsHandleCreated) NativeMethods.TryEnableDarkTitleBar(form.Handle);
        }

        public static BorderPanel WrapInput(Control inner)
        {
            BorderPanel panel = new BorderPanel();
            panel.BackColor = InputBack;
            panel.BorderColor = Border;
            panel.Padding = new Padding(1);
            panel.Margin = new Padding(0);
            inner.Dock = DockStyle.Fill;
            panel.Controls.Add(inner);
            return panel;
        }
    }

    internal sealed class BorderPanel : Panel
    {
        private Color _borderColor = Theme.Border;

        public Color BorderColor
        {
            get { return _borderColor; }
            set
            {
                if (_borderColor == value) return;
                _borderColor = value;
                Invalidate();
            }
        }

        public BorderPanel()
        {
            BackColor = Theme.InputBack;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(_borderColor, 1f))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    internal static class UiDraw
    {
        public static void FillRounded(Graphics graphics, Rectangle bounds, int radius, Color color)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (GraphicsPath path = UiHelpers.RoundRect(bounds, radius))
            using (SolidBrush brush = new SolidBrush(color))
            {
                graphics.FillPath(brush, path);
            }
        }

        public static TextFormatFlags TextFlags(string text, bool centerVertically)
        {
            TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            if (centerVertically) flags |= TextFormatFlags.VerticalCenter;
            if (RtlHelper.IsRtl(text))
            {
                flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            }
            else
            {
                flags |= TextFormatFlags.Left;
            }
            return flags;
        }

        public static string Preview(string content, int limit)
        {
            if (string.IsNullOrEmpty(content)) return "";
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            bool lastSpace = false;
            for (int index = 0; index < content.Length; index++)
            {
                char c = content[index];
                if (char.IsWhiteSpace(c))
                {
                    if (!lastSpace && builder.Length > 0) builder.Append(' ');
                    lastSpace = true;
                }
                else
                {
                    builder.Append(c);
                    lastSpace = false;
                }
                if (builder.Length > limit) break;
            }
            string text = builder.ToString().Trim();
            if (text.Length > limit) text = text.Substring(0, Math.Max(0, limit - 3)) + "...";
            return text;
        }
    }

    internal sealed class ColorSwatch : Control
    {
        private bool _selected;
        private bool _hover;

        public string ColorKey { get; private set; }

        public event EventHandler Picked;

        public bool IsSelected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        public ColorSwatch(string colorKey, string toolTip)
        {
            ColorKey = colorKey;
            Size = new Size(26, 26);
            Margin = new Padding(3, 2, 3, 2);
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            ToolTip tip = new ToolTip();
            tip.SetToolTip(this, toolTip);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle circle = new Rectangle(4, 4, Math.Max(1, Width - 9), Math.Max(1, Height - 9));

            if (!Enabled)
            {
                using (SolidBrush gray = new SolidBrush(Theme.BorderSoft))
                {
                    e.Graphics.FillEllipse(gray, circle);
                }
                return;
            }

            using (SolidBrush brush = new SolidBrush(Palette.ColorValue(ColorKey)))
            {
                e.Graphics.FillEllipse(brush, circle);
            }

            if (_selected)
            {
                using (Pen pen = new Pen(Color.White, 2f))
                {
                    e.Graphics.DrawEllipse(pen, circle);
                }
            }
            else if (_hover || Focused)
            {
                using (Pen pen = new Pen(Theme.Muted, 1.4f))
                {
                    e.Graphics.DrawEllipse(pen, circle);
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            Focus();
            if (Picked != null) Picked(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space || keyData == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) && Enabled)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (Picked != null) Picked(this, EventArgs.Empty);
                return;
            }
            base.OnKeyDown(e);
        }
    }

    internal sealed class HotkeyBox : TextBox
    {
        public event EventHandler CaptureCommitted;

        public HotkeyBox()
        {
            ShortcutsEnabled = false;
            Font = Theme.BaseFont;
            BackColor = Theme.InputBack;
            ForeColor = Theme.Text;
            BorderStyle = BorderStyle.None;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            Keys key = e.KeyCode;
            bool functionKey = key >= Keys.F1 && key <= Keys.F24;

            if (e.Control || e.Alt || functionKey)
            {
                string display = HotkeyParser.DisplayFromKey(key);
                if (display != null)
                {
                    uint modifiers = 0;
                    if (e.Control) modifiers |= NativeMethods.MOD_CONTROL;
                    if (e.Alt) modifiers |= NativeMethods.MOD_ALT;
                    if (e.Shift) modifiers |= NativeMethods.MOD_SHIFT;

                    string candidate = HotkeyParser.Format(modifiers, display);
                    try
                    {
                        HotkeySpec spec = HotkeyParser.Parse(candidate);
                        Text = spec.Text;
                        SelectionStart = Text.Length;
                        SelectionLength = 0;
                        if (CaptureCommitted != null) CaptureCommitted(this, EventArgs.Empty);
                    }
                    catch (HotkeyException)
                    {
                    }

                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }
    }

    internal sealed class TextPromptForm : Form
    {
        private TextBox _box;

        public string Value
        {
            get { return _box.Text; }
        }

        private TextPromptForm(string title, string labelText, string initial)
        {
            Text = title;
            Theme.StyleForm(this);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 150);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.BackColor = Theme.WindowBack;
            Controls.Add(root);

            Label label = Theme.MakeLabel(labelText);
            root.Controls.Add(label, 0, 0);

            _box = Theme.MakeInput();
            _box.Text = initial;
            _box.Dock = DockStyle.Top;
            BorderPanel inputPanel = Theme.WrapInput(_box);
            inputPanel.Height = 32;
            inputPanel.Margin = new Padding(0, 2, 0, 8);
            root.Controls.Add(inputPanel, 0, 1);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = false;
            buttons.AutoSize = true;
            buttons.Margin = new Padding(0);
            root.Controls.Add(buttons, 0, 3);

            Button ok = Theme.MakeButton("OK");
            ok.DialogResult = DialogResult.OK;
            ok.Margin = new Padding(6, 0, 0, 0);
            Button cancel = Theme.MakeButton("Cancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Margin = new Padding(0);
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            _box.TextChanged += delegate { RtlHelper.Apply(_box); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _box.Focus();
            _box.SelectAll();
        }

        public static string Prompt(
            IWin32Window owner,
            string title,
            string label,
            string initial,
            out bool accepted)
        {
            using (TextPromptForm form = new TextPromptForm(title, label, initial))
            {
                DialogResult result = form.ShowDialog(owner);
                accepted = result == DialogResult.OK;
                return form.Value;
            }
        }
    }
}
