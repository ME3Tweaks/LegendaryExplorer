using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ME3ExplorerCore.Packages.CloningImportingAndRelinking
{
    public class EntryExporter
    {
        public static void ExportExportToPackage(ExportEntry sourceExport, string newPackagePath)
        {
            MEPackageHandler.CreateAndSavePackage(newPackagePath, sourceExport.Game);
            using var newP = MEPackageHandler.OpenMEPackage(newPackagePath);

            // We will want to cache files in memory to greatly speed this up
            PackageCache pc = new PackageCache();
            Dictionary<ImportEntry, ExportEntry> impToExpMap = new Dictionary<ImportEntry, ExportEntry>();

            // Check and resolve all imports upstream in the level
            RecursiveGetAllLevelImportsAsExports(sourceExport, impToExpMap, pc);

            // Imports are resolvable. We should port in level imports then port in the rest
            foreach (var mapping in impToExpMap)
            {
                if (newP.FindEntry(mapping.Key.InstancedFullPath) == null)
                {
                    // port it in
                    var parent = PortParents(mapping.Value, newP);
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, mapping.Value, newP, parent, true, out _);
                }
            }

            // Import the original item now
            var lParent = PortParents(sourceExport, newP);
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport, newP, lParent, true, out _);


            Debug.WriteLine("Imports are resolvable!");
            newP.Save();
            pc.ReleasePackages();
        }

        /// <summary>
        /// Ports in the parents of the source entry into the target package. They should be Package exports. Items that are found in the target already are not ported. The direct parent of the source IEntry is returned, in the target package.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static IEntry PortParents(IEntry source, IMEPackage target)
        {
            Stack<IEntry> parentStack = new Stack<IEntry>();
            IEntry entry = source;
            while (entry.Parent != null)
            {
                parentStack.Push(entry.Parent);
                entry = entry.Parent;
            }

            // Create parents first
            IEntry parent = null;
            foreach (var pEntry in parentStack)
            {
                var existingEntry = target.FindEntry(pEntry.InstancedFullPath);
                if (existingEntry == null)
                {
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, pEntry, target, parent, true, out parent);
                } else
                {
                    parent = existingEntry;
                }
            }

            return parent;
        }

        private static void RecursiveGetAllLevelImportsAsExports(ExportEntry sourceExport, Dictionary<ImportEntry, ExportEntry> resolutionMap, PackageCache cache)
        {
            var references = EntryImporter.GetAllReferencesOfExport(sourceExport);
            var importReferences = references.OfType<ImportEntry>().OrderBy(x => x.InstancedFullPath).ToList();
            foreach (var import in importReferences)
            {
                // Populate imports to next file up
                var instancedFullPath = import.InstancedFullPath;
                if (instancedFullPath.StartsWith("Core."))
                    continue; // A lot of these are not resolvable cause they're native
                if (import.Game == MEGame.ME2 && instancedFullPath == "BioVFX_Z_TEXTURES.Generic.Glass_Shards_Norm")
                    continue; // This texture for some reason is not stored in package files... not sure where, or how it is loaded into memory

                var resolved = EntryImporter.ResolveImport(import, cache);
                if (resolved == null)
                {
                    throw new Exception($"Cannot safely export this export to a new package: Import {import.InstancedFullPath} is unresolvable");
                }

                // see if this is level resolved item
                var sourcePath = Path.GetFileNameWithoutExtension(resolved.FileRef.FilePath);
                if (IsLevelFile(sourcePath))
                {

                    resolutionMap[import] = resolved;
                    RecursiveGetAllLevelImportsAsExports(resolved, resolutionMap, cache);
                }
            }
        }

        /// <summary>
        /// Does this packagename start with BioA/D/P/S/Snd?
        /// </summary>
        /// <param name="packageNameWithoutExtension"></param>
        /// <returns></returns>
        public static bool IsLevelFile(string packageNameWithoutExtension)
        {
            if (packageNameWithoutExtension.StartsWith("BioA_", StringComparison.InvariantCultureIgnoreCase)) return true; //Art
            if (packageNameWithoutExtension.StartsWith("BioD_", StringComparison.InvariantCultureIgnoreCase)) return true; //Design
            if (packageNameWithoutExtension.StartsWith("BioP_", StringComparison.InvariantCultureIgnoreCase)) return true; //Persistent
            if (packageNameWithoutExtension.StartsWith("BioS_", StringComparison.InvariantCultureIgnoreCase)) return true; // Sound (ME2)
            if (packageNameWithoutExtension.StartsWith("BioSnd_", StringComparison.InvariantCultureIgnoreCase)) return true; // Sound (ME2,ME3)
            return false;
        }
    }
}
