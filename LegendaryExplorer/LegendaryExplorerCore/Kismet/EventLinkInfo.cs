using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


/// <summary>
/// Basic description of a single EventLink (bottom of kismet action - this includes all links)
/// </summary>
[DebuggerDisplay("EventLink {LinkDesc}")]
public class EventLinkInfo
{
    /// <summary>LinkDesc property value</summary>
    public string LinkDesc { get; set; }
    
    /// <summary>Expected type of event</summary>
    public IEntry ExpectedType { get; set; }
    
    /// <summary>Expected type name of event</summary>
    public string ExpectedTypeName => ExpectedType.ObjectName;

    /// <summary>Sequence objects that are linked to this event link</summary>
    public List<IEntry> LinkedNodes { get; set; }

    /// <summary>
    /// Factory method to create a <see cref="EventLinkInfo"/> from a SeqEventLink struct
    /// </summary>
    /// <param name="sp">SeqEventLink struct property</param>
    /// <param name="package">Package containing sequence object</param>
    /// <returns>New EventLinkInfo</returns>
    public static EventLinkInfo FromStruct(StructProperty sp, IMEPackage package)
    {
        return new EventLinkInfo()
        {
            LinkDesc = sp.GetProp<StrProperty>("LinkDesc"),
            ExpectedType = sp.GetProp<ObjectProperty>("ExpectedType").ResolveToEntry(package),
            LinkedNodes = sp.GetProp<ArrayProperty<ObjectProperty>>("LinkedEvents")?.Select(x => x.ResolveToEntry(package)).ToList() ?? new List<IEntry>()
        };
    }
}