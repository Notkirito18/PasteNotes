using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace PasteNotes
{
    internal static class SelfTest
    {
        private static StringBuilder _log;
        private static int _failures;
        private static int _checks;

        public static string LogPath
        {
            get { return Path.Combine(Path.GetTempPath(), "PasteNotesSelfTest.log"); }
        }

        public static int Run()
        {
            _log = new StringBuilder();
            _failures = 0;
            _checks = 0;
            string temporary = null;

            WriteLine("PasteNotes self-test");
            try
            {
                TestDefaults();
                TestJson();
                TestStore(out temporary);
                TestHotkeys();
                TestRtl();
                TestPalette();
                TestIcons();
            }
            catch (Exception ex)
            {
                Fail("Unexpected self-test error: " + ex);
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporary))
                {
                    try { Directory.Delete(temporary, true); }
                    catch { }
                }
            }

            string result = _failures == 0
                ? "PASS - " + _checks.ToString(CultureInfo.InvariantCulture) + " checks"
                : "FAIL - " + _failures.ToString(CultureInfo.InvariantCulture) +
                  " of " + _checks.ToString(CultureInfo.InvariantCulture) + " checks failed";
            WriteLine(result);

            try
            {
                File.WriteAllText(LogPath, _log.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }

            return _failures == 0 ? 0 : 1;
        }

        private static void TestDefaults()
        {
            AppData data = Defaults.CreateAppData();
            Check(data.Groups.Count == 8, "defaults contain 8 groups (found " + data.Groups.Count + ")");

            int templateCount = 0;
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (GroupData group in data.Groups)
            {
                templateCount += group.Templates.Count;
                Check(!string.IsNullOrEmpty(group.Id), "group id is set");
                Check(ids.Add(group.Id), "group id is unique");
                Check(!string.IsNullOrEmpty(group.Name), "group name is set");
                Check(!string.IsNullOrEmpty(group.Hotkey), "group hotkey is set");
                Check(Palette.Normalize(group.Color) == group.Color, "group color is normalized");

                try
                {
                    HotkeyParser.Parse(group.Hotkey);
                    Check(true, "group hotkey parses: " + group.Hotkey);
                }
                catch (HotkeyException ex)
                {
                    Fail("group hotkey does not parse (" + group.Hotkey + "): " + ex.Message);
                }

                foreach (TemplateData template in group.Templates)
                {
                    Check(!string.IsNullOrEmpty(template.Id), "template id is set");
                    Check(ids.Add(template.Id), "template id is unique");
                    Check(template.Title != null, "template title is not null");
                    Check(template.Content != null, "template content is not null");
                }
            }
            Check(templateCount == 78, "defaults contain 78 templates (found " + templateCount + ")");

            string greeting = Defaults.GreetingForSelfTest();
            Check(greeting.Length > 0, "expected greeting is embedded");
            Check(RtlHelper.IsRtl(greeting), "expected greeting is right-to-left");
            if (data.Groups.Count > 0 && data.Groups[0].Templates.Count > 0)
            {
                string first = data.Groups[0].Templates[0].Content;
                Check(first == greeting, "first default template matches the expected greeting");
                Check(RtlHelper.IsRtl(first), "first default template is right-to-left");
            }
            else
            {
                Fail("defaults do not contain the first template");
            }
        }

        private static void TestJson()
        {
            AppData data = Defaults.CreateAppData();
            data.Settings["restore_clipboard"] = false;
            string specialTitle = "Quote \" and <tag> & slash \\";
            string specialContent = "line 1\r\nline 2\ttabbed \u0001 control\r\nemoji \U0001F600";
            data.Groups[0].Templates[0].Title = specialTitle;
            data.Groups[0].Templates[0].Content = specialContent;

            string json = JsonCodec.SerializeAppData(data);
            Check(json.IndexOf("\"version\": 1", StringComparison.Ordinal) >= 0, "serialized JSON contains version");
            Check(json.IndexOf("\\u0001", StringComparison.Ordinal) >= 0, "serialized JSON escapes control characters");

            AppData parsed = JsonCodec.ParseAppData(json);
            Check(parsed.Groups.Count == data.Groups.Count, "JSON round-trip keeps group count");
            Check(parsed.Groups[0].Templates[0].Title == specialTitle, "JSON round-trip keeps special title");
            Check(parsed.Groups[0].Templates[0].Content == specialContent, "JSON round-trip keeps special content");
            object restoreValue;
            bool restore = parsed.Settings.TryGetValue("restore_clipboard", out restoreValue) &&
                           restoreValue != null &&
                           !Convert.ToBoolean(restoreValue, CultureInfo.InvariantCulture);
            Check(restore, "JSON round-trip keeps settings");

            bool versionRejected = false;
            try
            {
                JsonCodec.ParseAppData(json.Replace("\"version\": 1", "\"version\": 99"));
            }
            catch (DataVersionException)
            {
                versionRejected = true;
            }
            Check(versionRejected, "newer data version is rejected");

            bool rootRejected = false;
            try
            {
                JsonCodec.ParseAppData("[]");
            }
            catch (FormatException)
            {
                rootRejected = true;
            }
            Check(rootRejected, "non-object JSON root is rejected");
        }

        private static void TestStore(out string temporary)
        {
            temporary = Path.Combine(
                Path.GetTempPath(),
                "PasteNotesSelfTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            string path = Path.Combine(temporary, "data.json");

            DataStore store = new DataStore(path);
            store.Load();
            Check(File.Exists(path), "store seeds a new data file");
            Check(store.Data.Groups.Count == 8, "seeded store has 8 groups");

            GroupData group = store.AddGroup("Self-test group");
            Check(group != null && group.Templates.Count == 0, "store adds a group");
            TemplateData template = store.AddTemplate(group);
            template.Title = "Self-test template";
            template.Content = "Hello from the self-test.";
            store.Save();

            store.SetSetting("restore_clipboard", false);
            store.Save();
            Check(File.Exists(path + ".bak"), "store writes a backup file");

            DataStore loaded = new DataStore(path);
            loaded.Load();
            Check(loaded.Data.Groups.Count == 9, "saved store reloads all groups");
            Check(!loaded.GetBoolSetting("restore_clipboard", true), "saved store reloads settings");
            GroupData loadedGroup = loaded.FindGroup(group.Id);
            Check(loadedGroup != null, "saved store keeps group ids");
            if (loadedGroup != null)
            {
                TemplateData loadedTemplate = loaded.FindTemplate(group.Id, template.Id);
                Check(loadedTemplate != null, "saved store keeps template ids");
                if (loadedTemplate != null)
                {
                    Check(loadedTemplate.Title == "Self-test template", "saved store keeps template titles");
                    Check(loadedTemplate.Content == "Hello from the self-test.", "saved store keeps template content");
                }
            }

            File.Delete(path + ".bak");
            File.WriteAllText(path, "this is not json", new UTF8Encoding(false));
            DataStore recovered = new DataStore(path);
            recovered.Load();
            Check(recovered.Data.Groups.Count == 8, "unreadable data is replaced with defaults");
            string[] quarantined = Directory.GetFiles(temporary, "data.corrupt-*.json");
            Check(quarantined.Length == 1, "unreadable data is quarantined");
        }

        private static void TestHotkeys()
        {
            Check(HotkeyParser.Normalize("ctrl+alt+1") == "Ctrl+Alt+1", "hotkey normalization");
            Check(HotkeyParser.Normalize("F9") == "F9", "function key hotkey is allowed");
            Check(HotkeyParser.Normalize("Ctrl++") == "Ctrl++", "plus key hotkey is allowed");
            Check(HotkeyParser.Normalize("Shift+Win+K") == "Shift+Win+K", "Windows key hotkey is allowed");

            CheckRejected("A");
            CheckRejected("Ctrl+Alt");
            CheckRejected("Ctrl+A+B");
            CheckRejected("Ctrl+NotAKey");

            Check(HotkeyParser.DisplayFromKey(Keys.A) == "A", "display name for letter key");
            Check(HotkeyParser.DisplayFromKey(Keys.NumPad5) == "Num5", "display name for numpad key");
            Check(HotkeyParser.DisplayFromKey(Keys.Oemtilde) == "`", "display name for punctuation key");
        }

        private static void CheckRejected(string text)
        {
            bool rejected = false;
            try
            {
                HotkeyParser.Parse(text);
            }
            catch (HotkeyException)
            {
                rejected = true;
            }
            Check(rejected, "invalid hotkey is rejected: " + text);
        }

        private static void TestRtl()
        {
            Check(RtlHelper.IsRtl("\u0634\u0643\u0631\u0627\u064B \u0644\u062A\u0648\u0627\u0635\u0644\u0643\u0645"),
                "Arabic text is detected as right-to-left");
            Check(RtlHelper.IsRtl("\u05E9\u05DC\u05D5\u05DD"), "Hebrew text is detected as right-to-left");
            Check(!RtlHelper.IsRtl("Hello world"), "English text is detected as left-to-right");
            Check(!RtlHelper.IsRtl("12345"), "digit-only text is neutral");
            Check(!RtlHelper.IsRtl(""), "empty text is neutral");
        }

        private static void TestPalette()
        {
            Check(Palette.Normalize("BLUE") == "blue", "palette keys are case-insensitive");
            Check(Palette.Normalize("not-a-color") == "yellow", "unknown colors fall back to yellow");
            Check(Palette.HexValue("blue") == "#56A3E8", "palette hex values are stable");
            Check(Palette.Keys().Length == 8, "palette exposes 8 colors");
        }

        private static void TestIcons()
        {
            using (System.Drawing.Icon active = IconFactory.Create(false))
            using (System.Drawing.Icon paused = IconFactory.Create(true))
            {
                Check(active != null && active.Width >= 16, "active tray icon is generated");
                Check(paused != null && paused.Width >= 16, "paused tray icon is generated");
            }
        }

        private static void Check(bool condition, string message)
        {
            _checks++;
            if (condition)
            {
                WriteLine("ok   " + message);
                return;
            }
            _failures++;
            WriteLine("FAIL " + message);
        }

        private static void Fail(string message)
        {
            _checks++;
            _failures++;
            WriteLine("FAIL " + message);
        }

        private static void WriteLine(string message)
        {
            _log.AppendLine(message);
        }
    }
}
