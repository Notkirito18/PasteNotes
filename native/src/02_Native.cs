using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PasteNotes
{
    internal static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;
        public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        public const int SW_RESTORE = 9;
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const int VK_CONTROL = 0x11;
        public const int VK_V = 0x56;
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int valueSize);

        public static void TryEnableDarkTitleBar(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            int enabled = 1;
            int result = DwmSetWindowAttribute(hWnd, 20, ref enabled, 4);
            if (result != 0)
            {
                DwmSetWindowAttribute(hWnd, 19, ref enabled, 4);
            }
        }

        public static bool IsKeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        public static bool IsOwnedByCurrentProcess(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);
            return processId == (uint)Process.GetCurrentProcess().Id;
        }

        public static string GetWindowClass(IntPtr hWnd)
        {
            StringBuilder builder = new StringBuilder(256);
            GetClassName(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        public static string GetWindowTitle(IntPtr hWnd)
        {
            StringBuilder builder = new StringBuilder(512);
            GetWindowText(hWnd, builder, builder.Capacity);
            return builder.ToString();
        }

        public static bool ForceForeground(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return false;
            if (GetForegroundWindow() == hWnd) return true;

            if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);

            IntPtr foreground = GetForegroundWindow();
            uint currentThread = GetCurrentThreadId();
            uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, IntPtr.Zero);
            uint targetThread = GetWindowThreadProcessId(hWnd, IntPtr.Zero);
            bool attachedForeground = false;
            bool attachedTarget = false;

            try
            {
                if (foregroundThread != 0 && foregroundThread != currentThread)
                {
                    attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);
                }
                if (targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread)
                {
                    attachedTarget = AttachThreadInput(currentThread, targetThread, true);
                }

                SetForegroundWindow(hWnd);
                BringWindowToTop(hWnd);
                SetFocus(hWnd);
            }
            finally
            {
                if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
                if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
            }

            if (GetForegroundWindow() != hWnd)
            {
                try { SwitchToThisWindow(hWnd, true); }
                catch { }
            }
            return GetForegroundWindow() == hWnd;
        }

        public static bool SendPaste()
        {
            INPUT[] inputs = new INPUT[4];
            inputs[0] = KeyInput((ushort)VK_CONTROL, false);
            inputs[1] = KeyInput((ushort)VK_V, false);
            inputs[2] = KeyInput((ushort)VK_V, true);
            inputs[3] = KeyInput((ushort)VK_CONTROL, true);
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return sent == (uint)inputs.Length;
        }

        private static INPUT KeyInput(ushort virtualKey, bool keyUp)
        {
            INPUT input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.U.ki.wVk = virtualKey;
            input.U.ki.wScan = 0;
            input.U.ki.dwFlags = keyUp ? KEYEVENTF_KEYUP : 0;
            input.U.ki.time = 0;
            input.U.ki.dwExtraInfo = UIntPtr.Zero;
            return input;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    internal static class IconFactory
    {
        private static Icon _applicationIcon;

        public static Icon ApplicationIcon
        {
            get
            {
                if (_applicationIcon == null) _applicationIcon = Create(false);
                return _applicationIcon;
            }
        }

        public static Icon Create(bool paused)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                Color noteColor = paused ? Color.FromArgb(154, 160, 170) : Color.FromArgb(243, 193, 75);
                Color clipColor = Color.FromArgb(42, 47, 58);
                Color lineColor = Color.FromArgb(77, 58, 18);

                using (GraphicsPath note = UiHelpers.RoundRect(new Rectangle(6, 5, 20, 23), 4))
                using (SolidBrush brush = new SolidBrush(noteColor))
                {
                    graphics.FillPath(brush, note);
                }
                using (GraphicsPath clip = UiHelpers.RoundRect(new Rectangle(11, 3, 10, 6), 2))
                using (SolidBrush brush = new SolidBrush(clipColor))
                {
                    graphics.FillPath(brush, clip);
                }
                using (Pen pen = new Pen(lineColor, 1.6f))
                {
                    graphics.DrawLine(pen, 10, 15, 22, 15);
                    graphics.DrawLine(pen, 10, 19, 22, 19);
                    graphics.DrawLine(pen, 10, 23, 18, 23);
                }

                if (paused)
                {
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(30, 35, 48)))
                    {
                        graphics.FillEllipse(brush, 18, 18, 12, 12);
                    }
                    using (SolidBrush brush = new SolidBrush(Color.FromArgb(243, 193, 75)))
                    {
                        graphics.FillRectangle(brush, 21, 21, 2, 6);
                        graphics.FillRectangle(brush, 25, 21, 2, 6);
                    }
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    NativeMethods.DestroyIcon(handle);
                }
            }
        }
    }

    internal static class UiHelpers
    {
        public const int EM_SETCUEBANNER = 0x1501;

        public static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(1, radius * 2);
            if (diameter > bounds.Width) diameter = bounds.Width;
            if (diameter > bounds.Height) diameter = bounds.Height;
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void SetCueBanner(TextBox box, string text)
        {
            if (box == null || box.IsHandleCreated == false) return;
            NativeMethods.SendMessage(box.Handle, EM_SETCUEBANNER, new IntPtr(1), text);
        }

        public static bool IsShellWindow(IntPtr hWnd)
        {
            string className = NativeMethods.GetWindowClass(hWnd);
            return string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase)
                || string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase)
                || string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase);
        }

        public static int MeasureListHeight(Font font, int padding)
        {
            return Math.Max(18, TextRenderer.MeasureText("Ag", font).Height + padding);
        }
    }
}
