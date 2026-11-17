using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LegendaryExplorerCore.Coalesced;

namespace LegendaryExplorerCore.Shaders;

/// <summary>
/// Read-only LE3 source-CRC session. Uses the shipping lexical include walk, not D3D preprocessing:
/// inactive/commented includes count, Material.usf becomes MaterialTemplate.usf, and the virtual
/// VertexFactory.usf is excluded. This does not establish shader-registry completeness or parity.
/// Create a new session after source changes; each session snapshots its loaded texts and results.
/// </summary>
public sealed class LE3ShaderSourceCrc
{
    private const int MaxDepth = 7, MaxIncludesPerFile = 20;
    private readonly Func<string, string> loadSource;
    private readonly Dictionary<string, string> texts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Result> results = new(StringComparer.OrdinalIgnoreCase);

    public sealed class Result
    {
        public uint Crc { get; internal init; }
        /// <summary>Ordered include base names followed by the root filename, without extension.</summary>
        public IReadOnlyList<string> SourceOrder { get; internal init; }
    }

    /// <param name="loadSource">
    /// Supplies exact engine-decoded text for a clean filename ending in .usf. Do not normalize
    /// whitespace, line endings or text before returning it. Null or embedded NUL is rejected.
    /// </param>
    public LE3ShaderSourceCrc(Func<string, string> loadSource)
        => this.loadSource = loadSource ?? throw new ArgumentNullException(nameof(loadSource));

    /// <summary>
    /// Adapter for ASCII or BOM-tagged UTF16 files, decoded without newline/Unicode normalization.
    /// Native untagged non-ASCII uses the Windows ANSI code page; this adapter rejects that ambiguous
    /// case unless the caller explicitly supplies the Windows ANSI code page used by LE3. Do not
    /// infer it from Encoding.Default (.NET uses UTF8), or normalize the decoded source.
    /// </summary>
    public static LE3ShaderSourceCrc FromDirectory(string directory, int? windowsAnsiCodePage = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        string root = Path.GetFullPath(directory);
        Encoding ansi = GetExplicitAnsiEncoding(windowsAnsiCodePage);
        return new LE3ShaderSourceCrc(name => Decode(File.ReadAllBytes(Path.Combine(root, name)), ansi));
    }

    public Result Compute(string sourceFileName)
    {
        string root = CleanName(sourceFileName);
        if (results.TryGetValue(root, out var cached)) return cached;
        var includes = new List<string>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completedDepth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        Visit(root, MaxDepth);
        uint crc = 0;
        foreach (string include in includes) crc = Accumulate(crc, Read(include));
        crc = Accumulate(crc, Read(root));
        var order = new List<string>(includes) { BaseName(root) };
        var result = new Result { Crc = crc, SourceOrder = order.AsReadOnly() };
        results.Add(root, result);
        return result;

        void Visit(string file, int depth)
        {
            // Native GetShaderIncludes 0x7ff74ac17020 loads even at depth zero, scans the exact
            // case-sensitive '#include ' token and counts ignored directives against the cap.
            string text = Read(file);
            string clean = CleanName(file);
            // Rewalking a completed subtree at the same/lower depth cannot change the global
            // first-use order. Mark only after completion so cycles retain the native depth cap.
            if (completedDepth.TryGetValue(clean, out int visitedDepth) && visitedDepth >= depth) return;
            int position = 0;
            for (int count = 0; count < MaxIncludesPerFile && depth > 0; count++)
            {
                int start = text.IndexOf("#include ", position, StringComparison.Ordinal);
                if (start < 0) break;
                int opening = text.IndexOf('"', start), closing = opening < 0 ? -1 : text.IndexOf('"', opening + 1);
                if (closing < 0) throw new InvalidDataException($"Malformed LE3 lexical shader include in {file}.");
                string name = text.Substring(opening + 1, closing - opening - 1);
                position = closing + 1;
                if (name.Equals("Material.usf", StringComparison.OrdinalIgnoreCase)) name = "MaterialTemplate.usf";
                if (name.Equals("VertexFactory.usf", StringComparison.OrdinalIgnoreCase)) continue;
                Visit(name, depth - 1);
                string basename = BaseName(name);
                if (unique.Add(basename)) includes.Add(basename);
            }
            completedDepth[clean] = depth;
        }
    }

    private string Read(string name)
    {
        string clean = CleanName(name);
        if (texts.TryGetValue(clean, out var text)) return text;
        text = loadSource(clean);
        if (text == null || text.Contains('\0'))
            throw new InvalidDataException($"Missing or NUL-containing LE3 shader source: {clean}");
        texts.Add(clean, text);
        return text;
    }

    private static string CleanName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (name.Contains('\0')) throw new ArgumentException("Shader filenames cannot contain NUL.", nameof(name));
        // LoadShaderSourceFile strips either slash form before appending .usf when needed.
        int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        string clean = name[(slash + 1)..];
        if (clean.Length == 0 || clean.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || clean is "." or "..")
            throw new ArgumentException("Invalid clean shader filename.", nameof(name));
        return clean.EndsWith(".usf", StringComparison.OrdinalIgnoreCase) ? clean : clean + ".usf";
    }

    private static string BaseName(string name)
    {
        int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        string clean = name[(slash + 1)..];
        int dot = clean.LastIndexOf('.');
        return dot < 0 ? clean : clean[..dot];
    }

    private static uint Accumulate(uint crc, string text)
    {
        // Raw WCHAR code units, excluding terminator; preserve even unpaired UTF16 surrogates.
        byte[] bytes = new byte[checked(text.Length * 2)];
        for (int i = 0; i < text.Length; i++)
        {
            bytes[2 * i] = (byte)text[i];
            bytes[2 * i + 1] = (byte)(text[i] >> 8);
        }
        return Crc32.Compute(bytes, 0, bytes.Length, crc);
    }

    internal static Encoding GetExplicitAnsiEncoding(int? windowsAnsiCodePage)
    {
        if (!windowsAnsiCodePage.HasValue) return null;
        if (windowsAnsiCodePage.Value <= 0)
            throw new ArgumentException("An explicit positive Windows ANSI code page is required.", nameof(windowsAnsiCodePage));
        // UTF8 may itself be the native Windows ACP. It is a built-in .NET encoding, not one
        // supplied by CodePagesEncodingProvider. Avoid registering a process-wide provider.
        return CodePagesEncodingProvider.Instance.GetEncoding(windowsAnsiCodePage.Value)
            ?? Encoding.GetEncoding(windowsAnsiCodePage.Value);
    }

    internal static string Decode(byte[] bytes, Encoding ansi)
    {
        bool little = bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe;
        bool big = bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff;
        if ((little || big) && bytes.Length % 2 == 0)
        {
            var chars = new char[(bytes.Length - 2) / 2];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)(little ? bytes[2 + 2 * i] | bytes[3 + 2 * i] << 8 : bytes[2 + 2 * i] << 8 | bytes[3 + 2 * i]);
            return new string(chars);
        }
        if (little || big) throw new InvalidDataException("LE3 UTF16 source has an odd byte count.");
        if (ansi != null) return ansi.GetString(bytes);
        var ascii = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] >= 128) throw new InvalidDataException("Non-ASCII shader source requires exact native-decoded text or an even-length UTF16 BOM.");
            ascii[i] = (char)bytes[i];
        }
        return new string(ascii);
    }
}
