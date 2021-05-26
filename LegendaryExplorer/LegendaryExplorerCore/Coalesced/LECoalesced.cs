using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LegendaryExplorerCore.Helpers;

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
        public List<(string, string)> RelativePaths { get; } = new();

        public LECoalescedManifestInfo(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new Exceptions.CBundleException($"Failed to find a manifest at {manifestPath}");
            }

            using var manifestReader = new StreamReader(manifestPath);
            DestinationFilename = manifestReader.ReadLine();

            var countLine = manifestReader.ReadLine().Trim("\r\n ".ToCharArray());
            for (int i = 0; i < int.Parse(countLine); i++)
            {
                var lineChunks = manifestReader.ReadLine()?.Split(";;", 2, StringSplitOptions.RemoveEmptyEntries)
                    ?? throw new Exceptions.CBundleException("Expected to read a full line, got null");
                if (lineChunks.Length != 2)
                {
                    throw new Exceptions.CBundleException("Expected a manifest line to have 2 chunks");
                }
                RelativePaths.Add((lineChunks[0], lineChunks[1]));
            }
        }
    }

    [DebuggerDisplay("LECoalescedSection \"{Name}\"")]
    public class LECoalescedSection
    {
        public string Name { get; private set; }
        public List<(string, string)> Pairs { get; private set; } = new();

        public LECoalescedSection(string name)
        {
            Name = name;
        }

        public LECoalescedSection(BinaryReader reader)
        {
            Name = reader.ReadCoalescedString();

            var pairCount = reader.ReadInt32();
            //Debug.WriteLine($"Section {Name}, {pairCount} pairs");
            for (int i = 0; i < pairCount; i++)
            {
                var key = reader.ReadCoalescedString();
                var val = reader.ReadCoalescedString();

                Pairs.Add((key, val));
            }
        }
    }

    [DebuggerDisplay("LECoalescedFile \"{Name}\" with {Sections.Count} sections")]
    public class LECoalescedFile
    {
        public string Name { get; private set; }
        public List<LECoalescedSection> Sections { get; private set; } = new();

        public LECoalescedFile(string name)
        {
            Name = name;
        }

        public LECoalescedFile(BinaryReader reader)
        {
            Name = reader.ReadCoalescedString();

            var sectionCount = reader.ReadInt32();
            //Debug.WriteLine($"File {Name}, {sectionCount} sections");
            for (int i = 0; i < sectionCount; i++)
            {
                LECoalescedSection section = new(reader);
                Sections.Add(section);
            }
        }

        public static string EscapeName(string name) => name.Replace("\\", "_").Replace("..", "-");
    }

    [DebuggerDisplay("LECoalescedBundle \"{Name}\" with {Files.Count} files")]
    public class LECoalescedBundle
    {
        public string Name { get; private set; }
        public List<LECoalescedFile> Files { get; private set; } = new();

        public LECoalescedBundle(string name)
        {
            Name = name;
        }

        public static LECoalescedBundle ReadFromFile(string name, string path)
        {
            BinaryReader reader = new(new MemoryStream(File.ReadAllBytes(path)));
            LECoalescedBundle bundle = new(name);

            var fileCount = reader.ReadInt32();
            //Debug.WriteLine($"Bundle {bundle.Name}, {fileCount} files");

            for (int i = 0; i < fileCount; i++)
            {
                LECoalescedFile file = new(reader);
                bundle.Files.Add(file);
            }

            return bundle;
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
            LECoalescedFile currentFile = null;

            foreach (var relativePath in manifest.RelativePaths)
            {
                var filePath = Path.Combine(path, relativePath.Item1);
                if (!File.Exists(filePath)) { throw new Exceptions.CBundleException("Failed to find a file according to manifest, either the file was removed or the manifest was changed"); }
                StreamReader reader = new(filePath);

                currentFile = new LECoalescedFile(relativePath.Item2);

                LECoalescedSection currentSection = null;
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
                            currentFile.Sections.Add(currentSection);
                        }
                        currentSection = new LECoalescedSection(header);

                        continue;
                    }

                    // Pair
                    var chunks = line.Split('=', 2);
                    if (chunks.Length != 2) { throw new Exceptions.CBundleException("Expected to have exactly two chunks after splitting the line by ="); }

                    if (chunks[0].EndsWith("||"))  // It's a multiline value UGH
                    {
                        var strippedKey = chunks[0].Substring(0, chunks[0].Length - 2);

                        if (currentSection.Pairs.Count > 0 && currentSection.Pairs.Last().Item1 == strippedKey)  // It's a second or further line in multiline value
                        {
                            var last = currentSection.Pairs[currentSection.Pairs.Count() - 1];
                            currentSection.Pairs[currentSection.Pairs.Count() - 1]
                                = (last.Item1, last.Item2 + "\r\n" + chunks[1]);
                        }
                        else
                        {
                            currentSection.Pairs.Add((strippedKey, chunks[1]));
                        }
                    }
                    else
                    {
                        currentSection.Pairs.Add((chunks[0], chunks[1]));
                    }

                }

                if (currentSection is not null)
                {
                    currentFile.Sections.Add(currentSection);
                }
                bundle.Files.Add(currentFile);
            }

            return bundle;
        }

        public void WriteToDirectory(string destinationPath)
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var file in Files)
            {
                var outPath = Path.Combine(destinationPath, LECoalescedFile.EscapeName(file.Name));
                using var writerStream = new StreamWriter(outPath);
                foreach (var section in file.Sections)
                {
                    writerStream.WriteLine($"[{section.Name}]");
                    foreach (var pair in section.Pairs)
                    {
                        var lines = splitValue(pair.Item2);
                        if (lines is null || lines.Count() == 1)
                        {
                            writerStream.WriteLine($"{pair.Item1}={pair.Item2}");
                            continue;
                        }

                        foreach (var line in lines)
                        {
                            writerStream.WriteLine($"{pair.Item1}||={line}");
                        }
                    }
                }
            }

            // Write out a manifest to rebuild from.
            var manifestPath = Path.Combine(destinationPath, "mele.extractedbin");
            using var manifestWriter = new StreamWriter(manifestPath);
            manifestWriter.WriteLine($"{Name}");
            manifestWriter.WriteLine($"{Files.Count}");
            foreach (var file in Files)
            {
                manifestWriter.WriteLine($"{LECoalescedFile.EscapeName(file.Name)};;{file.Name}");
            }
        }

        public void WriteToFile(string destinationPath)
        {
            BinaryWriter writer = new(new MemoryStream());

            writer.Write((Int32)Files.Count);
            foreach (var file in Files)
            {
                writer.WriteCoalescedString(file.Name);
                writer.Write((Int32)file.Sections.Count);

                foreach (var section in file.Sections)
                {
                    if (section is null)
                    {
                        continue;
                    }

                    writer.WriteCoalescedString(section.Name);
                    writer.Write((Int32)section.Pairs.Count);

                    foreach (var pair in section.Pairs)
                    {
                        writer.WriteCoalescedString(pair.Item1);
                        writer.WriteCoalescedString(pair.Item2);
                    }
                }
            }

            (writer.BaseStream as MemoryStream).WriteToFile(destinationPath);
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
