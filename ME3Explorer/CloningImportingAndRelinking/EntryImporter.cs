using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using ME3Explorer.PackageEditorWPFControls;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using StreamHelpers;

namespace ME3Explorer
{
    public class EntryImporter
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
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] me3StackDummy = new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] UDKStackDummy = new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
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
            EntryTree destPackageTree = new EntryTree(destPcc);

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
                    newEntry = ImportExport(destPcc, sourceExport, link, portingOption == PortingOption.CloneAllDependencies);
                    relinkMap[sourceExport] = newEntry; //map old index to new index
                }
                else
                {
                    newEntry = GetOrAddCrossImportOrPackage(sourceEntry.FullPath, sourcePcc, destPcc,
                                                            sourcePackageTree.NumChildrenOf(sourceEntry) == 0 ? link : (int?)null);
                    relinkMap[sourceEntry] = newEntry;
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
                relinkResults = Relinker.RelinkAll(relinkMap, sourcePcc, portingOption == PortingOption.CloneAllDependencies);
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
                        IEntry sameObjInTarget = destPackageTree.GetDirectChildrenOf(newParent).FirstOrDefault(x => node.FullPath == x.FullPath);
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
                        entry = ImportExport(destPcc, exportNode, newParent.UIndex, portingOption == PortingOption.CloneAllDependencies);
                    }
                    else
                    {
                        entry = GetOrAddCrossImportOrPackage(node.FullPath, sourcePcc, destPcc);
                    }
                    relinkMap[node] = entry;

                    entry.Parent = newParent;

                    importChildrenOf(node, entry);
                }
            }

        }

        /// <summary>
        /// Imports an export from another package file.
        /// </summary>
        /// <param name="destPackage">Package to import to</param>
        /// <param name="ex">Export object from the other package to import</param>
        /// <param name="link">Local parent node UIndex</param>
        /// <param name="importExportDependencies">Whether to import exports that are referenced in header</param>
        /// <returns></returns>
        public static ExportEntry ImportExport(IMEPackage destPackage, ExportEntry ex, int link, bool importExportDependencies = false)
        {
            byte[] prePropBinary;
            if (ex.HasStack)
            {
                prePropBinary = destPackage.Game == MEGame.UDK ? UDKStackDummy :
                                destPackage.Game == MEGame.ME3 ? me3StackDummy : 
                                                                 me1Me2StackDummy;
            }
            else
            {
                int start = ex.GetPropertyStart();
                prePropBinary = new byte[start];
            }

            PropertyCollection props = ex.GetProperties();
            //store copy of names list in case something goes wrong
            List<string> names = destPackage.Names.ToList();
            try
            {
                if (ex.Game != destPackage.Game)
                {
                    props = EntryPruner.RemoveIncompatibleProperties(ex.FileRef, props, ex.ClassName, destPackage.Game);
                }
            }
            catch (Exception exception)
            {
                //restore namelist in event of failure.
                destPackage.setNames(names);
                MessageBox.Show($"Error occured while trying to import {ex.ObjectName.Instanced} : {exception.Message}");
                throw;
            }

            //takes care of slight header differences between ME1/2 and ME3
            byte[] newHeader = ex.GenerateHeader(destPackage.Game);

            //for supported classes, this will add any names in binary to the Name table, as well as take care of binary differences for cross-game importing
            //for unsupported classes, this will just copy over the binary
            byte[] binaryData = ExportBinaryConverter.ConvertPostPropBinary(ex, destPackage.Game).ToBytes(destPackage);

            //Set class.
            IEntry classValue = null;
            switch (ex.Class)
            {
                case ImportEntry sourceClassImport:
                    //The class of the export we are importing is an import. We should attempt to relink this.
                    classValue = GetOrAddCrossImportOrPackage(sourceClassImport.FullPath, ex.FileRef, destPackage);
                    break;
                case ExportEntry sourceClassExport when importExportDependencies:
                    classValue = ImportExport(destPackage, sourceClassExport,
                                              GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, ex.FileRef, destPackage)?.UIndex ?? 0, true);
                    break;
                case ExportEntry sourceClassExport:
                    //Todo: Add cross mapping support as multi-mode will allow this to work now
                    classValue = destPackage.Exports.FirstOrDefault(x => x.FullPath == sourceClassExport.FullPath && x.indexValue == sourceClassExport.indexValue);
                    break;
            }

            //Set superclass
            IEntry superclass = null;
            switch (ex.SuperClass)
            {
                case ImportEntry sourceSuperClassImport:
                    //The class of the export we are importing is an import. We should attempt to relink this.
                    superclass = GetOrAddCrossImportOrPackage(sourceSuperClassImport.FullPath, ex.FileRef, destPackage);
                    break;
                case ExportEntry sourceSuperClassExport when importExportDependencies:
                    classValue = ImportExport(destPackage, sourceSuperClassExport,
                                              GetOrAddCrossImportOrPackage(sourceSuperClassExport.ParentFullPath, ex.FileRef, destPackage)?.UIndex ?? 0, true);
                    break;
                case ExportEntry sourceSuperClassExport:
                    //Todo: Add cross mapping support as multi-mode will allow this to work now
                    superclass = destPackage.Exports.FirstOrDefault(x => x.FullPath == sourceSuperClassExport.FullPath && x.indexValue == sourceSuperClassExport.indexValue);
                    break;
            }

            //Check archetype.
            IEntry archetype = null;
            switch (ex.Archetype)
            {
                case ImportEntry sourceArchetypeImport:
                    archetype = GetOrAddCrossImportOrPackage(sourceArchetypeImport.FullPath, ex.FileRef, destPackage);
                    break;
                case ExportEntry sourceArchetypeImport when importExportDependencies:
                    classValue = ImportExport(destPackage, sourceArchetypeImport,
                                              GetOrAddCrossImportOrPackage(sourceArchetypeImport.ParentFullPath, ex.FileRef, destPackage)?.UIndex ?? 0, true);
                    break;
                case ExportEntry sourceArchetypExport:
                    archetype = destPackage.Exports.FirstOrDefault(x => x.FullPath == sourceArchetypExport.FullPath && x.indexValue == sourceArchetypExport.indexValue);
                    break;
            }

            var outputEntry = new ExportEntry(destPackage, prePropBinary, props, binaryData, ex.IsClass)
            {
                Header = newHeader,
                Class = classValue,
                ObjectName = ex.ObjectName,
                idxLink = link,
                SuperClass = superclass,
                Archetype = archetype
            };
            destPackage.AddExport(outputEntry);
            return outputEntry;
        }

        public static bool ReplaceExportDataWithAnother(ExportEntry incomingExport, ExportEntry targetExport)
        {

            MemoryStream res = new MemoryStream();
            if (incomingExport.HasStack)
            {
                //TODO: Find a unique NetIndex instead of writing a blank... don't know if that will fix multiplayer sync issues
                byte[] stackdummy = targetExport.Game == MEGame.UDK ? UDKStackDummy :
                                    targetExport.Game == MEGame.ME3 ? me3StackDummy : 
                                                                      me1Me2StackDummy;
                res.Write(stackdummy, 0, stackdummy.Length);
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
        /// <param name="importingPCC">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <param name="forcedLink">force this as parent</param>
        /// <returns></returns>
        public static IEntry GetOrAddCrossImportOrPackage(string importFullName, IMEPackage importingPCC, IMEPackage destinationPCC, int? forcedLink = null)
        {
            if (String.IsNullOrEmpty(importFullName))
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

            //see if this is an export Package and exists locally
            foreach (ExportEntry exp in destinationPCC.Exports)
            {
                if (exp.ClassName == "Package" && exp.FullPath == importFullName)
                {
                    return exp;
                }
            }

            if (forcedLink is int link)
            {
                ImportEntry importingImport = importingPCC.Imports.First(x => x.FullPath == importFullName); //this shouldn't be null
                var newImport = new ImportEntry(destinationPCC)
                {
                    idxLink = link,
                    ClassName = importingImport.ClassName,
                    ObjectName = importingImport.ObjectName,
                    PackageFile = importingImport.PackageFile
                };
                destinationPCC.AddImport(newImport);
                return newImport;
            }

            string[] importParts = importFullName.Split('.');

            //recursively ensure parent package exists. when importParts.Length == 1, this will return null
            IEntry parent = GetOrAddCrossImportOrPackage(String.Join(".", importParts.Take(importParts.Length - 1)), importingPCC, destinationPCC);


            foreach (ImportEntry imp in importingPCC.Imports)
            {
                if (imp.FullPath == importFullName)
                {
                    var import = new ImportEntry(destinationPCC)
                    {
                        idxLink = parent?.UIndex ?? 0,
                        ClassName = imp.ClassName,
                        ObjectName = imp.ObjectName,
                        PackageFile = imp.PackageFile
                    };
                    destinationPCC.AddImport(import);
                    return import;
                }
            }

            foreach (ExportEntry exp in importingPCC.Exports)
            {
                if (exp.ClassName == "Package" && exp.FullPath == importFullName)
                {
                    return ImportExport(destinationPCC, exp, parent?.UIndex ?? 0);
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

            //If it's not found in game files, then there's not much we can do ¯\_(ツ)_/¯
            if (info.pccPath == UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
            {
                return null;
            }

            if (IsSafeToImportFrom(info.pccPath, pcc.Game))
            {
                string package = Path.GetFileNameWithoutExtension(info.pccPath);
                return pcc.getEntryOrAddImport($"{package}.{className}");
            }

            //It's a class that's defined locally in every file that uses it.
            string sourceFilePath = null;
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
                    
            //Will make sure that, if the class is in a package, that package will exist in pcc
            IEntry parent = GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, sourcePackage, pcc);

            List<string> relinkResults = ImportAndRelinkEntries(PortingOption.CloneAllDependencies, sourceClassExport, pcc, parent, true, out IEntry result);
            //should probably do something with the relink results
            return result;
        }

        //SirCxyrtyx: These are not exhaustive lists, just the ones that I'm sure about
        private static readonly string[] me1FilesSafeToImportFrom = { "Core.u", "Engine.u", "BIOC_Base.u", "BIOC_BaseDLC_Vegas.u", "BIOC_BaseDLC_UNC.u" };

        private static readonly string[] me2FilesSafeToImportFrom = { "Core.pcc", "Engine.pcc", "SFXGame.pcc", "WwiseAudio.pcc" };

        private static readonly string[] me3FilesSafeToImportFrom = { "Core.pcc", "Engine.pcc", "SFXGame.pcc", "WwiseAudio.pcc", "Startup.pcc" };

        private static bool IsSafeToImportFrom(string path, MEGame game) =>
            (game switch
            {
                MEGame.ME1 => me1FilesSafeToImportFrom,
                MEGame.ME2 => me2FilesSafeToImportFrom,
                _ => me3FilesSafeToImportFrom
            }).Any(path.EndsWith);
    }
}