using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web.Script.Serialization;

namespace PasteNotes
{
    internal sealed class DataVersionException : Exception
    {
        public int Version { get; private set; }

        public DataVersionException(int version)
            : base("Data version " + version.ToString(CultureInfo.InvariantCulture) +
                   " is newer than supported version " + DataStore.DataVersion.ToString(CultureInfo.InvariantCulture))
        {
            Version = version;
        }
    }

    internal sealed class TemplateData
    {
        public string Id;
        public string Title;
        public string Content;

        public TemplateData()
        {
            Id = DataStore.NewId();
            Title = "New template";
            Content = "";
        }
    }

    internal sealed class GroupData
    {
        public string Id;
        public string Name;
        public string Hotkey;
        public string Color;
        public List<TemplateData> Templates;

        public GroupData()
        {
            Id = DataStore.NewId();
            Name = "New group";
            Hotkey = "";
            Color = Palette.DefaultKey;
            Templates = new List<TemplateData>();
        }
    }

    internal sealed class AppData
    {
        public int Version;
        public Dictionary<string, object> Settings;
        public List<GroupData> Groups;

        public static AppData CreateEmpty()
        {
            AppData data = new AppData();
            data.Version = DataStore.DataVersion;
            data.Settings = DataStore.DefaultSettings();
            data.Groups = new List<GroupData>();
            return data;
        }
    }

    internal sealed class DataStore
    {
        public const int DataVersion = 1;

        public string FilePath { get; private set; }
        public AppData Data { get; private set; }
        public bool LastSaveFailed { get; private set; }

        public DataStore(string path)
        {
            FilePath = path;
            Data = AppData.CreateEmpty();
        }

        public static Dictionary<string, object> DefaultSettings()
        {
            Dictionary<string, object> settings = new Dictionary<string, object>();
            settings["restore_clipboard"] = true;
            return settings;
        }

        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                Seed();
                Save();
                return;
            }

            AppData loaded = null;
            try
            {
                loaded = JsonCodec.ParseAppData(ReadText(FilePath));
            }
            catch (DataVersionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read data file: " + ex.Message);
            }

            if (loaded != null)
            {
                Data = loaded;
                Normalize();
                return;
            }

            string temporary = FilePath + ".tmp";
            if (File.Exists(temporary))
            {
                try
                {
                    Data = JsonCodec.ParseAppData(ReadText(temporary));
                    Normalize();
                    Save();
                    Log.Info("Recovered data from data.json.tmp");
                    return;
                }
                catch (DataVersionException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not recover data.json.tmp: " + ex.Message);
                }
            }

