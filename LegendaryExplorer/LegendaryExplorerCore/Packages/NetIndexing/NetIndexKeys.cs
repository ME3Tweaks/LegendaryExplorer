#nullable enable
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Packages.NetIndexing;

/// <summary>How net index scanning names and classifies objects, so every consumer keys them identically.</summary>
public static class NetIndexKeys
{
    /// <summary>
    /// The object's path including its linker, so the same object is keyed the same in every file. Objects under a root
    /// forced-export package (or root import) already carry it; objects belonging to the package file itself do not, e.g.
    /// SFXGame.pcc's Bio2DA is SFXGame.Bio2DA under BIOP_MP_COMMON's forced-export SFXGame package.
    /// </summary>
    public static string GetLinkerQualifiedPath(ExportEntry export)
    {
        IEntry root = export;
        while (root.Parent != null)
            root = root.Parent;

        bool rootIsLinker = root is ImportEntry || root is ExportEntry { ClassName: "Package", IsForcedExport: true };
        return rootIsLinker ? export.InstancedFullPath : $"{export.FileRef.FileNameNoExtension}.{export.InstancedFullPath}";
    }

    /// <summary>
    /// A root forced-export Package export: the declaration of a linker (guid and count), not a net object. Conformance
    /// gives these no net index.
    /// </summary>
    public static bool IsForcedExportRoot(ExportEntry export) =>
        export.idxLink == 0 && export.ClassName == "Package" && export.IsForcedExport;

    /// <summary>True if the file is a per-language package (<c>_INT</c>, <c>_LOC_DEU</c>, ...). Takes a file name or path, with or without extension.</summary>
    public static bool IsLocalizationFile(string fileNameOrPath) =>
        System.IO.Path.GetFileNameWithoutExtension(fileNameOrPath).GetUnrealLocalization() != MELocalization.None;
}
