using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    public class EntryExporter
    {
        private static List<EntryStringPair> ExportExportToPackageInternal(ExportEntry sourceExport, IMEPackage targetPackage, out IEntry portedEntry, PackageCache cache = null, RelinkerOptionsPackage customROP = null, ObjectInstanceDB targetDb = null)
        {
            List<EntryStringPair> issues = new List<EntryStringPair>();

            // Does entry already exist in destination package?
            var newEntry = targetPackage.FindEntry(sourceExport.InstancedFullPath, sourceExport.ClassName);
            if (newEntry != null)
            {
                portedEntry = newEntry;
                return issues;
            }

            // We will want to cache files in memory to greatly speed this up
            var newCache = cache == null;
            cache ??= new PackageCache();
            Dictionary<ImportEntry, ExportEntry> impToExpMap = new Dictionary<ImportEntry, ExportEntry>();

            // Check and resolve all imports upstream in the level
            if (customROP == null || customROP.CheckImportsWhenExportingToPackage)
            {
                var unresolvableImports = RecursiveGetAllLevelImportsAsExports(sourceExport, impToExpMap, cache, customROP);
                issues.AddRange(unresolvableImports);

                // Imports are resolvable. We should port in level imports then port in the rest
                var impToExpMapList = impToExpMap.ToList().OrderBy(x => x.Key.InstancedFullPath.Length); // Shorter names go first... should help ensure parents are generated first... maybe.....

                foreach (var mapping in impToExpMapList)
                {
                    if (targetPackage.FindEntry(mapping.Key.InstancedFullPath) == null)
                    {
                        // port it in
                        //Debug.WriteLine($"Porting in: {mapping.Key.InstancedFullPath}");
                        var parent = PortParents(mapping.Value, targetPackage, customROP: customROP);
                        customROP?.CrossPackageMap.Clear(); // Do not persist this value, we do not want double relink
                        var relinkResults1 = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, mapping.Value, targetPackage, parent, true,
                            customROP ?? new RelinkerOptionsPackage() { ImportExportDependencies = true, Cache = cache }, out _);
                        issues.AddRange(relinkResults1);
                    }
                    else
                    {
                        //Debug.WriteLine($"Already exists due to other porting in: {mapping.Key.InstancedFullPath}");
                    }
                }
            }

            // Clear cross package map as the ROP will be used again. And you don't want to reuse that.
            customROP?.CrossPackageMap.Clear();

            // Import the original item now
            var lParent = PortParents(sourceExport, targetPackage, cache: cache);

            // Test the entry was not ported in already, such as from a Parent reference
            newEntry = targetPackage.FindEntry(sourceExport.InstancedFullPath, sourceExport.ClassName);
            if (newEntry == null)
            {
                var relinkResults2 = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport, targetPackage, lParent, true,
                    customROP ?? new RelinkerOptionsPackage()
                    {
                        ImportExportDependencies = true,
                        Cache = cache,
                        TargetGameDonorDB = targetDb
                    }, out newEntry);
                issues.AddRange(relinkResults2);
            }

            if (newCache)
            {
                cache.ReleasePackages();
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
        /// <param name="packageCache"></param>
        /// <returns></returns>
        public static List<EntryStringPair> ExportExportToPackage(ExportEntry sourceExport, IMEPackage targetPackage, out IEntry newEntry, PackageCache packageCache = null, RelinkerOptionsPackage customROP = null)
        {
            var exp = ExportExportToPackageInternal(sourceExport, targetPackage, out var nEntry, packageCache, customROP);
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
        public static List<EntryStringPair> ExportExportToFile(ExportEntry sourceExport, string newPackagePath, out IEntry newEntry, bool? compress = null, PackageCache cache = null)
        {
            MEPackageHandler.CreateAndSavePackage(newPackagePath, sourceExport.Game);
            using var p = MEPackageHandler.OpenMEPackage(newPackagePath);
            var result = ExportExportToPackage(sourceExport, p, out newEntry, cache);
            p.Save();
            return result;
        }

        /// <summary>
        /// Ports in the parents of the source entry into the target package. Items that are found in the target already are not ported. The direct parent of the source IEntry is returned, in the target package.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="importAsImport">If the parents should be imported as an import instead of an export if they don't exist. This should only be used if you're porting in an import and creating its parents.</param>
        /// <returns></returns>
        public static IEntry PortParents(IEntry source, IMEPackage target, bool importAsImport = false, PackageCache cache = null, RelinkerOptionsPackage customROP = null)
        {
            var packagename = Path.GetFileNameWithoutExtension(source.FileRef.FilePath);
            if (packagename != null)
            {
                if (IsGlobalNonStartupFile(packagename) && !source.FileRef.FileNameNoExtension.CaseInsensitiveEquals(target.FileNameNoExtension))
                {
                    // Porting out of file
                    PrepareGlobalFileForPorting(source.FileRef, packagename);
                }
            }

            Stack<IEntry> parentStack = new Stack<IEntry>();
            IEntry entry = source;
            while (entry.Parent != null)
            {
                parentStack.Push(entry.Parent);
                entry = entry.Parent;
            }

            // If the paths don't match then one of then this is a 'forced export', and we have to have the
            // root package in the tree. Imports will always require package name at the root,
            // however exports do not if they are not forced export.
            bool portingIntoLinkerPackage = false;
            if (entry.InstancedFullPath != entry.MemoryFullPath)
            {
                // Are we porting into a file with a different linker?
                if (!source.GetLinker().CaseInsensitiveEquals(target.FileNameNoExtension))
                {
                    // Porting out of file that uses !ForcedExport
                    var parentPackage = target.FindEntry(entry.FileRef.FileNameNoExtension, "Package"); // Sure hope nothing indexing
                    if (parentPackage == null)
                    {
                        // Create parent package
                        if (entry.FileRef.Game == MEGame.UDK)
                        {
                            // Root packages should be imports
                            // Not sure on this one... This could cause issues on nested packages
                            parentPackage = ExportCreator.CreatePackageImport(target, entry.FileRef.FileNameNoExtension);
                        }
                        else
                        {
                            parentPackage = ExportCreator.CreatePackageExport(target, entry.FileRef.FileNameNoExtension);
                        }
                    }

                    parentStack.Push(parentPackage);
                }
                // Target linker is the same
                // We are porting a forced export object out of linker package into its linker package
                // Commented out cause I am not really sure how to handle this...
                /*
                else
                {
                    parentStack.Pop();
                    portingIntoLinkerPackage = true;
                }*/
            }

            var parentCount = parentStack.Count;

            // Create parents first
            IEntry parent = null;
            foreach (var pEntry in parentStack)
            {
                var ifp = pEntry.InstancedFullPath;
                if (portingIntoLinkerPackage)
                {
                    // Strip off the linker
                    ifp = pEntry.InstancedFullPath.Substring(pEntry.GetLinker().Length + 1);
                }
                var existingEntry = target.FindEntry(ifp);
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
                            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, pEntry, target, parent, false, customROP ?? new RelinkerOptionsPackage() { ImportExportDependencies = false, Cache = cache }, out parent);
                        }
                    }
                    else
                    {
                        // Port in with relink... this could get really ugly performance wise
                        // Unsure how this will work if it tries to bring over things as we port INTO linker package
                        EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.AddSingularAsChild, pEntry, target, parent, true, new RelinkerOptionsPackage() { ImportExportDependencies = true, PortImportsMemorySafe = true, Cache = cache }, out parent);
                    }
                    //var entriesAC = target.ExportCount;
                    //if (entriesAC - entriesBC > parentCount)
                    //{
                    //    // We ported in too many things!!
                    //    Debug.WriteLine("We appear to have ported too many things!!");
                    //    // Debugger.Break();
                    //}
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
        /// <param name="cache">Cache for the local operation, such as the localization files, the upstream level files. This cache will be modified as packages are opened</param>
        /// <returns></returns>
        private static List<EntryStringPair> RecursiveGetAllLevelImportsAsExports(ExportEntry sourceExport, Dictionary<ImportEntry, ExportEntry> resolutionMap, PackageCache cache, RelinkerOptionsPackage rop = null)
        {
            List<EntryStringPair> unresolvableImports = new List<EntryStringPair>();
            var references = EntryImporter.GetAllReferencesOfExport(sourceExport);
            // I think orderby ensures shorter names go first. So an import parent of an import will resolve first.
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
                var resolved = EntryImporter.ResolveImport(import, cache);
                if (resolved == null)
                {
                    unresolvableImports.Add(new EntryStringPair(import, $"Import {import.InstancedFullPath} could not be resolved - cannot be safely used"));
                    continue;
                }

                if (resolved.FileRef.Localization != MELocalization.None && rop != null && !rop.PortLocalizationImportsMemorySafe)
                {
                    unresolvableImports.Add(new EntryStringPair(import, $"Import {import.InstancedFullPath} is for localized content - we will not be porting this as an export. This may require manual adjustment of the LOC file to work."));
                    continue;
                }

                // see if this is level resolved item
                var sourcePath = Path.GetFileNameWithoutExtension(resolved.FileRef.FilePath);
                if (IsLevelFile(sourcePath) || IsGlobalNonStartupFile(sourcePath))
                {
                    resolutionMap[import] = resolved;
                    RecursiveGetAllLevelImportsAsExports(resolved, resolutionMap, cache, rop);
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
