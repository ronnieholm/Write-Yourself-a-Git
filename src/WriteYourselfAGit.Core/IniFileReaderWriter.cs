using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace WriteYourselfAGit.Core
{
    public class IniFileReaderWriter
    {
        private static readonly Regex SectionPattern = new Regex(@"^\[(?<section>(.+?))\]$");
        private static readonly Regex KeyValuePattern = new Regex(@"\t(?<key>(.+?)) = (?<value>(.+?))$");

        public readonly Dictionary<string, Dictionary<string, string>> Entries = new Dictionary<string, Dictionary<string, string>>();

        public void Deserialize(string[] lines)
        {
            var section = "";
            foreach (var line in lines)
            {
                var sectionMatch = SectionPattern.Match(line);
                var keyValueMatch = KeyValuePattern.Match(line);

                if (sectionMatch.Success)
                {
                    section = sectionMatch.Groups["section"].Value;
                    Entries[section] = new Dictionary<string, string>();
                }
                else if (keyValueMatch.Success)
                    Entries[section].Add(keyValueMatch.Groups["key"].Value, keyValueMatch.Groups["value"].Value);
            }
        }

        public void Set(string section, string key, string value)
        {
            var success = Entries.TryGetValue(section, out var keyValues);
            if (!success)
            {
                keyValues = new Dictionary<string, string>();
                Entries[section] = keyValues;
            }
            Entries[section][key] = value;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var section in Entries.Keys)
            {
                sb.AppendLine($"[{section}]");
                foreach (var keyValues in Entries[section])
                    sb.AppendLine($"\t{keyValues.Key} = {keyValues.Value}");
            }
            return sb.ToString();
        }
    }
}