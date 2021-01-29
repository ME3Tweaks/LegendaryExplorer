using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.Packages.CloningImportingAndRelinking
{
    public class EntryExporter
    {
        public static List<EntryStringPair> ExportExportToPackage(ExportEntry sourceExport, string newPackagePath)
        {
            List<EntryStringPair> issues = new List<EntryStringPair>();
            MEPackageHandler.CreateAndSavePackage(newPackagePath, sourceExport.Game);
            using var newP = MEPackageHandler.OpenMEPackage(newPackagePath);

            // We will want to cache files in memory to greatly speed this up
            PackageCache pc = new PackageCache();
            Dictionary<ImportEntry, ExportEntry> impToExpMap = new Dictionary<ImportEntry, ExportEntry>();

            // Check and resolve all imports upstream in the level
            var unresolvableImports = RecursiveGetAllLevelImportsAsExports(sourceExport, impToExpMap, pc);
            issues.AddRange(unresolvableImports);

            // Imports are resolvable. We should port in level imports then port in the rest
            var impToExpMapList = impToExpMap.ToList().OrderBy(x => x.Key.InstancedFullPath.Length); // Shorter names go first... should help ensure parents are generated first... maybe.....

            foreach (var mapping in impToExpMapList)
            {
                if (newP.FindEntry(mapping.Key.InstancedFullPath) == null)
                {
                    // port it in
                    Debug.WriteLine($"Porting in: {mapping.Key.InstancedFullPath}");
                    var parent = PortParents(mapping.Value, newP);
                    var relinkResults1 = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, mapping.Value, newP, parent, true, out _);
                    issues.AddRange(relinkResults1);
                }
                else
                {
                    Debug.WriteLine($"Already exists due to other porting in: {mapping.Key.InstancedFullPath}");
                }
            }

            // Import the original item now
            var lParent = PortParents(sourceExport, newP);
            var relinkResults2 = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport, newP, lParent, true, out _);
            issues.AddRange(relinkResults2);

            newP.Save();
            pc.ReleasePackages();

            return issues;
        }

        /// <summary>
        /// Ports in the parents of the source entry into the target package. They should be Package exports. Items that are found in the target already are not ported. The direct parent of the source IEntry is returned, in the target package.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static IEntry PortParents(IEntry source, IMEPackage target)
        {
            var packagename = Path.GetFileNameWithoutExtension(source.FileRef.FilePath);
            if (IsGlobalNonStartupFile(packagename))
            {
                PrepareGlobalFileForPorting(source.FileRef, packagename);

            }


            Stack<IEntry> parentStack = new Stack<IEntry>();
            IEntry entry = source;
            while (entry.Parent != null)
            {
                parentStack.Push(entry.Parent);
                entry = entry.Parent;
            }

            

            var parentCount = parentStack.Count;

            // Create parents first
            IEntry parent = null;
            foreach (var pEntry in parentStack)
            {
                var existingEntry = target.FindEntry(pEntry.InstancedFullPath);
                if (existingEntry == null)
                {
                    var entriesBC = target.ExportCount;
                    if (pEntry.ClassName == "Package")
                    {
                        // Port in package
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, pEntry, target, parent, false, out parent);
                    }
                    else
                    {
                        // Port in with relink... this could get really ugly performance wise
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, pEntry, target, parent, true, out parent);
                    }
                    var entriesAC = target.ExportCount;
                    if (entriesAC - entriesBC > parentCount)
                    {
                        // We ported in too many things!!
                        Debugger.Break();
                    }
                }
                else
                {
                    parent = existingEntry;
                }
            }

            return parent;
        }

        private static void PrepareGlobalFileForPorting(IMEPackage sourcePackage, string packagename)
        {
            // SirC if you ever see this i'm really sorry lol
            // This is an absolute hackjob cause changing relinker to do this would be hell
            // Mgamerz jan 2021
            //
            // Modify the global package by adding a same-named top level package,
            // and repoint everything at the root of the package to have this as a link
            // this way relinker doesn't need changed to import from these files properly
            if (sourcePackage.FindExport(packagename) == null)
            {
                ExportCreator.CreatePackageExport(sourcePackage, packagename);
            }

            var fileRefExp = sourcePackage.FindExport(packagename);
            var objReferencer = sourcePackage.Exports.FirstOrDefault(x => x.idxLink == 0 && x.ObjectName == "ObjectReferencer" && x.ClassName == "ObjectReferencer");
            if (objReferencer != null)
            {
                // We need to enumerate all referenced objects. They are the items unique in this 'package' that are not sourced from others.
                // We only care about the roots as the rest will cascade the change
                var refed = objReferencer.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedObjects").Select(x=>x.ResolveToEntry(sourcePackage) as ExportEntry).Where(x=>x != null && x.idxLink == 0).ToList();

                foreach(var refX in refed)
                {
                    // Point under package so it matches how it would be
                    // if it was cooked into a non-master file
                    refX.idxLink = fileRefExp.UIndex;
                }
            }


        }

        private static List<EntryStringPair> RecursiveGetAllLevelImportsAsExports(ExportEntry sourceExport, Dictionary<ImportEntry, ExportEntry> resolutionMap, PackageCache cache)
        {
            List<EntryStringPair> unresolvableImports = new List<EntryStringPair>();
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
                if (UnrealObjectInfo.IsAKnownNativeClass(import))
                    continue; // Known native items can never be imported
                var resolved = EntryImporter.ResolveImport(import, cache);
                if (resolved == null)
                {
                    unresolvableImports.Add(new EntryStringPair(import, $"Import {import.InstancedFullPath} could not be resolved - cannot be safely used"));
                    continue;
                }

                // see if this is level resolved item
                var sourcePath = Path.GetFileNameWithoutExtension(resolved.FileRef.FilePath);
                if (IsLevelFile(sourcePath) || IsGlobalNonStartupFile(sourcePath))
                {

                    resolutionMap[import] = resolved;
                    RecursiveGetAllLevelImportsAsExports(resolved, resolutionMap, cache);
                }
            }

            return unresolvableImports;
        }

        private static bool IsGlobalNonStartupFile(string sourcePath)
        {
            if (sourcePath.StartsWith("BIOG")) return true;
            if (sourcePath.StartsWith("GUI_")) return true;

            return false;
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
