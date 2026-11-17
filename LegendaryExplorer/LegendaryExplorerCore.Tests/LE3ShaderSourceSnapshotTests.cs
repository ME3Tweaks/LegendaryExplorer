using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Shaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests;

[TestClass]
public class LE3ShaderSourceSnapshotTests
{
    [TestMethod]
    public void SnapshotCopiesDictionaryAndProvidesIndependentCrcSessions()
    {
        var texts = new Dictionary<string, string> { ["Root.usf"] = "#include \"Common.usf\"\r\n", ["Common.usf"] = "old\r\n" };
        var snapshot = new LE3ShaderSourceSnapshot(texts);
        var first = snapshot.CreateCrcSession();
        uint crc = first.Compute("Root").Crc;
        texts["Root.usf"] = "new";
        texts.Clear();
        Assert.AreEqual("old\r\n", snapshot.ReadSource("COMMON"));
        Assert.AreEqual(crc, snapshot.CreateCrcSession().Compute("Root").Crc);
        Assert.ThrowsExactly<FileNotFoundException>(() => snapshot.ReadSource("Missing"));
        Assert.ThrowsExactly<ArgumentException>(() => snapshot.ReadSource("../Root"));
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("nul")]
    [DataRow("path")]
    [DataRow("duplicate")]
    public void SnapshotRejectsInvalidTextOrAmbiguousFlatNames(string shape)
    {
        var texts = new Dictionary<string, string> { [shape == "path" ? "sub/Root.usf" : "Root.usf"] = shape switch
            { "null" => null, "nul" => "a\0b", _ => "text" } };
        if (shape == "duplicate") texts.Add("ROOT", "second");
        Assert.ThrowsExactly<ArgumentException>(() => new LE3ShaderSourceSnapshot(texts));
    }

    [TestMethod]
    public void DirectoryCaptureSurvivesEditsDeletionAndPreservesNativeAnsiDecoding()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LE3SourceSnapshotTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "Root.usf");
            File.WriteAllBytes(path, [0x80, 0xe9, 13, 10]);
            File.WriteAllText(Path.Combine(directory, "Ignored.txt"), "not a shader");
            Assert.ThrowsExactly<InvalidDataException>(() => LE3ShaderSourceSnapshot.FromDirectory(directory));
            var snapshot = LE3ShaderSourceSnapshot.FromDirectory(directory, 1252);
            uint crc = LE3ShaderSourceCrc.FromDirectory(directory, 1252).Compute("Root").Crc;
            File.WriteAllText(path, "changed");
            File.Delete(path);
            Assert.AreEqual("\u20ac\u00e9\r\n", snapshot.ReadSource("Root"));
            Assert.AreEqual(crc, snapshot.CreateCrcSession().Compute("Root").Crc);
            Assert.ThrowsExactly<FileNotFoundException>(() => snapshot.ReadSource("Ignored.txt"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
