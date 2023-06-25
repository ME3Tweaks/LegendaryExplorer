using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;

// Tools to unpack/repack LE1 and LE2 coalesced files. 
// Originally by d00t (https://github.com/d00telemental/LECoal)

namespace LegendaryExplorerCore.Coalesced
{


    public static class BinaryExtensions
    {
        public static string ReadCoalescedString(this BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var bytes = reader.ReadBytes(length * -2);
            if (bytes.Length != 0)
            {
                var str = Encoding.Unicode.GetString(bytes, 0, bytes.Length - 2);
                return str;
            }

            return string.Empty;
        }

        public static void WriteCoalescedString(this BinaryWriter writer, string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                writer.Write((Int32)0);
                return;
            }

            var length = str.Length;
            var wideString = Encoding.Unicode.GetBytes(str + '\0');

            writer.Write((Int32)(length + 1) * -1);
            writer.Write(wideString);
        }
    }

    namespace Exceptions
    {
        public class CBundleException : Exception
        {
            public CBundleException(string message) : base(message) { }
        }

        public class CToolException : Exception
        {
            public CToolException(string message) : base(message) { }
        }
    }

    internal class LECoalescedManifestInfo
    {
        public string DestinationFilename { get; } = null;
        public List<string> ParticipatingRelativeFilePaths { get; } = new();

        public LECoalescedManifestInfo(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new Exceptions.CBundleException($"Failed to find a manifest at {manifestPath}");
            }

            var manifestReader = File.ReadAllLines(manifestPath);
            DestinationFilename = manifestReader[0];
            ParticipatingRelativeFilePaths.AddRange(manifestReader[1..]); // participating files are index 1 to end
        }
    }

    //[DebuggerDisplay("LECoalescedSection \"{Name}\"")]
    //public class LECoalescedSection
    //{
    //    public string Name { get; private set; }
    //    public List<(string, string)> Pairs { get; private set; } = new();

    //    public LECoalescedSection(string name)
    //    {
    //        Name = name;
    //    }

    //    public LECoalescedSection(BinaryReader reader)
    //    {
    //        Name = reader.ReadCoalescedString();


    //    }
    //}

    [DebuggerDisplay("LECoalescedBundle \"{Name}\" with {Files.Count} files")]
    public class LECoalescedBundle
    {
        public string Name { get; private set; }
        public CaseInsensitiveDictionary<DuplicatingIni> Files { get; } = new();

        public LECoalescedBundle(string name)
        {
            Name = name;
        }

        public static LECoalescedBundle ReadFromFile(string name, string path)
        {
            return ReadFromStream(new MemoryStream(File.ReadAllBytes(path)), name);
        }

        public static LECoalescedBundle ReadFromStream(Stream stream, string name)
        {
            return ReadFromBinaryReader(new BinaryReader(stream), name);
        }

        private static LECoalescedBundle ReadFromBinaryReader(BinaryReader reader, string name)
        {
            LECoalescedBundle bundle = new(name);

            var fileCount = reader.ReadInt32();
            //Debug.WriteLine($"Bundle {bundle.Name}, {fileCount} files");

            for (int i = 0; i < fileCount; i++)
            {
                var iniFullName = reader.ReadCoalescedString();
                var sectionCount = reader.ReadInt32();
                bundle.Files[Path.GetFileName(iniFullName)] = ReadCoalescedIni(reader, sectionCount);
            }

            return bundle;
        }

        private static DuplicatingIni ReadCoalescedIni(BinaryReader reader, int sectionCount)
        {
            DuplicatingIni ini = new DuplicatingIni();
            DuplicatingIni.Section s = null;
            for (int i = 0; i < sectionCount; i++)
            {
                if (s != null)
                {
                    ini.Sections.Add(s);
                }
                s = new DuplicatingIni.Section() { Header = reader.ReadCoalescedString() };

                var pairCount = reader.ReadInt32();
                //Debug.WriteLine($"Section {s.Header}, {pairCount} pairs");
                for (int j = 0; j < pairCount; j++)
                {
                    var key = reader.ReadCoalescedString();
                    var val = reader.ReadCoalescedString();

                    s.Entries.Add(new DuplicatingIni.IniEntry(key, val));
                }
            }

            if (s != null) // in case file has zero sections we must check if it is null.
            {
                ini.Sections.Add(s);
            }

            return ini;
        }

        public static LECoalescedBundle ReadFromDirectory(string name, string path)
        {
            var manifestPath = Path.Combine(path, "mele.extractedbin");
            if (!File.Exists(manifestPath))
            {
                throw new Exception("Didn't find a manifest in path");
            }

            LECoalescedManifestInfo manifest = new(manifestPath);
            LECoalescedBundle bundle = new(manifest.DestinationFilename);

            DuplicatingIni ini = null;
            foreach (var relativePath in manifest.ParticipatingRelativeFilePaths)
            {
                var filePath = Path.Combine(path, relativePath);
                if (!File.Exists(filePath)) { throw new Exceptions.CBundleException("Failed to find a file according to manifest, either the file was removed or the manifest was changed"); }
                StreamReader reader = new(filePath);

                ini = new DuplicatingIni();

                DuplicatingIni.Section currentSection = null;
                string line = null;
                while ((line = reader.ReadLine()) is not null)
                {
                    // Empty line
                    if (string.IsNullOrWhiteSpace(line)) { continue; }

                    // Section header
                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        var header = line.Substring(1, line.Length - 2);
                        if (header.Length < 1 || string.IsNullOrWhiteSpace(header)) { throw new Exceptions.CBundleException("Expected to have a header with text"); }

                        if (currentSection is not null)
                        {
                            ini.Sections.Add(currentSection);
                        }
                        currentSection = new DuplicatingIni.Section() { Header = header };

                        continue;
                    }

                    // Pair
                    var chunks = line.Split('=', 2);
                    if (chunks.Length != 2) { throw new Exceptions.CBundleException("Expected to have exactly two chunks after splitting the line by ="); }

                    if (chunks[0].EndsWith("||"))  // It's a multiline value UGH
                    {
                        var strippedKey = chunks[0].Substring(0, chunks[0].Length - 2);

                        if (currentSection.Entries.Count > 0 && currentSection.Entries[^1].Key == strippedKey)  // It's a second or further line in multiline value
                        {
                            var last = currentSection.Entries[^1];
                            currentSection.Entries[^1] = new(last.Key, last.Value + "\r\n" + chunks[1]);
                        }
                        else
                        {
                            currentSection.Entries.Add(new DuplicatingIni.IniEntry(strippedKey, chunks[1]));
                        }
                    }
                    else
                    {
                        currentSection.Entries.Add(new DuplicatingIni.IniEntry(chunks[0], chunks[1]));
                    }

                }

                if (currentSection is not null)
                {
                    ini.Sections.Add(currentSection);
                }
                bundle.Files[Path.GetFileName(relativePath)] = ini;
            }

            return bundle;
        }

        public void WriteToDirectory(string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var file in Files)
            {
                var outPath = Path.Combine(destinationPath, file.Key);
                using var writerStream = new StreamWriter(outPath);
                foreach (var section in file.Value.Sections)
                {
                    writerStream.WriteLine($"[{section.Header}]");
                    foreach (var pair in section.Entries)
                    {
                        var lines = splitValue(pair.Value);
                        if (lines is null || lines.Count() == 1)
                        {
                            writerStream.WriteLine($"{pair.Key}={pair.Value}");
                            continue;
                        }

                        // multiline
                        foreach (var line in lines)
                        {
                            writerStream.WriteLine($"{pair.Key}||={line}");
                        }
                    }
                }
            }

            // Write out a manifest to rebuild from.
            var manifestPath = Path.Combine(destinationPath, "mele.extractedbin");
            using var manifestWriter = new StreamWriter(manifestPath);
            manifestWriter.WriteLine($"{Name}");
            foreach (var file in Files)
            {
                manifestWriter.WriteLine(file.Key);
            }
        }

        public void WriteToFile(string destinationPath)
        {
            var ms = new MemoryStream();
            WriteToStream(ms);
            ms.WriteToFile(destinationPath);
        }

        internal List<string> splitValue(string val)
        {
            List<string> splitVal = null;

            if (val.Contains("\r\n"))
            {
                splitVal = val.Split("\r\n").ToList();
            }
            else if (val.Contains('\r') && !val.Contains('\n'))
            {
                splitVal = val.Split('\r').ToList();
            }
            else if (!val.Contains('\r') && val.Contains('\n'))
            {
                splitVal = val.Split('\n').ToList();
            }
            else if (val.Contains('\r') && val.Contains('\n'))
            {
                throw new Exception("Value contains both CR and LF but not in a CRLF sequence!");
            }

            return splitVal;
        }

        public void WriteToStream(Stream ms)
        {
            var writer = new BinaryWriter(ms);
            writer.Write(Files.Count);
            foreach (var file in Files)
            {
                writer.WriteCoalescedString(GetIniFullPath(file.Key));
                writer.Write(file.Value.Sections.Count);

                foreach (var section in file.Value.Sections)
                {
                    if (section is null)
                    {
                        continue;
                    }

                    writer.WriteCoalescedString(section.Header);
                    writer.Write(section.Entries.Count);

                    foreach (var pair in section.Entries)
                    {
                        writer.WriteCoalescedString(pair.Key);
                        writer.WriteCoalescedString(pair.Value);
                    }
                }
            }
        }

        private string GetIniFullPath(string filename)
        {
            var extension = Path.GetExtension(filename).ToLower();
            switch (extension)
            {
                case ".int":
                case ".ita":
                case ".deu":
                case ".pol":
                case ".fra":
                    return $@"..\..\Localization\{extension.Substring(1).ToUpper()}\{filename}";
                case ".ini":
                    return $@"..\..\BIOGame\Config\{filename}";
                case "" when CoalescedConverter.ProperNames.Contains(filename, StringComparer.InvariantCultureIgnoreCase): // No extension, may have been stripped
                    return $@"..\..\BIOGame\Config\{filename}.ini";
                case "":
                    return $@"..\..\BIOGame\Config\{filename}.int"; // It's one of those localization files. Just set it to int. These are never used anyways.
            }
            throw new Exception($"Filename '{filename}' has invalid file extension for LE1/LE2 Coalesced filename");
        }
    }

    /// <summary>
    /// Tools to unpack/repack LE1 and LE2 coalesced files. 
    /// </summary>
    public static class LECoalescedConverter
    {
        /// <summary>
        /// Unpacks a LE1/LE2 Coalesced.bin to an output folder which includes a manifest
        /// </summary>
        /// <param name="fromFile"></param>
        /// <param name="toDir"></param>
        public static void Unpack(string fromFile, string toDir)
        {
            var bundle = LECoalescedBundle.ReadFromFile(Path.GetFileName(fromFile), fromFile);
            bundle.WriteToDirectory(toDir);
        }

        public static LECoalescedBundle UnpackToMemory(string fromFile)
        {
            using var fs = File.OpenRead(fromFile);
            return UnpackToMemory(fs, fromFile);
        }

        public static LECoalescedBundle UnpackToMemory(Stream stream, string name)
        {
            return LECoalescedBundle.ReadFromStream(stream, name);
        }

        /// <summary>
        /// Packs a LE1/LE2 coalesced .extractedbin manifest file back into a Coalesced.bin
        /// </summary>
        /// <param name="fromDir"></param>
        /// <param name="toFile"></param>
        public static void Pack(string fromDir, string toFile)
        {
            var manifest = new LECoalescedManifestInfo(Path.Combine(fromDir, "mele.extractedbin"));
            var bundle = LECoalescedBundle.ReadFromDirectory(manifest.DestinationFilename, fromDir);
            bundle.WriteToFile(toFile);
        }

        /// <summary>
        /// Gets the original Coalesced.bin filename from an .extractedbin manifest
        /// </summary>
        /// <param name="manifestPath"></param>
        /// <returns></returns>
        public static string GetDestinationPathFromManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new Exceptions.CBundleException($"Failed to find a manifest at {manifestPath}");
            }

            using var manifestReader = new StreamReader(manifestPath);
            return manifestReader.ReadLine();
        }
    }
}
