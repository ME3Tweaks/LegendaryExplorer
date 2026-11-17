using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>
/// New exports for baked data in one package: numbered names that count on from the package's own, and the flags of the package's first export of the class
/// </summary>
public sealed class NewExports(IMEPackage package)
{
    /// <summary>Per new export's name, the next free number, and per class the flags new exports copy (both scan every export once).</summary>
    readonly Dictionary<string, int> nextNameNumber = new();
    readonly Dictionary<string, EObjectFlags?> templateFlags = new();

    /// <summary>A new export of <paramref name="className"/> under <paramref name="owner"/></summary>
    public ExportEntry Add(IEntry owner, string className, PropertyCollection props, ObjectBinary binary)
    {
        var export = new ExportEntry(package, owner, NextName(className), properties: props, binary: binary)
        {
            Class = EntryImporter.EnsureClassIsInFile(package, className, new RelinkerOptionsPackage()),
        };
        if (TemplateFlags(className) is { } flags) export.ObjectFlags = flags;
        package.AddExport(export);
        return export;
    }

    /// <summary>GetNextIndexedName, counting on from the first call instead of rescanning the exports for each new object.</summary>
    public NameReference NextName(string name)
    {
        if (!nextNameNumber.TryGetValue(name, out int number)) number = package.GetNextIndexedName(name).Number;
        nextNameNumber[name] = number + 1;
        return new NameReference(name, number);
    }

    /// <summary>The flags of the package's first export of these classes (in order of preference), if it has any.</summary>
    public EObjectFlags? TemplateFlags(params string[] classNames)
    {
        string key = string.Join("|", classNames);
        if (!templateFlags.TryGetValue(key, out var flags))
        {
            var template = classNames.Select(c => package.Exports.FirstOrDefault(e => e.ClassName == c)).FirstOrDefault(e => e != null);
            templateFlags[key] = flags = template?.ObjectFlags;
        }
        return flags;
    }
}
