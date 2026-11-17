#nullable enable
using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages.NetIndexing;

public enum DeclarationSource
{
    /// <summary>The package file's own header; the linker is the file name.</summary>
    PackageFile,
    /// <summary>A root forced-export Package export; the linker is that export's name.</summary>
    ForcedExportPackage
}

/// <summary>
/// A place where a linker declares its guid and net object count. The count is the last generation's NetObjectCount
/// (the summary's for a package file, the export's GenerationNetObjectCount for a forced export), which is what
/// UPackage::InitNetInfo reads; -1 means the export has none, which the engine treats as PKG_ServerSideOnly.
/// </summary>
public readonly record struct LinkerDeclaration(string Linker, Guid Guid, int NetObjectCount, DeclarationSource Source);

/// <summary>One non-root export, tagged with the linker it is nested under. Key is the linker-qualified path.</summary>
public readonly record struct ExportObservation(string Linker, string Key, int NetIndex, bool LocalizedResource);

/// <summary>Everything net index checking needs from a single package file.</summary>
public sealed class PackageNetScan
{
    public string Path { get; }
    /// <summary>The file name without extension; also the name of the file's own linker.</summary>
    public string FileName { get; }
    public bool IsLocalizationFile { get; }
    public string? SkipReason { get; private set; }
    public string? Error { get; private set; }
    public List<LinkerDeclaration> Declarations { get; } = [];
    public List<ExportObservation> Exports { get; } = [];
    public int ExportErrors { get; private set; }

    private PackageNetScan(string path, string fileName)
    {
        Path = path;
        FileName = fileName;
        IsLocalizationFile = NetIndexKeys.IsLocalizationFile(fileName);
    }

    public static PackageNetScan Read(string path, MEGame expectedGame)
    {
        try
        {
            using var package = MEPackageHandler.OpenMEPackage(path, forceLoadFromDisk: true);
            if (package.Game != expectedGame)
            {
                return new PackageNetScan(path, System.IO.Path.GetFileNameWithoutExtension(path)) { SkipReason = package.Game.ToString() };
            }

            return FromPackage(package, path);
        }
        catch (Exception ex)
        {
            return new PackageNetScan(path, System.IO.Path.GetFileNameWithoutExtension(path)) { Error = ex.Message };
        }
    }

    /// <summary>Scans an already-open package without touching disk. Path only labels the scan; it defaults to the package's file path.</summary>
    public static PackageNetScan FromPackage(IMEPackage package, string? path = null)
    {
        var scan = new PackageNetScan(path ?? package.FilePath ?? package.FileNameNoExtension, package.FileNameNoExtension);
        try
        {
            if (package.FileNameNoExtension.StartsWith("GuidCache", StringComparison.OrdinalIgnoreCase))
            {
                // Conformance never guids or inventories these; they are the guid map, not content.
                scan.SkipReason = "GuidCache";
                return scan;
            }

            scan.Declarations.Add(new LinkerDeclaration(package.FileNameNoExtension, package.PackageGuid,
                ((MEPackage)package).Gen0NetworkedObjectCount, DeclarationSource.PackageFile));
            foreach (var export in package.Exports)
            {
                try
                {
                    // Mirrors Conformance: root forced-export packages are the linker declarations and carry no net index.
                    if (NetIndexKeys.IsForcedExportRoot(export))
                    {
                        var counts = export.GenerationNetObjectCount;
                        scan.Declarations.Add(new LinkerDeclaration(export.InstancedFullPath, export.PackageGUID,
                            counts.Length > 0 ? counts[^1] : -1, DeclarationSource.ForcedExportPackage));
                        continue;
                    }

                    scan.Exports.Add(new ExportObservation(export.GetLinker(), NetIndexKeys.GetLinkerQualifiedPath(export), export.NetIndex,
                        export.ObjectFlags.Has(UnrealFlags.EObjectFlags.LocalizedResource)));
                }
                catch
                {
                    scan.ExportErrors++;
                }
            }
        }
        catch (Exception ex)
        {
            scan.Error = ex.Message;
        }

        return scan;
    }
}
