using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorerCore.Diagnostics
{
    public static class PackageDiags
    {

        /// <summary>
        /// Fixes forced export chain for the given export entry
        /// </summary>
        /// <param name="exp"></param>
        private static void FixFEChain(ExportEntry exp)
        {
            var root = exp.GetRoot();
            if (root == exp)
                return; // No chain

            var shouldBeFE = root is ExportEntry rExp ? rExp.IsForcedExport : true;
            if (shouldBeFE)
            {
                exp.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
            }
            else
            {
                exp.ExportFlags &= ~UnrealFlags.EExportFlags.ForcedExport;
            }

            if (exp.Parent is ExportEntry pExp)
            {
                FixFEChain(pExp);
            }
        }

        /// <summary>
        /// Scans the specified package and automatically repairs all invalid forced export chains.
        /// </summary>
        /// <remarks>This method displays a confirmation dialog before making any changes. After
        /// completion, a message box indicates that the operation is done. No action is taken if the package is not
        /// loaded or the user cancels the operation.</remarks>
        /// <param name="package">The package to scan and repair. Cannot be null.</param>
        public static void FixBadForcedExport(IMEPackage package)
        {
            var badLeaves = PackageDiags.GetBadForcedExportLeaves(package);

            foreach (var leaf in badLeaves)
            {
                FixFEChain(leaf.Entry as ExportEntry);
            }
        }

        private static bool hasValidFEChain(ExportEntry exp, bool isExpectingFE)
        {
            var parent = exp.Parent;
            if (parent == null)
                return true; // Nothing left to check
            if (parent is ImportEntry imp)
            {
                // Exports under imports must ALWAYS be forced exports
                // how could they possibly originate here?
                return isExpectingFE; // If we expect FE, this is valid
            }

            var parentE = parent as ExportEntry;
            if (parentE.IsForcedExport ^ isExpectingFE)
            {
                // It differs
                return false;
            }

            return hasValidFEChain(parentE, isExpectingFE);
        }

        /// <summary>
        /// Scans the package for forced export leaves that have inconsistent forced export flags in their parent chain
        /// </summary>
        /// <param name="package"></param>
        /// <returns></returns>
        public static List<EntryStringPair> GetBadForcedExportLeaves(IMEPackage package)
        {
            // Get list of leaves
            List<ExportEntry> leaves = new();
            foreach (var exp in package.Exports)
            {
                var children = exp.GetChildren();
                if (!children.Any())
                {
                    leaves.Add(exp);
                }
            }

            // Enumerate leaves and check their chains
            List<EntryStringPair> badLeaves = new();
            foreach (var leaf in leaves)
            {
                var originalValue = (leaf.ExportFlags & UnrealFlags.EExportFlags.ForcedExport) != 0;
                var isValidChain = hasValidFEChain(leaf, originalValue);
                if (!isValidChain)
                {
                    var rootObj = leaf.GetRoot();
                    if (rootObj.IsTrash())
                    {
                        // Technically these should be fixed too (should be ForcedExport to minimize memory usage), but we're not going to consider them
                        // as bad leaves
                        continue;
                    }
                    badLeaves.Add(new EntryStringPair(leaf, $"Inconsistent forced export on {leaf.InstancedFullPath}, should be forced: {(leaf.GetRoot() is ExportEntry root ? root.IsForcedExport : false)}"));
                }
            }
            return badLeaves;
        }
    }
}