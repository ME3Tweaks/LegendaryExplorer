using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Coalesced;
using LegendaryExplorerCore.Shaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ShaderSourceCrcTests
{
    [TestMethod]
    public void NativeLexicalWalkUsesPostorderAndUniqueCaseInsensitiveBaseNames()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Root.usf"] = "#include \"A.usf\"\n#include \"B.usf\"\n#include \"A.USF\"",
            ["A.usf"] = "#include \"Common.usf\"\nA", ["B.usf"] = "#include \"Common.usf\"\nB", ["Common.usf"] = "C"
        };
        var result = new LE3ShaderSourceCrc(name => files[name]).Compute("Root");
        CollectionAssert.AreEqual(new[] { "Common", "A", "B", "Root" }, new List<string>(result.SourceOrder));
        byte[] expected = Encoding.Unicode.GetBytes(files["Common.usf"] + files["A.usf"] + files["B.usf"] + files["Root.usf"]);
        Assert.AreEqual(Crc32.Compute(expected, 0, expected.Length), result.Crc);
    }

    [TestMethod]
    public void IncludesAreLexicalNotPreprocessedAndVirtualMaterialIsReplaced()
    {
        var requested = new List<string>();
        var result = new LE3ShaderSourceCrc(name =>
        {
            requested.Add(name);
            return name == "Root.usf" ? "// #include \"Common.usf\"\n#if 0\n#include \"mAtErIaL.uSf\"\n#endif\n#include \"VertexFactory.usf\"\n#include\t\"Ignored.usf\"" : name;
        }).Compute("Root");
        CollectionAssert.AreEqual(new[] { "Common", "MaterialTemplate", "Root" }, new List<string>(result.SourceOrder));
        CollectionAssert.AreEqual(new[] { "Root.usf", "Common.usf", "MaterialTemplate.usf" }, requested);
    }

    [TestMethod]
    public void IgnoredDirectivesCountAgainstTwentyIncludeLimit()
    {
        string root = string.Concat(System.Linq.Enumerable.Repeat("#include \"VertexFactory.usf\"\n", 20)) + "#include \"NeverRead.usf\"";
        var result = new LE3ShaderSourceCrc(name => name == "Root.usf" ? root : throw new InvalidOperationException()).Compute("Root");
        CollectionAssert.AreEqual(new[] { "Root" }, new List<string>(result.SourceOrder));
    }

    [TestMethod]
    public void CyclesAreBoundedByNativeDepthRatherThanDroppingTheCyclicInclude()
    {
        int reads = 0;
        var result = new LE3ShaderSourceCrc(_ => { reads++; return "#include \"Root.usf\""; }).Compute("Root");
        Assert.AreEqual(1, reads);
        CollectionAssert.AreEqual(new[] { "Root", "Root" }, new List<string>(result.SourceOrder));
    }

    [TestMethod]
    public void DepthZeroStillLoadsItsSourceButDoesNotVisitItsIncludes()
    {
        var reads = new List<string>();
        var result = new LE3ShaderSourceCrc(name =>
        {
            reads.Add(name);
            int index = int.Parse(name[..name.IndexOf('.')]);
            return $"#include \"{index + 1}.usf\"";
        }).Compute("0");
        Assert.AreEqual(8, reads.Count);
        Assert.AreEqual("7.usf", reads[7]);
        CollectionAssert.AreEqual(new[] { "7", "6", "5", "4", "3", "2", "1", "0" }, new List<string>(result.SourceOrder));
    }

    [TestMethod]
    public void SessionSnapshotsTextAndResultsAndPreservesRawWcharAndNewlines()
    {
        string text = "a\r\n\ud800";
        var session = new LE3ShaderSourceCrc(_ => text);
        var first = session.Compute("Root");
        byte[] raw = [0x61, 0, 13, 0, 10, 0, 0, 0xd8];
        Assert.AreEqual(Crc32.Compute(raw, 0, raw.Length), first.Crc);
        text = "a\n\ud800";
        Assert.AreSame(first, session.Compute("ROOT.usf"));
        Assert.AreNotEqual(first.Crc, new LE3ShaderSourceCrc(_ => text).Compute("Root").Crc);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("bad\0source")]
    [DataRow("#include \"unterminated")]
    public void MissingNulOrMalformedTextFailsWithoutCachingAnInvalidResult(string text)
    {
        var session = new LE3ShaderSourceCrc(_ => text);
        Assert.ThrowsExactly<InvalidDataException>(() => session.Compute("Root"));
    }

    [TestMethod]
    [DataRow("ascii")]
    [DataRow("little")]
    [DataRow("big")]
    [DataRow("ambiguous")]
    public void DirectoryAdapterReadsAsciiAndUtf16WithoutAssumingUtf8(string encoding)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LE3SourceCrcTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        byte[] bytes = encoding switch
        {
            "little" => [0xff, 0xfe, 0x41, 0, 13, 0, 10, 0],
            "big" => [0xfe, 0xff, 0, 0x41, 0, 13, 0, 10],
            "ambiguous" => [0xc3, 0xa9], _ => [0x41, 13, 10]
        };
        File.WriteAllBytes(Path.Combine(directory, "Root.usf"), bytes);
        try
        {
            var session = LE3ShaderSourceCrc.FromDirectory(directory);
            if (encoding == "ambiguous") Assert.ThrowsExactly<InvalidDataException>(() => session.Compute("Root"));
            else Assert.AreEqual(new LE3ShaderSourceCrc(_ => "A\r\n").Compute("Root").Crc, session.Compute("Root").Crc);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ExplicitWindowsCodePageDecodesUntaggedBytesAndDoesNotOverrideUtf16Bom()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LE3SourceCrcTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "Ansi.usf"), [0x80, 0xe9, 13, 10]);
            File.WriteAllBytes(Path.Combine(directory, "Wide.usf"), [0xfe, 0xff, 0x20, 0xac]);
            File.WriteAllBytes(Path.Combine(directory, "Odd.usf"), [0xff, 0xfe, 0x41]);
            var session = LE3ShaderSourceCrc.FromDirectory(directory, windowsAnsiCodePage: 1252);
            Assert.AreEqual(new LE3ShaderSourceCrc(_ => "\u20ac\u00e9\r\n").Compute("Ansi").Crc, session.Compute("Ansi").Crc);
            Assert.AreEqual(new LE3ShaderSourceCrc(_ => "\u20ac").Compute("Wide").Crc, session.Compute("Wide").Crc);
            Assert.ThrowsExactly<InvalidDataException>(() => session.Compute("Odd"));
            Assert.ThrowsExactly<ArgumentException>(() => LE3ShaderSourceCrc.FromDirectory(directory, -100));
            Assert.ThrowsExactly<ArgumentException>(() => LE3ShaderSourceCrc.FromDirectory(directory, 0));
            File.WriteAllBytes(Path.Combine(directory, "Utf8.usf"), [0xc3, 0xa9]);
            Assert.AreEqual(new LE3ShaderSourceCrc(_ => "\u00e9").Compute("Utf8").Crc,
                LE3ShaderSourceCrc.FromDirectory(directory, 65001).Compute("Utf8").Crc);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
