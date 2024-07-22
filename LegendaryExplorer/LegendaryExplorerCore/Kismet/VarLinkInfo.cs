using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Kismet;

/// <summary>
/// Basic description of a single VarLink (bottom of kismet action - this includes all links)
/// </summary>
[DebuggerDisplay("VarLink {LinkDesc}, ExpectedType: {ExpectedTypeName}")]
public class VarLinkInfo
{
    /// <summary>LinkDesc property value</summary>
    public string LinkDesc { get; set; }
    /// <summary>PropertyName property value</summary>
    public string PropertyName { get; set; }
    /// <summary>Expected type of variable</summary>
    public IEntry ExpectedType { get; set; }
    /// <summary>Expected type name of variable</summary>
    public string ExpectedTypeName => ExpectedType.ObjectName;
    /// <summary>Sequence objects that are linked to this var link</summary>
    public List<IEntry> LinkedNodes { get; set; }

    /// <summary>
    /// Factory method to create a <see cref="VarLinkInfo"/> from a SeqVarLink struct
    /// </summary>
    /// <param name="sp">SeqVarLink struct property</param>
    /// <param name="package">Package containing sequence object</param>
    /// <returns>New VarLinkInfo</returns>
    public static VarLinkInfo FromStruct(StructProperty sp, IMEPackage package)
    {
        return new VarLinkInfo()
        {
            LinkDesc = sp.GetProp<StrProperty>("LinkDesc"),
            PropertyName = sp.GetProp<NameProperty>("PropertyName")?.Value,
            ExpectedType = sp.GetProp<ObjectProperty>("ExpectedType").ResolveToEntry(package),
            LinkedNodes = sp.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables")?.Select(x => x.ResolveToEntry(package)).ToList() ?? new List<IEntry>()
        };
    }
}