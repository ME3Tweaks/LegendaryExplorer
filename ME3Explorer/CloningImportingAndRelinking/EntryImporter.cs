using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using ME3Explorer.PackageEditorWPFControls;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using StreamHelpers;

namespace ME3Explorer
{
    public static class EntryImporter
    {
        public enum PortingOption
        {
            CloneTreeAsChild,
            AddSingularAsChild,
            ReplaceSingular,
            MergeTreeChildren,
            Cancel,
            CloneAllDependencies //not available for user selection, for use by programmers in specialized situations
        }

        private static readonly byte[] me1Me2StackDummy =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] me3StackDummy =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] UDKStackDummy =
        {
            0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        /// <summary>
        /// Imports <paramref name="sourceEntry"/> (and possibly its children) to <paramref name="destPcc"/> in a manner defined by <paramref name="portingOption"/>
        /// If no <paramref name="relinkMap"/> is provided, method will create one
        /// </summary>
        /// <param name="portingOption"></param>
        /// <param name="sourceEntry"></param>
        /// <param name="destPcc"></param>
        /// <param name="targetLinkEntry">Can be null if cloning as a top-level entry</param>
        /// <param name="shouldRelink"></param>
        /// <param name="newEntry"></param>
        /// <param name="relinkMap"></param>
        /// <returns></returns>
        public static List<string> ImportAndRelinkEntries(PortingOption portingOption, IEntry sourceEntry, IMEPackage destPcc, IEntry targetLinkEntry, bool shouldRelink,
                                                          out IEntry newEntry, Dictionary<IEntry, IEntry> relinkMap = null)
        {
            relinkMap ??= new Dictionary<IEntry, IEntry>();
            IMEPackage sourcePcc = sourceEntry.FileRef;
            EntryTree sourcePackageTree = new EntryTree(sourcePcc);

            if (portingOption == PortingOption.ReplaceSingular)
            {
                //replace data only
                if (sourceEntry is ExportEntry entry)
                {
                    relinkMap.Add(entry, targetLinkEntry);
                    ReplaceExportDataWithAnother(entry, targetLinkEntry as ExportEntry);
                }
            }

            if (portingOption == PortingOption.MergeTreeChildren || portingOption == PortingOption.ReplaceSingular)
            {
                newEntry = targetLinkEntry; //Root item is the one we just dropped. Use that as the root.
            }
            else
            {
                int link = targetLinkEntry?.UIndex ?? 0;
                if (sourceEntry is ExportEntry sourceExport)
                {
                    //importing an export
                    newEntry = ImportExport(destPcc, sourceExport, link, portingOption == PortingOption.CloneAllDependencies, relinkMap);
                }
                else
                {
                    newEntry = GetOrAddCrossImportOrPackage(sourceEntry.FullPath, sourcePcc, destPcc,
                                                            forcedLink: sourcePackageTree.NumChildrenOf(sourceEntry) == 0 ? link : (int?)null, objectMapping: relinkMap);
                }

                newEntry.idxLink = link;
            }


            //if this node has children
            if ((portingOption == PortingOption.CloneTreeAsChild || portingOption == PortingOption.MergeTreeChildren || portingOption == PortingOption.CloneAllDependencies)
             && sourcePackageTree.NumChildrenOf(sourceEntry) > 0)
            {
                importChildrenOf(sourceEntry, newEntry);
            }

            List<string> relinkResults = null;
            if (shouldRelink)
            {
                relinkResults = Relinker.RelinkAll(relinkMap, portingOption == PortingOption.CloneAllDependencies);
            }

            return relinkResults;

            void importChildrenOf(IEntry sourceNode, IEntry newParent)
            {
                foreach (IEntry node in sourcePackageTree.GetDirectChildrenOf(sourceNode))
                {
                    if (portingOption == PortingOption.MergeTreeChildren)
                    {
                        //we must check to see if there is an item already matching what we are trying to port.

                        //Todo: We may need to enhance target checking here as fullpath may not be reliable enough. Maybe have to do indexing, or something.
                        IEntry sameObjInTarget = newParent.GetChildren().FirstOrDefault(x => node.FullPath == x.FullPath);
                        if (sameObjInTarget != null)
                        {
                            relinkMap[node] = sameObjInTarget;

                            //merge children to this node instead
                            importChildrenOf(node, sameObjInTarget);

                            continue;
                        }
                    }

                    IEntry entry;
                    if (node is ExportEntry exportNode)
                    {
                        entry = ImportExport(destPcc, exportNode, newParent.UIndex, portingOption == PortingOption.CloneAllDependencies, relinkMap);
                    }
                    else
                    {
                        entry = GetOrAddCrossImportOrPackage(node.FullPath, sourcePcc, destPcc, objectMapping: relinkMap);
                    }

                    entry.Parent = newParent;

                    importChildrenOf(node, entry);
                }
            }

        }

        /// <summary>
        /// Imports an export from another package file.
        /// </summary>
        /// <param name="destPackage">Package to import to</param>
        /// <param name="sourceExport">Export object from the other package to import</param>
        /// <param name="link">Local parent node UIndex</param>
        /// <param name="importExportDependencies">Whether to import exports that are referenced in header</param>
        /// <param name="objectMapping"></param>
        /// <returns></returns>
        public static ExportEntry ImportExport(IMEPackage destPackage, ExportEntry sourceExport, int link, bool importExportDependencies = false, IDictionary<IEntry, IEntry> objectMapping = null)
        {
            byte[] prePropBinary;
            if (sourceExport.HasStack)
            {
                var ms = new MemoryStream();
                ms.WriteFromBuffer(sourceExport.Data.Slice(0, 8));
                ms.WriteFromBuffer(destPackage.Game switch
                {
                    MEGame.UDK => UDKStackDummy,
                    MEGame.ME3 => me3StackDummy,
                    _ => me1Me2StackDummy
                });
                prePropBinary = ms.ToArray();
            }
            else
            {
                int start = sourceExport.GetPropertyStart();
                if (start == 16)
                {
                    var ms = new MemoryStream(sourceExport.Data.Slice(0, 16));
                    ms.JumpTo(4);
                    int newNameIdx = destPackage.FindNameOrAdd(sourceExport.FileRef.GetNameEntry(ms.ReadInt32()));
                    ms.JumpTo(4);
                    ms.WriteInt32(newNameIdx);
                    prePropBinary = ms.ToArray();
                }
                else
                {
                    prePropBinary = sourceExport.Data.Slice(0, start);
                }
            }

            PropertyCollection props = sourceExport.GetProperties();
            //store copy of names list in case something goes wrong
            List<string> names = destPackage.Names.ToList();
            try
            {
                if (sourceExport.Game != destPackage.Game)
                {
                    props = EntryPruner.RemoveIncompatibleProperties(sourceExport.FileRef, props, sourceExport.ClassName, destPackage.Game);
                }
            }
            catch (Exception exception) when(!App.IsDebug)
            {
                //restore namelist in event of failure.
                destPackage.setNames(names);
                MessageBox.Show($"Error occured while trying to import {sourceExport.ObjectName.Instanced} : {exception.Message}");
                throw;
            }

            //takes care of slight header differences between ME1/2 and ME3
            byte[] newHeader = sourceExport.GenerateHeader(destPackage.Game, true);

            //for supported classes, this will add any names in binary to the Name table, as well as take care of binary differences for cross-game importing
            //for unsupported classes, this will just copy over the binary
            ObjectBinary binaryData = ExportBinaryConverter.ConvertPostPropBinary(sourceExport, destPackage.Game);

            //Set class.
            IEntry classValue = null;
            switch (sourceExport.Class)
            {
                case ImportEntry sourceClassImport:
                    //The class of the export we are importing is an import. We should attempt to relink this.
                    classValue = GetOrAddCrossImportOrPackage(sourceClassImport.FullPath, sourceExport.FileRef, destPackage, objectMapping: objectMapping);
                    break;
                case ExportEntry sourceClassExport:
                    classValue = destPackage.Exports.FirstOrDefault(x => x.FullPath == sourceClassExport.FullPath && x.indexValue == sourceClassExport.indexValue);
                    if (classValue is null && importExportDependencies)
                    {
                        IEntry classParent = GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, sourceExport.FileRef, destPackage, true, objectMapping);
                        classValue = ImportExport(destPackage, sourceClassExport, classParent?.UIndex ?? 0, true, objectMapping);
                    }
                    break;
            }

            //Set superclass
            IEntry superclass = null;
            switch (sourceExport.SuperClass)
            {
                case ImportEntry sourceSuperClassImport:
                    //The class of the export we are importing is an import. We should attempt to relink this.
                    superclass = GetOrAddCrossImportOrPackage(sourceSuperClassImport.FullPath, sourceExport.FileRef, destPackage, objectMapping: objectMapping);
                    break;
                case ExportEntry sourceSuperClassExport:
                    superclass = destPackage.Exports.FirstOrDefault(x => x.FullPath == sourceSuperClassExport.FullPath && x.indexValue == sourceSuperClassExport.indexValue);
                    if (superclass is null && importExportDependencies)
                    {
                        IEntry superClassParent = GetOrAddCrossImportOrPackage(sourceSuperClassExport.ParentFullPath, sourceExport.FileRef, destPackage, 
                                                                               true, objectMapping);
                        superclass = ImportExport(destPackage, sourceSuperClassExport, superClassParent?.UIndex ?? 0,  true, objectMapping);
                    }
                    break;
            }

            //Check archetype.
            IEntry archetype = null;
            switch (sourceExport.Archetype)
            {
                case ImportEntry sourceArchetypeImport:
                    archetype = GetOrAddCrossImportOrPackage(sourceArchetypeImport.FullPath, sourceExport.FileRef, destPackage, objectMapping: objectMapping);
                    break;
                case ExportEntry sourceArchetypeExport:
                    archetype = destPackage.Exports.FirstOrDefault(x => x.FullPath == sourceArchetypeExport.FullPath && x.indexValue == sourceArchetypeExport.indexValue);
                    if (archetype is null && importExportDependencies)
                    {
                        IEntry archetypeParent = GetOrAddCrossImportOrPackage(sourceArchetypeExport.ParentFullPath, sourceExport.FileRef, destPackage, 
                                                                              true, objectMapping);
                        archetype = ImportExport(destPackage, sourceArchetypeExport, archetypeParent?.UIndex ?? 0, true, objectMapping);
                    }
                    break;
            }

            var newExport = new ExportEntry(destPackage, prePropBinary, props, binaryData, sourceExport.IsClass)
            {
                Header = newHeader,
                Class = classValue,
                ObjectName = sourceExport.ObjectName,
                idxLink = link,
                SuperClass = superclass,
                Archetype = archetype,
                DataOffset = 0
            };
            destPackage.AddExport(newExport);
            if (objectMapping != null)
            {
                objectMapping[sourceExport] = newExport;
            }
            return newExport;
        }

        public static bool ReplaceExportDataWithAnother(ExportEntry incomingExport, ExportEntry targetExport)
        {

            MemoryStream res = new MemoryStream();
            if (incomingExport.HasStack)
            {
                res.WriteFromBuffer(incomingExport.Data.Slice(0, 8));
                res.WriteFromBuffer(targetExport.Game switch
                {
                    MEGame.UDK => UDKStackDummy,
                    MEGame.ME3 => me3StackDummy,
                    _ => me1Me2StackDummy
                });
            }
            else
            {
                int start = incomingExport.GetPropertyStart();
                res.Write(new byte[start], 0, start);
            }

            //store copy of names list in case something goes wrong
            List<string> names = targetExport.FileRef.Names.ToList();
            try
            {
                incomingExport.GetProperties().WriteTo(res, targetExport.FileRef);
            }
            catch (Exception exception)
            {
                //restore namelist in event of failure.
                targetExport.FileRef.setNames(names);
                MessageBox.Show($"Error occured while replacing data in {incomingExport.ObjectName.Instanced} : {exception.Message}");
                return false;
            }
            res.WriteFromBuffer(ExportBinaryConverter.ConvertPostPropBinary(incomingExport, targetExport.Game).ToBytes(targetExport.FileRef));
            targetExport.Data = res.ToArray();
            return true;
        }

        /// <summary>
        /// Adds an import from the importingPCC to the destinationPCC with the specified importFullName, or returns the existing one if it can be found. 
        /// This will add parent imports and packages as neccesary
        /// </summary>
        /// <param name="importFullName">GetFullPath() of an import from ImportingPCC</param>
        /// <param name="sourcePcc">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <param name="forcedLink">force this as parent</param>
        /// <param name="importNonPackageExportsToo"></param>
        /// <param name="objectMapping"></param>
        /// <returns></returns>
        public static IEntry GetOrAddCrossImportOrPackage(string importFullName, IMEPackage sourcePcc, IMEPackage destinationPCC,
                                                          bool importNonPackageExportsToo = false, IDictionary<IEntry, IEntry> objectMapping = null, int? forcedLink = null)
        {
            if (string.IsNullOrEmpty(importFullName))
            {
                return null;
            }

            //see if this import exists locally
            foreach (ImportEntry imp in destinationPCC.Imports)
            {
                if (imp.FullPath == importFullName)
                {
                    return imp;
                }
            }

            //see if this export exists locally
            foreach (ExportEntry exp in destinationPCC.Exports)
            {
                if (exp.FullPath == importFullName)
                {
                    return exp;
                }
            }

            if (forcedLink is int link)
            {
                ImportEntry importingImport = sourcePcc.Imports.First(x => x.FullPath == importFullName); //this shouldn't be null
                var newImport = new ImportEntry(destinationPCC)
                {
                    idxLink = link,
                    ClassName = importingImport.ClassName,
                    ObjectName = importingImport.ObjectName,
                    PackageFile = importingImport.PackageFile
                };
                destinationPCC.AddImport(newImport);
                if (objectMapping != null)
                {
                    objectMapping[importingImport] = newImport;
                }

                return newImport;
            }

            string[] importParts = importFullName.Split('.');

            //recursively ensure parent exists. when importParts.Length == 1, this will return null
            IEntry parent = GetOrAddCrossImportOrPackage(string.Join(".", importParts.Take(importParts.Length - 1)), sourcePcc, destinationPCC,
                                                         importNonPackageExportsToo, objectMapping);


            foreach (ImportEntry sourceImport in sourcePcc.Imports)
            {
                if (sourceImport.FullPath == importFullName)
                {
                    var newImport = new ImportEntry(destinationPCC)
                    {
                        idxLink = parent?.UIndex ?? 0,
                        ClassName = sourceImport.ClassName,
                        ObjectName = sourceImport.ObjectName,
                        PackageFile = sourceImport.PackageFile
                    };
                    destinationPCC.AddImport(newImport);
                    if (objectMapping != null)
                    {
                        objectMapping[sourceImport] = newImport;
                    }
                    return newImport;
                }
            }

            foreach (ExportEntry sourceExport in sourcePcc.Exports)
            {
                if ((importNonPackageExportsToo || sourceExport.ClassName == "Package") && sourceExport.FullPath == importFullName)
                {
                    return ImportExport(destinationPCC, sourceExport, parent?.UIndex ?? 0, importNonPackageExportsToo, objectMapping);
                }
            }

            throw new Exception($"Unable to add {importFullName} to file! Could not find it!");
        }
        
        /// <summary>
         /// Adds an import from the importingPCC to the destinationPCC with the specified importFullName, or returns the existing one if it can be found. 
         /// This will add parent imports and packages as neccesary
         /// </summary>
         /// <param name="importFullName">GetFullPath() of an import from ImportingPCC</param>
         /// <param name="sourcePcc">PCC to import imports from</param>
         /// <param name="destinationPCC">PCC to add imports to</param>
         /// <param name="objectMapping"></param>
         /// <returns></returns>
        public static IEntry GetOrAddCrossImportOrPackageFromGlobalFile(string importFullName, IMEPackage sourcePcc, IMEPackage destinationPCC, IDictionary<IEntry, IEntry> objectMapping = null)
        {
            string packageName = Path.GetFileNameWithoutExtension(sourcePcc.FilePath);
            if (string.IsNullOrEmpty(importFullName))
            {
                return destinationPCC.getEntryOrAddImport(packageName, "Package");
            }

            string localSearchPath = $"{packageName}.{importFullName}";

            //see if this import exists locally
            foreach (ImportEntry imp in destinationPCC.Imports)
            {
                if (imp.FullPath == localSearchPath)
                {
                    return imp;
                }
            }

            //see if this export exists locally
            foreach (ExportEntry exp in destinationPCC.Exports)
            {
                if (exp.FullPath == localSearchPath)
                {
                    return exp;
                }
            }

            string[] importParts = importFullName.Split('.');

            //recursively ensure parent exists
            IEntry parent = GetOrAddCrossImportOrPackageFromGlobalFile(string.Join(".", importParts.Take(importParts.Length - 1)), sourcePcc, destinationPCC, objectMapping);


            foreach (ImportEntry sourceImport in sourcePcc.Imports)
            {
                if (sourceImport.FullPath == importFullName)
                {
                    var newImport = new ImportEntry(destinationPCC)
                    {
                        idxLink = parent?.UIndex ?? 0,
                        ClassName = sourceImport.ClassName,
                        ObjectName = sourceImport.ObjectName,
                        PackageFile = sourceImport.PackageFile
                    };
                    destinationPCC.AddImport(newImport);
                    if (objectMapping != null)
                    {
                        objectMapping[sourceImport] = newImport;
                    }
                    return newImport;
                }
            }

            foreach (ExportEntry sourceExport in sourcePcc.Exports)
            {
                if (sourceExport.FullPath == importFullName)
                {
                    var newImport = new ImportEntry(destinationPCC)
                    {
                        idxLink = parent?.UIndex ?? 0,
                        ClassName = sourceExport.ClassName,
                        ObjectName = sourceExport.ObjectName,
                        PackageFile = "Core" //No clue how to figure out what this should be. Might not even matter?
                    };
                    destinationPCC.AddImport(newImport);
                    if (objectMapping != null)
                    {
                        objectMapping[sourceExport] = newImport;
                    }
                    return newImport;
                }
            }

            throw new Exception($"Unable to add {importFullName} to file! Could not find it!");
        }

        public static IEntry EnsureClassIsInFile(IMEPackage pcc, string className)
        {
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

            ClassInfo info = UnrealObjectInfo.GetClassOrStructInfo(pcc.Game, className);

            //backup some package state so we can undo changes if something goes wrong
            int exportCount = pcc.ExportCount;
            int importCount = pcc.ImportCount;
            List<string> nameListBackup = pcc.Names.ToList();
            try
            {
                if (IsSafeToImportFrom(info.pccPath, pcc.Game))
                {
                    string package = Path.GetFileNameWithoutExtension(info.pccPath);
                    return pcc.getEntryOrAddImport($"{package}.{className}");
                }

                //It's a class that's defined locally in every file that uses it.
                string sourceFilePath = null;
                if (info.pccPath == UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
                {
                    string resourceFilePath = App.CustomResourceFilePath(pcc.Game);
                    if (File.Exists(resourceFilePath))
                    {
                        sourceFilePath = resourceFilePath;
                    }
                }
                else
                {
                    string testPath = Path.Combine(MEDirectories.BioGamePath(pcc.Game), info.pccPath);
                    if (File.Exists(testPath))
                    {
                        sourceFilePath = testPath;
                    }
                    else if (pcc.Game == MEGame.ME1)
                    {
                        testPath = Path.Combine(ME1Directory.gamePath, info.pccPath);
                        if (File.Exists(testPath))
                        {
                            sourceFilePath = testPath;
                        }
                    }
                }

                if (sourceFilePath is null)
                {
                    //can't find file to import from. This may occur if user does not have game or neccesary dlc installed 
                    return null;
                }

                using IMEPackage sourcePackage = MEPackageHandler.OpenMEPackage(sourceFilePath);

                if (!sourcePackage.IsUExport(info.exportIndex))
                {
                    return null; //not sure how this would happen
                }

                ExportEntry sourceClassExport = sourcePackage.GetUExport(info.exportIndex);

                if (sourceClassExport.ObjectName != className)
                {
                    return null;
                }
                    
                //Will make sure that, if the class is in a package, that package will exist in pcc
                IEntry parent = GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, sourcePackage, pcc);

                List<string> relinkResults = ImportAndRelinkEntries(PortingOption.CloneAllDependencies, sourceClassExport, pcc, parent, true, out IEntry result);
                if (relinkResults?.Count > 0)
                {
                    ListDialog ld = new ListDialog(relinkResults, "Relink report", "The following items failed to relink.", null);
                    ld.Show();
                }
                return result;
            }
            catch (Exception e)
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
                pcc.setNames(nameListBackup);
                new ExceptionHandlerDialogWPF(e).ShowDialog();
                return null;
            }
        }

        //SirCxyrtyx: These are not exhaustive lists, just the ones that I'm sure about
        private static readonly string[] me1FilesSafeToImportFrom = { "Core.u", "Engine.u", "BIOC_Base.u", "BIOC_BaseDLC_Vegas.u", "BIOC_BaseDLC_UNC.u" };

        private static readonly string[] me2FilesSafeToImportFrom = { "Core.pcc", "Engine.pcc", "SFXGame.pcc", "WwiseAudio.pcc" };

        private static readonly string[] me3FilesSafeToImportFrom = { "Core.pcc", "Engine.pcc", "SFXGame.pcc", "WwiseAudio.pcc", "Startup.pcc", "GFxUI.pcc", "GameFramework.pcc" };

        public static bool IsSafeToImportFrom(string path, MEGame game)
        {
            string fileName = Path.GetFileName(path);
            return (game switch
            {
                MEGame.ME1 => me1FilesSafeToImportFrom,
                MEGame.ME2 => me2FilesSafeToImportFrom,
                _ => me3FilesSafeToImportFrom
            }).Any(f => fileName == f);
        }

        public static bool CanImport(string className, MEGame game) => CanImport(UnrealObjectInfo.GetClassOrStructInfo(game, className), game);

        public static bool CanImport(ClassInfo classInfo, MEGame game) => classInfo != null && IsSafeToImportFrom(classInfo.pccPath, game);

        public static byte[] CreateStack(MEGame game, int stateNodeUIndex)
        {
            var ms = new MemoryStream();
            ms.WriteInt32(stateNodeUIndex);
            ms.WriteInt32(stateNodeUIndex);
            ms.WriteFromBuffer(game switch
            {
                MEGame.UDK => UDKStackDummy,
                MEGame.ME3 => me3StackDummy,
                _ => me1Me2StackDummy
            });
            return ms.ToArray();
        }
    }
}