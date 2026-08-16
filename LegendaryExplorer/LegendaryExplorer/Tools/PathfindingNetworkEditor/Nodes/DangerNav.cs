using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    /// <summary>
    /// Represents a packed danger-nav entry for a <see cref="CoverSlot"/>.
    /// The data is stored as a packed int: upper 16 bits = DangerCost, lower 16 bits = NavRefs index.
    /// </summary>
    public class DangerNav
    {
        /// <summary>Danger cost value (upper 16 bits of the packed int).</summary>
        public int DangerCost { get; set; }

        /// <summary>The target nav point export, or <see langword="null"/> if unresolved or cross-level.</summary>
        public ExportEntry? NavExport { get; set; }

        /// <summary>Guid of the target nav point for cross-level references.</summary>
        public Guid NavGuid { get; set; }

        /// <summary>Resolved at post-load time: the <see cref="NavigationPoint"/> node, or <see langword="null"/> if unresolvable.</summary>
        public NavigationPoint? ResolvedNav { get; set; }

        public static DangerNav FromStruct(int value, IMEPackage package, Level level)
        {
            var dangerCost = (value & unchecked((int)0xFFFF0000)) >> 16;
            var navRefIdx = value & 0x0000FFFF;

            ExportEntry? navExport = null;
            Guid navGuid = Guid.Empty;

            if (navRefIdx < level.NavRefs.Count)
            {
                var navRef = level.NavRefs[navRefIdx];
                if (navRef > 0)
                {
                    navExport = package.GetUExport(navRef);
                }
                else
                {
                    navGuid = level.CrossLevelNavGuidRefs
                        .FirstOrDefault(x => x.CoverIndexIdx == navRefIdx).Guid;
                }
            }

            return new DangerNav
            {
                DangerCost = dangerCost,
                NavExport = navExport,
                NavGuid = navGuid,
            };
        }

        internal void ResolvePostLoad(Dictionary<ExportEntry, NavigationPoint> exportLookupMap, Dictionary<Guid, NavigationPoint> crossLevelLookup)
        {
            if (NavExport == null && NavGuid != Guid.Empty)
            {
                if (crossLevelLookup.TryGetValue(NavGuid, out var resolvedNav))
                {
                    ResolvedNav = resolvedNav;
                }
                else
                {
                    Debug.WriteLine("DangerNav: Failed to resolve cross-level nav point.");
                }
            }
            else if (NavExport != null)
            {
                if (exportLookupMap.TryGetValue(NavExport, out var resolvedNav))
                {
                    ResolvedNav = resolvedNav;
                }
                else
                {
                    Debug.WriteLine("DangerNav: Failed to resolve in-file nav point.");
                }
            }
        }
    }
}
