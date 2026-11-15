using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PasteNotes
{
    internal sealed class HotkeyException : Exception
    {
        public HotkeyException(string message) : base(message) { }
    }

    internal sealed class HotkeySpec
    {
        public uint Modifiers;
        public uint VirtualKey;
        public string Text;

        public HotkeySpec(uint modifiers, uint virtualKey, string text)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
            Text = text;
        }
    }

    internal static class HotkeyParser
    {
        private sealed class KeyToken
        {
            public uint VirtualKey;
            public string Display;

            public KeyToken(uint virtualKey, string display)
            {
                VirtualKey = virtualKey;
                Display = display;
            }
        }

        private static readonly Dictionary<string, KeyToken> Aliases =
            new Dictionary<string, KeyToken>(StringComparer.OrdinalIgnoreCase)
            {
                { "BACKSPACE", new KeyToken(0x08, "Backspace") },
                { "TAB", new KeyToken(0x09, "Tab") },
                { "ENTER", new KeyToken(0x0D, "Enter") },
                { "RETURN", new KeyToken(0x0D, "Enter") },
                { "ESC", new KeyToken(0x1B, "Esc") },
                { "ESCAPE", new KeyToken(0x1B, "Esc") },
                { "SPACE", new KeyToken(0x20, "Space") },
                { "PAGEUP", new KeyToken(0x21, "PgUp") },
                { "PGUP", new KeyToken(0x21, "PgUp") },
                { "PAGEDOWN", new KeyToken(0x22, "PgDown") },
                { "PGDN", new KeyToken(0x22, "PgDown") },
                { "END", new KeyToken(0x23, "End") },
                { "HOME", new KeyToken(0x24, "Home") },
                { "LEFT", new KeyToken(0x25, "Left") },
                { "UP", new KeyToken(0x26, "Up") },
                { "RIGHT", new KeyToken(0x27, "Right") },
                { "DOWN", new KeyToken(0x28, "Down") },
                { "INSERT", new KeyToken(0x2D, "Ins") },
                { "INS", new KeyToken(0x2D, "Ins") },
                { "DELETE", new KeyToken(0x2E, "Del") },
                { "DEL", new KeyToken(0x2E, "Del") },
                { "PRINT", new KeyToken(0x2C, "Print") },
                { "PRINTSCREEN", new KeyToken(0x2C, "Print") },
                { "PAUSE", new KeyToken(0x13, "Pause") },
                { "MENU", new KeyToken(0x5D, "Menu") },
                { "HELP", new KeyToken(0x2F, "Help") },
                { "CAPSLOCK", new KeyToken(0x14, "CapsLock") },
                { "NUMLOCK", new KeyToken(0x90, "NumLock") },
                { "SCROLLLOCK", new KeyToken(0x91, "ScrollLock") },
                { "-", new KeyToken(0xBD, "-") },
                { "=", new KeyToken(0xBB, "=") },
                { "[", new KeyToken(0xDB, "[") },
                { "]", new KeyToken(0xDD, "]") },
                { "\\", new KeyToken(0xDC, "\\") },
                { ";", new KeyToken(0xBA, ";") },
                { "'", new KeyToken(0xDE, "'") },
                { ",", new KeyToken(0xBC, ",") },
                { ".", new KeyToken(0xBE, ".") },
                { "/", new KeyToken(0xBF, "/") },
                { "`", new KeyToken(0xC0, "`") },
                { "+", new KeyToken(0xBB, "+") },
                { "PLUS", new KeyToken(0xBB, "+") },
                { "NUM0", new KeyToken(0x60, "Num0") },
                { "NUM1", new KeyToken(0x61, "Num1") },
                { "NUM2", new KeyToken(0x62, "Num2") },
                { "NUM3", new KeyToken(0x63, "Num3") },
                { "NUM4", new KeyToken(0x64, "Num4") },
                { "NUM5", new KeyToken(0x65, "Num5") },
                { "NUM6", new KeyToken(0x66, "Num6") },
                { "NUM7", new KeyToken(0x67, "Num7") },
                { "NUM8", new KeyToken(0x68, "Num8") },
                { "NUM9", new KeyToken(0x69, "Num9") },
                { "NUMADD", new KeyToken(0x6B, "NumAdd") },
                { "NUMSUBTRACT", new KeyToken(0x6D, "NumSubtract") },
                { "NUMMULTIPLY", new KeyToken(0x6A, "NumMultiply") },
                { "NUMDIVIDE", new KeyToken(0x6F, "NumDivide") },
                { "NUMDECIMAL", new KeyToken(0x6E, "NumDecimal") }
            };

        public static HotkeySpec Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                throw new HotkeyException("No hotkey set");
            }

            string work = text.Trim();
            string explicitKey = null;
            if (work.EndsWith("+", StringComparison.Ordinal))
            {
                explicitKey = "+";
                work = work.Substring(0, work.Length - 1);
                if (work.EndsWith("+", StringComparison.Ordinal))
                {
                    work = work.Substring(0, work.Length - 1);
                }
            }

            string[] parts = work.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            uint modifiers = 0;
            string keyName = explicitKey;

            foreach (string partValue in parts)
            {
                string part = partValue.Trim();
                if (part.Length == 0) continue;
                string upper = part.ToUpperInvariant();

                if (upper == "CTRL" || upper == "CONTROL")
                {
                    modifiers |= NativeMethods.MOD_CONTROL;
                    continue;
                }
                if (upper == "ALT")
                {
                    modifiers |= NativeMethods.MOD_ALT;
                    continue;
                }
                if (upper == "SHIFT")
                {
                    modifiers |= NativeMethods.MOD_SHIFT;
                    continue;
                }
                if (upper == "WIN" || upper == "WINDOWS" || upper == "META" || upper == "SUPER")
                {
                    modifiers |= NativeMethods.MOD_WIN;
                    continue;
                }

                if (keyName != null)
                {
                    throw new HotkeyException("Hotkey must contain exactly one key");
                }
                keyName = part;
            }

            if (keyName == null)
            {
                throw new HotkeyException("Hotkey must include a non-modifier key");
            }

            KeyToken token = ResolveKey(keyName);
            if (modifiers == 0 && !IsFunctionKey(token.VirtualKey))
            {
                throw new HotkeyException("Use at least one modifier (Ctrl/Alt/Shift/Win) or an F-key");
            }

            return new HotkeySpec(modifiers, token.VirtualKey, Format(modifiers, token.Display));
        }

        public static string Normalize(string text)
        {
            return Parse(text).Text;
        }

        public static string Format(uint modifiers, string keyDisplay)
        {
            List<string> parts = new List<string>();
            if ((modifiers & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & NativeMethods.MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & NativeMethods.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & NativeMethods.MOD_WIN) != 0) parts.Add("Win");
            parts.Add(keyDisplay);
            return string.Join("+", parts.ToArray());
        }

        public static bool IsFunctionKey(uint virtualKey)
        {
            return virtualKey >= 0x70 && virtualKey <= 0x87;
        }

        public static string DisplayFromKey(Keys keyCode)
        {
            if (keyCode >= Keys.A && keyCode <= Keys.Z)
            {
                return ((char)('A' + (int)keyCode - (int)Keys.A)).ToString();
            }
            if (keyCode >= Keys.D0 && keyCode <= Keys.D9)
            {
                return ((char)('0' + (int)keyCode - (int)Keys.D0)).ToString();
            }
            if (keyCode >= Keys.NumPad0 && keyCode <= Keys.NumPad9)
            {
                return "Num" + ((int)keyCode - (int)Keys.NumPad0).ToString(CultureInfo.InvariantCulture);
            }
            if (keyCode >= Keys.F1 && keyCode <= Keys.F24)
            {
                return "F" + ((int)keyCode - (int)Keys.F1 + 1).ToString(CultureInfo.InvariantCulture);
            }

            switch (keyCode)
            {
                case Keys.Back: return "Backspace";
                case Keys.Tab: return "Tab";
                case Keys.Return: return "Enter";
                case Keys.Escape: return "Esc";
                case Keys.Space: return "Space";
                case Keys.PageUp: return "PgUp";
                case Keys.PageDown: return "PgDown";
                case Keys.End: return "End";
                case Keys.Home: return "Home";
                case Keys.Left: return "Left";
                case Keys.Up: return "Up";
                case Keys.Right: return "Right";
                case Keys.Down: return "Down";
                case Keys.Insert: return "Ins";
                case Keys.Delete: return "Del";
                case Keys.PrintScreen: return "Print";
                case Keys.Pause: return "Pause";
                case Keys.Apps: return "Menu";
                case Keys.Help: return "Help";
                case Keys.CapsLock: return "CapsLock";
                case Keys.NumLock: return "NumLock";
                case Keys.Scroll: return "ScrollLock";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemPipe: return "\\";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.Oemtilde: return "`";
                case Keys.Add: return "NumAdd";
                case Keys.Subtract: return "NumSubtract";
                case Keys.Multiply: return "NumMultiply";
                case Keys.Divide: return "NumDivide";
                case Keys.Decimal: return "NumDecimal";
                default: return null;
            }
        }

        private static KeyToken ResolveKey(string name)
        {
            string raw = name.Trim();
            KeyToken token;
            if (Aliases.TryGetValue(raw, out token)) return token;

            if (raw.Length == 1)
            {
                char c = char.ToUpperInvariant(raw[0]);
                if (c >= 'A' && c <= 'Z') return new KeyToken((uint)c, c.ToString());
                if (c >= '0' && c <= '9') return new KeyToken((uint)c, c.ToString());
            }

            string upper = raw.ToUpperInvariant();
            if (upper.StartsWith("F", StringComparison.Ordinal) && upper.Length > 1)
            {
                int number;
                if (int.TryParse(upper.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out number))
                {
                    if (number >= 1 && number <= 24)
                    {
                        return new KeyToken((uint)(0x70 + number - 1), "F" + number.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            throw new HotkeyException("Unsupported key: " + name);
        }
    }

    internal sealed class HotkeyManager : IDisposable
    {
        private readonly HotkeyHost _host;
        private readonly Dictionary<int, string> _registered;
        private int _nextId;

        public event Action<string> HotkeyPressed;

        public HotkeyManager()
        {
            _registered = new Dictionary<int, string>();
            _nextId = 1;
            _host = new HotkeyHost(this);
        }

        public Dictionary<string, string> Apply(List<GroupData> groups, bool paused)
        {
            UnregisterAll();
            Dictionary<string, string> results = new Dictionary<string, string>();
            if (paused)
            {
                foreach (GroupData group in groups) results[group.Id] = "Paused";
                return results;
            }

            Dictionary<string, string> used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (GroupData group in groups)
            {
                if (string.IsNullOrEmpty(group.Hotkey) || group.Hotkey.Trim().Length == 0)
                {
                    results[group.Id] = "No hotkey set";
                    continue;
                }

                HotkeySpec spec;
                try
                {
                    spec = HotkeyParser.Parse(group.Hotkey);
                }
                catch (HotkeyException ex)
                {
                    results[group.Id] = ex.Message;
                    continue;
                }

                if (used.ContainsKey(spec.Text))
                {
                    results[group.Id] = "Conflicts with group '" + used[spec.Text] + "'";
                    continue;
                }
                used[spec.Text] = group.Name;

                int id = _nextId;
                _nextId++;
                bool ok = NativeMethods.RegisterHotKey(
                    _host.Handle,
                    id,
                    spec.Modifiers | NativeMethods.MOD_NOREPEAT,
                    spec.VirtualKey);
                if (!ok)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED)
                    {
                        results[group.Id] = "Hotkey is already in use by another app";
                    }
                    else
                    {
                        results[group.Id] = "Could not register hotkey (error " +
                            error.ToString(CultureInfo.InvariantCulture) + ")";
                    }
                    continue;
                }

                _registered[id] = group.Id;
                results[group.Id] = null;
            }
            return results;
        }

        public void UnregisterAll()
        {
            foreach (int id in new List<int>(_registered.Keys))
            {
                NativeMethods.UnregisterHotKey(_host.Handle, id);
            }
            _registered.Clear();
            _nextId = 1;
        }

        internal void Dispatch(int id)
        {
            string groupId;
            if (_registered.TryGetValue(id, out groupId))
            {
                Action<string> handler = HotkeyPressed;
                if (handler != null) handler(groupId);
            }
        }

        public void Dispose()
        {
            UnregisterAll();
            _host.Destroy();
        }
    }

    internal sealed class HotkeyHost : NativeWindow
    {
        private readonly HotkeyManager _manager;

        public HotkeyHost(HotkeyManager manager)
        {
            _manager = manager;
            CreateParams parameters = new CreateParams();
            parameters.Caption = "PasteNotesHotkeyHost";
            parameters.Parent = new IntPtr(-3);
            CreateHandle(parameters);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WM_HOTKEY)
            {
                _manager.Dispatch(message.WParam.ToInt32());
                return;
            }
            base.WndProc(ref message);
        }

        public void Destroy()
        {
            if (Handle != IntPtr.Zero) DestroyHandle();
        }
    }
}
