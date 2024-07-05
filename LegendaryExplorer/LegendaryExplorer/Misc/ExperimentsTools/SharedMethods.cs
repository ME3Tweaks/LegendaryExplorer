using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LegendaryExplorer.Misc.ExperimentsTools
{
    public static class SharedMethods
    {
        #region Exkywor's Experiments Shared Methods
        /// <summary>
        /// Shows an error dialog with the given message.
        /// </summary>
        /// <param name="errMsg">Message to display.</param>
        public static void ShowError(string errMsg) => MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
        /// <summary>
        /// Shows an success dialog with the given message.
        /// </summary>
        /// <param name="msg">Message to display.</param>
        public static void ShowSuccess(string msg) => MessageBox.Show(msg, "Success", MessageBoxButton.OK);

        /// <summary>
        /// Generate a default ExpressionGUID.
        /// </summary>
        /// <returns>ExpressionGUID StructProperty</returns>
        public static StructProperty GenerateExpressionGUID()
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new IntProperty(0, "A"));
            props.Add(new IntProperty(0, "B"));
            props.Add(new IntProperty(0, "C"));
            props.Add(new IntProperty(0, "D"));

            return new StructProperty("Guid", props, "ExpressionGUID", true);
        }

        /// <summary>
        /// Cast the entry to ExportEntry, or resolve it if it's an ImportEntry.
        /// </summary>
        /// <param name="entry">Entry to resolve.</param>
        /// <returns>Resulting ExportEntry.</returns>
        public static ExportEntry ResolveEntryToExport(IEntry entry)
        {
            if (entry is ImportEntry import) { return EntryImporter.ResolveImport(import); }
            else { return (ExportEntry)entry; }
        }

        /// <summary>
        /// By SirCxyrtyx.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="name"></param>
        /// <param name="className"></param>
        /// <param name="parent"></param>
        /// <param name="properties"></param>
        /// <param name="binary"></param>
        /// <param name="prePropBinary"></param>
        /// <param name="super"></param>
        /// <param name="archetype"></param>
        /// <returns>New ExportEntry</returns>
        public static ExportEntry CreateExport(IMEPackage pcc, NameReference name, string className, IEntry parent, PropertyCollection properties = null, ObjectBinary binary = null, byte[] prePropBinary = null, IEntry super = null, IEntry archetype = null)
        {
            IEntry classEntry = className.CaseInsensitiveEquals("Class") ? null : EntryImporter.EnsureClassIsInFile(pcc, className, new RelinkerOptionsPackage());

            var exp = new ExportEntry(pcc, parent, name, prePropBinary, properties, binary, binary is UClass)
            {
                Class = classEntry,
                SuperClass = super,
                Archetype = archetype
            };
            pcc.AddExport(exp);
            return exp;
        }

        /// <summary>
        /// Adds the stack to the binary of the given export.
        /// </summary>
        /// <param name="export">Export to write to</param>
        public static void AddStack(ExportEntry export)
        {
            if (export.HasStack)
            {
                return;
            }
            var ms = new MemoryStream();
            ms.WriteInt32(export.Class.UIndex);
            if (export.Game != MEGame.UDK)
            {
                ms.WriteInt32(export.Class.UIndex);
            }
            ms.WriteInt64(-1);
            if (export.Game >= MEGame.ME3)
            {
                ms.WriteInt16(0);
            }
            else
            {
                ms.WriteInt32(0);
            }
            ms.WriteInt32(0);
            ms.WriteInt32(-1);
            ms.WriteInt32(export.NetIndex);
            ms.Write(export.DataReadOnly[export.GetPropertyStart()..]);
            export.ObjectFlags |= UnrealFlags.EObjectFlags.HasStack;
            export.Data = ms.ToArray();
        }

        /// <summary>
        /// Gets the common prefix of two strings. Assumes both only difer at the end.
        /// </summary>
        /// <param name="s1">First string.</param>
        /// <param name="s2">Second string.</param>
        /// <returns>Union of input strings.</returns>
        public static string GetCommonPrefix(string s1, string s2)
        {
            if (s1.Length == 0 || s2.Length == 0 || char.ToLower(s1[0]) != char.ToLower(s2[0]))
            {
                return "";
            }

            for (int i = 1; i < s1.Length && i < s2.Length; i++)
            {
                if (char.ToLower(s1[i]) != char.ToLower(s2[i]))
                {
                    return s1[..i];
                }
            }

            return s1.Length < s2.Length ? s1 : s2;
        }

        public static IEntry GetTheWorld(IMEPackage pcc) => pcc.FindEntry("TheWorld");
        public static IEntry GetPersistentLevel(IMEPackage pcc) => pcc.FindEntry("TheWorld.PersistentLevel");
        public static List<ExportEntry> GetExports(IMEPackage pcc, string className) =>
            pcc.Exports.Where(export => export.ClassName.Equals(className)).ToList();

        public static void RebuildStreamingLevels(IMEPackage pcc)
        {
            try
            {
                var levelStreamingKismets = new List<ExportEntry>();
                ExportEntry bioworldinfo = null;
                foreach (ExportEntry exp in pcc.Exports)
                {
                    switch (exp.ClassName)
                    {
                        case "BioWorldInfo" when exp.ObjectName == "BioWorldInfo":
                            bioworldinfo = exp;
                            continue;
                        case "LevelStreamingKismet" when exp.ObjectName == "LevelStreamingKismet":
                            levelStreamingKismets.Add(exp);
                            continue;
                    }
                }

                levelStreamingKismets = levelStreamingKismets
                    .OrderBy(o => o.GetProperty<NameProperty>("PackageName").ToString()).ToList();
                if (bioworldinfo != null)
                {
                    var streamingLevelsProp =
                        bioworldinfo.GetProperty<ArrayProperty<ObjectProperty>>("StreamingLevels") ??
                        new ArrayProperty<ObjectProperty>("StreamingLevels");

                    streamingLevelsProp.Clear();
                    foreach (ExportEntry exp in levelStreamingKismets)
                    {
                        streamingLevelsProp.Add(new ObjectProperty(exp.UIndex));
                    }

                    bioworldinfo.WriteProperty(streamingLevelsProp);
                }
                else
                {
                    throw new Exception("No BioWorldInfo object found in this file.");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error setting streaming levels:\n{e.Message}");
            }
        }

        /// <summary>
        /// Converts a hex string to its byte representation.
        /// </summary>
        /// <param name="hex">Hex string to convert.</param>
        /// <returns>Byte representation of the hex string.</returns>
        public static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Checks if a dest position is within a given distance of the origin.
        /// </summary>
        /// <param name="origin">Origin position.</param>
        /// <param name="dest">Dest position to check.</param>
        /// <param name="dist">Max distance from origin.</param>
        /// <returns>True if the dest is within dist</returns>
        public static bool InDist(float origin, float dest, float dist)
        {
            return Math.Abs((dest - origin)) <= dist;
        }

        /// <summary>
        /// Calculates the FNV132 hash of the given string.
        /// IMPORTANT: This may not be compeletely bug-free or may be missing a couple of details, but so far it works.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The decimal representation of the hash.</returns>
        public static uint CalculateFNV132Hash(string name)
        {
            byte[] bytedName = Encoding.ASCII.GetBytes(name.ToLower()); // Wwise automatically lowecases the input

            // FNV132 hashing algorithm
            uint hash = 2166136261;
            foreach (byte namebyte in bytedName)
            {
                hash = ((hash * 16777619) ^ namebyte) & 0xFFFFFFFF;
            }
            return hash;
        }

        /// <summary>
        /// Generate a random ID, using a provided Random object.
        /// </summary>
        /// <param name="random">Random object to generate from.</param>
        /// <returns>Random ID</returns>
        public static uint GenerateRandomID(Random random)
        {
            byte[] randomID = new byte[4];
            random.NextBytes(randomID);
            return BitConverter.ToUInt32(randomID, 0);
        }

        /// <summary>
        /// Convert a big endian hex to its little endian representation.
        /// </summary>
        /// <param name="bigEndian">Endian to convert.</param>
        /// <returns>Little endian.</returns>
        public static string BigToLittleEndian(string bigEndian)
        {
            byte[] asCurrentEndian = new byte[4];
            string littleEndian = "";
            for (int i = 0; i < 4; i++)
            {
                asCurrentEndian[i] = Convert.ToByte(bigEndian.Substring(i * 2, 2), 16);
                littleEndian = $"{asCurrentEndian[i]:X2}{littleEndian}";
            }
            return littleEndian;
        }

        /// <summary>
        /// Prompts the user for an int, verifying that the int is valid.
        /// </summary>
        /// <param name="msg">Message to display for the prompt.</param>
        /// <param name="err">Error message to display.</param>
        /// <param name="biggerThan">Number the input must be bigger than. If not provided -2,147,483,648 will be used.</param>
        /// <param name="title">Title for the prompt.</param>
        /// <returns>The input int.</returns>
        public static int PromptForInt(string msg, string err, int biggerThan = -2147483648, string title = "")
        {
            if (PromptDialog.Prompt(null, msg, title) is string stringPrompt)
            {
                int intPrompt;
                if (string.IsNullOrEmpty(stringPrompt) || !int.TryParse(stringPrompt, out intPrompt) || !(intPrompt > biggerThan))
                {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return -1;
                }
                return intPrompt;
            }
            return -1;
        }

        /// <summary>
        /// Prompts the user for a float, verifying that the float is valid.
        /// </summary>
        /// <param name="msg">Message to display for the Prompt.</param>
        /// <param name="err">Error message to display.</param>
        /// <param name="title">Title for the Prompt.</param>
        /// <returns>The input int.</returns>
        public static float PromptForFloat(string msg, string err, string title = "")
        {
            if (PromptDialog.Prompt(null, msg, title) is string stringPrompt)
            {
                float floatPrompt;
                if (string.IsNullOrEmpty(stringPrompt) || !float.TryParse(stringPrompt, out floatPrompt))
                {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return -1;
                }
                return floatPrompt;
            }
            return -1;
        }

        /// <summary>
        /// Prompts the user for a string, verifying tha the string is valid.
        /// </summary>
        /// <param name="msg">Message to display for the Prompt.</param>
        /// <param name="err">Error message to display.</param>
        /// <returns>The input string.</returns>
        public static string PromptForStr(string msg, string err)
        {
            if (PromptDialog.Prompt(null, msg) is string str)
            {
                if (string.IsNullOrEmpty(str))
                {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return null;
                }
                return str;
            }
            return null;
        }

        /// <summary>
        /// Prompts the user for a reference, verifying tha the reference is valid.
        /// </summary>
        /// <param name="msg">Message to display for the Prompt.</param>
        /// <param name="err">Error message to display.</param>
        /// <returns>The input reference.</returns>
        public static string PromptForRef(string msg, string err)
        {
            if (PromptDialog.Prompt(null, msg) is string stringRef)
            {
                int intRef;
                if (string.IsNullOrEmpty(stringRef) || !int.TryParse(stringRef, out intRef))
                {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return null;
                }
                return intRef.ToString();
            }
            return null;
        }

        /// <summary>
        /// Prompts the user for Yes/No, and returns their response.
        /// </summary>
        /// <param name="msg">Message to display.</param>
        /// <param name="caption">Caption to display.</param>
        /// <returns>Whether the user selected Yes or No.</returns>
        public static bool PromptForBool(string msg, string caption)
        {
            return MessageBoxResult.Yes == MessageBox.Show(msg, caption, MessageBoxButton.YesNo);
        }
        #endregion
    }
}
