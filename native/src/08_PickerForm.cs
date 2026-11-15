using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PasteNotes
{
    internal sealed class PickerForm : Form
    {
        private sealed class PickerRow
        {
            public TemplateData Template;
            public string Preview;

            public PickerRow(TemplateData template, string preview)
            {
                Template = template;
                Preview = preview;
            }
        }

        private readonly GroupData _group;
        private readonly Color _accent;
        private readonly Color _onAccent;
        private readonly List<TemplateData> _matches;
        private TextBox _search;
        private BorderPanel _searchBorder;
        private ListBox _list;
        private Label _emptyLabel;
        private Label _header;
        private string _emptyMessage;
        private bool _finished;
        private int _shownAt;

        public event Action<string> TemplateSelected;
        public event Action<bool> Cancelled;

        public PickerForm(GroupData group)
        {
            _group = group;
            _accent = Palette.ColorValue(group.Color);
            _onAccent = ColorTranslator.FromHtml(Palette.TextOnColorHex);
            _matches = new List<TemplateData>();
            _emptyMessage = "No templates in this group";

            Theme.StyleForm(this);
            Text = "PasteNotes";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(500, 460);
            MinimumSize = new Size(320, 260);
            KeyPreview = true;
            Padding = new Padding(1);
            DoubleBuffered = true;
            BackColor = Theme.WindowBack;

            BuildUi();
            ApplyFilter();
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Theme.WindowBack;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            _header = new Label();
            _header.Dock = DockStyle.Fill;
            _header.Margin = new Padding(0);
            _header.Padding = new Padding(14, 0, 14, 0);
            _header.BackColor = _accent;
            _header.ForeColor = _onAccent;
            _header.Font = Theme.TitleFont;
            _header.Text = _group.Name;
            _header.TextAlign = RtlHelper.IsRtl(_group.Name)
                ? ContentAlignment.MiddleRight
                : ContentAlignment.MiddleLeft;
            root.Controls.Add(_header, 0, 0);

            Panel searchHost = new Panel();
            searchHost.Dock = DockStyle.Fill;
            searchHost.BackColor = Theme.WindowBack;
            searchHost.Padding = new Padding(12, 9, 12, 9);
            searchHost.Margin = new Padding(0);
            root.Controls.Add(searchHost, 0, 1);

            _searchBorder = new BorderPanel();
            _searchBorder.Dock = DockStyle.Fill;
            _searchBorder.BackColor = Theme.InputBack;
            _searchBorder.BorderColor = Theme.Border;
            _searchBorder.Padding = new Padding(8, 5, 8, 5);
            searchHost.Controls.Add(_searchBorder);

            _search = Theme.MakeInput();
            _search.BorderStyle = BorderStyle.None;
            _search.Dock = DockStyle.Fill;
            _search.Font = new Font("Segoe UI", 10.5f);
            _search.BackColor = Theme.InputBack;
            _search.ForeColor = Theme.Text;
            _search.TextChanged += delegate
            {
                RtlHelper.Apply(_search);
                ApplyFilter();
            };
            _search.KeyDown += SearchKeyDown;
            _search.Enter += delegate
            {
                if (_finished) return;
                _searchBorder.BorderColor = _accent;
            };
            _search.Leave += delegate
            {
                if (_finished) return;
                _searchBorder.BorderColor = Theme.Border;
            };
            _searchBorder.Controls.Add(_search);

            _list = new ListBox();
            Theme.StyleListBox(_list);
            _list.ItemHeight = Math.Max(42, TextRenderer.MeasureText("Ag", Theme.PickerTitleFont).Height + 24);
            _list.Dock = DockStyle.Fill;
            _list.Margin = new Padding(0);
            _list.BackColor = Theme.WindowBack;
            _list.DrawItem += ListDrawItem;
            _list.MouseUp += ListMouseUp;
            _list.TabStop = false;
            _list.Cursor = Cursors.Hand;

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.Margin = new Padding(10, 0, 10, 0);
            listHost.BackColor = Theme.WindowBack;
            listHost.Controls.Add(_list);

            _emptyLabel = new Label();
            _emptyLabel.Dock = DockStyle.Fill;
            _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _emptyLabel.BackColor = Theme.WindowBack;
            _emptyLabel.ForeColor = Theme.Muted;
            _emptyLabel.Font = Theme.BoldFont;
            _emptyLabel.Text = _emptyMessage;
            _emptyLabel.Visible = false;
            _emptyLabel.Cursor = Cursors.Hand;
            _emptyLabel.Click += delegate { if (!_finished) _search.Focus(); };
            listHost.Controls.Add(_emptyLabel);
            _emptyLabel.BringToFront();

            root.Controls.Add(listHost, 0, 2);

            Label hint = Theme.MakeLabel("Type to search  |  Up/Down move  |  Enter paste  |  Esc cancel");
            hint.Font = Theme.SmallFont;
            hint.ForeColor = Theme.Muted;
            hint.Margin = new Padding(14, 6, 14, 10);
            root.Controls.Add(hint, 0, 3);

            _search.HandleCreated += delegate
            {
                UiHelpers.SetCueBanner(_search, "Search templates...");
            };
            if (_search.IsHandleCreated)
            {
                UiHelpers.SetCueBanner(_search, "Search templates...");
            }
        }

        public void OpenAtCursor()
        {
            Point cursor = Cursor.Position;
            Screen screen = Screen.FromPoint(cursor);
            Rectangle area = screen.WorkingArea;
            int width = Math.Min(Width, area.Width);
            int height = Math.Min(Height, area.Height);

            int x = cursor.X + 14;
            int y = cursor.Y + 18;
            if (x + width > area.Right) x = area.Right - width;
            if (y + height > area.Bottom) y = cursor.Y - height - 16;
            x = Math.Max(area.Left, Math.Min(x, area.Right - width));
            y = Math.Max(area.Top, Math.Min(y, area.Bottom - height));

            Location = new Point(x, y);
            _shownAt = Environment.TickCount;
            Show();
            NativeMethods.ForceForeground(Handle);
            _search.Focus();
            _search.SelectAll();
        }

        public void DismissSilently()
        {
            _finished = true;
            Hide();
        }

        private void ApplyFilter()
        {
            string query = _search.Text.Trim();
            _matches.Clear();
            foreach (TemplateData template in _group.Templates)
            {
                if (query.Length == 0)
                {
                    _matches.Add(template);
                    continue;
                }

                string title = template.Title ?? "";
                string content = template.Content ?? "";
                if (title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    content.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _matches.Add(template);
                }
            }

            _list.BeginUpdate();
            _list.Items.Clear();
            if (_matches.Count == 0)
            {
                _emptyMessage = _group.Templates.Count == 0
                    ? "No templates in this group"
                    : "No matching templates";
                _emptyLabel.Text = _emptyMessage;
                _emptyLabel.Visible = true;
                _emptyLabel.BringToFront();
            }
            else
            {
                _emptyLabel.Visible = false;
                foreach (TemplateData template in _matches)
                {
                    _list.Items.Add(new PickerRow(
                        template,
                        UiDraw.Preview(template.Content, 140)));
                }
            }
            _list.EndUpdate();

            if (_matches.Count > 0)
            {
                _list.SelectedIndex = 0;
                _list.TopIndex = 0;
            }
            else
            {
                _list.ClearSelected();
            }
        }

        private void SearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                MoveCurrent(1);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                MoveCurrent(-1);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                MoveCurrent(8);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                MoveCurrent(-8);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                AcceptCurrent();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Cancel(true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void MoveCurrent(int delta)
        {
            if (_matches.Count == 0) return;
            int current = _list.SelectedIndex;
            if (current < 0 || current >= _matches.Count) current = 0;
            int target = Math.Max(0, Math.Min(_matches.Count - 1, current + delta));
            _list.SelectedIndex = target;

            int visible = Math.Max(1, _list.ClientSize.Height / Math.Max(1, _list.ItemHeight));
            int top = _list.TopIndex;
            if (target < top)
            {
                _list.TopIndex = target;
            }
            else if (target >= top + visible)
            {
                _list.TopIndex = Math.Max(0, target - visible + 1);
            }
        }

        private void AcceptCurrent()
        {
            if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _list.Items.Count) return;
            PickerRow row = _list.Items[_list.SelectedIndex] as PickerRow;
            if (row == null || row.Template == null) return;
            Accept(row.Template);
        }

        private void Accept(TemplateData template)
        {
            if (_finished) return;
            _finished = true;
            Hide();
            Action<string> handler = TemplateSelected;
            if (handler != null) handler(template.Content);
        }

        private void Cancel(bool refocus)
        {
            if (_finished) return;
            _finished = true;
            Hide();
            Action<bool> handler = Cancelled;
            if (handler != null) handler(refocus);
        }

        private void ListMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _finished) return;
            int index = _list.IndexFromPoint(e.Location);
            if (index < 0 || index >= _list.Items.Count)
            {
                _search.Focus();
                return;
            }

            _list.SelectedIndex = index;
            PickerRow row = _list.Items[index] as PickerRow;
            if (row == null || row.Template == null)
            {
                _search.Focus();
                return;
            }
            Accept(row.Template);
        }

        private void ListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;
            PickerRow row = _list.Items[e.Index] as PickerRow;
            if (row == null || row.Template == null) return;

            bool selected = (e.State & DrawItemState.Selected) != 0 && row.Template != null;
            using (SolidBrush background = new SolidBrush(Theme.WindowBack))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
            }

            Rectangle rowRect = new Rectangle(
                e.Bounds.X + 3,
                e.Bounds.Y + 2,
                Math.Max(1, e.Bounds.Width - 6),
                Math.Max(1, e.Bounds.Height - 4));
            if (selected)
            {
                UiDraw.FillRounded(e.Graphics, rowRect, 6, _accent);
            }

            string title = string.IsNullOrEmpty(row.Template.Title)
                ? "Untitled template"
                : row.Template.Title;
            bool titleRtl = RtlHelper.IsRtl(title);
            TextFormatFlags titleFlags = TextFormatFlags.NoPrefix |
                                        TextFormatFlags.EndEllipsis |
                                        TextFormatFlags.SingleLine |
                                        TextFormatFlags.VerticalCenter;
            if (titleRtl)
            {
                titleFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
            }
            else
            {
                titleFlags |= TextFormatFlags.Left;
            }

            Rectangle titleBounds = new Rectangle(
                e.Bounds.X + 14,
                e.Bounds.Y + 4,
                Math.Max(1, e.Bounds.Width - 28),
                22);
            TextRenderer.DrawText(
                e.Graphics,
                title,
                Theme.PickerTitleFont,
                titleBounds,
                selected ? _onAccent : Theme.Text,
                titleFlags);

            if (row.Preview.Length > 0)
            {
                bool previewRtl = RtlHelper.IsRtl(row.Preview);
                TextFormatFlags previewFlags = TextFormatFlags.NoPrefix |
                                               TextFormatFlags.EndEllipsis |
                                               TextFormatFlags.SingleLine |
                                               TextFormatFlags.VerticalCenter;
                if (previewRtl)
                {
                    previewFlags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
                }
                else
                {
                    previewFlags |= TextFormatFlags.Left;
                }

                Rectangle previewBounds = new Rectangle(
                    e.Bounds.X + 14,
                    e.Bounds.Y + 25,
                    Math.Max(1, e.Bounds.Width - 28),
                    Math.Max(12, e.Bounds.Height - 28));
                TextRenderer.DrawText(
                    e.Graphics,
                    row.Preview,
                    Theme.SmallFont,
                    previewBounds,
                    selected ? Color.FromArgb(165, _onAccent) : Theme.Muted,
                    previewFlags);
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (_finished) return;
            if (Environment.TickCount - _shownAt < 200) return;
            Cancel(false);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_finished)
            {
                _finished = true;
                Action<bool> handler = Cancelled;
                if (handler != null) handler(true);
            }
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) return;
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = UiHelpers.RoundRect(new Rectangle(0, 0, Width, Height), 10))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = UiHelpers.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 10))
            using (Pen pen = new Pen(_accent, 1.4f))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}
