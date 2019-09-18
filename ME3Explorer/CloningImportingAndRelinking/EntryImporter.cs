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
        private static readonly byte[] me1Me2StackDummy =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] me3UDKStackDummy = new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
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
        public static List<string> ImportAndRelinkEntries(TreeMergeDialog.PortingOption portingOption, IEntry sourceEntry, IMEPackage destPcc, IEntry targetLinkEntry, bool shouldRelink,
                                                          out IEntry newEntry, Dictionary<IEntry, IEntry> relinkMap = null)
        {
            relinkMap = relinkMap ?? new Dictionary<IEntry, IEntry>();
            IMEPackage sourcePcc = sourceEntry.FileRef;
            EntryTree sourcePackageTree = new EntryTree(sourcePcc);
            EntryTree destPackageTree = new EntryTree(destPcc);

            if (portingOption == TreeMergeDialog.PortingOption.ReplaceSingular)
            {
                //replace data only
                if (sourceEntry is ExportEntry entry)
                {
                    relinkMap.Add(entry, targetLinkEntry);
                    ReplaceExportDataWithAnother(entry, targetLinkEntry as ExportEntry);
                }
            }

            if (portingOption == TreeMergeDialog.PortingOption.MergeTreeChildren || portingOption == TreeMergeDialog.PortingOption.ReplaceSingular)
            {
                newEntry = targetLinkEntry; //Root item is the one we just dropped. Use that as the root.
            }
            else
            {
                int link = targetLinkEntry?.idxLink ?? 0;
                if (sourceEntry is ExportEntry sourceExport)
                {
                    //importing an export
                    newEntry = importExport(destPcc, sourceExport, link);
                    relinkMap[sourceExport] = newEntry; //map old index to new index
                }
                else
                {
                    newEntry = getOrAddCrossImportOrPackage(sourceEntry.GetFullPath, sourcePcc, destPcc,
                                                            sourcePackageTree.NumChildrenOf(sourceEntry) == 0 ? link : (int?)null);
                    relinkMap[sourceEntry] = newEntry;
                }

                newEntry.idxLink = link;
            }


            //if this node has children
            if ((portingOption == TreeMergeDialog.PortingOption.CloneTreeAsChild || portingOption == TreeMergeDialog.PortingOption.MergeTreeChildren)
             && sourcePackageTree.NumChildrenOf(sourceEntry) > 0)
            {
                importChildrenOf(sourceEntry, newEntry);
            }

            List<string> relinkResults = null;
            if (shouldRelink)
            {
                relinkResults = Relinker.RelinkAll(relinkMap, sourcePcc);
            }

            return relinkResults;

            void importChildrenOf(IEntry sourceNode, IEntry newParent)
            {
                foreach (IEntry node in sourcePackageTree.GetDirectChildrenOf(sourceNode))
                {
                    if (portingOption == TreeMergeDialog.PortingOption.MergeTreeChildren)
                    {
                        //we must check to see if there is an item already matching what we are trying to port.

                        //Todo: We may need to enhance target checking here as getfullpath may not be reliable enough. Maybe have to do indexing, or something.
                        IEntry sameObjInTarget = destPackageTree.GetDirectChildrenOf(newParent).FirstOrDefault(x => node.GetFullPath == x.GetFullPath);
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
                        entry = importExport(destPcc, exportNode, newParent.UIndex);
                    }
                    else
                    {
                        entry = getOrAddCrossImportOrPackage(node.GetFullPath, sourcePcc, destPcc);
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
        /// <param name="outputEntry">Newly generated export entry reference</param>
        /// <returns></returns>
        public static ExportEntry importExport(IMEPackage destPackage, ExportEntry ex, int link)
        {
            byte[] prePropBinary;
            if (ex.HasStack)
            {
                prePropBinary = destPackage.Game < MEGame.ME3 ? me1Me2StackDummy : me3UDKStackDummy;
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
                MessageBox.Show($"Error occured while trying to import {ex.ObjectName} : {exception.Message}");
                throw;
            }

            //takes care of slight header differences between ME1/2 and ME3
            byte[] newHeader = ex.GenerateHeader(destPackage.Game);

            //for supported classes, this will add any names in binary to the Name table, as well as take care of binary differences for cross-game importing
            //for unsupported classes, this will just copy over the binary
            byte[] binaryData = ExportBinaryConverter.ConvertPostPropBinary(ex, destPackage.Game).ToBytes(destPackage);

            int classValue = 0;
            int archetype = 0;
            int superclass = 0;
            //Set class. This will only work if the class is an import, as we can't reliably pull in exports without lots of other stuff.
            if (ex.idxClass < 0)
            {
                //The class of the export we are importing is an import. We should attempt to relink this.
                ImportEntry portingFromClassImport = ex.FileRef.getImport(ex.idxClass);
                IEntry newClassImport = getOrAddCrossImportOrPackage(portingFromClassImport.GetFullPath, ex.FileRef, destPackage);
                classValue = newClassImport.UIndex;
            }
            else if (ex.idxClass > 0)
            {
                //Todo: Add cross mapping support as multi-mode will allow this to work now
                ExportEntry portingInClass = ex.FileRef.getUExport(ex.idxClass);
                ExportEntry matchingExport = destPackage.Exports.FirstOrDefault(x => x.GetIndexedFullPath == portingInClass.GetIndexedFullPath);
                if (matchingExport != null)
                {
                    classValue = matchingExport.UIndex;
                }
            }

            //Set superclass
            if (ex.idxSuperClass < 0)
            {
                //The class of the export we are importing is an import. We should attempt to relink this.
                ImportEntry portingFromClassImport = ex.FileRef.getImport(ex.idxSuperClass);
                IEntry newClassImport = getOrAddCrossImportOrPackage(portingFromClassImport.GetFullPath, ex.FileRef, destPackage);
                superclass = newClassImport.UIndex;
            }
            else if (ex.idxSuperClass > 0)
            {
                //Todo: Add cross mapping support as multi-mode will allow this to work now
                ExportEntry portingInClass = ex.FileRef.getUExport(ex.idxSuperClass);
                ExportEntry matchingExport = destPackage.Exports.FirstOrDefault(x => x.GetIndexedFullPath == portingInClass.GetIndexedFullPath);
                if (matchingExport != null)
                {
                    superclass = matchingExport.UIndex;
                }
            }

            //Check archetype.
            if (ex.idxArchtype < 0)
            {
                ImportEntry portingFromClassImport = ex.FileRef.getImport(ex.idxArchtype);
                IEntry newClassImport = getOrAddCrossImportOrPackage(portingFromClassImport.GetFullPath, ex.FileRef, destPackage);
                archetype = newClassImport.UIndex;
            }
            else if (ex.idxArchtype > 0)
            {
                ExportEntry portingInClass = ex.FileRef.getUExport(ex.idxArchtype);
                ExportEntry matchingExport = destPackage.Exports.FirstOrDefault(x => x.GetIndexedFullPath == portingInClass.GetIndexedFullPath);
                if (matchingExport != null)
                {
                    archetype = matchingExport.UIndex;
                }
            }

            var outputEntry = new ExportEntry(destPackage, prePropBinary, props, binaryData)
            {
                Header = newHeader,
                idxClass = classValue,
                idxObjectName = destPackage.FindNameOrAdd(ex.FileRef.getNameEntry(ex.idxObjectName)),
                idxLink = link,
                idxSuperClass = superclass,
                idxArchtype = archetype
            };
            destPackage.addExport(outputEntry);
            return outputEntry;
        }

        public static bool ReplaceExportDataWithAnother(ExportEntry incomingExport, ExportEntry targetExport)
        {

            MemoryStream res = new MemoryStream();
            if (incomingExport.HasStack)
            {
                //TODO: Find a unique NetIndex instead of writing a blank... don't know if that will fix multiplayer sync issues
                byte[] stackdummy = targetExport.Game < MEGame.ME3 ? me1Me2StackDummy : me3UDKStackDummy;
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
                MessageBox.Show($"Error occured while replacing data in {incomingExport.ObjectName} : {exception.Message}");
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
        public static IEntry getOrAddCrossImportOrPackage(string importFullName, IMEPackage importingPCC, IMEPackage destinationPCC, int? forcedLink = null)
        {
            if (String.IsNullOrEmpty(importFullName))
            {
                return null;
            }

            //see if this import exists locally
            foreach (ImportEntry imp in destinationPCC.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    return imp;
                }
            }

            //see if this is an export Package and exists locally
            foreach (ExportEntry exp in destinationPCC.Exports)
            {
                if (exp.ClassName == "Package" && exp.GetFullPath == importFullName)
                {
                    return exp;
                }
            }

            if (forcedLink is int link)
            {
                ImportEntry importingImport = importingPCC.Imports.First(x => x.GetFullPath == importFullName); //this shouldn't be null
                var newImport = new ImportEntry(destinationPCC)
                {
                    idxLink = link,
                    idxClassName = destinationPCC.FindNameOrAdd(importingImport.ClassName),
                    idxObjectName = destinationPCC.FindNameOrAdd(importingImport.ObjectName),
                    idxPackageFile = destinationPCC.FindNameOrAdd(importingImport.PackageFile)
                };
                destinationPCC.addImport(newImport);
                return newImport;
            }

            string[] importParts = importFullName.Split('.');

            //recursively ensure parent package exists. when importParts.Length == 1, this will return null
            IEntry parent = getOrAddCrossImportOrPackage(String.Join(".", importParts.Take(importParts.Length - 1)), importingPCC, destinationPCC);


            foreach (ImportEntry imp in importingPCC.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    var import = new ImportEntry(destinationPCC)
                    {
                        idxLink = parent?.UIndex ?? 0,
                        idxClassName = destinationPCC.FindNameOrAdd(imp.ClassName),
                        idxObjectName = destinationPCC.FindNameOrAdd(imp.ObjectName),
                        idxPackageFile = destinationPCC.FindNameOrAdd(imp.PackageFile)
                    };
                    destinationPCC.addImport(import);
                    return import;
                }
            }

            foreach (ExportEntry exp in importingPCC.Exports)
            {
                if (exp.ClassName == "Package" && exp.GetFullPath == importFullName)
                {
                    return importExport(destinationPCC, exp, parent?.UIndex ?? 0);
                }
            }

            throw new Exception($"Unable to add {importFullName} to file! Could not find it!");
        }
    }
}