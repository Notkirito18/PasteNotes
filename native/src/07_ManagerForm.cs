using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PasteNotes
{
    internal sealed class ManagerForm : Form
    {
        private readonly DataStore _store;
        private readonly System.Windows.Forms.Timer _saveTimer;
        private readonly System.Windows.Forms.Timer _hotkeyTimer;
        private readonly System.Windows.Forms.Timer _statusTimer;

        private GroupData _group;
        private TemplateData _template;
        private bool _loading;
        private bool _allowClose;
        private Dictionary<string, string> _statuses;
        private readonly Dictionary<string, ColorSwatch> _swatches;

        private ListBox _groupList;
        private ListBox _templateList;
        private HotkeyBox _hotkeyBox;
        private Button _clearHotkeyButton;
        private Label _hotkeyStatus;
        private Label _savedLabel;
        private Label _statusLabel;
        private Label _colorLabel;
        private TextBox _titleBox;
        private TextBox _contentBox;
        private CheckBox _restoreCheck;
        private ToolTip _savedTip;

        public event EventHandler HotkeysChanged;
        public event EventHandler HiddenToTray;

        public ManagerForm(DataStore store)
        {
            _store = store;
            _statuses = new Dictionary<string, string>();
            _swatches = new Dictionary<string, ColorSwatch>(StringComparer.OrdinalIgnoreCase);
            _saveTimer = new System.Windows.Forms.Timer();
            _saveTimer.Interval = 400;
            _saveTimer.Tick += delegate { SaveNow(); };
            _hotkeyTimer = new System.Windows.Forms.Timer();
            _hotkeyTimer.Interval = 250;
            _hotkeyTimer.Tick += delegate
            {
                _hotkeyTimer.Stop();
                if (HotkeysChanged != null) HotkeysChanged(this, EventArgs.Empty);
            };
            _statusTimer = new System.Windows.Forms.Timer();
            _statusTimer.Interval = 6000;
            _statusTimer.Tick += delegate
            {
                _statusTimer.Stop();
                if (!IsDisposed) _statusLabel.Text = "Ready";
            };

            BuildUi();
            LoadSettingsUi();
            ReloadGroups(_store.Data.Groups.Count > 0 ? _store.Data.Groups[0].Id : null);
            SetSavedState("All changes saved", Theme.Muted);
        }

        private void BuildUi()
        {
            Text = "PasteNotes";
            Theme.StyleForm(this);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1020, 700);
            MinimumSize = new Size(880, 600);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Theme.WindowBack;
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.Padding = new Padding(12);
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 286f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            TableLayoutPanel left = BuildGroupPanel();
            left.Margin = new Padding(0, 0, 14, 0);
            root.Controls.Add(left, 0, 0);

            TableLayoutPanel right = BuildTemplatePanel();
            right.Margin = new Padding(0);
            root.Controls.Add(right, 1, 0);

            _statusLabel = Theme.MakeLabel("Ready");
            _statusLabel.AutoSize = false;
            _statusLabel.AutoEllipsis = true;
            _statusLabel.Height = 22;
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.ForeColor = Theme.Muted;
            _statusLabel.Margin = new Padding(2, 4, 2, 0);
            root.Controls.Add(_statusLabel, 0, 1);
            root.SetColumnSpan(_statusLabel, 2);
        }

        private TableLayoutPanel BuildGroupPanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Theme.WindowBack;
            panel.ColumnCount = 1;
            panel.RowCount = 4;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label heading = Theme.MakeHeading("Groups");
            heading.Margin = new Padding(0, 0, 0, 6);
            panel.Controls.Add(heading, 0, 0);

            _groupList = new ListBox();
            Theme.StyleListBox(_groupList);
            _groupList.Dock = DockStyle.Fill;
            _groupList.DrawItem += GroupListDrawItem;
            _groupList.SelectedIndexChanged += GroupSelected;
            BorderPanel listPanel = new BorderPanel();
            listPanel.Dock = DockStyle.Fill;
            listPanel.Padding = new Padding(1);
            listPanel.Margin = new Padding(0, 0, 0, 8);
            listPanel.Controls.Add(_groupList);
            panel.Controls.Add(listPanel, 0, 1);

            FlowLayoutPanel firstRow = new FlowLayoutPanel();
            firstRow.Dock = DockStyle.Fill;
            firstRow.AutoSize = true;
            firstRow.WrapContents = true;
            firstRow.Margin = new Padding(0);
            firstRow.BackColor = Theme.WindowBack;
            Button addGroup = Theme.MakeButton("Add");
            addGroup.Click += delegate { AddGroup(); };
            Button renameGroup = Theme.MakeButton("Rename");
            renameGroup.Click += delegate { RenameGroup(); };
            Button deleteGroup = Theme.MakeButton("Delete");
            deleteGroup.Click += delegate { DeleteGroup(); };
            firstRow.Controls.Add(addGroup);
            firstRow.Controls.Add(renameGroup);
            firstRow.Controls.Add(deleteGroup);
            panel.Controls.Add(firstRow, 0, 2);

            FlowLayoutPanel secondRow = new FlowLayoutPanel();
            secondRow.Dock = DockStyle.Fill;
            secondRow.AutoSize = true;
            secondRow.WrapContents = true;
            secondRow.Margin = new Padding(0);
            secondRow.BackColor = Theme.WindowBack;
            Button moveUp = Theme.MakeButton("Move up");
            moveUp.Click += delegate { MoveGroup(-1); };
            Button moveDown = Theme.MakeButton("Move down");
            moveDown.Click += delegate { MoveGroup(1); };
            secondRow.Controls.Add(moveUp);
            secondRow.Controls.Add(moveDown);
            panel.Controls.Add(secondRow, 0, 3);

            return panel;
        }

        private TableLayoutPanel BuildTemplatePanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Theme.WindowBack;
            panel.ColumnCount = 1;
            panel.RowCount = 10;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 36f));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 64f));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            FlowLayoutPanel colorRow = new FlowLayoutPanel();
            colorRow.Dock = DockStyle.Fill;
            colorRow.AutoSize = true;
            colorRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            colorRow.WrapContents = false;
            colorRow.Margin = new Padding(0, 0, 0, 6);
            colorRow.BackColor = Theme.WindowBack;

            _colorLabel = Theme.MakeLabel("Group color:");
            _colorLabel.AutoSize = false;
            _colorLabel.Width = 92;
            _colorLabel.Height = 28;
            _colorLabel.TextAlign = ContentAlignment.MiddleLeft;
            _colorLabel.Margin = new Padding(0, 0, 6, 0);
            colorRow.Controls.Add(_colorLabel);

            string[] keys = Palette.Keys();
            foreach (string key in keys)
            {
                ColorSwatch swatch = new ColorSwatch(key, Palette.Label(key));
                swatch.Picked += SwatchPicked;
                _swatches[key] = swatch;
                colorRow.Controls.Add(swatch);
            }
            panel.Controls.Add(colorRow, 0, 0);

            TableLayoutPanel hotkeyRow = new TableLayoutPanel();
            hotkeyRow.Dock = DockStyle.Fill;
            hotkeyRow.AutoSize = true;
            hotkeyRow.ColumnCount = 4;
            hotkeyRow.RowCount = 1;
            hotkeyRow.Margin = new Padding(0, 0, 0, 6);
            hotkeyRow.BackColor = Theme.WindowBack;
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            hotkeyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210f));
            hotkeyRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label hotkeyLabel = Theme.MakeLabel("Group hotkey:");
            hotkeyLabel.AutoSize = false;
            hotkeyLabel.Width = 98;
            hotkeyLabel.Height = 30;
            hotkeyLabel.TextAlign = ContentAlignment.MiddleLeft;
            hotkeyLabel.Margin = new Padding(0, 0, 6, 0);
            hotkeyRow.Controls.Add(hotkeyLabel, 0, 0);

            _hotkeyBox = new HotkeyBox();
            _hotkeyBox.Dock = DockStyle.Fill;
            _hotkeyBox.CaptureCommitted += delegate { CommitHotkey(true); };
            _hotkeyBox.KeyDown += HotkeyKeyDown;
            _hotkeyBox.Leave += delegate { CommitHotkey(false); };
            _hotkeyBox.HandleCreated += delegate
            {
                UiHelpers.SetCueBanner(_hotkeyBox, "Press shortcut, e.g. Ctrl+E");
            };
            BorderPanel hotkeyPanel = Theme.WrapInput(_hotkeyBox);
            hotkeyPanel.Dock = DockStyle.Fill;
            hotkeyPanel.Height = 30;
            hotkeyPanel.Margin = new Padding(0, 3, 8, 3);
            hotkeyRow.Controls.Add(hotkeyPanel, 1, 0);

            _clearHotkeyButton = Theme.MakeButton("Clear");
            _clearHotkeyButton.Margin = new Padding(0, 2, 8, 2);
            _clearHotkeyButton.Click += delegate
            {
                _hotkeyBox.Text = "";
                CommitHotkey(true);
                _hotkeyBox.Focus();
            };
            hotkeyRow.Controls.Add(_clearHotkeyButton, 2, 0);

            _hotkeyStatus = new Label();
            _hotkeyStatus.AutoSize = false;
            _hotkeyStatus.Dock = DockStyle.Fill;
            _hotkeyStatus.TextAlign = ContentAlignment.MiddleLeft;
            _hotkeyStatus.AutoEllipsis = true;
            _hotkeyStatus.ForeColor = Theme.Muted;
            _hotkeyStatus.Margin = new Padding(0, 2, 0, 2);
            hotkeyRow.Controls.Add(_hotkeyStatus, 3, 0);

            panel.Controls.Add(hotkeyRow, 0, 1);

            Label templatesHeading = Theme.MakeHeading("Templates");
            templatesHeading.Margin = new Padding(0, 4, 0, 4);
            panel.Controls.Add(templatesHeading, 0, 2);

            _templateList = new ListBox();
            Theme.StyleListBox(_templateList);
            _templateList.Dock = DockStyle.Fill;
            _templateList.DrawItem += TemplateListDrawItem;
            _templateList.SelectedIndexChanged += TemplateSelected;
            BorderPanel templateListPanel = new BorderPanel();
            templateListPanel.Dock = DockStyle.Fill;
            templateListPanel.MinimumSize = new Size(0, 100);
            templateListPanel.Padding = new Padding(1);
            templateListPanel.Margin = new Padding(0, 0, 0, 8);
            templateListPanel.Controls.Add(_templateList);
            panel.Controls.Add(templateListPanel, 0, 3);

            FlowLayoutPanel templateButtons = new FlowLayoutPanel();
            templateButtons.Dock = DockStyle.Fill;
            templateButtons.AutoSize = true;
            templateButtons.WrapContents = true;
            templateButtons.Margin = new Padding(0, 0, 0, 4);
            templateButtons.BackColor = Theme.WindowBack;
            Button addTemplate = Theme.MakeButton("Add");
            addTemplate.Click += delegate { AddTemplate(); };
            Button duplicateTemplate = Theme.MakeButton("Duplicate");
            duplicateTemplate.Click += delegate { DuplicateTemplate(); };
            Button deleteTemplate = Theme.MakeButton("Delete");
            deleteTemplate.Click += delegate { DeleteTemplate(); };
            Button templateUp = Theme.MakeButton("Move up");
            templateUp.Click += delegate { MoveTemplate(-1); };
            Button templateDown = Theme.MakeButton("Move down");
            templateDown.Click += delegate { MoveTemplate(1); };
            templateButtons.Controls.Add(addTemplate);
            templateButtons.Controls.Add(duplicateTemplate);
            templateButtons.Controls.Add(deleteTemplate);
            templateButtons.Controls.Add(templateUp);
            templateButtons.Controls.Add(templateDown);
            panel.Controls.Add(templateButtons, 0, 4);

            Label titleLabel = Theme.MakeLabel("Title");
            titleLabel.Margin = new Padding(0, 0, 0, 3);
            panel.Controls.Add(titleLabel, 0, 5);

            _titleBox = Theme.MakeInput();
            _titleBox.Dock = DockStyle.Fill;
            _titleBox.TextChanged += TitleChanged;
            BorderPanel titlePanel = Theme.WrapInput(_titleBox);
            titlePanel.Dock = DockStyle.Fill;
            titlePanel.Height = 32;
            titlePanel.Margin = new Padding(0, 0, 0, 7);
            panel.Controls.Add(titlePanel, 0, 6);

            Label contentLabel = Theme.MakeLabel("Content");
            contentLabel.Margin = new Padding(0, 0, 0, 3);
            panel.Controls.Add(contentLabel, 0, 7);

            _contentBox = Theme.MakeInput();
            _contentBox.Multiline = true;
            _contentBox.AcceptsReturn = true;
            _contentBox.AcceptsTab = true;
            _contentBox.ScrollBars = ScrollBars.Vertical;
            _contentBox.WordWrap = true;
            _contentBox.Dock = DockStyle.Fill;
            _contentBox.TextChanged += ContentChanged;
            BorderPanel contentPanel = Theme.WrapInput(_contentBox);
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.MinimumSize = new Size(0, 140);
            contentPanel.Margin = new Padding(0, 0, 0, 8);
            panel.Controls.Add(contentPanel, 0, 8);

            TableLayoutPanel bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.AutoSize = true;
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.Margin = new Padding(0);
            bottom.BackColor = Theme.WindowBack;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190f));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _restoreCheck = Theme.MakeCheckBox("Restore previous clipboard after pasting");
            _restoreCheck.CheckedChanged += RestoreToggled;
            bottom.Controls.Add(_restoreCheck, 0, 0);

            _savedLabel = new Label();
            _savedLabel.AutoSize = false;
            _savedLabel.Dock = DockStyle.Fill;
            _savedLabel.Height = 28;
            _savedLabel.TextAlign = ContentAlignment.MiddleRight;
            _savedLabel.ForeColor = Theme.Muted;
            _savedLabel.Cursor = Cursors.Hand;
            _savedLabel.Text = "All changes saved";
            _savedLabel.Click += delegate
            {
                if (_store.LastSaveFailed) SaveNow();
            };
            bottom.Controls.Add(_savedLabel, 1, 0);
            panel.Controls.Add(bottom, 0, 9);

            _savedTip = new ToolTip();
            _savedTip.SetToolTip(_savedLabel, "Changes are saved automatically.");
            _savedTip.SetToolTip(_hotkeyBox, "Click the field, then press the keys. Example: Ctrl+E.");
            _savedTip.SetToolTip(_clearHotkeyButton, "Remove this group's hotkey.");

            return panel;
        }

        private void LoadSettingsUi()
        {
            _loading = true;
            _restoreCheck.Checked = _store.GetBoolSetting("restore_clipboard", true);
            _loading = false;
        }

        private void ReloadGroups(string selectId)
        {
            _loading = true;
            _groupList.BeginUpdate();
            _groupList.Items.Clear();
            foreach (GroupData group in _store.Data.Groups)
            {
                _groupList.Items.Add(group);
            }
            _groupList.EndUpdate();
            _loading = false;

            if (_store.Data.Groups.Count == 0)
            {
                SelectGroup(null);
                return;
            }

            int row = 0;
            if (!string.IsNullOrEmpty(selectId))
            {
                for (int index = 0; index < _groupList.Items.Count; index++)
                {
                    GroupData item = _groupList.Items[index] as GroupData;
                    if (item != null && item.Id == selectId)
                    {
                        row = index;
                        break;
                    }
                }
            }

            _loading = true;
            _groupList.SelectedIndex = row;
            _loading = false;
            SelectGroup(_store.Data.Groups[row]);
        }

        private void SelectGroup(GroupData group)
        {
            _group = group;
            _loading = true;
            bool enabled = group != null;
            _hotkeyBox.Enabled = enabled;
            _clearHotkeyButton.Enabled = enabled;
            _hotkeyBox.Text = enabled ? group.Hotkey : "";
            _loading = false;

            ReloadTemplates(null);
            RefreshColorSwatches();
            RefreshHotkeyStatus();
        }

        private void ReloadTemplates(string selectId)
        {
            _loading = true;
            _templateList.BeginUpdate();
            _templateList.Items.Clear();
            List<TemplateData> templates = _group == null ? new List<TemplateData>() : _group.Templates;
            foreach (TemplateData template in templates)
            {
                _templateList.Items.Add(template);
            }
            _templateList.EndUpdate();
            _loading = false;

            if (templates.Count == 0)
            {
                SelectTemplate(null);
                return;
            }

            int row = 0;
            if (!string.IsNullOrEmpty(selectId))
            {
                for (int index = 0; index < _templateList.Items.Count; index++)
                {
                    TemplateData item = _templateList.Items[index] as TemplateData;
                    if (item != null && item.Id == selectId)
                    {
                        row = index;
                        break;
                    }
                }
            }

            _loading = true;
            _templateList.SelectedIndex = row;
            _loading = false;
            SelectTemplate(templates[row]);
        }

        private void SelectTemplate(TemplateData template)
        {
            _template = template;
            _loading = true;
            bool enabled = template != null;
            _titleBox.Enabled = enabled;
            _contentBox.Enabled = enabled;
            _titleBox.Text = enabled ? template.Title : "";
            _contentBox.Text = enabled ? template.Content : "";
            _loading = false;
            RtlHelper.Apply(_titleBox);
            RtlHelper.ApplyEditor(_contentBox);
        }

        private void GroupSelected(object sender, EventArgs e)
        {
            if (_loading) return;
            GroupData group = _groupList.SelectedItem as GroupData;
            SelectGroup(group);
        }

        private void TemplateSelected(object sender, EventArgs e)
        {
            if (_loading) return;
            TemplateData template = _templateList.SelectedItem as TemplateData;
            SelectTemplate(template);
        }

        private void TitleChanged(object sender, EventArgs e)
        {
            if (_loading || _template == null) return;
            _template.Title = _titleBox.Text;
            RtlHelper.Apply(_titleBox);
            _templateList.Invalidate();
            ScheduleSave();
        }

        private void ContentChanged(object sender, EventArgs e)
        {
            if (_loading || _template == null) return;
            _template.Content = _contentBox.Text;
            RtlHelper.ApplyEditor(_contentBox);
            ScheduleSave();
        }

        private void RestoreToggled(object sender, EventArgs e)
        {
            if (_loading) return;
            _store.SetSetting("restore_clipboard", _restoreCheck.Checked);
            ScheduleSave();
        }

        private void HotkeyKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                CommitHotkey(true);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (_group != null)
                {
                    _loading = true;
                    _hotkeyBox.Text = _group.Hotkey;
                    _loading = false;
                }
                RefreshHotkeyStatus();
            }
        }

        private void CommitHotkey(bool showMessage)
        {
            if (_loading || _group == null) return;

            string raw = _hotkeyBox.Text.Trim();
            string normalized;
            if (raw.Length == 0)
            {
                normalized = "";
            }
            else
            {
                try
                {
                    normalized = HotkeyParser.Normalize(raw);
                }
                catch (HotkeyException ex)
                {
                    _loading = true;
                    _hotkeyBox.Text = _group.Hotkey;
                    _loading = false;
                    _hotkeyStatus.Text = "Invalid hotkey";
                    _hotkeyStatus.ForeColor = Theme.Danger;
                    if (showMessage)
                    {
                        SetStatus("Hotkey not changed: " + ex.Message);
                    }
                    return;
                }
            }

            _loading = true;
            _hotkeyBox.Text = normalized;
            _loading = false;

            if (normalized == _group.Hotkey)
            {
                RefreshHotkeyStatus();
                return;
            }

            _group.Hotkey = normalized;
            _hotkeyStatus.Text = normalized.Length == 0 ? "Not set" : "Checking...";
            _hotkeyStatus.ForeColor = Theme.Muted;
            ScheduleSave();
            _hotkeyTimer.Stop();
            _hotkeyTimer.Start();
            if (showMessage)
            {
                SetStatus(normalized.Length == 0 ? "Hotkey cleared." : "Hotkey set to " + normalized + ".");
            }
        }

        private void SwatchPicked(object sender, EventArgs e)
        {
            if (_loading || _group == null) return;
            ColorSwatch swatch = sender as ColorSwatch;
            if (swatch == null) return;
            _group.Color = Palette.Normalize(swatch.ColorKey);
            RefreshColorSwatches();
            _groupList.Invalidate();
            ScheduleSave();
            SetStatus("Color set to " + Palette.Label(_group.Color) + ".");
        }

        private void RefreshColorSwatches()
        {
            string selected = _group == null ? "" : Palette.Normalize(_group.Color);
            foreach (KeyValuePair<string, ColorSwatch> pair in _swatches)
            {
                pair.Value.Enabled = _group != null;
                pair.Value.IsSelected = _group != null && string.Equals(pair.Key, selected, StringComparison.OrdinalIgnoreCase);
                pair.Value.Invalidate();
            }
            _colorLabel.Enabled = _group != null;
        }

        public void SetHotkeyStatuses(Dictionary<string, string> statuses)
        {
            _statuses = statuses ?? new Dictionary<string, string>();
            RefreshHotkeyStatus();
        }

        private void RefreshHotkeyStatus()
        {
            if (_hotkeyStatus == null) return;
            if (_group == null)
            {
                _hotkeyStatus.Text = "";
                _hotkeyStatus.ForeColor = Theme.Muted;
                _hotkeyStatus.Tag = null;
                return;
            }

            string message;
            if (!_statuses.TryGetValue(_group.Id, out message))
            {
                _hotkeyStatus.Text = "";
                _hotkeyStatus.ForeColor = Theme.Muted;
                _hotkeyStatus.Tag = null;
                return;
            }

            if (message == null)
            {
                _hotkeyStatus.Text = "Active";
                _hotkeyStatus.ForeColor = Theme.Success;
                _hotkeyStatus.Tag = null;
            }
            else if (message == "No hotkey set")
            {
                _hotkeyStatus.Text = "Not set";
                _hotkeyStatus.ForeColor = Theme.Warning;
                _hotkeyStatus.Tag = null;
            }
            else if (message == "Paused")
            {
                _hotkeyStatus.Text = "Paused";
                _hotkeyStatus.ForeColor = Theme.Muted;
                _hotkeyStatus.Tag = null;
            }
            else
            {
                _hotkeyStatus.Text = message;
                _hotkeyStatus.ForeColor = Theme.Danger;
                _hotkeyStatus.Tag = message;
            }

            if (_hotkeyStatus.Tag != null)
            {
                _savedTip.SetToolTip(_hotkeyStatus, _hotkeyStatus.Tag.ToString());
            }
            else
            {
                _savedTip.SetToolTip(_hotkeyStatus, "");
            }
        }

        private void GroupListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            GroupData group = _groupList.Items[e.Index] as GroupData;
            if (group == null) return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color back = selected ? Theme.RowSelected : Theme.InputBack;
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            int dotSize = 12;
            Rectangle dot = new Rectangle(
                e.Bounds.X + 9,
                e.Bounds.Y + Math.Max(0, (e.Bounds.Height - dotSize) / 2),
                dotSize,
                dotSize);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(Palette.ColorValue(group.Color)))
            {
                e.Graphics.FillEllipse(brush, dot);
            }

            Rectangle textBounds = new Rectangle(
                e.Bounds.X + 28,
                e.Bounds.Y,
                Math.Max(1, e.Bounds.Width - 34),
                e.Bounds.Height);
            TextFormatFlags flags = UiDraw.TextFlags(group.Name, true);
            TextRenderer.DrawText(
                e.Graphics,
                group.Name,
                _groupList.Font,
                textBounds,
                selected ? Color.White : Theme.Text,
                flags);
        }

        private void TemplateListDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            TemplateData template = _templateList.Items[e.Index] as TemplateData;
            if (template == null) return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color back = selected ? Theme.RowSelected : Theme.InputBack;
            using (SolidBrush brush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            Color accent = _group == null ? Theme.Muted : Palette.ColorValue(_group.Color);
            using (SolidBrush brush = new SolidBrush(accent))
            {
                int barHeight = Math.Max(8, e.Bounds.Height - 14);
                e.Graphics.FillRectangle(
                    brush,
                    new Rectangle(e.Bounds.X + 9, e.Bounds.Y + (e.Bounds.Height - barHeight) / 2, 4, barHeight));
            }

            string title = string.IsNullOrEmpty(template.Title) ? "Untitled template" : template.Title;
            Rectangle textBounds = new Rectangle(
                e.Bounds.X + 20,
                e.Bounds.Y,
                Math.Max(1, e.Bounds.Width - 26),
                e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                title,
                _templateList.Font,
                textBounds,
                selected ? Color.White : Theme.Text,
                UiDraw.TextFlags(title, true));
        }

        private void AddGroup()
        {
            bool accepted;
            string initial = _store.UniqueGroupName("New group");
            string value = TextPromptForm.Prompt(this, "New group", "Group name:", initial, out accepted);
            if (!accepted) return;

            string name = value.Trim();
            if (name.Length == 0) name = _store.UniqueGroupName("New group");
            GroupData group = _store.AddGroup(name);
            ReloadGroups(group.Id);
            ScheduleSave();
            if (HotkeysChanged != null) HotkeysChanged(this, EventArgs.Empty);
            SetStatus("Added group '" + group.Name + "'. Set a hotkey for it.");
        }

        private void RenameGroup()
        {
            if (_group == null) return;
            bool accepted;
            string value = TextPromptForm.Prompt(this, "Rename group", "Group name:", _group.Name, out accepted);
            if (!accepted) return;

            string name = value.Trim();
            if (name.Length == 0) return;
            _group.Name = name;
            ReloadGroups(_group.Id);
            ScheduleSave();
            SetStatus("Group renamed.");
        }

        private void DeleteGroup()
        {
            if (_group == null) return;
            DialogResult answer = MessageBox.Show(
                this,
                "Delete group '" + _group.Name + "' and all of its templates?",
                "Delete group",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            string name = _group.Name;
            _store.RemoveGroup(_group.Id);
            _group = null;
            _template = null;
            ReloadGroups(null);
            ScheduleSave();
            if (HotkeysChanged != null) HotkeysChanged(this, EventArgs.Empty);
            SetStatus("Deleted group '" + name + "'.");
        }

        private void MoveGroup(int delta)
        {
            if (_group == null) return;
            if (_store.MoveGroup(_group.Id, delta))
            {
                ReloadGroups(_group.Id);
                ScheduleSave();
            }
        }

        private void AddTemplate()
        {
            if (_group == null) return;
            TemplateData template = _store.AddTemplate(_group);
            if (template == null) return;
            ReloadTemplates(template.Id);
            ScheduleSave();
            _titleBox.Focus();
            _titleBox.SelectAll();
            SetStatus("Added a template. Type a title and its text.");
        }

        private void DuplicateTemplate()
        {
            if (_group == null || _template == null) return;
            string sourceTitle = _template.Title;
            TemplateData copy = _store.DuplicateTemplate(_group, _template);
            if (copy == null) return;
            ReloadTemplates(copy.Id);
            ScheduleSave();
            SetStatus("Duplicated template '" + sourceTitle + "'.");
        }

        private void DeleteTemplate()
        {
            if (_group == null || _template == null) return;
            DialogResult answer = MessageBox.Show(
                this,
                "Delete template '" + _template.Title + "'?",
                "Delete template",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            string title = _template.Title;
            _store.RemoveTemplate(_group, _template.Id);
            _template = null;
            ReloadTemplates(null);
            ScheduleSave();
            SetStatus("Deleted template '" + title + "'.");
        }

        private void MoveTemplate(int delta)
        {
            if (_group == null || _template == null) return;
            if (_store.MoveTemplate(_group, _template, delta))
            {
                ReloadTemplates(_template.Id);
                ScheduleSave();
            }
        }

        private void ScheduleSave()
        {
            SetSavedState("Saving...", Theme.Warning);
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void SaveNow()
        {
            _saveTimer.Stop();
            try
            {
                _store.Save();
                SetSavedState("All changes saved", Theme.Muted);
                _savedTip.SetToolTip(_savedLabel, "Changes are saved automatically.");
            }
            catch (Exception ex)
            {
                Log.Error("Could not save PasteNotes data", ex);
                SetSavedState("Save failed - click to retry", Theme.Danger);
                _savedTip.SetToolTip(_savedLabel, ex.Message);
                SetStatus("Could not save data: " + ex.Message);
            }
        }

        private void SetSavedState(string text, Color color)
        {
            _savedLabel.Text = text;
            _savedLabel.ForeColor = color;
        }

        private void SetStatus(string message)
        {
            _statusLabel.Text = message;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        public void FlushSave()
        {
            _saveTimer.Stop();
            SaveNow();
        }

        public void PrepareExit()
        {
            _allowClose = true;
            _saveTimer.Stop();
            _hotkeyTimer.Stop();
            _statusTimer.Stop();
            SaveNow();
        }

        public void ShowManager()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            NativeMethods.ForceForeground(Handle);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _saveTimer.Stop();
            SaveNow();
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
                if (HiddenToTray != null) HiddenToTray(this, EventArgs.Empty);
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_groupList != null && _groupList.SelectedIndex >= 0)
            {
                _groupList.Focus();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_saveTimer != null) _saveTimer.Dispose();
                if (_hotkeyTimer != null) _hotkeyTimer.Dispose();
                if (_statusTimer != null) _statusTimer.Dispose();
                if (_savedTip != null) _savedTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
