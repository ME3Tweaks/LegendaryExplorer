using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Shaders;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    /// <summary>
    /// Options that change how porting is done
    /// </summary>
    public class PortingOptions
    {
        /// <summary>
        /// What option was chosen
        /// </summary>
        public EntryImporter.PortingOption PortingOptionChosen { get; set; }

        /// <summary>
        /// If the porting should use donors. This option only does stuff if porting between games
        /// </summary>
        public bool PortUsingDonors { get; set; }

        /// <summary>
        /// Configures <see cref="RelinkerOptionsPackage.GenerateImportsForGlobalFiles"/> to force porting exports when porting out of a global file (when set to false), rather than imports.
        /// </summary>
        public bool PortGlobalsAsImports { get; set; } = true;

        /// <summary>
        /// If exports should be attempted to be resolved as imports in the destination package. If they can be resolved, port as imports instead
        /// </summary>
        public bool PortExportsAsImportsWhenPossible { get; set; }
        /// <summary>
        /// If imports should be tested for resolution in the destination package on port. If they fail, port as resolved source exports instead.
        /// </summary>
        public bool PortExportsMemorySafe { get; set; }
    }
    public static class EntryImporter
    {
        public enum PortingOption
        {
            CloneTreeAsChild,
            AddSingularAsChild,
            ReplaceSingular,
            MergeTreeChildren,
            Cancel,
            CloneAllDependencies,
            ReplaceSingularWithRelink
        }

        private static ReadOnlySpan<byte> ME1ME2StackDummy =>
        [
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        ];

        private static ReadOnlySpan<byte> ME3LEStackDummy =>
        [
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        ];

        private static ReadOnlySpan<byte> UDKStackDummy =>
        [
            0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        ];

        private static ReadOnlySpan<byte> GetStackDummy(MEGame game) => game switch
        {
            MEGame.UDK => UDKStackDummy,
            MEGame.ME1 => ME1ME2StackDummy,
            MEGame.ME2 => ME1ME2StackDummy,
            _ => ME3LEStackDummy
        };

        public static List<EntryStringPair> ImportAndRelinkEntries(PortingOption portingOption, IEntry sourceEntry, IMEPackage destPcc, IEntry targetLinkEntry, bool shouldRelink, RelinkerOptionsPackage rop, out IEntry newEntry)
        {
            // Porting option 'ReplaceSingular' covers the same as 'ReplaceSingularWithRelink'
            // as it just flips the ROP option 'ImportExportDependencies'
            if (portingOption == PortingOption.ReplaceSingularWithRelink)
            {
                portingOption = PortingOption.ReplaceSingular;
                rop.ImportExportDependencies = true;
            }

            IMEPackage sourcePcc = sourceEntry.FileRef;

            rop.IsCrossGame = sourcePcc.Game != destPcc.Game;

            if (portingOption == PortingOption.ReplaceSingular)
            {
                //replace data only
                if (sourceEntry is ExportEntry entry)
                {
                    rop.CrossPackageMap.Add(entry, targetLinkEntry);
                    ReplaceExportDataWithAnother(entry, targetLinkEntry as ExportEntry, rop);
                }
            }

            if (portingOption is PortingOption.MergeTreeChildren or PortingOption.ReplaceSingular)
            {
                newEntry = targetLinkEntry; //Root item is the one we just dropped. Use that as the root.
            }
            else
            {
                int link = targetLinkEntry?.UIndex ?? 0;
                if (sourceEntry is ExportEntry sourceExport)
                {
                    //importing an export (check if it exists first, if it does, just link to it)
                    newEntry = destPcc.FindExport(sourceEntry.InstancedFullPath) ?? ImportExport(destPcc, sourceExport, link, rop);
                }
                else
                {
                    newEntry = GetOrAddCrossImportOrPackage(sourceEntry.InstancedFullPath, sourcePcc, destPcc, rop, forcedLink: sourcePcc.Tree.NumChildrenOf(sourceEntry) == 0 ? link : (int?)null);
                }
            }

            //if this node has children
            // Is it correct to import children if we clone all dependencies? Isn't relink supposed to take care of this?

            // Crossgen Sept 30 2021: Disabled import children when doing clone all dependencies as not all children are dependendencies
            // Crossgen Oct 8 2021: Re-enabled, but only for packages, as they won't have any direct references to children
            if (((portingOption is PortingOption.CloneTreeAsChild or PortingOption.MergeTreeChildren || (portingOption == PortingOption.CloneAllDependencies && sourceEntry.ClassName == "Package"))
             && sourcePcc.Tree.NumChildrenOf(sourceEntry) > 0) && rop.ImportChildrenOfPackages)
            {
                importChildrenOf(sourceEntry, newEntry);
            }

            // DISABLED IN CROSSGEN AS RELINKER HAS CHANGED
            //for shader porting. For some reason the relinkMap gets cleared during relinking, so make the list here
            //var sourceExports = rop.CrossPackageMap.Keys.OfType<ExportEntry>().ToList();

            List<EntryStringPair> relinkResults = null;
            if (shouldRelink)
            {
                Relinker.RelinkAll(rop);
                relinkResults = rop.RelinkReport;
            }

            //Port Shaders
            //var portingCache = ShaderCacheManipulator.GetLocalShadersForMaterials(sourceExports); // CrossGen Disabled
            var portingCache = ShaderCacheManipulator.GetLocalShadersForMaterials(rop.CrossPackageMap.Keys.OfType<ExportEntry>().ToList(), rop.GamePathOverride);
            if (portingCache is not null)
            {
                if (destPcc.Game != sourcePcc.Game)
                {
                    rop.ErrorOccurredCallback?.Invoke($"You cannot port Materials from {sourcePcc.Game} into {destPcc.Game}");
                }
                else
                {
                    ShaderCacheManipulator.AddShadersToFile(destPcc, portingCache);
                }
            }

            // Reindex - disabled for now as it causes issues
            //Dictionary<string, ExportEntry> itemsToReindex = new Dictionary<string, ExportEntry>();
            //foreach (var v in relinkMap.Values)
            //{
            //    if (v is ExportEntry export && export.indexValue > 0)
            //    {
            //        itemsToReindex[export.FullPath] = export; // Match on full path. Not instanced full path!
            //    }
            //}

            //foreach (var item in itemsToReindex)
            //{
            //    ReindexExportEntriesWithSamePath(item.Value);
            //}

            return relinkResults ?? []; // Pass back empty list if we have no results

            void importChildrenOf(IEntry sourceNode, IEntry newParent)
            {
                foreach (IEntry node in sourceNode.GetChildren().ToList())
                {
                    //we must check to see if there is an item already matching what we are trying to port.

                    //Todo: We may need to enhance target checking here as fullpath may not be reliable enough. Maybe have to do indexing, or something.
                    IEntry sameObjInTarget = newParent.GetChildren().FirstOrDefault(x => node.InstancedFullPath == x.InstancedFullPath);

                    if (portingOption == PortingOption.MergeTreeChildren)
                    {
                        if (sameObjInTarget != null)
                        {
                            rop.CrossPackageMap[node] = sameObjInTarget;

                            //merge children to this node instead
                            importChildrenOf(node, sameObjInTarget);
                            continue;
                        }
                    }

                    if (portingOption == PortingOption.CloneAllDependencies && sameObjInTarget != null)
                    {
                        // Already exists in target
                        rop.CrossPackageMap[node] = sameObjInTarget;
                        importChildrenOf(node, sameObjInTarget);
                        continue;
                    }

                    // CROSSGEN
                    if (destPcc.FindExport(node.InstancedFullPath) != null && portingOption == PortingOption.CloneAllDependencies)
                    {
                        Debugger.Break();
                    }

                    IEntry entry;
                    if (node is ExportEntry exportNode)
                    {
                        entry = ImportExport(destPcc, exportNode, newParent.UIndex, rop);
                    }
                    else
                    {
                        entry = GetOrAddCrossImportOrPackage(node.InstancedFullPath, sourcePcc, destPcc, rop, forcedLink: newParent.UIndex);
                    }

                    importChildrenOf(node, entry);

                    // ORIGINAL CODE
                    //if (portingOption == PortingOption.MergeTreeChildren)
                    //{
                    //    //we must check to see if there is an item already matching what we are trying to port.

                    //    //Todo: We may need to enhance target checking here as fullpath may not be reliable enough. Maybe have to do indexing, or something.
                    //    IEntry sameObjInTarget = newParent.GetChildren().FirstOrDefault(x => node.InstancedFullPath == x.InstancedFullPath);
                    //    if (sameObjInTarget != null)
                    //    {
                    //        relinkMap[node] = sameObjInTarget;

                    //        //merge children to this node instead
                    //        importChildrenOf(node, sameObjInTarget);

                    //        continue;
                    //    }
                    //}

                    //// CROSSGEN
                    //if (destPcc.FindExport(node.InstancedFullPath) != null)
                    //{
                    //    Debugger.Break();
                    //}

                    //IEntry entry;
                    //if (node is ExportEntry exportNode)
                    //{
                    //    entry = ImportExport(destPcc, exportNode, newParent.UIndex, portingOption == PortingOption.CloneAllDependencies, relinkMap, errorOccuredCallback, targetGameDonorDB);
                    //}
                    //else
                    //{
                    //    entry = GetOrAddCrossImportOrPackage(node.InstancedFullPath, sourcePcc, destPcc, objectMapping: relinkMap, forcedLink: newParent.UIndex, targetDonorFileDB: targetGameDonorDB);
                    //}

                    //importChildrenOf(node, entry);
                }
            }
        }

        public static void ReindexExportEntriesWithSamePath(ExportEntry entry)
        {
            string prefixToReindex = entry.ParentInstancedFullPath;
            string objectname = entry.ObjectName.Name;

            int index = 1; //we'll start at 1.
            foreach (ExportEntry export in entry.FileRef.Exports)
            {
                //Check object name is the same, the package path count is the same, the package prefix is the same, and the item is not of type Class

                // Could this be optimized somehow?
                if (export.ParentInstancedFullPath == prefixToReindex && !export.IsClass && objectname == export.ObjectName.Name)
                {
                    export.indexValue = index;
                    index++;
                }
            }
        }

        // CROSSGEN-V HACKS!
        public static List<string> NonDonorItems = [];

        // TODO: FIND WAY TO HANDLE DUPLICATE NAMED OBJECTS IN GAMES
        // 1: Identify badly named objects
        // 2: Probably have to find some hack or warning for entry lookups when these paths are encountered.
        // Note: The following list is not complete for ME1.
        private static readonly string[] badlyNamedME1Assets =
        [
            "BIOA_JUG80_T.JUG80_SAIL",
            "BIOA_ICE60_T.checker",
        ];
        // END CROSSGEN-V HACKS

        /// <summary>
        /// Imports an export from another package file. Does not perform a relink, if you want to relink, use ImportAndRelinkEntries().
        /// </summary>
        /// <param name="destPackage">Package to import to</param>
        /// <param name="sourceExport">Export object from the other package to import</param>
        /// <param name="link">Local parent node UIndex</param>
        /// <param name="rop">Options for relinking</param>
        /// <returns></returns>
        public static ExportEntry ImportExport(IMEPackage destPackage, ExportEntry sourceExport, int link, RelinkerOptionsPackage rop)
        {
            //Debug.WriteLine($"Importing {sourceExport.InstancedFullPath}");
            //if (sourceExport.InstancedFullPath == "BioVFX_Z_TEXTURES.Generic.Biotic_Current_2")
            //    Debugger.Break();
            // CROSSGEN - WILL NEED HEAVY REWORK IF THIS IS TO BE MERGED TO BETA
            // Cause there's a lot of things that seem to have to be manually accounted for
            // To do cross game porting you MUST have a cache object on the ROP
            // or it'll take ages!
            if (rop.TargetGameDonorDB != null && (sourceExport.indexValue == 0 || sourceExport.Game.ToOppositeGeneration() == destPackage.Game) && rop.Cache != null && CanDonateObject(sourceExport) && !sourceExport.InstancedFullPath.StartsWith("TheWorld.")) // Actors cannot be donors
            {
                // Port in donor instead
                var ifp = sourceExport.InstancedFullPath;

                //Debug.WriteLine($@"Porting {ifp}");
                //if (ifp.Contains("TUR_ARM_HVYa_Des_Diff_Stack"))
                //    Debugger.Break();
                var donorFiles = rop.TargetGameDonorDB.GetFilesContainingObject(ifp, sourceExport.FileRef.Localization);
                if (donorFiles == null || !donorFiles.Any())
                {
                    // Try without the front part
                    if (ifp.Contains('.'))
                    {
                        ifp = ifp.Substring(ifp.IndexOf('.') + 1);
                        donorFiles = rop.TargetGameDonorDB.GetFilesContainingObject(ifp, sourceExport.FileRef.Localization);
                    }
                }
                bool usingDonor = false;
                if (donorFiles != null && donorFiles.Any())
                {
                    // See if any packages are open in our cache that already contain this asset
                    IMEPackage donorPackage = null;
                    bool isCached = false;

                    if (badlyNamedME1Assets.Contains(ifp))
                    {
                        // Force use to use a donor without the cache
                        rop.Cache?.ReleasePackages(); // Drop the cache so we have to look in the list of packages
                    }

                    foreach (var df in donorFiles)
                    {
                        if (!df.RepresentsPackageFilePath())
                        {
                            Debugger.Break(); // LOOKUP INTO DB FAILED
                        }

                        if (sourceExport.FileRef.Localization != MELocalization.None)
                        {
                            // Localization must match
                            var dfLoc = df.GetUnrealLocalization();
                            if (sourceExport.FileRef.Localization != dfLoc)
                            {
                                //Debug.WriteLine($"Rejecting donor file {Path.GetFileName(df)}, localization doesn't match source {sourceExport.FileRef.Localization}");
                                continue;
                            }
                            // Debug.WriteLine($"Accepting donor file {Path.GetFileName(df)}, localization matches");
                        }

                        string dfp = df;
                        if (!File.Exists(dfp))
                        {
                            // Relative to game
                            dfp = Path.Combine(MEDirectories.GetDefaultGamePath(destPackage.Game), df);
                        }

                        if (rop.Cache.TryGetCachedPackage(dfp, false, out donorPackage))
                        {
                            var seIFP = ifp;
                            var testExp = donorPackage.FindExport(seIFP);
                            if (testExp.ClassName == sourceExport.ClassName || (sourceExport.ClassName == "BioSWF" && testExp.ClassName == "GFxMovieInfo") || (sourceExport.ClassName == "Material" && testExp.ClassName == "MaterialInstanceConstant"))
                            {
                                sourceExport = testExp;
                                isCached = true;
                                usingDonor = true;
                                break;
                            }

                            // CROSSGEN ONLY: We allow substitution of MaterialInstanceConstant for Material with donor system
                            // as we need to be able to tune things
                            if (testExp.ClassName != sourceExport.ClassName)
                            {
                                // have to manually try to find the export...
                                var properDonor = donorPackage.Exports.FirstOrDefault(x => x.InstancedFullPath == seIFP && x.ClassName == sourceExport.ClassName);

                                if (properDonor == null)
                                {
                                    if (sourceExport.ClassName != "Model" && sourceExport.ClassName != "Brush")
                                        Debug.WriteLine($"CLASSES DIFFER FOR DONORS, CAN'T FIND SUITABLE REPLACEMENT: {sourceExport.ClassName} vs {testExp.ClassName} for {testExp.InstancedFullPath}");
                                }
                                else
                                {
                                    // Dunno if this will work...
                                    Debug.WriteLine($"Porting same-IFP differing-types object {seIFP}");
                                    sourceExport = properDonor;
                                    isCached = true;
                                    usingDonor = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!isCached)
                    {
                        var dfp = Path.Combine(MEDirectories.GetDefaultGamePath(destPackage.Game), donorFiles[0]);
                        if (rop.Cache.TryGetCachedPackage(dfp, true, out donorPackage))
                        {
                            var testExp = donorPackage.FindExport(ifp);
                            if (testExp.ClassName == sourceExport.ClassName || (sourceExport.ClassName == "BioSWF" && testExp.ClassName == "GFxMovieInfo") || (sourceExport.ClassName == "Material" && testExp.ClassName == "MaterialInstanceConstant")) // Use GFxMovieInfo Donors for BioSWF export
                            {
                                sourceExport = testExp;
                                usingDonor = true;
                            }
                            else if (testExp.ClassName != sourceExport.ClassName)
                            {
                                // have to manually try to find the export...
                                var properDonor = donorPackage.Exports.FirstOrDefault(x => x.InstancedFullPath == ifp && x.ClassName == sourceExport.ClassName);

                                if (properDonor == null)
                                {
                                    if (sourceExport.ClassName != "Model" && sourceExport.ClassName != "Brush")
                                        Debug.WriteLine($"CLASSES DIFFER FOR DONORS, CAN'T FIND SUITABLE REPLACEMENT: {sourceExport.ClassName} vs {testExp.ClassName} for {testExp.InstancedFullPath}");
                                }
                                else
                                {
                                    // Dunno if this will work...
                                    Debug.WriteLine($"Porting same-IFP differing-types object {sourceExport.InstancedFullPath}");
                                    sourceExport = properDonor;
                                    usingDonor = true;
                                }
                            }
                        }
                    }
                }

                if (!usingDonor && !ifp.StartsWith(@"TheWorld"))
                {
                    if (sourceExport.ClassName == "Material")
                    {
                        var entryStr = $"{sourceExport.ClassName} {sourceExport.InstancedFullPath}";
                        Debug.WriteLine($@"Not ported using donor: {sourceExport.InstancedFullPath} ({sourceExport.ClassName})");
                        if (!NonDonorItems.Contains(entryStr))
                        {
                            NonDonorItems.Add(entryStr);
                        }
                    }
                }
            }

            byte[] prePropBinary;
            if (sourceExport.HasStack)
            {
                var dummy = GetStackDummy(destPackage.Game);
                prePropBinary = new byte[8 + dummy.Length];
                sourceExport.DataReadOnly[..8].CopyTo(prePropBinary);
                dummy.CopyTo(prePropBinary.AsSpan(8));
            }
            else
            {
                int start = sourceExport.GetPropertyStart();
                if (start == 16)
                {
                    var sourceSpan = sourceExport.DataReadOnly[..16];
                    int newNameIdx = destPackage.FindNameOrAdd(sourceExport.FileRef.GetNameEntry(MemoryMarshal.Read<int>(sourceSpan[4..])));
                    prePropBinary = sourceSpan.ToArray();
                    MemoryMarshal.Write(prePropBinary.AsSpan(4), in newNameIdx);
                }
                else
                {
                    prePropBinary = sourceExport.DataReadOnly[..start].ToArray();
                }
            }

            PropertyCollection props = sourceExport.GetProperties();

            //store copy of names list in case something goes wrong
            if (sourceExport.Game != destPackage.Game)
            {
                List<string> names = [.. destPackage.Names];
                try
                {
                    if (sourceExport.Game != destPackage.Game)
                    {
                        ApplyCrossGamePropertyFixes(sourceExport, destPackage, props);
                        bool removedProperties = false;
                        props = EntryPruner.RemoveIncompatibleProperties(sourceExport.FileRef, props, sourceExport.ClassName, destPackage.Game, ref removedProperties);
                    }
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    //restore namelist in event of failure.
                    destPackage.restoreNames(names);
                    rop.ErrorOccurredCallback?.Invoke($"Error occurred while trying to import {sourceExport.ObjectName.Instanced} : {exception.Message}");
                    throw; //should we throw?
                }
            }

            //takes care of slight header differences between ME1/2 and ME3
            byte[] newHeader = sourceExport.GenerateHeader(destPackage, false); //The header needs relinked or it will be wrong if it has a component map!

            ////for supported classes, this will add any names in binary to the Name table, as well as take care of binary differences for cross-game importing
            ////for unsupported classes, this will just copy over the binary
            ////sometimes converting binary requires altering the properties as well
            ObjectBinary binaryData = ExportBinaryConverter.ConvertPostPropBinary(sourceExport, destPackage.Game, props);

            //Set class.

            if (sourceExport.ObjectName == @"Default__BioStartLocationMP")
                Debugger.Break();
            IEntry classValue = null;
            switch (sourceExport.Class)
            {
                case ImportEntry sourceClassImport:
                    //The class of the export we are importing is an import. We should attempt to relink this.
                    classValue = GetOrAddCrossImportOrPackage(sourceClassImport.InstancedFullPath, sourceExport.FileRef, destPackage, rop);
                    break;
                case ExportEntry sourceClassExport:
                    if (rop.GenerateImportsForGlobalFiles && IsSafeToImportFrom(sourceExport.FileRef.FilePath, destPackage))
                    {
                        classValue = GenerateEntryForGlobalFileExport(sourceClassExport.InstancedFullPath, sourceExport.FileRef, destPackage, rop);
                        break;
                    }
                    classValue = destPackage.FindExport(sourceClassExport.InstancedFullPath);
                    if (classValue is null && rop.ImportExportDependencies)
                    {
                        IEntry classParent = GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, sourceExport.FileRef, destPackage, rop);

                        // Todo: Support ImportExportsAsImports
                        if (rop.PortExportsAsImportsWhenPossible)
                        {
                            // Try convert to import
                            var testImport = new ImportEntry(sourceClassExport, classParent?.UIndex ?? 0, destPackage);
                            if (EntryImporter.TryResolveImport(testImport, out var resolved, localCache: rop.Cache))
                            {
                                destPackage.AddImport(testImport);
                                classValue = testImport;
                                // Debug.WriteLine($"Redirected importable export {classValue.InstancedFullPath} to import from {resolved.FileRef.FilePath}");
                            }
                        }

                        classValue ??= ImportExport(destPackage, sourceClassExport, classParent?.UIndex ?? 0, rop);
                    }
                    break;
            }

            //Set superclass
            IEntry superclass = null;
            switch (sourceExport.SuperClass)
            {
                case ImportEntry sourceSuperClassImport:
                    //The class of the export we are importing is an import. We should attempt to relink this.
                    superclass = GetOrAddCrossImportOrPackage(sourceSuperClassImport.InstancedFullPath, sourceExport.FileRef, destPackage, rop);
                    break;
                case ExportEntry sourceSuperClassExport:
                    if (rop.GenerateImportsForGlobalFiles && IsSafeToImportFrom(sourceExport.FileRef.FilePath, destPackage))
                    {
                        superclass = GenerateEntryForGlobalFileExport(sourceSuperClassExport.InstancedFullPath, sourceExport.FileRef, destPackage, rop);
                        break;
                    }
                    superclass = destPackage.FindEntry(sourceSuperClassExport.InstancedFullPath);
                    if (superclass is null && rop.ImportExportDependencies)
                    {
                        IEntry superClassParent = GetOrAddCrossImportOrPackage(sourceSuperClassExport.ParentFullPath,
                            sourceExport.FileRef, destPackage, rop);

                        if (rop.PortExportsAsImportsWhenPossible)
                        {
                            // Try convert to import
                            var testImport = new ImportEntry(sourceSuperClassExport, superClassParent?.UIndex ?? 0, destPackage);
                            if (EntryImporter.TryResolveImport(testImport, out var resolved, localCache: rop.Cache))
                            {
                                destPackage.AddImport(testImport);
                                superclass = testImport;
                                // Debug.WriteLine($"Redirected importable export {superclass.InstancedFullPath} to import from {resolved.FileRef.FilePath}");
                            }
                        }

                        // Port as export
                        superclass ??= ImportExport(destPackage, sourceSuperClassExport, superClassParent?.UIndex ?? 0, rop);
                    }

                    break;
            }

            //Check archetype.
            IEntry archetype = null;
            switch (sourceExport.Archetype)
            {
                case ImportEntry sourceArchetypeImport:
                    archetype = GetOrAddCrossImportOrPackage(sourceArchetypeImport.InstancedFullPath, sourceExport.FileRef, destPackage, rop);
                    break;
                case ExportEntry sourceArchetypeExport:
                    if (rop.GenerateImportsForGlobalFiles && IsSafeToImportFrom(sourceExport.FileRef.FilePath, destPackage))
                    {
                        archetype = GenerateEntryForGlobalFileExport(sourceArchetypeExport.InstancedFullPath, sourceExport.FileRef, destPackage, rop);
                        break;
                    }
                    archetype = destPackage.FindEntry(sourceArchetypeExport.InstancedFullPath);
                    if (archetype is null && rop.ImportExportDependencies)
                    {
                        IEntry archetypeParent = GetOrAddCrossImportOrPackage(
                            sourceArchetypeExport.ParentInstancedFullPath, sourceExport.FileRef, destPackage, rop);

                        if (rop.PortExportsAsImportsWhenPossible)
                        {
                            // Try convert to import
                            var testImport = new ImportEntry(sourceArchetypeExport, archetypeParent?.UIndex ?? 0, destPackage);
                            if (EntryImporter.TryResolveImport(testImport, out var resolved, localCache: rop.Cache))
                            {
                                destPackage.AddImport(testImport);
                                archetype = testImport;
                                // Debug.WriteLine($"Redirected importable export {sourceArchetypeExport.InstancedFullPath} to import from {resolved.FileRef.FilePath}");
                            }
                        }

                        archetype ??= ImportExport(destPackage, sourceArchetypeExport, archetypeParent?.UIndex ?? 0, rop);
                    }

                    break;
            }

            EndianBitConverter.WriteAsBytes(destPackage.FindNameOrAdd(sourceExport.ObjectName.Name), newHeader.AsSpan(ExportEntry.OFFSET_idxObjectName), destPackage.Endian);
            EndianBitConverter.WriteAsBytes(sourceExport.ObjectName.Number, newHeader.AsSpan(ExportEntry.OFFSET_indexValue), destPackage.Endian);
            EndianBitConverter.WriteAsBytes(link, newHeader.AsSpan(ExportEntry.OFFSET_idxLink), destPackage.Endian);

            var newExport = new ExportEntry(destPackage, newHeader, prePropBinary, props, binaryData, sourceExport.IsClass)
            {
                Class = classValue,
                SuperClass = superclass,
                Archetype = archetype,
            };
            destPackage.AddExport(newExport);
            if (rop.CrossPackageMap != null) // Is this check necessary with ROP?
            {
                rop.CrossPackageMap[sourceExport] = newExport;
            }

            return newExport;
        }

        internal static void ApplyCrossGamePropertyFixes(ExportEntry sourceExport, IMEPackage destPackage, PropertyCollection props)
        {
            // 06/25/2022 - Support corrections to allow more sucessful porting
            // Mgamerz

            // Doesn't go backwards for OT since they use different Wwise versions
            if (sourceExport.Game == MEGame.LE2 && destPackage.Game == MEGame.LE3 && sourceExport.ClassName == "WwiseEvent")
            {
                // Convert the property format
                var refs = props.GetProp<ArrayProperty<StructProperty>>("References");
                if (refs != null)
                {
                    props.RemoveNamedProperty("References");
                    if (refs.Count == 1)
                    {
                        Debug.WriteLine($@"ApplyCrossGamePropertyFixes(): WwiseEvent LE2 -> LE3 for {sourceExport.InstancedFullPath}");
                        var relationShips = refs[0].GetProp<StructProperty>("Relationships");
                        relationShips.Properties.RemoveNamedProperty("Streams"); // Does this need moved to the binary?
                        props.AddOrReplaceProp(relationShips); // Move 'RelationShips' struct to the root

                        // Add Id from binary to props as it seems to have moved 
                        WwiseEvent ob = ObjectBinary.From<WwiseEvent>(sourceExport);
                        props.Add(new IntProperty((int)ob.WwiseEventID, "Id"));
                    }
                }
            }

            if (sourceExport.Game.IsGame3() && destPackage.Game.IsGame3() && sourceExport.ClassName == "WwiseEvent")
            {
                // DurationMilliseconds <-> DurationSeconds
                var prop = props.GetProp<FloatProperty>("DurationMilliseconds");
                if (prop == null)
                {
                    prop = props.GetProp<FloatProperty>("DurationSeconds");
                    if (prop == null)
                        return;// Might be loop which doesn't have duration.
                    prop.Name = "DurationMilliseconds";
                    prop.Value *= 1000;
                }
                else
                {
                    prop.Name = "DurationSeconds";
                    prop.Value /= 1000;
                }
            }

            if (sourceExport.Game.IsGame3() != destPackage.Game.IsGame3() && sourceExport.IsA("Actor") && props.GetProp<StructProperty>("location") is { } locationProp)
            {
                locationProp.Name = destPackage.Game.IsGame3() ? "location" : "Location";
            }
        }

        private static bool CanDonateObject(ExportEntry export)
        {
            // These classes cannot be donated across games.
            switch (export.ClassName)
            {
                case "Package":
                case "Brush":
                case "Model":
                    return false;
            }

            // Lightmaps should not be donated
            if (export.IsA("LightMapTexture2D"))
            {
                return false;
            }

            return true;
        }

        public static bool ReplaceExportDataWithAnother(ExportEntry incomingExport, ExportEntry targetExport, RelinkerOptionsPackage rop)
        {
            using var res = new EndianReader(MemoryManager.GetMemoryStream()) { Endian = targetExport.FileRef.Endian };
            if (incomingExport.HasStack)
            {
                res.Writer.Write(incomingExport.DataReadOnly.Slice(0, 8));
                res.Writer.WriteFromBuffer(GetStackDummy(targetExport.Game));
            }
            else
            {
                //int start = incomingExport.GetPropertyStart();
                // 06/08/2024 - When copying data over we should keep the pre-props binary instead of just writing zeros
                res.Writer.Write(incomingExport.DataReadOnly.Slice(0,incomingExport.GetPropertyStart()));
                //res.Writer.Write(new byte[start], 0, start);
            }

            //store copy of names list in case something goes wrong
            List<string> names = [.. targetExport.FileRef.Names];
            try
            {
                PropertyCollection props = incomingExport.GetProperties();
                ObjectBinary binary = ExportBinaryConverter.ConvertPostPropBinary(incomingExport, targetExport.Game, props);
                props.WriteTo(res.Writer, targetExport.FileRef);
                res.Writer.WriteFromBuffer(binary.ToBytes(targetExport.FileRef));
            }
            catch (Exception exception)
            {
                //restore namelist in event of failure.
                targetExport.FileRef.restoreNames(names);
                rop.ErrorOccurredCallback?.Invoke($"Error occurred while replacing data in {incomingExport.ObjectName.Instanced} : {exception.Message}");
                return false;
            }
            targetExport.Data = res.ToArray();
            return true;
        }

        /// <summary>
        /// Adds an import from the importingPCC to the destinationPCC with the specified INSTANCED fullname, or returns the existing one if it can be found. 
        /// This will add parent imports and packages as neccesary
        /// </summary>
        /// <param name="importFullNameInstanced">INSTANCED full path of an import from ImportingPCC</param>
        /// <param name="sourcePcc">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <param name="rop">Options for relinking</param>
        /// <param name="forcedLink">force this as parent</param>
        /// <returns></returns>
        public static IEntry GetOrAddCrossImportOrPackage(string importFullNameInstanced, IMEPackage sourcePcc, IMEPackage destinationPCC, RelinkerOptionsPackage rop, int? forcedLink = null)
        {
            if (string.IsNullOrEmpty(importFullNameInstanced))
            {
                return null;
            }

            var foundEntry = destinationPCC.FindEntry(importFullNameInstanced);
            if (foundEntry != null)
            {
                return foundEntry;
            }

            string[] importParts = importFullNameInstanced.Split('.');

            //if importing something into eg. SFXGame.pcc, this will ensure links to SFXGame imports will link up to the proper exports in SFXGame
            //if (sourcePcc.Game == MEGame.ME1 && destinationPCC.Game == MEGame.LE1 && importParts[0] == "BIOC_Base")
            //{
            //    importParts[0] = "SFXGame"; // BIOC_Base was renamed to SFXGame in ME2
            //}

            // Looking for things in SFXGame which doesn't have package export name in it's own file
            if (importParts.Length > 1 && importParts[0].CaseInsensitiveEquals(destinationPCC.FileNameNoExtension))
            {
                foundEntry = destinationPCC.FindEntry(string.Join('.', importParts[1..]));
                if (foundEntry != null)
                {
                    return foundEntry;
                }
            }

            if (forcedLink is int link)
            {
                ImportEntry importingImport = sourcePcc.FindImport(importFullNameInstanced); // this shouldn't be null
                var newImport = new ImportEntry(destinationPCC, link, importingImport.ObjectName)
                {
                    ClassName = importingImport.ClassName,
                    PackageFile = importingImport.PackageFile
                };
                destinationPCC.AddImport(newImport);
                if (rop.CrossPackageMap != null) // would this ever be null? Is this leftover from original relinker code? // 09/30/2021
                {
                    rop.CrossPackageMap[importingImport] = newImport;
                }

                return newImport;
            }

            //recursively ensure parent exists. when importParts.Length == 1, this will return null

            // DEBUG---
            IEntry parent = null;
            //if (importParts[0] == "SFXGame")
            //{
            //    // Pull from global instead
            //    parent = GetOrAddCrossImportOrPackageFromGlobalFile(string.Join('.', importParts[..^1]), sourcePcc, destinationPCC, objectMapping);
            //}
            // ENDDEBUG

            parent ??= GetOrAddCrossImportOrPackage(string.Join('.', importParts[..^1]), sourcePcc, destinationPCC, rop);

            var sourceEntry = sourcePcc.FindEntry(importFullNameInstanced);
            if (sourceEntry is ImportEntry imp) // import not found
            {
                // Code below forces Package objects to be imported as exports instead of imports. However if an object is an import (that works properly) the parent already has to exist upstream.
                // Some BioP for some reason use exports instead of imports when referencing sfxgame content even if they have no export children
                // not sure it has any functional difference
                // Mgamerz 3/21/2021

                //if (imp.ClassName == "Package")
                //{
                //    // Debug. Create package export instead.
                //    return ExportCreator.CreatePackageExport(destinationPCC, imp.ObjectName, parent, null);
                //}
                //else
                {
                    var newImport = new ImportEntry(destinationPCC, parent, imp.ObjectName)
                    {
                        ClassName = imp.ClassName,
                        PackageFile = imp.PackageFile
                    };
                    destinationPCC.AddImport(newImport);
                    if (rop.CrossPackageMap != null) // Is this null check necessary? 09/30/2021
                    {
                        rop.CrossPackageMap[sourceEntry] = newImport;
                    }

                    return newImport;
                }
            }

            if (sourceEntry is ExportEntry foundMatchingExport)
            {
                // Try convert to import in destination package if user chose option
                if (rop.PortExportsAsImportsWhenPossible)
                {
                    // Try convert to import
                    var testImport = new ImportEntry(foundMatchingExport, parent?.UIndex ?? 0, destinationPCC);
                    if (EntryImporter.TryResolveImport(testImport, out var resolved, localCache: rop.Cache))
                    {
                        destinationPCC.AddImport(testImport);
                        // Debug.WriteLine($"Redirected importable export {importFullNameInstanced} to import from {resolved.FileRef.FilePath}");
                        return testImport;
                    }
                }

                if (rop.ImportExportDependencies || foundMatchingExport.ClassName == "Package")
                {
                    return ImportExport(destinationPCC, foundMatchingExport, parent?.UIndex ?? 0, rop);
                }
            }

            throw new Exception($"Unable to add {importFullNameInstanced} to file! Could not find it!");
        }

        /// <summary>
        /// Adds an import from the importingPCC to the destinationPCC with the specified importFullName, or returns the existing one if it can be found. 
        /// This will add parent imports and packages as neccesary
        /// </summary>
        /// <param name="importFullNameInstanced">GetFullPath() of an import from ImportingPCC</param>
        /// <param name="sourcePcc">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <param name="rop">Options for relinking</param>
        /// <returns></returns>
        public static IEntry GenerateEntryForGlobalFileExport(string importFullNameInstanced, IMEPackage sourcePcc, IMEPackage destinationPCC, RelinkerOptionsPackage rop, Action<EntryStringPair> doubleClickCallback = null)
        {
            string packageName = sourcePcc.FileNameNoExtension;
            if (string.IsNullOrEmpty(importFullNameInstanced)) // If passing in an empty string, we're generating an import for the package file itself
            {
                return destinationPCC.GetEntryOrAddImport(packageName, "Package");
            }

            string properImportInstancedFullPath = importFullNameInstanced;

            // We check if package name is same as the import being generated, as this export won't actually exist
            // in a global file
            if (sourcePcc.FileNameNoExtension != importFullNameInstanced && !importFullNameInstanced.Contains('.'))
            {
                // We might be passed in the expected import
                // We need to strip off the filename if the export is not marked as ForcedExport.
                var exportToBuildImportFor = sourcePcc.FindExport(importFullNameInstanced);
                if (exportToBuildImportFor == null && importFullNameInstanced.StartsWith($"{packageName}."))
                {
                    exportToBuildImportFor = sourcePcc.FindExport(importFullNameInstanced.Substring($"{packageName}.".Length));
                }

                if (!exportToBuildImportFor.IsForcedExport)
                {
                    // NOT FORCED EXPORT - Look for entry nested under the proper path
                    properImportInstancedFullPath = $"{packageName}.{importFullNameInstanced}";
                }
            }

            //see if this export exists locally in the package, under a class of same name (Engine class in Engine.pcc for example)
            var foundEntry = destinationPCC.FindEntry(properImportInstancedFullPath);
            if (foundEntry != null)
            {
                return foundEntry;
            }

            //// Try the name directly
            //foundEntry = destinationPCC.FindEntry(importFullNameInstanced);
            //if (foundEntry != null)
            //{
            //    return foundEntry;
            //}

            string[] importParts = properImportInstancedFullPath.Split('.');

            //recursively ensure parent exists
            var importName = string.Join(".", importParts.Take(importParts.Length - 1));
            IEntry parent = null;
            if (importName == "")
            {
                //// Todo: We need a DB or something to know if we need to prepend the parent or not here.
                //// TODO: THIS IS A HACK FOR CROSSGEN. WE NEED A BETTER SOLUTION THAN THIS TO TELL IF WE
                //// NEED TO PREPEND THE PACKAGE FILENAME OR NOT BASED ON IF THE REFERENCED EXPORT IS 
                //// A FORCED EXPORT (in which case, we should not) OR NOT (in which case, we should)
                //if (importParts[0] == importFullNameInstanced.StartsWith("BIOG", StringComparison.InvariantCultureIgnoreCase))
                //{
                //    // This is the root we want
                //    // Do not add another parent
                //}
                //else
                //{
                //// Generate import for the package file itself.
                if (packageName == importParts[0])
                {
                    return GenerateEntryForGlobalFileExport(importName, sourcePcc, destinationPCC, rop, doubleClickCallback);
                }
                // No parent otherwise, I think?
                //}
            }
            else
            {
                parent = GenerateEntryForGlobalFileExport(importName, sourcePcc, destinationPCC, rop, doubleClickCallback);
            }

            ImportEntry matchingSourceImport = sourcePcc.FindImport(importFullNameInstanced);
            if (matchingSourceImport != null)
            {
                var newImport = new ImportEntry(destinationPCC, parent, matchingSourceImport.ObjectName)
                {
                    ClassName = matchingSourceImport.ClassName,
                    PackageFile = matchingSourceImport.PackageFile
                };
                destinationPCC.AddImport(newImport);
                if (rop.CrossPackageMap != null) // Is this necesssary with ROP?
                {
                    rop.CrossPackageMap[matchingSourceImport] = newImport;
                }

                return newImport;
            }

            // Use the source to build an import
            ExportEntry matchingSourceExport = sourcePcc.FindExport(importFullNameInstanced);
            if (matchingSourceExport != null)
            {
                var foundImp = destinationPCC.FindImport(properImportInstancedFullPath);
                if (foundImp != null) return foundImp;
                //if (matchingSourceExport.ObjectName == "Metal_Cube")
                //    Debugger.Break();
                string pf = Path.GetFileNameWithoutExtension(matchingSourceExport.FileRef.FilePath); // Not 100% sure this is good idea since they can load from stream...
                if (matchingSourceExport.Class != null)
                {
                    pf = matchingSourceExport.Class.GetRootName(); // This is correct 'most' times but not always
                    // Try to determine what file the class is defined out of
                    if (pf != "Engine" && pf != "Core" && pf != "SFXGame" && pf != "BIOC_Base")
                    {
                        Debug.WriteLine($@"Converting export to import in global file, unknown base class root {pf} for {matchingSourceExport.InstancedFullPath}. We are going to convert it to 'Core'");
                        pf = "Core";
                    }
                }
                else
                {
                    // It's a class. Class is defined in core, I hope
                    pf = "Core";
                }

                var newImport = new ImportEntry(destinationPCC, parent, matchingSourceExport.ObjectName)
                {
                    ClassName = matchingSourceExport.ClassName,
                    PackageFile = pf
                };
                destinationPCC.AddImport(newImport);
                if (rop.CrossPackageMap != null) // Is this nececessary with ROP?
                {
                    rop.CrossPackageMap[matchingSourceExport] = newImport;
                }

                return newImport;
            }

            throw new Exception($"Unable to add {importFullNameInstanced} to file! Could not find it!");
        }

        //SirCxyrtyx: These are not exhaustive lists, just the ones that I'm sure about
        private static readonly string[] me1FilesSafeToImportFrom = ["Core.u", "Engine.u", "GameFramework.u", "PlotManagerMap.u", "BIOC_Base.u"];

        private static readonly string[] me1FilesSafeToImportFromPostLoad =
        [
            // These files are safe to import from if the file doing the import is post-save (e.g. it is not a seekfree or startup file)
            
        ];

        private static readonly string[] me2FilesSafeToImportFrom =
        [
            "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "PlotManagerMap.pcc", "SFXGame.pcc", "Startup_INT.pcc"
        ];

        private static readonly string[] me2FilesSafeToImportFromPostLoad =
        [
            // These files are safe to import from if the file doing the import is post-save (e.g. it is not a seekfree or startup file)
            
        ];

        private static readonly string[] me3FilesSafeToImportFrom =
        [
            //Class libary: These files contain ME3's standard library of classes, structs, enums... Also a few assets
            "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc",
            //Assets: these files contain assets common enough that they are always loaded into memory
            "Startup.pcc", "GesturesConfig.pcc", "BIOG_Humanoid_MASTER_MTR_R.pcc", "BIOG_HMM_HED_PROMorph.pcc"
        ];

        private static readonly string[] me3FilesSafeToImportFromPostLoad =
        [
            // These files are safe to import from if the file doing the import is post-save (e.g. it is not a seekfree or startup file)
            "BIO_COMMON.pcc",
            "GesturesConfig.pcc" // Some animations
        ];
        //TODO: make LE lists more exhaustive
        private static readonly string[] le1FilesSafeToImportFrom =
        [
            // Determined from Debug Logger
            "Core.pcc",
            "Engine.pcc",
            "IpDrv.pcc",
            "GFxUI.pcc",
            "PlotManagerMap.pcc", // Also LOC file
            "SFXOnlineFoundation.pcc",
            "SFXGame.pcc",
            "SFXStrategicAI.pcc",
            "SFXGameContent_Powers.pcc",
            "PlotManager.pcc",
            "PlotManagerDLC_UNC.pcc",
            "BIOC_Materials.pcc",
            "SFXWorldResources.pcc",
            "SFXVehicleResources.pcc",
            "Startup_INT.pcc",

        // SFXWorldResources and SFXVehicleResources are always loaded
        // EXCEPT ON STA MAPS!!
        // At least according to coalesced, not sure if that's actually true
        ];

        private static readonly string[] le1FilesSafeToImportFromPostLoad =
        [
            // These files are safe to import from if the file doing the import is post-save (e.g. it is not a seekfree or startup file)
            
        ];

        private static readonly string[] le2FilesSafeToImportFrom =
        [
            "Core.pcc", "Engine.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "PlotManagerMap.pcc", "SFXGame.pcc", "Startup_INT.pcc"
        ];

        private static readonly string[] le2FilesSafeToImportFromPostLoad =
        [
            // These files are safe to import from if the file doing the import is post-save (e.g. it is not a seekfree or startup file)

            // Add DLC startup files here?
            "Startup_METR_Patch01_INT.pcc"
        ];

        private static readonly string[] le3FilesSafeToImportFrom =
        [
            //Class libary: These files contain ME3's standard library of classes, structs, enums... Also a few assets
            // Note: You must use MELoadedFiles for Startup.pcc as it exists in METR Patch and is not used by game! (and is also wrong file)
            "Core.pcc", "Engine.pcc", "Startup.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc",
        ];

        private static readonly string[] le3FilesSafeToImportFromPostLoad =
        [
            // These files are safe to import from if the file doing the import is post-save (e.g. it is not a seekfree or startup file)
            "BIO_COMMON.pcc",
            "GesturesConfig.pcc", // Some animations
            // "BioP_Global.pcc" // This only applies to combat files so not sure how this would be handled for non combat
            // Add DLC startup files here
        ];

        // Advanced users can add to this list to make additional files be treated as safe
        // For example if they want to use a custom startup file
        // These only are allowed for post load usage!
        // These have size 0 since they are going to be very rarely used so don't allocate anything for them
        // Usages: Mass Effect Randomizer, probably DropTheSquid
        private static readonly SortedSet<string> me1FilesSafeToImportFromUser = [];
        private static readonly SortedSet<string> me2FilesSafeToImportFromUser = [];
        private static readonly SortedSet<string> me3FilesSafeToImportFromUser = [];
        private static readonly SortedSet<string> le1FilesSafeToImportFromUser = [];
        private static readonly SortedSet<string> le2FilesSafeToImportFromUser = [];
        private static readonly SortedSet<string> le3FilesSafeToImportFromUser = [];

        /// <summary>
        /// Gets the list of safe to import files from (user defined) for a game. For public read-only access use <see cref="UserSpecifiedSafeToImportFromFiles(MEGame)"/>
        /// </summary>
        /// <param name="game">Game to get safe files for.</param>
        /// <returns>The set of files that can be imported from (user defined)</returns>
        /// <exception cref="Exception">If game is not supported</exception>
        private static SortedSet<string> InternalGetUserSafeToImportFromFiles(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => me1FilesSafeToImportFromUser,
                MEGame.ME2 => me2FilesSafeToImportFromUser,
                MEGame.ME3 => me3FilesSafeToImportFromUser,
                MEGame.LE1 => le1FilesSafeToImportFromUser,
                MEGame.LE2 => le2FilesSafeToImportFromUser,
                MEGame.LE3 => le3FilesSafeToImportFromUser,
                MEGame.UDK => [], // Dunno how to do Set.Empty<string>()
                _ => throw new Exception($"Cannot lookup user-defined safe post-load files for {game}")
            };
        }

        /// <summary>
        /// Adds a user-safe file to import to the specified game. Only use this if you really know what you're doing. The file must exist in the default game directory.
        /// </summary>
        /// <param name="game">Which game to append the safe file to</param>
        /// <param name="safeToImportFromFile">The path to the safe file to use when adding an import</param>
        public static bool AddUserSafeToImportFromFile(MEGame game, string safeToImportFromFile)
        {
            if (!InternalGetUserSafeToImportFromFiles(game).Contains(safeToImportFromFile))
            {
                return InternalGetUserSafeToImportFromFiles(game).Add(safeToImportFromFile);
            }
            return false;
        }

        /// <summary>
        /// Removes a user-safe file to import from specified game.
        /// </summary>
        /// <param name="game"></param>
        /// <param name="safeToImportFromFile"></param>
        public static bool RemoveSafeToImportFromFile(MEGame game, string safeToImportFromFile)
        {
            return InternalGetUserSafeToImportFromFiles(game).Remove(safeToImportFromFile);
        }

        /// <summary>
        /// Removes all 
        /// </summary>
        /// <param name="game"></param>
        /// <exception cref="Exception"></exception>
        public static void ClearUserSafeToImportFromFiles(MEGame game)
        {
            InternalGetUserSafeToImportFromFiles(game).Clear();
        }

        public static bool IsSafeToImportFrom(string path, IMEPackage localPackage, bool useImportHints = true)
        {
            if (localPackage == null) return false;
            if (useImportHints && localPackage.LECLTagData != null && localPackage.LECLTagData.ImportHintFiles?.Count > 0)
            {
                var fname = Path.GetFileName(path);
                if (localPackage.LECLTagData.ImportHintFiles.Contains(fname, StringComparer.InvariantCultureIgnoreCase))
                    return true; // It's an importable hint
            }

            return IsSafeToImportFrom(path, localPackage.Game, localPackage.FilePath);
        }

        /// <summary>
        /// Determines if a file "should" be safe to import from. In order to test postload files, a sourceFilePath must be provided.
        /// </summary>
        /// <param name="path">Path/filename of the file that is being checked if is safe to import from</param>
        /// <param name="game">The game to check against</param>
        /// <param name="sourceFilePath">The file path the import would be placed into - if not provided, only global safe files will be included and precedence will not be checked. Can be only filename or full path, both will work.</param>
        /// <returns></returns>
        public static bool IsSafeToImportFrom(string path, MEGame game, string sourceFilePath)
        {
            string fileName = Path.GetFileName(path);
            string sourceFileName = sourceFilePath != null ? Path.GetFileName(sourceFilePath) : null;

            IEnumerable<string> fileList = FilesSafeToImportFrom(game);
            if (sourceFileName != null)
            {
                if (IsPostLoadFile(sourceFileName, game))
                {
                    fileList = fileList.Concat(FilesSafeToImportFromPostLoad(game)).Concat(UserSpecifiedSafeToImportFromFiles(game));
                }
            }
            foreach (var f in fileList)
            {
                if (f == sourceFileName)
                    break; // You can't import from yourself
                if (f == fileName)
                    return true;
            }
            return false;
            //return fileList.Any(f => fileName == f);
        }

        /// <summary>
        /// If this file is used (according to BioWare naming rules) after save loads - e.g. BioA files
        /// </summary>
        /// <param name="sourceFilePath">Full path of the existing file</param>
        /// <param name="game">Game to check</param>
        /// <returns></returns>
        public static bool IsPostLoadFile(string sourceFilePath, MEGame game)
        {
            var postLoadTest = Path.GetFileName(sourceFilePath);
            if (game.IsGame1())
            {
                if (postLoadTest.StartsWith("BIOA_", StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            else if (game.IsGame2() || game.IsGame3())
            {
                // DO NOT ADD BIOG
                if (postLoadTest.StartsWith("BioA_", StringComparison.InvariantCultureIgnoreCase) ||
                    postLoadTest.StartsWith("BioD_", StringComparison.InvariantCultureIgnoreCase) ||
                    postLoadTest.StartsWith("BioS_", StringComparison.InvariantCultureIgnoreCase) ||
                    postLoadTest.StartsWith("BioP_", StringComparison.InvariantCultureIgnoreCase) ||
                    postLoadTest.StartsWith("BioS_", StringComparison.InvariantCultureIgnoreCase) ||
                    postLoadTest.StartsWith("BioH_", StringComparison.InvariantCultureIgnoreCase) ||
                    postLoadTest.StartsWith("SFXCharacterClass", StringComparison.InvariantCultureIgnoreCase)) // These load after. Technically in Game 2 one loads before (soldier) - not sure if we should filter that out
                {
                    return true;
                }

                if (game == MEGame.LE3)
                {
                    // BioNPC is used by LE3 framework. It will have a lot of these and might confuse devs so we mark it as a 
                    // post load file since it will be common enough for devs.
                    if (postLoadTest.StartsWith("BioNPC_", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the global list of files that a game roots into memory on game startup and is typically safe to import from
        /// </summary>
        /// <param name="game">Game to pull files from</param>
        /// <returns></returns>
        /// <exception cref="Exception">If value is not an OT or LE Game 1/2/3. UDK will not throw an exception, just return an empty list</exception>
        public static string[] FilesSafeToImportFrom(MEGame game) =>
            game switch
            {
                MEGame.ME1 => me1FilesSafeToImportFrom,
                MEGame.ME2 => me2FilesSafeToImportFrom,
                MEGame.ME3 => me3FilesSafeToImportFrom,
                MEGame.LE1 => le1FilesSafeToImportFrom,
                MEGame.LE2 => le2FilesSafeToImportFrom,
                MEGame.LE3 => le3FilesSafeToImportFrom,
                MEGame.UDK => FileLib.BaseFileNames(MEGame.UDK),
                _ => throw new Exception($"Cannot lookup safe files for {game}")
            };

        /// <summary>
        /// Returns the list of files that a game roots into memory AFTER a save file has loaded - e.g. first level transition. These files are NOT safe to import from unless from specific "post-load"
        /// files, such as BioP and BioD. This does not include global safe files.
        /// </summary>
        /// <param name="game">Game to pull files from</param>
        /// <returns></returns>
        /// <exception cref="Exception">If value is not an OT or LE Game 1/2/3. UDK will not throw an exception, just return an empty list</exception>
        public static string[] FilesSafeToImportFromPostLoad(MEGame game) =>
            game switch
            {
                MEGame.ME1 => me1FilesSafeToImportFromPostLoad,
                MEGame.ME2 => me2FilesSafeToImportFromPostLoad,
                MEGame.ME3 => me3FilesSafeToImportFromPostLoad,
                MEGame.LE1 => le1FilesSafeToImportFromPostLoad,
                MEGame.LE2 => le2FilesSafeToImportFromPostLoad,
                MEGame.LE3 => le3FilesSafeToImportFromPostLoad,
                MEGame.UDK => [],
                _ => throw new Exception($"Cannot lookup post-load safe files for {game}")
            };

        /// <summary>
        /// Returns the list of user-specified safe-to-import-from files. These files must exist in the game directory in order to be used.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static IReadOnlySet<string> UserSpecifiedSafeToImportFromFiles(MEGame game) => InternalGetUserSafeToImportFromFiles(game);

        // I'm not sure what tools use these, or what they were used for.
        // I don't think any ME3Tweaks (non LEX) tooling uses it, may be others?
        public static bool CanImport(string className, IMEPackage destPackage) => CanImport(GlobalUnrealObjectInfo.GetClassOrStructInfo(destPackage.Game, className), destPackage);
        public static bool CanImport(string className, MEGame game, string sourceFilePath = null) => CanImport(GlobalUnrealObjectInfo.GetClassOrStructInfo(game, className), game, sourceFilePath);

        public static bool CanImport(ClassInfo classInfo, MEGame game, string sourceFilePath = null) => classInfo != null && IsSafeToImportFrom(classInfo.pccPath, game, sourceFilePath);
        public static bool CanImport(ClassInfo classInfo, IMEPackage destPackage) => classInfo != null && IsSafeToImportFrom(classInfo.pccPath, destPackage);

        public static byte[] CreateStack(MEGame game, int stateNodeUIndex)
        {
            using var ms = MemoryManager.GetMemoryStream();
            ms.WriteInt32(stateNodeUIndex);
            ms.WriteInt32(stateNodeUIndex);
            ms.WriteFromBuffer(GetStackDummy(game));
            return ms.ToArray();
        }

        /// <summary>
        /// Attempts to resolve the import by looking at associated files that are loaded before this one. This method does not use a global file cache, the passed in cache may have items added to it.
        /// </summary>
        /// <param name="entry">The import to resolve</param>
        /// <param name="localCache">Package cache if you wish to keep packages held open, for example if you're resolving many imports</param>
        /// <param name="localization">Three letter localization code, all upper case. Defaults to INT.</param>
        /// <returns>The resolved export, or null if the referenced import could not be found</returns>
        public static ExportEntry ResolveImport(ImportEntry entry, PackageCache localCache = null, string localization = "INT")
        {
            return ResolveImport(entry, null, localCache, localization);
        }

        /// <summary>
        /// Returns true if a import can be resolved using the LEC resolver code. This is not 100% accurate to the game. The passed in package cache may have empty (no data) loaded packages
        /// inserted into it!
        /// </summary>
        /// <param name="entry"><inheritdoc cref="ResolveImport(ImportEntry, PackageCache, PackageCache, string, bool, IEnumerable{string}, string)"/></param>
        /// <param name="globalCache"><inheritdoc cref="ResolveImport(ImportEntry, PackageCache, PackageCache, string, bool, IEnumerable{string}, string)"/></param>
        /// <param name="lookupCache"><inheritdoc cref="ResolveImport(ImportEntry, PackageCache, PackageCache, string, bool, IEnumerable{string}, string)"/></param>
        /// <param name="localization"><inheritdoc cref="ResolveImport(ImportEntry, PackageCache, PackageCache, string, bool, IEnumerable{string}, string)"/></param>
        /// <param name="localDirFiles"><inheritdoc cref="ResolveImport(ImportEntry, PackageCache, PackageCache, string, bool, IEnumerable{string}, string)"/></param>
        /// <returns></returns>
        public static bool CanResolveImport(ImportEntry entry, PackageCache globalCache, PackageCache lookupCache, string localization = "INT", IEnumerable<string> localDirFiles = null)
        {
            var exp = ResolveImport(entry, globalCache, lookupCache, localization, true, localDirFiles);
            return exp != null;
        }

        /// <summary>
        /// Attempts to resolve the import by looking at associated files that are loaded before this one, and by looking at globally loaded files.
        /// </summary>
        /// <param name="entry">The import to resolve</param>
        /// <param name="globalCache">Package cache that contains global files like SFXGame, Startup, etc. The cache will not be modified but can be used to reduce disk I/O.</param>
        /// <param name="lookupCache">Package cache if you wish to keep packages held open, for example if you're resolving many imports</param>
        /// <param name="localization">Three letter localization code, all upper case. Defaults to INT.</param>
        /// <param name="unsafeLoad">If we are only testing for existence; use unsafe partial load. DO NOT USE THE RESULTING VALUE IF YOU SET THIS TO TRUE</param>
        /// <param name="localDirFiles">Files in the local directory. These will be checked last</param>
        /// <param name="gameRootOverride">The root path of the game. If null, the default path will be used</param>
        /// <returns></returns>
        public static ExportEntry ResolveImport(ImportEntry entry, PackageCache globalCache, PackageCache lookupCache, string localization = "INT", bool unsafeLoad = false, IEnumerable<string> localDirFiles = null, string gameRootOverride = null)
        {
            var entryFullPath = entry.InstancedFullPath;
            //if (entry.ObjectName == "HMM_EYE_MASTER_OVRD_MAT")
            //{

            //    // Debugger.Break();
            //}
            CaseInsensitiveDictionary<string> gameFiles = MELoadedFiles.GetFilesLoadedInGame(entry.Game, forceUseCached: true, gameRootOverride: gameRootOverride);

            var filesToCheck = GetPossibleImportFiles(entry, localization, gameRootOverride: gameRootOverride);

            string containingDirectory = null;

            foreach (var fileName in filesToCheck)
            {
                if (gameFiles.TryGetValue(fileName, out var fullgamepath))
                {
                    var export = containsImportedExport(fullgamepath);
                    if (export != null)
                    {
                        return export;
                    }
                }

                //Try local.
                containingDirectory ??= Path.GetDirectoryName(entry.FileRef.FilePath);
                if (containingDirectory != null)
                {
                    var localPath = Path.Combine(containingDirectory, fileName);
                    if (!localPath.Equals(fullgamepath, StringComparison.InvariantCultureIgnoreCase) && (
                            (localDirFiles?.Contains(localPath, StringComparer.InvariantCultureIgnoreCase) == true)
                            || (localDirFiles == null && File.Exists(localPath))))
                    {
                        var export = containsImportedExport(localPath);
                        if (export != null)
                        {
                            return export;
                        }
                    }
                }
            }
            return null;

            IMEPackage openPackageMethod(string packagePath)
            {
                if (unsafeLoad)
                {
                    // Load no export data - we only care about the table data
                    return MEPackageHandler.UnsafePartialLoad(packagePath, x => false);
                }
                else
                {
                    return MEPackageHandler.OpenMEPackage(packagePath, forceLoadFromDisk: true);
                }
            }

            //Perform check and lookup
            ExportEntry containsImportedExport(string packagePath)
            {
                //Debug.WriteLine($"Checking file {packagePath} for {entryFullPath}");
                IMEPackage package = null;
                if (globalCache != null)
                {
                    package = globalCache.GetCachedPackage(packagePath, false);
                }

                package ??= lookupCache != null ? lookupCache.GetCachedPackage(packagePath, openPackageMethod: openPackageMethod) : openPackageMethod(packagePath);

                if (package == null)
                    Debugger.Break();

                var packName = Path.GetFileNameWithoutExtension(packagePath);
                var packageParts = entryFullPath.Split('.').ToList();

                // Coded a bit weird for optimization on allocations
                string forcedExportPath = null;
                if (packageParts.Count > 1 && packName == packageParts[0]) // Remove 'SFXGame' from 'SFXGame.BioSeqAct...'
                {
                    packageParts.RemoveAt(0);
                    forcedExportPath = string.Join(".", packageParts);
                }
                else if (packName == packageParts[0])
                {
                    //it's literally the file itself (an imported package like SFXGame)
                    return package.Exports.FirstOrDefault(x => x.idxLink == 0); //this will be at top of the tree
                }

                if (forcedExportPath != null)
                {
                    // We will try both with stripped header and non-stripped
                    // ForcedExport does not use package name as root (e.g. does not use 'SFXGame')
                    // Non-ForcedExport are native to the package (e.g. does use 'SFXGame')
                    var clippedExport = package.FindExport(forcedExportPath);
                    if (clippedExport != null)
                    {
                        if (clippedExport.IsForcedExport)
                        {
                            return null; // This import should not resolve! ForcedExport cannot use the packagename as the root of the import.
                        }
                        return clippedExport; // The export is not marked ForcedExport so this is fine to resolve
                    }
                }
                return package.FindExport(entryFullPath);
            }
        }

        /// <summary>
        /// Gets the list of files that an import entry can potentially reference - this is not the same as the game, but is a user
        /// attempt to mimic it (it is unlikely to be 100% accurate)
        /// </summary>
        /// <param name="entry">Import entry to check</param>
        /// <returns>List of filenames of POSSIBLE associated sources, they may not exist</returns>
        public static List<string> GetPossibleImportFiles(ImportEntry entry, string localization = @"INT", string gameRootOverride = null)
        {
            return GetPossibleImportFiles(entry.FileRef, entry, localization, gameRootOverride);
        }

        /// <summary>
        /// Returns the list of files that a file might try to import from. This list is not 100% accurate, but an attempt to mimic BioWare's system
        /// </summary>
        /// <param name="package">Package file that is being checked</param>
        /// <param name="entry">The import being checked - this can be null if you only want package-file possibilities</param>
        /// <param name="localization">Localization to return files for</param>
        /// <returns>List of filenames of potential files imported from - this does not include filepaths and not all files will be guaranteed to exist</returns>
        public static List<string> GetPossibleImportFiles(IMEPackage package, ImportEntry entry, string localization = @"INT", string gameRootOverride = null)
        {
            var gameFiles = MELoadedFiles.GetFilesLoadedInGame(package.Game, forceUseCached: true, gameRootOverride: gameRootOverride);

            List<string> filesToCheck = new List<string>();

            // 06/12/2022 - Disabled this code since we have a more definitive list of files safe to import from.
            // This just added dupes to the list and worked under assumption that you could import from a SeekFreePackage like
            // SFXPawn_Banshee - which was false, it must have been dynamic loaded

            //string upkOrPcc = package.Game == MEGame.ME1 ? ".upk" : ".pcc";
            // Check if there is package that has this name. This works for things like resolving SFXPawn_Banshee
            //if (entry != null)
            //{
            //    bool addPackageFile = gameFiles.TryGetValue(entry.ObjectName + upkOrPcc, out var efxPath) && !filesToCheck.Contains(efxPath);

            //    // Let's see if there is same-named top level package folder file. This will resolve class imports from SFXGame, Engine, etc.
            //    IEntry p = entry.Parent;
            //    if (p != null)
            //    {
            //        while (p.Parent != null)
            //        {
            //            p = p.Parent;
            //        }

            //        if (p.ClassName == "Package")
            //        {
            //            if (gameFiles.TryGetValue($"{p.ObjectName}{upkOrPcc}", out var efPath) && !filesToCheck.Contains(efxPath))
            //            {
            //                filesToCheck.Add(Path.GetFileName(efPath));
            //            }
            //            else if (package.Game == MEGame.ME1)
            //            {
            //                if (gameFiles.TryGetValue(p.ObjectName + ".u", out var path) && !filesToCheck.Contains(efxPath))
            //                {
            //                    filesToCheck.Add(Path.GetFileName(path));
            //                }
            //            }
            //        }
            //    }
            //    if (addPackageFile)
            //    {
            //        filesToCheck.Add(Path.GetFileName(efxPath));
            //    }
            //}

            //add related files that will be loaded at the same time (eg. for BioD_Nor_310, check BioD_Nor_310_LOC_INT, BioD_Nor, and BioP_Nor)
            filesToCheck.AddRange(GetPossibleAssociatedFiles(package, localization));

            //add base definition files that are always loaded (Core, Engine, etc.)
            foreach (var fileName in FilesSafeToImportFrom(package.Game))
            {
                if (gameFiles.TryGetValue(fileName, out var efPath))
                {
                    filesToCheck.Add(Path.GetFileName(efPath));
                }
            }

            // Add post-load files if we determine our local file is a post-load
            if (package.FilePath != null && (package.LECLTagData != null && package.LECLTagData.IsPostLoadFile) || IsPostLoadFile(package.FilePath, package.Game))
            {
                foreach (var fileName in FilesSafeToImportFromPostLoad(package.Game))
                {
                    if (gameFiles.TryGetValue(fileName, out var efPath))
                    {
                        filesToCheck.Add(Path.GetFileName(efPath));
                    }
                }
            }

            // 08/24/2022: LECLData import hints
            if (package.LECLTagData != null && package.LECLTagData.ImportHintFiles != null)
            {
                // File has been tagged with hints as to other files it can import from, such as a mod shipping a file that uses its own startup
                filesToCheck.AddRange(package.LECLTagData.ImportHintFiles);
            }

            //add startup files (always loaded)
            /* IEnumerable<string> startups;
            if (entry.Game.IsGame2() || entry.Game is MEGame.LE1)
            {
                startups = gameFiles.Keys.Where(x => x.Contains("Startup_", StringComparison.InvariantCultureIgnoreCase) && x.Contains($"_{localization}", StringComparison.InvariantCultureIgnoreCase)); //me2 this will unfortunately include the main startup file
            }
            else
            {
                startups = gameFiles.Keys.Where(x => x.Contains("Startup_", StringComparison.InvariantCultureIgnoreCase)); //me2 this will unfortunately include the main startup file
            }*/

            return filesToCheck;
        }

        public static List<string> GetPossibleAssociatedFiles(IMEPackage package, string localization = "INT", bool includeNonBioPRelated = true)
        {
            // This method doesn't really work for files loaded from a stream as they have null FilePath - like certain files in M3
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(package.FilePath)?.ToLower();
            var associatedFiles = new List<string>();
            string bioFileExt = package.Game == MEGame.ME1 ? ".sfm" : ".pcc";
            if (includeNonBioPRelated)
            {
                associatedFiles.Add($"{filenameWithoutExtension}_LOC_{localization}{bioFileExt}"); //todo: support users setting preferred language of game files
            }

            associatedFiles.AddRange(GetBioXParentFiles(package.Game, filenameWithoutExtension, true, true, bioFileExt, localization));

            if (package.Game == MEGame.ME3 && filenameWithoutExtension.Contains("MP", StringComparison.OrdinalIgnoreCase) && !filenameWithoutExtension.CaseInsensitiveEquals("BIOP_MP_COMMON"))
            {
                associatedFiles.Add("BIOP_MP_COMMON.pcc");
            }

            return associatedFiles;
        }

        /// <summary>
        /// Gets the possible parent files of the specified package name according to the BioWare tier system.
        /// </summary>
        /// <param name="filenameWithoutExtension"></param>
        /// <param name="includeNonBioPTiers">If files other than BioP should be included (like Bio files should be included</param>
        /// <param name="bioFileExt"></param>
        /// <param name="localization"></param>
        /// <returns></returns>
        public static List<string> GetBioXParentFiles(MEGame game, string filenameWithoutExtension, bool includeNonBioPTiers, bool includeLocalizations, string bioFileExt, string localization)
        {
            filenameWithoutExtension = filenameWithoutExtension.ToLower(); // Ensure lowercase
            List<string> associatedFiles = [];
            var isBioXfile = filenameWithoutExtension?.Length > 5 &&
                                         filenameWithoutExtension.StartsWith("bio") &&
                                         !filenameWithoutExtension.StartsWith("biog") && // BioG are cooked seek free and are not meant to be imported from
                                         filenameWithoutExtension[4] == '_';
            if (isBioXfile)
            {
                // Do not include extensions in the results of this, they will be appended in resulting file
                static string bioXNextFileLookup(string filenameWithoutExtensionX)
                {
                    //Lookup parents
                    var bioType = filenameWithoutExtensionX[3];
                    string[] parts = filenameWithoutExtensionX.Split('_');
                    if (parts.Length >= 2) //BioA_Nor_WowThatsAlot310
                    {
                        var levelName = parts[1];
                        switch (bioType)
                        {
                            case 'a' when parts.Length > 2:
                                return $"bioa_{levelName}";
                            case 'd' when parts.Length > 2:
                                return $"biod_{levelName}";
                            case 's' when parts.Length > 2:
                                return $"bios_{levelName}"; //BioS has no subfiles as far as I know but we'll just put this here anyways.
                            case 'a' when parts.Length == 2:
                            case 'd' when parts.Length == 2:
                            case 's' when parts.Length == 2:
                                return $"biop_{levelName}";
                        }
                    }

                    return null;
                }

                string nextfile = bioXNextFileLookup(filenameWithoutExtension);
                while (nextfile != null)
                {
                    if (includeNonBioPTiers)
                    {
                        associatedFiles.Add($"{nextfile}{bioFileExt}");
                        if (includeLocalizations)
                        {
                            associatedFiles.Add(
                                $"{nextfile}_LOC_{localization}{bioFileExt}"); //todo: support users setting preferred language of game files
                        }
                    }
                    else if (nextfile.Length > 3 && nextfile[3] == 'p')
                    {
                        associatedFiles.Add($"{nextfile}{bioFileExt}");
                    }
                    nextfile = bioXNextFileLookup(nextfile.ToLower());
                }
            }

            return associatedFiles;
        }

        public static IEntry EnsureClassIsInFile(IMEPackage pcc, string className, RelinkerOptionsPackage rop, string gamePathOverride = null)
        {
            //When ensuring classes in Engine and SFXGame, this will save a very large amount of time
            if (pcc.FindEntry(className) is { IsClass: true } topLevelClassEntry)
            {
                return topLevelClassEntry;
            }

            //check to see class is already in file
            foreach (ImportEntry import in pcc.Imports)
            {
                if (import.IsClass && import.ObjectName == className)
                {
                    return import;
                }
            }
            foreach (ExportEntry export in pcc.Exports)
            {
                if (export.IsClass && export.ObjectName == className)
                {
                    return export;
                }
            }

            ClassInfo info = GlobalUnrealObjectInfo.GetClassOrStructInfo(pcc.Game, className);

            if (info is null)
            {
                rop.RelinkReport.Add(new EntryStringPair($"Could not locate class: '{className}'"));
                return null;
            }

            //backup some package state so we can undo changes if something goes wrong
            int exportCount = pcc.ExportCount;
            int importCount = pcc.ImportCount;
            List<string> nameListBackup = [.. pcc.Names];

            // If there is no package cache, we may have to open package
            // If we open package not with cache we should make sure we re-close the package
            IMEPackage nonCachedOpenedPackage = null;
            try
            {
                IMEPackage packageToImportFrom = null; // Not inlined for clarity of scope and purpose
                Stream loadStream = null; // The package stream to open. It might have to load from an SFAR

                #region Read from DLC_TestPatch (ME3 only)
                // Caching this would be pretty complicated so we're just not going to do that
                // Files are pretty small anyways so won't have too big of a performance improvement
                if (pcc.Game is MEGame.ME3 && info.pccPath.StartsWith("DLC_TestPatch"))
                {
                    string fileName = Path.GetFileName(info.pccPath);
                    string testPatchSfarPath = ME3Directory.TestPatchSFARPath;
                    if (testPatchSfarPath is null)
                    {
                        return null;
                    }

                    var patchSFAR = new DLCPackage(testPatchSfarPath);
                    int fileIdx = patchSFAR.FindFileEntry(fileName);
                    if (fileIdx == -1)
                    {
                        return null;
                    }

                    MemoryStream sfarEntry = patchSFAR.DecompressEntry(fileIdx);
                    using IMEPackage patchPcc = MEPackageHandler.OpenMEPackageFromStream(sfarEntry.SeekBegin());
                    patchPcc.IsMemoryPackage = true;
                    if (patchPcc.TryGetUExport(info.exportIndex, out ExportEntry export) && export.IsClass && export.ObjectName == className)
                    {
                        string packageName = export.ParentName;
                        if (IsSafeToImportFrom($"{packageName}.pcc", MEGame.ME3, pcc.FilePath))
                        {
                            return pcc.GetEntryOrAddImport($"{packageName}.{className}", "Class");
                        }
                        else
                        {
                            loadStream = sfarEntry.SeekBegin();
                        }
                    }
                }
                #endregion

                // No cache and not testpatch. Can this be imported?
                // Not actually sure if you can import testpatch since I think it just
                // patches on object load but who knows
                if (loadStream == null && rop.GenerateImportsForGlobalFiles && IsSafeToImportFrom(info.pccPath, pcc))
                {
                    string package = Path.GetFileNameWithoutExtension(info.pccPath);
                    // 'instancedFullPath' is only populated when inventoried at runtime.
                    if (info.forcedExport && info.instancedFullPath != null)
                    {
                        return pcc.GetEntryOrAddImport(info.instancedFullPath, "Class"); // It's a forced export
                    }
                    else if (info.instancedFullPath != null)
                    {
                        return pcc.GetEntryOrAddImport($"{package}.{info.instancedFullPath}", "Class");
                    }
                    else
                    {
                        // The original code pre 02/27/2023
                        return pcc.GetEntryOrAddImport($"{package}.{className}", "Class");
                    }
                }

                string fullPackagePath = Path.Combine(MEDirectories.GetBioGamePath(pcc.Game, gamePathOverride), info.pccPath);
                if (loadStream is null && (rop.Cache == null || !rop.Cache.TryGetCachedPackage(fullPackagePath, true, out packageToImportFrom)))
                {
                    // Loadstream is null and we don't have a package cache or we could not load it from disk
                    //It's a class that's defined locally in every file that uses it.
                    if (info.pccPath == GlobalUnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
                    {
                        loadStream = LegendaryExplorerCoreUtilities.GetCustomAppResourceStream(pcc.Game);
                        //string resourceFilePath = App.CustomResourceFilePath(pcc.Game);
                        //if (File.Exists(resourceFilePath))
                        //{
                        //    sourceFilePath = resourceFilePath;
                        //}
                    }
                    else
                    {
                        if (File.Exists(fullPackagePath))
                        {
                            loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(fullPackagePath);
                        }
                        else if (pcc.Game == MEGame.ME1)
                        {
                            fullPackagePath = Path.Combine(gamePathOverride ?? ME1Directory.DefaultGamePath, info.pccPath);
                            if (File.Exists(fullPackagePath))
                            {
                                loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(fullPackagePath);
                            }
                        }
                    }

                    if (loadStream is null)
                    {
                        //can't find file to import from. This may occur if user does not have game or neccesary dlc installed 
                        return null;
                    }

                    packageToImportFrom = MEPackageHandler.OpenMEPackageFromStream(loadStream);
                    nonCachedOpenedPackage = packageToImportFrom; // Needs re-closed at end
                }

                if (packageToImportFrom == null && loadStream != null)
                {
                    // Open package (TestPatch)
                    packageToImportFrom = MEPackageHandler.OpenMEPackageFromStream(loadStream);
                }

                if (packageToImportFrom == null)
                {
                    Debug.WriteLine(@"Could not find package to import from!");
                    return null;
                }

                if (!packageToImportFrom.IsUExport(info.exportIndex))
                {
                    return null; //not sure how this would happen
                }

                ExportEntry sourceClassExport = packageToImportFrom.GetUExport(info.exportIndex);

                if (sourceClassExport.ObjectName != className)
                {
                    return null;
                }

                //Will make sure that, if the class is in a package, that package will exist in pcc
                IEntry parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, packageToImportFrom, pcc, rop);

                var relinkResults = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceClassExport, pcc, parent, true, rop, out IEntry result);

                // Notify of our relink results so we can merge them into a higher list if necessary
                // Disabled in CrossGen 09/30/2021 as we pass through a ROP that has it and will use a single list
                //if (relinkResults?.Count > 0)
                //{
                //    RelinkResultsAvailable?.Invoke(relinkResults);
                //}

                return result;
            }
            catch (Exception)
            {
                //remove added entries
                var entriesToRemove = new List<IEntry>();
                for (int i = exportCount; i < pcc.Exports.Count; i++)
                {
                    entriesToRemove.Add(pcc.Exports[i]);
                }

                for (int i = importCount; i < pcc.Imports.Count; i++)
                {
                    entriesToRemove.Add(pcc.Imports[i]);
                }

                EntryPruner.TrashEntries(pcc, entriesToRemove);
                pcc.restoreNames(nameListBackup);
                return null;
            }
            finally
            {
                nonCachedOpenedPackage?.Dispose(); // If opened from non-cache, make sure it's closed
            }
        }

        /// <summary>
        /// Gets a recursive list of things the specified export references
        /// </summary>
        /// <param name="export">The export to check</param>
        /// <param name="includeLink">If the link should be included, which can sometimes pull in way more stuff than you want</param>
        /// <returns></returns>
        public static List<IEntry> GetAllReferencesOfExport(ExportEntry export, bool includeLink = false)
        {
            var referencedItems = new List<IEntry>();
            RecursiveGetDependencies(export, referencedItems, includeLink);
            return referencedItems.Distinct().ToList();
        }

        private static void AddEntryReference(int referenceIdx, IMEPackage package, List<IEntry> referencedItems)
        {
            if (package.TryGetEntry(referenceIdx, out IEntry reference) && !referencedItems.Contains(reference))
            {
                referencedItems.Add(reference);
            }
        }

        private static void RecursiveGetDependencies(ExportEntry relinkingExport, List<IEntry> referencedItems, bool includeLink)
        {
            var localExportReferences = new List<ExportEntry>();

            // Compiles list of items local to this entry
            void AddReferenceLocal(int entryUIndex)
            {
                if (relinkingExport.FileRef.TryGetUExport(entryUIndex, out ExportEntry exp) && referencedItems.All(x => x.UIndex != entryUIndex))
                {
                    // Add objects that we have not referenced yet.
                    localExportReferences.Add(exp);
                }
                // Global add
                AddEntryReference(entryUIndex, relinkingExport.FileRef, referencedItems);
            }

            if (includeLink && relinkingExport.Parent != null)
            {
                AddReferenceLocal(relinkingExport.Parent.UIndex);
            }

            // Pre-props binary
            byte[] prePropBinary = relinkingExport.GetPrePropBinary();

            //Relink stack
            if (relinkingExport.HasStack)
            {
                int uIndex = BitConverter.ToInt32(prePropBinary, 0);
                AddReferenceLocal(uIndex);

                uIndex = BitConverter.ToInt32(prePropBinary, 4);
                AddReferenceLocal(uIndex);
            }
            //Relink Component's TemplateOwnerClass
            else if (relinkingExport.TemplateOwnerClassIdx is var toci and >= 0)
            {
                int uIndex = BitConverter.ToInt32(prePropBinary, toci);
                AddReferenceLocal(uIndex);
            }

            // Metadata
            if (relinkingExport.SuperClass != null)
                AddReferenceLocal(relinkingExport.idxSuperClass);
            if (relinkingExport.Archetype != null)
                AddReferenceLocal(relinkingExport.idxArchetype);
            if (relinkingExport.Class != null)
                AddReferenceLocal(relinkingExport.idxClass);

            // Properties
            var props = relinkingExport.GetProperties();
            foreach (var prop in props)
            {
                RecursiveGetPropDependencies(prop, AddReferenceLocal);
            }

            // Binary
            var bin = ObjectBinary.From(relinkingExport);
            if (bin != null)
            {
                List<int> binUIndexes = bin.GetUIndexes(relinkingExport.Game);
                foreach (int binUIndex in binUIndexes)
                {
                    AddReferenceLocal(binUIndex);
                }
            }

            // We have now collected all local references
            // We should reach out and see if we need to index others.
            foreach (var v in localExportReferences)
            {
                RecursiveGetDependencies(v, referencedItems, true);
            }
        }

        private static void RecursiveGetPropDependencies(Property prop, Action<int> addReference)
        {
            if (prop is ObjectProperty op)
            {
                addReference(op.Value);
            }
            else if (prop is StructProperty sp)
            {
                foreach (var p in sp.Properties)
                {
                    RecursiveGetPropDependencies(p, addReference);
                }
            }
            else if (prop is ArrayProperty<StructProperty> asp)
            {
                foreach (var p in asp)
                {
                    RecursiveGetPropDependencies(p, addReference);
                }
            }
            else if (prop is ArrayProperty<ObjectProperty> aop)
            {
                foreach (var p in aop)
                {
                    addReference(p.Value);
                }
            }
            else if (prop is DelegateProperty dp)
            {
                addReference(dp.Value.ContainingObjectUIndex);
            }
        }

        /// <summary>
        /// Attempts to resolve the specified import and returns true or false if it did.
        /// </summary>
        /// <param name="importEntry"></param>
        /// <param name="export"></param>
        /// <param name="globalCache"></param>
        /// <param name="localCache"></param>
        /// <param name="localization"></param>
        /// <param name="unsafeLoad"></param>
        /// <param name="localDirFiles"></param>
        /// <returns></returns>
        public static bool TryResolveImport(ImportEntry importEntry, out ExportEntry export, PackageCache globalCache = null, PackageCache localCache = null, string localization = @"INT", bool unsafeLoad = false, IEnumerable<string> localDirFiles = null)
        {
            export = ResolveImport(importEntry, globalCache, localCache, localization, unsafeLoad, localDirFiles);
            return export != null;
        }

        /// <summary>
        /// Experimental: Converts an export to an import. Only use if you really know what you're doing!
        /// </summary>
        /// <param name="export"></param>
        /// <returns></returns>
        public static ImportEntry ConvertExportToImport(ExportEntry export, string forcedPackageFile = null)
        {
            string packageFile = ImportEntry.GetPackageFile(export);
            if (forcedPackageFile == null && GlobalUnrealObjectInfo.GetClasses(export.Game).TryGetValue(export.ClassName, out var classInfo))
            {
                // PackageFile on an import is the package file that contains the class of the class
                // E.g.
                // Texture_Shepard is of type 'Texture2D'
                // An import of this would have PackageFile 'Engine'
                // Because the class of Texture_Shepard is Texture2D and Texture2D is defined in Engine.
                // Now, how do we find that?
            }
            var convertedItem = new ImportEntry(export.FileRef, export.Parent, new NameReference(export.ObjectName.Name + "_TMP", export.ObjectName.Number))
            {
                ClassName = export.ClassName,
                PackageFile = forcedPackageFile ?? packageFile,
            };
            export.FileRef.AddImport((ImportEntry)convertedItem);

            // Update all references
            var referencingEntries = export.GetEntriesThatReferenceThisOne();
            foreach (var f in referencingEntries)
            {
                // Make a new map for every iteration since this technically could add some items... somehow...
                var objectMap = new ListenableDictionary<IEntry, IEntry>
                {
                    { export, convertedItem }, // Convert references to import
                    { f.Key, f.Key } // Force this to relink on itself.
                };
                RelinkerOptionsPackage rop = new RelinkerOptionsPackage()
                {
                    CrossPackageMap = objectMap
                };
                Relinker.RelinkAll(rop);
            }

            // Cleanup temporary stuff
            convertedItem.ObjectName = export.ObjectName;
            EntryPruner.TrashEntries(export.FileRef, new[] { export });

            return convertedItem;
        }
    }
}