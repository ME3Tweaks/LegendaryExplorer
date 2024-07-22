using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    public class EntryExporter
    {
        private static List<EntryStringPair> ExportExportToPackageInternal(ExportEntry sourceExport, IMEPackage targetPackage, out IEntry portedEntry, PackageCache globalCache = null, PackageCache pc = null, ObjectInstanceDB targetDb = null)
        {
            List<EntryStringPair> issues = new List<EntryStringPair>();

            // We will want to cache files in memory to greatly speed this up
            var newCache = pc == null;
            pc ??= new PackageCache();
            Dictionary<ImportEntry, ExportEntry> impToExpMap = new Dictionary<ImportEntry, ExportEntry>();

            // Check and resolve all imports upstream in the level
            var unresolvableImports = RecursiveGetAllLevelImportsAsExports(sourceExport, impToExpMap, globalCache, pc);
            issues.AddRange(unresolvableImports);

            // Imports are resolvable. We should port in level imports then port in the rest
            var impToExpMapList = impToExpMap.ToList().OrderBy(x => x.Key.InstancedFullPath.Length); // Shorter names go first... should help ensure parents are generated first... maybe.....

            foreach (var mapping in impToExpMapList)
            {
                if (targetPackage.FindEntry(mapping.Key.InstancedFullPath) == null)
                {
                    // port it in
                    //Debug.WriteLine($"Porting in: {mapping.Key.InstancedFullPath}");
                    var parent = PortParents(mapping.Value, targetPackage);
                    var relinkResults1 = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, mapping.Value, targetPackage, parent, true, new RelinkerOptionsPackage() { ImportExportDependencies = true, Cache = pc, TargetGameDonorDB = targetDb }, out _);
                    issues.AddRange(relinkResults1);
                }
                else
                {
                    //Debug.WriteLine($"Already exists due to other porting in: {mapping.Key.InstancedFullPath}");
                }
            }

            // Import the original item now
            var lParent = PortParents(sourceExport, targetPackage);

            // Test the entry was not ported in already, such as from a Parent reference
            var newEntry = targetPackage.FindEntry(sourceExport.InstancedFullPath);
            if (newEntry == null)
            {
                var relinkResults2 = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport, targetPackage, lParent, true, new RelinkerOptionsPackage() { ImportExportDependencies = true, Cache = pc, TargetGameDonorDB = targetDb }, out newEntry);
                issues.AddRange(relinkResults2);
            }

            if (newCache)
            {
                pc.ReleasePackages();
            }

            portedEntry = newEntry;

            return issues;
        }

        /// <summary>
        /// Exports the export and all required dependencies to the target package. Does not save the target package.
        /// </summary>
        /// <param name="sourceExport"></param>
        /// <param name="targetPackage"></param>
        /// <param name="newEntry"></param>
        /// <param name="compress"></param>
        /// <param name="globalCache"></param>
        /// <param name="packageCache"></param>
        /// <returns></returns>
        public static List<EntryStringPair> ExportExportToPackage(ExportEntry sourceExport, IMEPackage targetPackage, out IEntry newEntry, PackageCache globalCache = null, PackageCache packageCache = null, ObjectInstanceDB targetDb = null)
        {
            var exp = ExportExportToPackageInternal(sourceExport, targetPackage, out var nEntry, globalCache, packageCache, targetDb);
            newEntry = nEntry;
            return exp;
        }

        /// <summary>
        /// Exports the export and all required dependencies to a package file located at the specified path.
        /// </summary>
        /// <param name="sourceExport"></param>
        /// <param name="newPackagePath"></param>
        /// <param name="newEntry"></param>
        /// <param name="compress"></param>
        /// <param name="globalCache"></param>
        /// <param name="pc"></param>
        /// <returns></returns>
        public static List<EntryStringPair> ExportExportToFile(ExportEntry sourceExport, string newPackagePath, out IEntry newEntry, bool? compress = null, PackageCache globalCache = null, PackageCache pc = null)
        {
            MEPackageHandler.CreateAndSavePackage(newPackagePath, sourceExport.Game);
            using var p = MEPackageHandler.OpenMEPackage(newPackagePath);
            var result = ExportExportToPackage(sourceExport, p, out newEntry, globalCache, pc);
            p.Save();
            return result;
        }

        /// <summary>
        /// Ports in the parents of the source entry into the target package. They should be Package exports. Items that are found in the target already are not ported. The direct parent of the source IEntry is returned, in the target package.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="importAsImport">If the parents should be imported as an import instead of an export if they don't exist. This should only be used if you're porting in an import and creating its parents.</param>
        /// <returns></returns>
        public static IEntry PortParents(IEntry source, IMEPackage target, bool importAsImport = false)
        {
            var packagename = Path.GetFileNameWithoutExtension(source.FileRef.FilePath);
            if (packagename != null && IsGlobalNonStartupFile(packagename))
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
                        if (importAsImport)
                        {
                            var newImport = new ImportEntry(target, parent, pEntry.ObjectName)
                            {
                                ClassName = "Package",
                                PackageFile = "Core"
                            };
                            target.AddImport(newImport);
                            parent = newImport;
                        }
                        else
                        {
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, pEntry, target, parent, false, new RelinkerOptionsPackage() { ImportExportDependencies = false }, out parent);
                        }
                    }
                    else
                    {
                        // Port in with relink... this could get really ugly performance wise
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, pEntry, target, parent, true, new RelinkerOptionsPackage() { ImportExportDependencies = true }, out parent);
                    }
                    var entriesAC = target.ExportCount;
                    if (entriesAC - entriesBC > parentCount)
                    {
                        // We ported in too many things!!
                        Debug.WriteLine("We appear to have ported too many things!!");
                        // Debugger.Break();
                    }
                }
                else
                {
                    parent = existingEntry;
                }
            }

            return parent;
        }

        /// <summary>
        /// Prepares a global package for porting by moving all objects referenced by it's objectreferencer into the subpackage of the same name
        /// </summary>
        /// <param name="sourcePackage"></param>
        /// <param name="packagename"></param>
        public static void PrepareGlobalFileForPorting(IMEPackage sourcePackage, string packagename)
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
                var refed = objReferencer.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedObjects").Select(x => x.ResolveToEntry(sourcePackage) as ExportEntry).Where(x => x != null && x.idxLink == 0).ToList();

                foreach (var refX in refed)
                {
                    // Point under package so it matches how it would be
                    // if it was cooked into a non-master file
                    refX.idxLink = fileRefExp.UIndex;

                    // Set as ForcedExport as we are now 'forced' into subpackage export
                    refX.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
                }
            }

            sourcePackage.InvalidateLookupTable();
        }

        /// <summary>
        /// Attempts to resolve all imports in a level file. 
        /// </summary>
        /// <param name="sourceExport"></param>
        /// <param name="resolutionMap"></param>
        /// <param name="globalCache">Global cache that will not have it's contents modified. Should contain things like SFXGame, Startup, etc.</param>
        /// <param name="cache">Cache for the local operation, such as the localization files, the upstream level files. This cache will be modified as packages are opened</param>
        /// <returns></returns>
        private static List<EntryStringPair> RecursiveGetAllLevelImportsAsExports(ExportEntry sourceExport, Dictionary<ImportEntry, ExportEntry> resolutionMap, PackageCache globalCache, PackageCache cache)
        {
            List<EntryStringPair> unresolvableImports = new List<EntryStringPair>();
            var references = EntryImporter.GetAllReferencesOfExport(sourceExport);
            var importReferences = references.OfType<ImportEntry>().OrderBy(x => x.InstancedFullPath).ToList();
            foreach (var import in importReferences)
            {
                // Populate imports to next file up
                var instancedFullPath = import.InstancedFullPath;
                if (instancedFullPath.StartsWith("Core.") || instancedFullPath.StartsWith("Engine."))
                    continue; // A lot of these are not resolvable cause they're native
                if (import.Game.IsGame2() && instancedFullPath == "BioVFX_Z_TEXTURES.Generic.Glass_Shards_Norm")
                    continue; // This texture is straight up missing from the game for some reason
                if (import.IsAKnownNativeClass())
                    continue; // Known native items can never be imported
                var resolved = EntryImporter.ResolveImport(import, globalCache, cache);
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
                    RecursiveGetAllLevelImportsAsExports(resolved, resolutionMap, globalCache, cache);
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
