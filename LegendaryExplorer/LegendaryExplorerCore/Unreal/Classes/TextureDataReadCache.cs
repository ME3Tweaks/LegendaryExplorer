using System;
using System.Collections.Generic;
using System.IO;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.Classes;

/// <summary>
/// Reuses resolved texture archive paths and read-only streams during a bounded load.
/// Dispose at the end of the load so later edits can replace archives or change local overrides.
/// Mip data is not cached, and the normal texture decompression path is preserved.
/// Not thread-safe: mips may share an archive stream, and a seek and its read are one operation.
/// </summary>
public sealed class TextureDataReadCache : IDisposable
{
    private readonly Dictionary<(MEGame Game, string PackageDirectory, string Name), string> paths = [];
    private readonly Dictionary<string, FileStream> streams = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private bool disposed;

    public byte[] ReadMip(Texture2DMipInfo mip, MEGame game)
        => ReadMip(game, mip.Mip, mip.storageType, mip.uncompressedSize, mip.compressedSize, mip.externalOffset,
            mip.TextureCacheName, mip.Export?.FileRef.FilePath);

    /// <summary>Reads captured mip metadata without accessing a live package on a background thread.</summary>
    public byte[] ReadMip(MEGame game, byte[] data, StorageTypes storage, int uncompressedSize, int compressedSize,
        int offset, string cacheName, string packagePath)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return Texture2D.GetTextureData(game, data, storage, true, uncompressedSize, compressedSize, offset, cacheName, null, null, packagePath, this);
    }

    private static (MEGame, string, string) Key(MEGame game, string packagePath, string name) => (game, Path.GetDirectoryName(packagePath), name);

    internal string FindPath(MEGame game, string packagePath, string name) => paths.GetValueOrDefault(Key(game, packagePath, name));

    internal void RememberPath(MEGame game, string packagePath, string name, string path) => paths[Key(game, packagePath, name)] = path;

    internal void ReadFile(string path, Action<Stream> read)
    {
        if (!streams.TryGetValue(path, out var stream))
        {
            // Other tools append to archives while a level is open. What's already here doesn't move.
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            streams.Add(path, stream);
        }
        read(stream);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var stream in streams.Values) stream.Dispose();
        streams.Clear();
        paths.Clear();
    }
}
