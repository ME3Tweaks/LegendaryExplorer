using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LegendaryExplorerCore.Shaders;

/// <summary>
/// Immutable, caller-owned LE3 shader-source set. Translation, D3D compilation and native source
/// CRCs can share this identity without reopening mutable files between jobs. Flat native filenames
/// are required; directory paths and duplicate case-insensitive names are rejected. This is not a
/// shader-registry completeness or native bytecode-parity guarantee.
/// </summary>
public sealed class LE3ShaderSourceSnapshot
{
    private readonly Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="decodedSources">Exact engine-decoded text, preserving line endings and Unicode
    /// code units. The dictionary is copied; callers cannot subsequently change the snapshot.</param>
    public LE3ShaderSourceSnapshot(IReadOnlyDictionary<string, string> decodedSources)
    {
        ArgumentNullException.ThrowIfNull(decodedSources);
        foreach (var pair in decodedSources)
        {
            string name = ValidateName(pair.Key);
            if (pair.Value == null || pair.Value.Contains('\0'))
                throw new ArgumentException($"Missing or NUL-containing source: {name}", nameof(decodedSources));
            if (!sources.TryAdd(name, pair.Value))
                throw new ArgumentException($"Duplicate LE3 source filename: {name}", nameof(decodedSources));
        }
    }

    /// <summary>
    /// Captures the flat .usf files immediately. Untagged non-ASCII needs the caller-explicit Windows
    /// ANSI code page used by LE3; UTF16 BOMs take precedence. No files are modified. The caller must
    /// prevent concurrent directory edits during capture; subsequent edits cannot affect this set.
    /// </summary>
    public static LE3ShaderSourceSnapshot FromDirectory(string directory, int? windowsAnsiCodePage = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        string root = Path.GetFullPath(directory);
        Encoding ansi = LE3ShaderSourceCrc.GetExplicitAnsiEncoding(windowsAnsiCodePage);
        var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            if (!path.EndsWith(".usf", StringComparison.OrdinalIgnoreCase)) continue;
            texts.Add(Path.GetFileName(path), LE3ShaderSourceCrc.Decode(File.ReadAllBytes(path), ansi));
        }
        return new LE3ShaderSourceSnapshot(texts);
    }

    public string ReadSource(string filename)
    {
        string name = ValidateName(filename);
        return sources.TryGetValue(name, out string text) ? text
            : throw new FileNotFoundException($"LE3 source snapshot does not contain {name}.", name);
    }

    /// <summary>Independent mutable CRC session over this immutable text set.</summary>
    public LE3ShaderSourceCrc CreateCrcSession() => new(ReadSource);

    // D3D receives the same decoded text for roots and physical includes. UTF8 here is only the
    // compiler input transport, not an assumption about native source-file decoding or CRC bytes.
    internal Stream OpenCompileSource(string filename)
        => new MemoryStream(Encoding.UTF8.GetBytes(ReadSource(filename)), writable: false);

    private static string ValidateName(string filename)
    {
        ArgumentException.ThrowIfNullOrEmpty(filename);
        if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || filename.Contains('/') || filename.Contains('\\') || filename is "." or "..")
            throw new ArgumentException("LE3 snapshot sources require clean filenames, not paths.", nameof(filename));
        return filename.EndsWith(".usf", StringComparison.OrdinalIgnoreCase) ? filename : filename + ".usf";
    }
}
