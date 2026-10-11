using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// Owns a bake until it is saved or discarded. Packages keep their source paths for asset resolution;
/// only the texture cache is staged on disk. The destination cache lock is held throughout the preview.
/// Close any renderer using these packages before saving or disposing this object.
/// </summary>
public sealed class LightmassBakePreview : IDisposable
{
    internal List<IMEPackage> Packages { get; } = [];
    public IReadOnlyList<IMEPackage> Levels => Packages;
    public LightmassBakeResult Result { get; internal set; }
    public string TemporaryDirectory { get; }
    internal string CopiedCacheSource { get; }
    public bool HasChanges => Result?.Rewrites.Count > 0;
    public bool IsSaved { get; private set; }

    readonly FileStream cacheLock;
    readonly string cacheName, destinationCache;
    readonly Dictionary<string, string> destinations = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, FileStamp> expected = new(StringComparer.OrdinalIgnoreCase);
    bool disposed, cacheSaved;

    readonly record struct FileStamp(bool Exists, long Length, DateTime Modified)
    {
        public static FileStamp Read(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? new(true, file.Length, file.LastWriteTimeUtc) : default;
        }
    }

    internal LightmassBakePreview(LightmassBakeJob job, FileStream cacheLock)
    {
        this.cacheLock = cacheLock;
        try
        {
            cacheName = job.TfcName;
            destinationCache = Path.GetFullPath(Path.Combine(job.ResolvedTfcDirectory, cacheName + ".tfc"));
            expected[destinationCache] = FileStamp.Read(destinationCache);
            foreach (var file in job.Files.Where(f => f.Editable))
            {
                string source = Path.GetFullPath(file.Path);
                string destination = string.IsNullOrWhiteSpace(job.OutputDirectory) ? source
                    : Path.GetFullPath(Path.Combine(job.OutputDirectory, Path.GetFileName(source)));
                destinations[source] = destination;
                expected[source] = FileStamp.Read(source);
                expected[destination] = FileStamp.Read(destination);
            }
            TemporaryDirectory = Directory.CreateTempSubdirectory("LEX-LightmassPreview-").FullName;
            // Keep the existing cache's GUID and offsets, including references from packages outside this job.
            if (File.Exists(destinationCache))
            {
                File.Copy(destinationCache, Path.Combine(TemporaryDirectory, cacheName + ".tfc"));
                CopiedCacheSource = destinationCache;
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Renderer override for rewritten packages only. Untouched/context textures still resolve from their original paths.
    /// Returning null asks the renderer to use its normal texture lookup.
    /// </summary>
    public byte[] ReadPreviewTexture(Texture2DMipInfo mip)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (mip.Export == null || Result == null || !Result.Rewrites.ContainsKey(mip.Export.FileRef)
            || !string.Equals(mip.TextureCacheName, cacheName, StringComparison.OrdinalIgnoreCase)) return null;
        return Texture2D.GetTextureData(mip.Export.Game, mip.Mip, mip.storageType, true,
            mip.uncompressedSize, mip.compressedSize, mip.externalOffset, mip.TextureCacheName,
            packagePathForLocalLookup: Path.Combine(TemporaryDirectory, mip.Export.FileRef.FileNameNoExtension + ".pcc"));
    }

    /// <summary>
    /// Saves the exact packages inspected in preview. Serialize everything before publishing the cache, then replace
    /// files individually. A failed save can be retried; SavedFiles records any packages already published.
    /// </summary>
    public void Save(Action<string> log = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsSaved) return;
        log ??= _ => { };
        if (!HasChanges) { IsSaved = true; return; }
        VerifyUnchanged();
        var files = Result.Rewrites.Keys.Where(p => destinations.ContainsKey(Path.GetFullPath(p.FilePath)))
            .Select(p => (Package: p, Destination: destinations[Path.GetFullPath(p.FilePath)],
                Staged: Path.Combine(TemporaryDirectory, Path.GetFileName(p.FilePath)))).ToList();
        foreach (var file in files)
            LightmassBakeJob.Save(file.Package, file.Staged);
        VerifyUnchanged();

        if (!cacheSaved && Result.TfcPath != null)
        {
            log($"Saving {destinationCache}...");
            Publish(Result.TfcPath, destinationCache);
            expected[destinationCache] = FileStamp.Read(destinationCache);
            cacheSaved = true;
        }
        foreach (var file in files)
        {
            log($"Saving {file.Destination}...");
            Publish(file.Staged, file.Destination);
            expected[file.Destination] = FileStamp.Read(file.Destination);
            if (!Result.SavedFiles.Contains(file.Destination, StringComparer.OrdinalIgnoreCase))
                Result.SavedFiles.Add(file.Destination);
        }
        Result.TfcPath = destinationCache;
        IsSaved = true;
    }

    void VerifyUnchanged()
    {
        foreach (var (path, stamp) in expected)
            if (FileStamp.Read(path) != stamp)
                throw new IOException($"{path} changed since this bake started. Discard this preview and bake again before saving.");
    }

    static void Publish(string staged, string destination)
    {
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".lexbake-tmp";
        try
        {
            File.Copy(staged, temporary);
            File.Move(temporary, destination, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"{destination} could not be saved: {e.Message}", e);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            foreach (var package in Packages) package.Dispose();
            if (TemporaryDirectory != null) Directory.Delete(TemporaryDirectory, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A cleanup failure must not hide a bake/save error or keep the destination locked.
            Debug.WriteLine($"Couldn't remove Lightmass preview {TemporaryDirectory}: {e.Message}");
        }
        finally
        {
            cacheLock.Dispose();
        }
    }
}
