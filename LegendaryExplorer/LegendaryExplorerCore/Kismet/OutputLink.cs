using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Kismet;

/// <summary>
/// Represents an output link from a sequence object
/// </summary>
public class OutputLink
{
    /// <summary>The sequence object that this links to</summary>
    public IEntry LinkedOp { get; set; }
    /// <summary>The InputLinkIdx property of this link</summary>
    public int InputLinkIdx { get; set; }

    /// <summary>
    /// Generates a SeqOpInputOutputLink StructProperty from this OutboundLink
    /// </summary>
    /// <returns>Created StructProperty</returns>
    public StructProperty GenerateStruct()
    {
        return new StructProperty("SeqOpOutputInputLink", false,
            new ObjectProperty(LinkedOp.UIndex, "LinkedOp"),
            new IntProperty(InputLinkIdx, "InputLinkIdx"),
            new NoneProperty());
    }

    /// <summary>
    /// Factory method to create an <see cref="OutputLink"/> from a SeqOpOutputInputLink StructProperty
    /// </summary>
    /// <param name="sp">SeqOpOutputInputLink StructProperty</param>
    /// <param name="package">Package file that contains this sequence</param>
    /// <returns>New OutboundLink</returns>
    public static OutputLink FromStruct(StructProperty sp, IMEPackage package)
    {
        return new OutputLink()
        {
            LinkedOp = sp.GetProp<ObjectProperty>("LinkedOp")?.ResolveToEntry(package),
            InputLinkIdx = sp.GetProp<IntProperty>("InputLinkIdx")
        };
    }

    /// <summary>
    /// Factory method to create an OutboundLink
    /// </summary>
    /// <param name="exportEntry">Sequence object to create link to</param>
    /// <param name="inputLinkIdx">Link index</param>
    /// <returns>New OutboundLink</returns>
    public static OutputLink FromTargetExport(ExportEntry exportEntry, int inputLinkIdx)
    {
        //HB 12/14/21: Why is this not just a constructor?
        return new OutputLink()
        {
            LinkedOp = exportEntry,
            InputLinkIdx = inputLinkIdx
        };
    }
}