using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Coalesced
{
    // Ported from ME2Randomizer (Legendary Edition)

    /// <summary>
    /// Loads a text-based ini file as a CoalesceAsset for single API editing.
    /// </summary>
    public static class ConfigFileProxy
    {
        /// <summary>
        /// Loads an ini from a file on disk. This does NOT work for the ME2 Coalesced.ini as it is a binary file!
        /// </summary>
        /// <param name="iniFile"></param>
        /// <returns></returns>
        public static CoalesceAsset LoadIni(string iniFile)
        {
            return ParseIni(File.ReadAllText(iniFile));
        }

        /// <summary>
        /// Parses a game 2 - style ini file into a ConfigFileProxy (ini file with prefixes)
        /// </summary>
        /// <param name="iniText">The text of the file</param>
        /// <returns>Parsed asset object</returns>
        public static CoalesceAsset ParseIni(string iniText)
        {
            CoalesceAsset cfp = new CoalesceAsset();
            var splits = iniText.Split('\n');
            CoalesceSection currentConfigSection = null;
            foreach (var line in splits)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue; //blank line
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    //New section
                    var header = trimmed.Trim('[', ']');
                    if (!cfp.Sections.TryGetValue(header, out currentConfigSection))
                    {
                        // Some config files in the wild seem to have duplicate section names for some reason
                        // This detects that case and uses the existing one instead
                        currentConfigSection = new CoalesceSection(header);
                        cfp.Sections.Add(header, currentConfigSection);
                    }
                }
                else if (currentConfigSection == null)
                {
                    continue; //this parser only supports section items
                }
                else
                {
                    // Extract key and value.
                    var separator = trimmed.IndexOf('=');
                    if (separator > 0)
                    {
                        string key = trimmed.Substring(0, separator).Trim();
                        string value = trimmed.Substring(separator + 1).Trim();
                        var type = GetIniDataType(key);
                        key = StripType(key);
                        CoalesceProperty prop = new CoalesceProperty(key);
                        prop.Add(new CoalesceValue(value, type));
                        currentConfigSection.AddEntry(prop);
                    }
                    else
                    {
                        // Debug.WriteLine($"ConfigFileProxy: Skipping line {trimmed}");
                    }
                }
            }

            return cfp;
        }

        /// <summary>
        /// Strips off the type prefix on this value - that is, the first character if it matches a type we support parsing.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string StripType(string key)
        {
            if (key[0] == '+') return key.Substring(1);
            if (key[0] == '!') return key.Substring(1);
            if (key[0] == '-') return key.Substring(1);
            if (key[0] == '.') return key.Substring(1);
            if (key[0] == '>') return key.Substring(1);
            return key;
        }

        /// <summary>
        /// If the key starts with a parse action typing. Includes the M3 specific > type 0.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool IsTyped(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 1) return false;
            if (key[0] == '+') return true;
            if (key[0] == '!') return true;
            if (key[0] == '-') return true;
            if (key[0] == '.') return true;
            if (key[0] == '>') return true;
            return false;
        }

        /// <summary>
        /// Converts this CoalesceAsset to a Game2-style ini
        /// </summary>
        /// <returns>string of ini</returns>
        public static string GetGame2IniText(this CoalesceAsset asset)
        {
            StringBuilder sb = new StringBuilder();
            bool isFirst = true;
            foreach (var section in asset.Sections)
            {
                if (!isFirst)
                {
                    // Make easier to read
                    sb.AppendLine();
                }
                sb.AppendLine($"[{section.Key}]");
                foreach (var keyValuePairs in section.Value)
                {
                    foreach (var vp in keyValuePairs.Value)
                    {
                        sb.AppendLine($"{GetGame2IniDataPrefix(vp.ParseAction)}{keyValuePairs.Key}={vp.Value}");
                    }
                }
                isFirst = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Gets the data type of an ini based on the key of a key value pair
        /// </summary>
        /// <param name="key">The key. Must be at least one character long.</param>
        /// <returns>The parse action typing, add by default.</returns>
        public static CoalesceParseAction GetIniDataType(string key)
        {
            if (key[0] == '+') return CoalesceParseAction.AddUnique; // AddUnique 3 (enum 1)
            if (key[0] == '!') return CoalesceParseAction.RemoveProperty; // Clear 1 
            if (key[0] == '-') return CoalesceParseAction.Remove; // Subtract 4
            if (key[0] == '.') return CoalesceParseAction.Add; // UE3 documentation specifies . = Add but with duplicates (Type 2)
            if (key[0] == '>') return CoalesceParseAction.New; // MODDING SPECIFIC SYMBOL: This was added in Game 3 -> Clear and assign a single value

            // If unspecified, we treat it as just add.
            return CoalesceParseAction.Add;
        }

        /// <summary>
        /// Gets the data type of an ini based on the key of a key value pair
        /// </summary>
        /// <param name="action">Action to convert</param>
        /// <returns>At max, a single character string. Will be empty if it is an add operation.</returns>
        public static string GetGame2IniDataPrefix(CoalesceParseAction action)
        {
            // ME2 does not have a 'New' (.). Maybe it does but it's not used.
            if (action == CoalesceParseAction.AddUnique) return "+"; // Unique (3)
            if (action == CoalesceParseAction.RemoveProperty) return "!"; // Clear (1)
            if (action == CoalesceParseAction.Remove) return "-"; // Subtract 4
                                                                  // if (action == CoalesceParseAction.Add) return ">"; // M3 specific - we don't put it here as this is not supported in game 2
            return ""; // Add (2) has no prefix, Type 0 New does not exist in ME2
        }
    }
}
