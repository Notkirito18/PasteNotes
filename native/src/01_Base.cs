using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PasteNotes
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            NativeMethods.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            if (HasArgument(args, "--selftest"))
            {
                return SelfTest.Run();
            }

            bool createdNew;
            Mutex mutex = new Mutex(true, SingleInstance.MutexName, out createdNew);
            if (!createdNew)
            {
                SingleInstance.SignalShow();
                mutex.Dispose();
                return 0;
            }

            try
            {
                AppPaths.Initialize();
                Log.Initialize(AppPaths.LogFile);
                Log.Info("PasteNotes starting");
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                using (PasteNotesContext context = new PasteNotesContext(args))
                {
                    Application.Run(context);
                }
                return 0;
            }
            catch (DataVersionException ex)
            {
                Log.Error("Data version is newer than this build", ex);
                MessageBox.Show(
                    "The PasteNotes data file was created by a newer version.\r\n\r\n" +
                    AppPaths.DataFile + "\r\n\r\n" +
                    "This build will not modify it. Use a newer PasteNotes build.",
                    "PasteNotes",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
            catch (Exception ex)
            {
                Log.Error("Fatal startup error", ex);
                MessageBox.Show(
                    "PasteNotes could not start.\r\n\r\n" + ex.Message +
                    "\r\n\r\nLog: " + AppPaths.LogFile,
                    "PasteNotes",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
            finally
            {
                if (createdNew)
                {
                    try { mutex.ReleaseMutex(); }
                    catch { }
                }
                mutex.Dispose();
                Log.Info("PasteNotes stopped");
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

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Log.Error("Unhandled UI exception", e.Exception);
            MessageBox.Show(
                "PasteNotes hit an unexpected error.\r\n\r\n" + e.Exception.Message +
                "\r\n\r\nLog: " + AppPaths.LogFile,
                "PasteNotes",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            Log.Error("Unhandled application exception", ex);
        }
    }

    internal static class SingleInstance
    {
        public const string MutexName = "Local\\PasteNotes.SingleInstance.v1";
        public const string ShowEventName = "Local\\PasteNotes.ShowManager.v1";

        public static void SignalShow()
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    using (EventWaitHandle handle = EventWaitHandle.OpenExisting(ShowEventName))
                    {
                        handle.Set();
                        return;
                    }
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
                catch
                {
                    return;
                }
            }
        }
    }

    internal static class AppPaths
    {
        public static string DataDirectory { get; private set; }
        public static bool Portable { get; private set; }

        public static string DataFile
        {
            get { return Path.Combine(DataDirectory, "data.json"); }
        }

        public static string LogFile
        {
            get { return Path.Combine(DataDirectory, "pasteNotes.log"); }
        }

        public static string ExeDirectory
        {
            get { return Path.GetDirectoryName(Application.ExecutablePath); }
        }

        public static void Initialize()
        {
            string overrideDirectory = Environment.GetEnvironmentVariable("PASTENOTES_DATA_DIR");
            if (!string.IsNullOrEmpty(overrideDirectory))
            {
                DataDirectory = Path.GetFullPath(overrideDirectory);
                Directory.CreateDirectory(DataDirectory);
                Portable = false;
                return;
            }

            string exeDirectory = ExeDirectory;
            string marker = Path.Combine(exeDirectory, "PasteNotes.portable");
            if (File.Exists(marker))
            {
                string candidate = Path.Combine(exeDirectory, "PasteNotesData");
                if (IsWritableDirectory(candidate))
                {
                    DataDirectory = candidate;
                    Portable = true;
                    return;
                }
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData))
            {
                appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData",
                    "Roaming");
            }
            DataDirectory = Path.Combine(appData, "PasteNotes");
            Directory.CreateDirectory(DataDirectory);
            Portable = false;
        }

        private static bool IsWritableDirectory(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class Log
    {
        private const long MaxBytes = 1000000;
        private static readonly object Sync = new object();
        private static string _path;

        public static void Initialize(string path)
        {
            try
            {
                _path = path;
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            }
            catch
            {
                _path = null;
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message, Exception exception)
        {
            string text = message;
            if (exception != null)
            {
                text += "\r\n" + exception;
            }
            Write("ERROR", text);
        }

        private static void Write(string level, string message)
        {
            if (string.IsNullOrEmpty(_path)) return;
            lock (Sync)
            {
                try
                {
                    RotateIfNeeded();
                    string line = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0:yyyy-MM-dd HH:mm:ss} {1} {2}\r\n",
                        DateTime.Now,
                        level,
                        message);
                    File.AppendAllText(_path, line, new UTF8Encoding(false));
                }
                catch
                {
                }
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(_path)) return;
                FileInfo info = new FileInfo(_path);
                if (info.Length < MaxBytes) return;

                for (int index = 3; index >= 1; index--)
                {
                    string source = _path + "." + index.ToString(CultureInfo.InvariantCulture);
                    string target = _path + "." + (index + 1).ToString(CultureInfo.InvariantCulture);
                    if (File.Exists(source))
                    {
                        if (File.Exists(target)) File.Delete(target);
                        File.Move(source, target);
                    }
                }
                string first = _path + ".1";
                if (File.Exists(first)) File.Delete(first);
                File.Move(_path, first);
            }
            catch
            {
            }
        }
    }

    internal static class Palette
    {
        public const string DefaultKey = "yellow";
        public const string TextOnColorHex = "#1b202b";

        private static readonly string[] Order = new string[]
        {
            "yellow", "green", "blue", "pink", "purple", "orange", "red", "gray"
        };

        private static readonly Dictionary<string, string> Values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "yellow", "#F2C94C" },
                { "green", "#6FCF97" },
                { "blue", "#56A3E8" },
                { "pink", "#EE8FBE" },
                { "purple", "#A98BE0" },
                { "orange", "#F2994A" },
                { "red", "#EB5757" },
                { "gray", "#9AA0AA" }
            };

        public static string Normalize(string key)
        {
            if (!string.IsNullOrEmpty(key) && Values.ContainsKey(key)) return key.ToLowerInvariant();
            return DefaultKey;
        }

        public static string HexValue(string key)
        {
            return Values[Normalize(key)];
        }

        public static Color ColorValue(string key)
        {
            return ColorTranslator.FromHtml(HexValue(key));
        }

        public static string Label(string key)
        {
            string normalized = Normalize(key);
            return char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
        }

        public static string[] Keys()
        {
            return (string[])Order.Clone();
        }
    }

    internal static class RtlHelper
    {
        public static bool IsRtl(string text)
        {
            return DirectionOf(text) == 1;
        }

        private static int DirectionOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            for (int index = 0; index < text.Length; index++)
            {
                char c = text[index];
                int code = c;

                if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    int point = char.ConvertToUtf32(c, text[index + 1]);
                    if (point >= 0x10800 && point <= 0x10FFF) return 1;
                    if (point >= 0x1E800 && point <= 0x1EFFF) return 1;
                    index++;
                    continue;
                }

                if (code >= 0x0590 && code <= 0x05FF) return 1;
                if (code >= 0x0600 && code <= 0x06FF)
                {
                    if ((code >= 0x0660 && code <= 0x0669) || (code >= 0x06F0 && code <= 0x06F9))
                    {
                        continue;
                    }
                    return 1;
                }
                if (code >= 0x0750 && code <= 0x077F) return 1;
                if (code >= 0x08A0 && code <= 0x08FF) return 1;
                if (code >= 0xFB1D && code <= 0xFDFF) return 1;
                if (code >= 0xFE70 && code <= 0xFEFF) return 1;

                if ((code >= 'A' && code <= 'Z') || (code >= 'a' && code <= 'z')) return -1;
                if (char.IsLetter(c)) return -1;
            }
            return 0;
        }

        public static void Apply(TextBox box)
        {
            if (box == null) return;
            ApplyDirection(box, IsRtl(box.Text));
        }

        public static void ApplyEditor(TextBox box)
        {
            if (box == null) return;
            string text = box.Text;
            if (string.IsNullOrEmpty(text))
            {
                ApplyDirection(box, false);
                return;
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            int caret = box.SelectionStart;
            if (caret < 0) caret = 0;
            if (caret > normalized.Length) caret = normalized.Length;

            int lineIndex = 0;
            for (int index = 0; index < caret; index++)
            {
                if (normalized[index] == '\n') lineIndex++;
            }

            string[] lines = normalized.Split('\n');
            int direction = 0;

            if (lineIndex < lines.Length) direction = DirectionOf(lines[lineIndex]);
            for (int index = lineIndex - 1; index >= 0 && direction == 0; index--)
            {
                direction = DirectionOf(lines[index]);
            }
            for (int index = 0; index < lines.Length && direction == 0; index++)
            {
                direction = DirectionOf(lines[index]);
            }

            ApplyDirection(box, direction == 1);
        }

        private static void ApplyDirection(TextBox box, bool rtl)
        {
            RightToLeft desired = rtl ? RightToLeft.Yes : RightToLeft.No;
            HorizontalAlignment alignment = rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            if (box.RightToLeft != desired)
            {
                box.RightToLeft = desired;
            }
            if (box.TextAlign != alignment)
            {
                box.TextAlign = alignment;
            }
        }
    }
}
