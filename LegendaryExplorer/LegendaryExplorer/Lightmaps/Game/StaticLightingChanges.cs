using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Lightmaps.Game;

/// <summary>
/// What's changed since its level was last baked: lights that contribute to static lighting, and actors a bake lights or casts shadows from, added
/// or changed by a Level Editor commit, and a level whose commit deleted one. Kept in the file, as the cooker's RF_MarkedByCooker, which shipping
/// levels don't have and the games never see: they keep only flags &amp; 0x867F012500080700 of an export they load (ULinkerLoad::CreateExport, LE1 RVA
/// 0x1354A0, LE2 RVA 0xDFAD0, LE3 RVA 0xFBDD0). A bake clears it in the levels it finishes rebaking
/// </summary>
public static class StaticLightingChanges
{
    private const UnrealFlags.EObjectFlags Flag = UnrealFlags.EObjectFlags.MarkedByCooker;

    public static bool IsMarked(ExportEntry export) => (export.ObjectFlags & Flag) != 0;

    public static void Mark(ExportEntry export)
    {
        if (!IsMarked(export)) export.ObjectFlags |= Flag;
    }

    /// <summary>For a bake of <paramref name="package"/>: nothing in it is changed since its last bake any more</summary>
    /// <returns>Whether anything was marked</returns>
    public static bool ClearAll(IMEPackage package)
    {
        bool any = false;
        foreach (var export in package.Exports.Where(IsMarked))
        {
            export.ObjectFlags &= ~Flag;
            any = true;
        }
        return any;
    }
}
