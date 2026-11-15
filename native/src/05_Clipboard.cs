using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PasteNotes
{
    internal sealed class ClipboardSnapshot
    {
        public DataObject Data;
        public bool HasRestorableData;
        public bool IsEmpty;
        public uint SequenceAtCapture;
    }

    internal static class ClipboardService
    {
        public static ClipboardSnapshot Capture()
        {
            try
            {
                IDataObject source = Clipboard.GetDataObject();
                ClipboardSnapshot snapshot = new ClipboardSnapshot();
                snapshot.Data = new DataObject();
                snapshot.SequenceAtCapture = NativeMethods.GetClipboardSequenceNumber();
                snapshot.IsEmpty = true;
                if (source == null) return snapshot;

                string[] formats = source.GetFormats(false);
                if (formats == null || formats.Length == 0) return snapshot;
                snapshot.IsEmpty = false;

                foreach (string format in formats)
                {
                    if (string.IsNullOrEmpty(format) || IsUnsafeFormat(format)) continue;
                    object value = null;
                    try { value = source.GetData(format, false); }
                    catch { continue; }

                    object copy = CopyValue(value);
                    if (copy == null) continue;
                    try
                    {
                        snapshot.Data.SetData(format, copy);
                        snapshot.HasRestorableData = true;
                    }
                    catch
                    {
                    }
                }
                return snapshot;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not snapshot the clipboard: " + ex.Message);
                return null;
            }
        }

        public static bool SetText(string text)
        {
            string value = text ?? "";
            for (int attempt = 0; attempt < 6; attempt++)
            {
                try
                {
                    Clipboard.SetText(value, TextDataFormat.UnicodeText);
                    return Clipboard.ContainsText(TextDataFormat.UnicodeText);
                }
                catch (ExternalException)
                {
                    Thread.Sleep(25);
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not write text to the clipboard: " + ex.Message);
                    return false;
                }
            }
            return false;
        }

        public static bool Clear()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                try
                {
                    Clipboard.Clear();
                    return true;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(25);
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not clear the clipboard: " + ex.Message);
                    return false;
                }
            }
            return false;
        }

        public static bool Restore(ClipboardSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Data == null) return false;
            if (!snapshot.HasRestorableData) return Clear();
            for (int attempt = 0; attempt < 6; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(snapshot.Data, true);
                    return true;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(25);
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not restore the clipboard: " + ex.Message);
                    return false;
                }
            }
            return false;
        }

        private static bool IsUnsafeFormat(string format)
        {
            return string.Equals(format, DataFormats.MetafilePict, StringComparison.OrdinalIgnoreCase)
                || string.Equals(format, DataFormats.Palette, StringComparison.OrdinalIgnoreCase)
                || string.Equals(format, DataFormats.EnhancedMetafile, StringComparison.OrdinalIgnoreCase)
                || string.Equals(format, DataFormats.Serializable, StringComparison.OrdinalIgnoreCase);
        }

        private static object CopyValue(object value)
        {
            if (value == null) return null;

            string text = value as string;
            if (text != null) return text;

            string[] files = value as string[];
            if (files != null) return (string[])files.Clone();

            byte[] bytes = value as byte[];
            if (bytes != null) return (byte[])bytes.Clone();

            Bitmap bitmap = value as Bitmap;
            if (bitmap != null) return new Bitmap(bitmap);

            Image image = value as Image;
            if (image != null) return new Bitmap(image);

            MemoryStream memory = value as MemoryStream;
            if (memory != null) return memory.ToArray();

            Stream stream = value as Stream;
            if (stream != null)
            {
                using (MemoryStream copy = new MemoryStream())
                {
                    stream.Position = 0;
                    stream.CopyTo(copy);
                    return copy.ToArray();
                }
            }

            return null;
        }
    }

    internal sealed class PasteService : IDisposable
    {
        private ClipboardSnapshot _pending;
        private uint _sequenceAfterSet;
        private System.Windows.Forms.Timer _restoreTimer;

        public string LastError { get; private set; }

        public bool Paste(string text, IntPtr target, bool restoreClipboard)
        {
            LastError = null;

            if (string.IsNullOrEmpty(text))
            {
                LastError = "This template is empty.";
                return false;
            }
            if (target == IntPtr.Zero || !NativeMethods.IsWindow(target))
            {
                LastError = "The target window is no longer open.";
                return false;
            }
            if (NativeMethods.IsOwnedByCurrentProcess(target))
            {
                LastError = "The target window belongs to PasteNotes.";
                return false;
            }
            if (UiHelpers.IsShellWindow(target))
            {
                LastError = "No editable window was selected. Click the window you want to paste into, then use the hotkey again.";
                return false;
            }

            ClipboardSnapshot snapshot = null;
            if (restoreClipboard)
            {
                snapshot = ClipboardService.Capture();
                if (snapshot == null)
                {
                    LastError = "The clipboard is busy. Paste cancelled to avoid losing its contents.";
                    return false;
                }
                if (!snapshot.HasRestorableData && !snapshot.IsEmpty)
                {
                    LastError = "The clipboard contains a format PasteNotes cannot safely preserve. Disable 'Restore previous clipboard' or copy text first.";
                    return false;
                }
            }

            if (!ClipboardService.SetText(text))
            {
                RestoreNow(snapshot);
                LastError = "Could not write the template to the clipboard.";
                return false;
            }

            uint sequence = NativeMethods.GetClipboardSequenceNumber();

            if (!NativeMethods.ForceForeground(target))
            {
                RestoreNow(snapshot);
                LastError = "Could not restore focus to the target window.";
                return false;
            }

            Thread.Sleep(70);

            if (NativeMethods.GetForegroundWindow() != target)
            {
                RestoreNow(snapshot);
                LastError = "The target window changed before the paste could be sent.";
                return false;
            }

            if (!NativeMethods.SendPaste())
            {
                RestoreNow(snapshot);
                LastError = "Windows did not accept the paste keystroke.";
                return false;
            }

            if (restoreClipboard && snapshot != null)
            {
                ScheduleRestore(snapshot, sequence);
            }
            return true;
        }

        public void RestorePending()
        {
            CancelRestoreTimer();
            if (_pending != null)
            {
                if (NativeMethods.GetClipboardSequenceNumber() == _sequenceAfterSet)
                {
                    ClipboardService.Restore(_pending);
                }
                _pending = null;
            }
        }

        private void ScheduleRestore(ClipboardSnapshot snapshot, uint sequence)
        {
            CancelRestoreTimer();
            _pending = snapshot;
            _sequenceAfterSet = sequence;
            _restoreTimer = new System.Windows.Forms.Timer();
            _restoreTimer.Interval = 350;
            _restoreTimer.Tick += delegate
            {
                CancelRestoreTimer();
                if (_pending == null) return;
                if (NativeMethods.GetClipboardSequenceNumber() == _sequenceAfterSet)
                {
                    ClipboardService.Restore(_pending);
                }
                else
                {
                    Log.Info("Clipboard changed during paste; skipped restoring the previous contents.");
                }
                _pending = null;
            };
            _restoreTimer.Start();
        }

        private void CancelRestoreTimer()
        {
            if (_restoreTimer != null)
            {
                _restoreTimer.Stop();
                _restoreTimer.Dispose();
                _restoreTimer = null;
            }
        }

        private static void RestoreNow(ClipboardSnapshot snapshot)
        {
            if (snapshot != null)
            {
                ClipboardService.Restore(snapshot);
            }
        }

        public void Dispose()
        {
            RestorePending();
        }
    }
}
