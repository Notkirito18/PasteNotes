using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PasteNotes
{
    internal sealed class PasteNotesContext : ApplicationContext
    {
        private readonly DataStore _store;
        private readonly ManagerForm _manager;
        private readonly HotkeyManager _hotkeys;
        private readonly PasteService _paste;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _pauseItem;
        private readonly Icon _activeIcon;
        private readonly Icon _pausedIcon;
        private readonly SynchronizationContext _ui;

        private EventWaitHandle _showEvent;
        private RegisteredWaitHandle _showWait;
        private PickerForm _picker;
        private IntPtr _target;
        private bool _paused;
        private bool _exiting;
        private bool _cleaned;
        private bool _trayHintShown;

        public PasteNotesContext(string[] args)
        {
            bool firstRun = !File.Exists(AppPaths.DataFile);

            _store = new DataStore(AppPaths.DataFile);
            _store.Load();

            _manager = new ManagerForm(_store);
            _manager.HotkeysChanged += delegate { ApplyHotkeys(false); };
            _manager.HiddenToTray += ManagerHiddenToTray;
            IntPtr managerHandle = _manager.Handle;
            if (managerHandle == IntPtr.Zero)
            {
                Log.Warn("The manager window handle was not created during startup.");
            }

            _ui = SynchronizationContext.Current;
            _hotkeys = new HotkeyManager();
            _hotkeys.HotkeyPressed += OnHotkey;
            _paste = new PasteService();

            _activeIcon = IconFactory.Create(false);
            _pausedIcon = IconFactory.Create(true);

            _menu = new ContextMenuStrip();
            _menu.Font = Theme.BaseFont;
            _menu.BackColor = Theme.PanelBack;
            _menu.ForeColor = Theme.Text;
            _menu.ShowImageMargin = false;
            _menu.ShowCheckMargin = true;

            ToolStripMenuItem openItem = new ToolStripMenuItem("Open PasteNotes");
            openItem.Click += delegate { ShowManager(); };
            _pauseItem = new ToolStripMenuItem("Pause hotkeys");
            _pauseItem.Click += delegate { SetPaused(!_paused); };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate { ExitApp(); };
            _menu.Items.Add(openItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_pauseItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(exitItem);

            _tray = new NotifyIcon();
            _tray.Icon = _activeIcon;
            _tray.Text = "PasteNotes - snippet hotkeys";
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;
            _tray.MouseClick += TrayMouseClick;

            CreateShowEvent();
            ApplyHotkeys(true);

            if (HasArgument(args, "--show") || firstRun)
            {
                StartOnUi(delegate { ShowManager(); });
            }
        }

        private static bool HasArgument(string[] args, string name)
        {
            if (args == null) return false;
            foreach (string arg in args)
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void CreateShowEvent()
        {
            try
            {
                bool createdNew;
                _showEvent = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    SingleInstance.ShowEventName,
                    out createdNew);
                _showWait = ThreadPool.RegisterWaitForSingleObject(
                    _showEvent,
                    ShowRequested,
                    null,
                    Timeout.Infinite,
                    false);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not create the single-instance show event: " + ex.Message);
            }
        }

        private void ShowRequested(object state, bool timedOut)
        {
            if (_exiting) return;
            StartOnUi(delegate { ShowManager(); });
        }

        private void StartOnUi(Action action)
        {
            if (action == null || _exiting) return;
            SynchronizationContext context = _ui;
            if (context != null)
            {
                context.Post(delegate { action(); }, null);
                return;
            }
            try
            {
                _manager.BeginInvoke(action);
            }
            catch
            {
            }
        }

        private void TrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ShowManager();
        }

        private void ManagerHiddenToTray(object sender, EventArgs e)
        {
            if (_trayHintShown) return;
            _trayHintShown = true;
            ShowBalloon(
                "PasteNotes",
                "PasteNotes is still running. Use the tray icon to open it again.",
                ToolTipIcon.Info);
        }

        public void ShowManager()
        {
            if (_exiting) return;
            _manager.ShowManager();
        }

        private void ApplyHotkeys(bool notify)
        {
            if (_paused)
            {
                _hotkeys.UnregisterAll();
                _manager.SetHotkeyStatuses(PausedStatuses());
                return;
            }

            Dictionary<string, string> results = _hotkeys.Apply(_store.Data.Groups, false);
            _manager.SetHotkeyStatuses(results);

            if (!notify) return;

            List<string> problems = new List<string>();
            foreach (GroupData group in _store.Data.Groups)
            {
                string message;
                if (!results.TryGetValue(group.Id, out message)) continue;
                if (message == null || message == "No hotkey set") continue;
                problems.Add(group.Name + ": " + message);
            }

            if (problems.Count == 0) return;
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            int count = Math.Min(4, problems.Count);
            for (int index = 0; index < count; index++)
            {
                if (index > 0) builder.AppendLine();
                builder.Append(problems[index]);
            }
            if (problems.Count > count) builder.AppendLine("...");
            ShowBalloon("PasteNotes hotkey problem", builder.ToString(), ToolTipIcon.Warning);
        }

        private Dictionary<string, string> PausedStatuses()
        {
            Dictionary<string, string> statuses = new Dictionary<string, string>();
            foreach (GroupData group in _store.Data.Groups)
            {
                statuses[group.Id] = "Paused";
            }
            return statuses;
        }

        private void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            _pauseItem.Checked = paused;
            _tray.Icon = paused ? _pausedIcon : _activeIcon;

            if (paused)
            {
                _hotkeys.UnregisterAll();
                _manager.SetHotkeyStatuses(PausedStatuses());
                ShowBalloon("PasteNotes", "Hotkeys paused", ToolTipIcon.Info);
            }
            else
            {
                ApplyHotkeys(true);
                ShowBalloon("PasteNotes", "Hotkeys resumed", ToolTipIcon.Info);
            }
        }

        private void OnHotkey(string groupId)
        {
            if (_paused || _exiting) return;
            GroupData group = _store.FindGroup(groupId);
            if (group == null) return;

            _target = NativeMethods.GetForegroundWindow();
            ClosePickerSilently();

            PickerForm picker = new PickerForm(group);
            picker.TemplateSelected += OnTemplateSelected;
            picker.Cancelled += OnPickerCancelled;
            _picker = picker;
            picker.OpenAtCursor();
        }

        private void ClosePickerSilently()
        {
            PickerForm previous = _picker;
            _picker = null;
            if (previous == null) return;
            try
            {
                previous.DismissSilently();
                previous.Dispose();
            }
            catch
            {
            }
        }

        private void OnTemplateSelected(string text)
        {
            PickerForm picker = _picker;
            _picker = null;
            IntPtr target = _target;
            bool restore = _store.GetBoolSetting("restore_clipboard", true);

            StartOnUi(delegate
            {
                if (picker != null)
                {
                    try { picker.Dispose(); }
                    catch { }
                }
                PasteAndReport(text, target, restore);
            });
        }

        private void PasteAndReport(string text, IntPtr target, bool restore)
        {
            if (_exiting) return;
            bool pasted = _paste.Paste(text, target, restore);
            if (pasted) return;
            string error = _paste.LastError;
            if (string.IsNullOrEmpty(error)) error = "The template could not be pasted.";
            Log.Warn("Paste failed: " + error);
            ShowBalloon("PasteNotes - paste failed", error, ToolTipIcon.Warning);
        }

        private void OnPickerCancelled(bool refocus)
        {
            PickerForm picker = _picker;
            _picker = null;
            IntPtr target = _target;

            StartOnUi(delegate
            {
                if (picker != null)
                {
                    try { picker.Dispose(); }
                    catch { }
                }
                if (refocus && target != IntPtr.Zero && NativeMethods.IsWindow(target))
                {
                    NativeMethods.ForceForeground(target);
                }
            });
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            if (_tray == null || !_tray.Visible) return;
            if (string.IsNullOrEmpty(text)) return;
            if (text.Length > 240) text = text.Substring(0, 237) + "...";
            try
            {
                _tray.ShowBalloonTip(5000, title, text, icon);
            }
            catch
            {
            }
        }

        public void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            Cleanup();
            ExitThread();
        }

        private void Cleanup()
        {
            if (_cleaned) return;
            _cleaned = true;

            if (_picker != null)
            {
                try
                {
                    _picker.DismissSilently();
                    _picker.Dispose();
                }
                catch
                {
                }
                _picker = null;
            }

            try { _hotkeys.UnregisterAll(); }
            catch { }

            try
            {
                _manager.PrepareExit();
                _manager.Hide();
            }
            catch { }

            if (_showWait != null)
            {
                try { _showWait.Unregister(null); }
                catch { }
                _showWait = null;
            }
            if (_showEvent != null)
            {
                try { _showEvent.Close(); }
                catch { }
                _showEvent = null;
            }

            try { _tray.Visible = false; }
            catch { }

            try { _hotkeys.Dispose(); } catch { }
            try { _paste.Dispose(); } catch { }
            try { _manager.Dispose(); } catch { }
            try { _menu.Dispose(); } catch { }
            try { _tray.Dispose(); } catch { }
            try { _activeIcon.Dispose(); } catch { }
            try { _pausedIcon.Dispose(); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _exiting = true;
                Cleanup();
            }
            base.Dispose(disposing);
        }
    }
}