            string backup = FilePath + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    Data = JsonCodec.ParseAppData(ReadText(backup));
                    Normalize();
                    Save();
                    Log.Info("Recovered data from data.json.bak");
                    return;
                }
                catch (DataVersionException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warn("Could not recover data.json.bak: " + ex.Message);
                }
            }

            QuarantineCorruptFile();
            Seed();
            Save();
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string temporary = FilePath + ".tmp";
                string backup = FilePath + ".bak";
                string json = JsonCodec.SerializeAppData(Data);
                File.WriteAllText(temporary, json, new UTF8Encoding(false));

                if (File.Exists(FilePath))
                {
                    try
                    {
                        File.Replace(temporary, FilePath, backup, true);
                    }
                    catch
                    {
                        try
                        {
                            File.Copy(FilePath, backup, true);
                            File.Copy(temporary, FilePath, true);
                            File.Delete(temporary);
                        }
                        catch (Exception ex)
                        {
                            LastSaveFailed = true;
                            throw new IOException("Could not replace data.json", ex);
                        }
                    }
                }
                else
                {
                    File.Move(temporary, FilePath);
                }

                LastSaveFailed = false;
            }
            catch
            {
                LastSaveFailed = true;
                throw;
            }
        }

        public void Seed()
        {
            Data = Defaults.CreateAppData();
            Normalize();
        }

        public bool GetBoolSetting(string key, bool fallback)
        {
            if (Data == null || Data.Settings == null) return fallback;
            object value;
            if (!Data.Settings.TryGetValue(key, out value)) return fallback;
            if (value is bool) return (bool)value;
            try { return Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        public string GetStringSetting(string key, string fallback)
        {
            if (Data == null || Data.Settings == null) return fallback;
            object value;
            if (!Data.Settings.TryGetValue(key, out value) || value == null) return fallback;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public int GetIntSetting(string key, int fallback)
        {
            if (Data == null || Data.Settings == null) return fallback;
            object value;
            if (!Data.Settings.TryGetValue(key, out value) || value == null) return fallback;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        public void SetSetting(string key, object value)
        {
            if (Data.Settings == null) Data.Settings = DefaultSettings();
            Data.Settings[key] = value;
        }

        public GroupData FindGroup(string groupId)
        {
            foreach (GroupData group in Data.Groups)
            {
                if (group.Id == groupId) return group;
            }
            return null;
        }

        public TemplateData FindTemplate(string groupId, string templateId)
        {
            GroupData group = FindGroup(groupId);
            if (group == null) return null;
            foreach (TemplateData template in group.Templates)
            {
                if (template.Id == templateId) return template;
            }
            return null;
        }

        public string UniqueGroupName(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) baseName = "New group";
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GroupData group in Data.Groups) names.Add(group.Name);
            if (!names.Contains(baseName)) return baseName;
            int index = 2;
            while (names.Contains(baseName + " " + index.ToString(CultureInfo.InvariantCulture))) index++;
            return baseName + " " + index.ToString(CultureInfo.InvariantCulture);
        }

        public string UniqueTemplateTitle(GroupData group, string baseTitle)
        {
            if (string.IsNullOrEmpty(baseTitle)) baseTitle = "New template";
            HashSet<string> titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TemplateData template in group.Templates) titles.Add(template.Title);
            if (!titles.Contains(baseTitle)) return baseTitle;
            int index = 2;
            while (titles.Contains(baseTitle + " " + index.ToString(CultureInfo.InvariantCulture))) index++;
            return baseTitle + " " + index.ToString(CultureInfo.InvariantCulture);
        }

        public GroupData AddGroup(string name)
        {
            GroupData group = new GroupData();
            group.Name = string.IsNullOrEmpty(name) ? UniqueGroupName("New group") : name.Trim();
            Data.Groups.Add(group);
            return group;
        }

        public bool RemoveGroup(string groupId)
        {
            int before = Data.Groups.Count;
            Data.Groups.RemoveAll(delegate(GroupData group) { return group.Id == groupId; });
            return Data.Groups.Count != before;
        }

        public bool MoveGroup(string groupId, int delta)
        {
            int index = Data.Groups.FindIndex(delegate(GroupData group) { return group.Id == groupId; });
            int target = index + delta;
            if (index < 0 || target < 0 || target >= Data.Groups.Count) return false;
            GroupData item = Data.Groups[index];
            Data.Groups[index] = Data.Groups[target];
            Data.Groups[target] = item;
            return true;
        }

        public TemplateData AddTemplate(GroupData group)
        {
            if (group == null) return null;
            TemplateData template = new TemplateData();
            template.Title = UniqueTemplateTitle(group, "New template");
            group.Templates.Add(template);
            return template;
        }

        public bool RemoveTemplate(GroupData group, string templateId)
        {
            if (group == null) return false;
            int before = group.Templates.Count;
            group.Templates.RemoveAll(delegate(TemplateData template) { return template.Id == templateId; });
            return group.Templates.Count != before;
        }

        public TemplateData DuplicateTemplate(GroupData group, TemplateData source)
        {
            if (group == null || source == null) return null;
            TemplateData copy = new TemplateData();
            copy.Title = UniqueTemplateTitle(group, source.Title + " copy");
            copy.Content = source.Content;
            int index = group.Templates.IndexOf(source);
            if (index < 0) index = group.Templates.Count - 1;
            group.Templates.Insert(index + 1, copy);
            return copy;
        }

        public bool MoveTemplate(GroupData group, TemplateData template, int delta)
        {
            if (group == null || template == null) return false;
            int index = group.Templates.IndexOf(template);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= group.Templates.Count) return false;
            group.Templates[index] = group.Templates[target];
            group.Templates[target] = template;
            return true;
        }

        private static string ReadText(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private void Normalize()
        {
            if (Data.Settings == null) Data.Settings = DefaultSettings();
            if (!Data.Settings.ContainsKey("restore_clipboard")) Data.Settings["restore_clipboard"] = true;

            HashSet<string> groupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (GroupData group in Data.Groups)
            {
                if (string.IsNullOrEmpty(group.Id) || groupIds.Contains(group.Id))
                {
                    group.Id = NewId();
                }
                groupIds.Add(group.Id);

                if (group.Name == null || group.Name.Trim().Length == 0) group.Name = "Untitled group";
                if (group.Hotkey == null) group.Hotkey = "";
                group.Color = Palette.Normalize(group.Color);
                if (group.Templates == null) group.Templates = new List<TemplateData>();

                HashSet<string> templateIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (TemplateData template in group.Templates)
                {
                    if (string.IsNullOrEmpty(template.Id) || templateIds.Contains(template.Id))
                    {
                        template.Id = NewId();
                    }
                    templateIds.Add(template.Id);
                    if (template.Title == null || template.Title.Trim().Length == 0) template.Title = "Untitled template";
                    if (template.Content == null) template.Content = "";
                }
            }
        }

        private void QuarantineCorruptFile()
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                string name = Path.GetFileNameWithoutExtension(FilePath);
                string extension = Path.GetExtension(FilePath);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string target = Path.Combine(directory, name + ".corrupt-" + stamp + extension);
                File.Move(FilePath, target);
                Log.Warn("Quarantined unreadable data file as " + target);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not quarantine the data file: " + ex.Message);
            }
        }
    }

    internal static class JsonCodec
    {
        public static AppData ParseAppData(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 200;

            object raw = serializer.DeserializeObject(json);
            Dictionary<string, object> root = raw as Dictionary<string, object>;
            if (root == null) throw new FormatException("The data file root is not a JSON object.");

            int version = GetInt(root, "version", 1);
            if (version > DataStore.DataVersion) throw new DataVersionException(version);

            AppData data = AppData.CreateEmpty();
            data.Version = DataStore.DataVersion;

            Dictionary<string, object> settings = GetDictionary(root, "settings");
            if (settings != null)
            {
                foreach (KeyValuePair<string, object> pair in settings)
                {
                    data.Settings[pair.Key] = pair.Value;
                }
            }

            object groupsValue;
            if (root.TryGetValue("groups", out groupsValue))
            {
                foreach (object item in AsEnumerable(groupsValue))
                {
                    Dictionary<string, object> groupDictionary = item as Dictionary<string, object>;
                    if (groupDictionary == null) continue;

                    GroupData group = new GroupData();
                    group.Id = GetString(groupDictionary, "id");
                    if (string.IsNullOrEmpty(group.Id)) group.Id = DataStore.NewId();
                    group.Name = GetString(groupDictionary, "name");
                    if (group.Name == null) group.Name = "Untitled group";
                    group.Hotkey = GetString(groupDictionary, "hotkey");
                    if (group.Hotkey == null) group.Hotkey = "";
                    group.Color = Palette.Normalize(GetString(groupDictionary, "color"));

                    object templatesValue;
                    if (groupDictionary.TryGetValue("templates", out templatesValue))
                    {
                        foreach (object templateItem in AsEnumerable(templatesValue))
                        {
                            Dictionary<string, object> templateDictionary = templateItem as Dictionary<string, object>;
                            if (templateDictionary == null) continue;

                            TemplateData template = new TemplateData();
                            template.Id = GetString(templateDictionary, "id");
                            if (string.IsNullOrEmpty(template.Id)) template.Id = DataStore.NewId();
                            template.Title = GetString(templateDictionary, "title");
                            if (template.Title == null) template.Title = "Untitled template";
                            template.Content = GetString(templateDictionary, "content");
                            if (template.Content == null) template.Content = "";
                            group.Templates.Add(template);
                        }
                    }
                    data.Groups.Add(group);
                }
            }

            return data;
        }

        public static string SerializeAppData(AppData data)
        {
            StringBuilder builder = new StringBuilder(65536);
            builder.Append("{\r\n");
            Indent(builder, 1);
            builder.Append("\"version\": ").Append(data.Version.ToString(CultureInfo.InvariantCulture)).Append(",\r\n");
            Indent(builder, 1);
            builder.Append("\"settings\": ");
            WriteValue(builder, data.Settings, 1);
            builder.Append(",\r\n");
            Indent(builder, 1);
            builder.Append("\"groups\": ");
            WriteGroups(builder, data.Groups, 1);
            builder.Append("\r\n}");
            return builder.ToString();
        }

        private static void WriteGroups(StringBuilder builder, List<GroupData> groups, int level)
        {
            if (groups == null || groups.Count == 0)
            {
                builder.Append("[]");
                return;
            }
            builder.Append("[\r\n");
            for (int index = 0; index < groups.Count; index++)
            {
                Indent(builder, level + 1);
                WriteGroup(builder, groups[index], level + 1);
                if (index < groups.Count - 1) builder.Append(',');
                builder.Append("\r\n");
            }
            Indent(builder, level);
            builder.Append(']');
        }

        private static void WriteGroup(StringBuilder builder, GroupData group, int level)
        {
            builder.Append("{\r\n");
            Indent(builder, level + 1);
            builder.Append("\"id\": ");
            WriteString(builder, group.Id);
            builder.Append(",\r\n");
            Indent(builder, level + 1);
            builder.Append("\"name\": ");
            WriteString(builder, group.Name);
            builder.Append(",\r\n");
            Indent(builder, level + 1);
            builder.Append("\"hotkey\": ");
            WriteString(builder, group.Hotkey);
            builder.Append(",\r\n");
            Indent(builder, level + 1);
            builder.Append("\"color\": ");
            WriteString(builder, group.Color);
            builder.Append(",\r\n");
            Indent(builder, level + 1);
            builder.Append("\"templates\": ");
            WriteTemplates(builder, group.Templates, level + 1);
            builder.Append("\r\n");
            Indent(builder, level);
            builder.Append('}');
        }

        private static void WriteTemplates(StringBuilder builder, List<TemplateData> templates, int level)
        {
            if (templates == null || templates.Count == 0)
            {
                builder.Append("[]");
                return;
            }
            builder.Append("[\r\n");
            for (int index = 0; index < templates.Count; index++)
            {
                TemplateData template = templates[index];
                Indent(builder, level + 1);
                builder.Append("{\r\n");
                Indent(builder, level + 2);
                builder.Append("\"id\": ");
                WriteString(builder, template.Id);
                builder.Append(",\r\n");
                Indent(builder, level + 2);
                builder.Append("\"title\": ");
                WriteString(builder, template.Title);
                builder.Append(",\r\n");
                Indent(builder, level + 2);
                builder.Append("\"content\": ");
                WriteString(builder, template.Content);
                builder.Append("\r\n");
                Indent(builder, level + 1);
                builder.Append('}');
                if (index < templates.Count - 1) builder.Append(',');
                builder.Append("\r\n");
            }
            Indent(builder, level);
            builder.Append(']');
        }

        private static void WriteValue(StringBuilder builder, object value, int level)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            string text = value as string;
            if (text != null)
            {
                WriteString(builder, text);
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is short || value is byte ||
                value is uint || value is ulong || value is ushort ||
                value is double || value is float || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            IDictionary<string, object> dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                if (dictionary.Count == 0)
                {
                    builder.Append("{}");
                    return;
                }
                builder.Append("{\r\n");
                int index = 0;
                foreach (KeyValuePair<string, object> pair in dictionary)
                {
                    Indent(builder, level + 1);
                    WriteString(builder, pair.Key);
                    builder.Append(": ");
                    WriteValue(builder, pair.Value, level + 1);
                    if (index < dictionary.Count - 1) builder.Append(',');
                    builder.Append("\r\n");
                    index++;
                }
                Indent(builder, level);
                builder.Append('}');
                return;
            }

            IEnumerable sequence = value as IEnumerable;
            if (sequence != null)
            {
                List<object> items = new List<object>();
                foreach (object item in sequence) items.Add(item);
                if (items.Count == 0)
                {
                    builder.Append("[]");
                    return;
                }
                builder.Append("[\r\n");
                for (int index = 0; index < items.Count; index++)
                {
                    Indent(builder, level + 1);
                    WriteValue(builder, items[index], level + 1);
                    if (index < items.Count - 1) builder.Append(',');
                    builder.Append("\r\n");
                }
                Indent(builder, level);
                builder.Append(']');
                return;
            }

            WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (value != null)
            {
                for (int index = 0; index < value.Length; index++)
                {
                    char c = value[index];
                    switch (c)
                    {
                        case '"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            if (c < 0x20)
                            {
                                builder.Append("\\u");
                                builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(c);
                            }
                            break;
                    }
                }
            }
            builder.Append('"');
        }

        private static void Indent(StringBuilder builder, int level)
        {
            builder.Append(' ', level * 2);
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value)) return null;
            return value as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value) || value == null) return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(Dictionary<string, object> dictionary, string key, int fallback)
        {
            object value;
            if (dictionary == null || !dictionary.TryGetValue(key, out value) || value == null) return fallback;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private static IEnumerable AsEnumerable(object value)
        {
            IEnumerable sequence = value as IEnumerable;
            if (sequence == null || value is string) return new object[0];
            return sequence;
        }
    }

    internal static class Defaults
    {
        // BEGIN DEFAULTS BLOB
        private const string DefaultsCompressedBase64 = "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";
        // END DEFAULTS BLOB

        // BEGIN EXPECTED GREETING
        private const string ExpectedGreeting = "\u0634\u0643\u0631\u064b\u0627 \u0644\u062a\u0648\u0627\u0635\u0644\u0643\u0645 \u0645\u0639 \u0627\u0644\u0641\u0631\u062f\u0627\u0646 \u0644\u0644\u0635\u0631\u0627\u0641\u0629\u060c \u0646\u062a\u0645\u0646\u0649 \u0644\u0643\u0645 \u064a\u0648\u0645\u064b\u0627 \u0633\u0639\u064a\u062f\u064b\u0627!";
        // END EXPECTED GREETING

        public static AppData CreateAppData()
        {
            byte[] compressed = Convert.FromBase64String(DefaultsCompressedBase64);
            string json;
            using (MemoryStream memory = new MemoryStream(compressed))
            using (GZipStream gzip = new GZipStream(memory, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(gzip, new UTF8Encoding(false)))
            {
                json = reader.ReadToEnd();
            }

            AppData data = JsonCodec.ParseAppData(json);
            data.Version = DataStore.DataVersion;
            data.Settings = DataStore.DefaultSettings();
            return data;
        }

        public static string GreetingForSelfTest()
        {
            return ExpectedGreeting;
        }
    }
}
