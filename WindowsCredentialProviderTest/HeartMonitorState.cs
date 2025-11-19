using System;
using System.IO;

namespace WindowsCredentialProviderTest
{
    public class HeartMonitorState
    {
        public int version { get; set; }
        public string last_update_utc { get; set; }
        public string primary_device_id { get; set; }
        public bool allow_unlock { get; set; }
    }

    public static class HeartMonitorStateManager
    {
        private static readonly string STATE_FILE_PATH = CredentialProviderConfig.LoadConfig().StateFilePath;

        public static HeartMonitorState ReadState()
        {
            try
            {
                if (!File.Exists(STATE_FILE_PATH))
                {
                    return null;
                }

                string jsonContent = null;

                // Use FileStream with appropriate sharing options to handle file locking
                try
                {
                    using (var fileStream = new FileStream(STATE_FILE_PATH, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var reader = new StreamReader(fileStream))
                    {
                        jsonContent = reader.ReadToEnd();
                    }
                }
                catch (Exception)
                {
                    return null; // If we can't read the file, assume unlock is not allowed
                }

                var state = ParseJsonState(jsonContent);
                return state;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool IsPhoneUnlockAllowed()
        {
            var state = ReadState();
            return state != null && state.allow_unlock;
        }

        private static HeartMonitorState ParseJsonState(string jsonContent)
        {
            var state = new HeartMonitorState();
            if (string.IsNullOrEmpty(jsonContent))
                return state;

            try
            {
                string s = jsonContent;

                // allow_unlock (boolean)
                var rawAllow = ExtractRawValue(s, "allow_unlock", "AllowUnlock");
                if (!string.IsNullOrEmpty(rawAllow))
                {
                    if (rawAllow.StartsWith("\"")) rawAllow = rawAllow.Trim('"');
                    state.allow_unlock = rawAllow.Equals("true", StringComparison.OrdinalIgnoreCase) || rawAllow == "1";
                }

                // version (int)
                var rawVersion = ExtractRawValue(s, "version", "Version");
                if (!string.IsNullOrEmpty(rawVersion))
                {
                    var rv = rawVersion.Trim('"');
                    if (int.TryParse(rv, out int v)) state.version = v;
                }

                // last_update_utc (string)
                var lastUpdate = ExtractRawValue(s, "last_update_utc", "LastUpdateUtc");
                if (!string.IsNullOrEmpty(lastUpdate)) state.last_update_utc = lastUpdate.Trim('"');

                // primary_device_id (string)
                var deviceId = ExtractRawValue(s, "primary_device_id", "PrimaryDeviceId");
                if (!string.IsNullOrEmpty(deviceId)) state.primary_device_id = deviceId.Trim('"');
            }
            catch (Exception)
            {
            }

            return state;
        }

        private static string ExtractRawValue(string s, string key, string altKey)
        {
            if (string.IsNullOrEmpty(s)) return null;

            int idx = s.IndexOf('"' + key + '"', StringComparison.OrdinalIgnoreCase);
            if (idx < 0 && !string.IsNullOrEmpty(altKey))
                idx = s.IndexOf('"' + altKey + '"', StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            int colon = s.IndexOf(':', idx);
            if (colon < 0) return null;
            int j = colon + 1;
            while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
            if (j >= s.Length) return null;

            if (s[j] == '"')
            {
                int start = j + 1;
                int end = s.IndexOf('"', start);
                if (end < 0) return null;
                return s.Substring(start, end - start);
            }

            int endIdx = j;
            while (endIdx < s.Length && s[endIdx] != ',' && s[endIdx] != '}' && s[endIdx] != '\n' && s[endIdx] != '\r') endIdx++;
            return s.Substring(j, endIdx - j).Trim();
        }
    }
}