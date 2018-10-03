using KFreonLib.Debugging;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public partial class PackageEditorWPF
    {
        /// <summary>
        /// Attempts to relink unreal property data using propertycollection when cross porting an export
        /// </summary>
        private List<string> relinkObjects2(IMEPackage importpcc)
        {
            List<string> relinkResults = new List<string>();
            //relink each modified export

            //We must convert this to a list, as this list will be updated as imports are cross mapped during relinking.
            //This process speeds up same-relinks later.
            //This is a list because otherwise we would get a concurrent modification exception.
            //Since we only enumerate exports and append imports to this list we will not need to worry about recursive links
            //I am sure this won't come back to be a pain for me.
            List<KeyValuePair<int, int>> crossPCCObjectMappingList = crossPCCObjectMap.ToList();
            for (int i = 0; i < crossPCCObjectMappingList.Count; i++)
            {
                KeyValuePair<int, int> entry = crossPCCObjectMappingList[i];
                if (entry.Key > 0)
                {
                    PropertyCollection transplantProps = importpcc.Exports[entry.Key].GetProperties();
                    relinkResults.AddRange(relinkPropertiesRecursive(importpcc, Pcc, transplantProps, crossPCCObjectMappingList));
                    Pcc.getExport(entry.Value).WriteProperties(transplantProps);
                }
            }

            return relinkResults;
        }

        private List<string> relinkPropertiesRecursive(IMEPackage importingPCC, IMEPackage destinationPCC, PropertyCollection transplantProps, List<KeyValuePair<int, int>> crossPCCObjectMappingList)
        {
            List<string> relinkResults = new List<string>();
            foreach (UProperty prop in transplantProps)
            {
                if (prop is StructProperty)
                {
                    relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, destinationPCC, (prop as StructProperty).Properties, crossPCCObjectMappingList));
                }
                else if (prop is ArrayProperty<StructProperty>)
                {
                    foreach (StructProperty arrayStructProperty in prop as ArrayProperty<StructProperty>)
                    {
                        relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, destinationPCC, arrayStructProperty.Properties, crossPCCObjectMappingList));
                    }
                }
                else if (prop is ArrayProperty<ObjectProperty>)
                {
                    foreach (ObjectProperty objProperty in prop as ArrayProperty<ObjectProperty>)
                    {
                        string result = relinkObjectProperty(importingPCC, destinationPCC, objProperty, crossPCCObjectMappingList);
                        if (result != null)
                        {
                            relinkResults.Add(result);
                        }
                    }
                }
                if (prop is ObjectProperty)
                {
                    //relink
                    string result = relinkObjectProperty(importingPCC, destinationPCC, prop as ObjectProperty, crossPCCObjectMappingList);
                    if (result != null)
                    {
                        relinkResults.Add(result);
                    }
                }
            }
            return relinkResults;
        }

        private string relinkObjectProperty(IMEPackage importingPCC, IMEPackage destinationPCC, ObjectProperty objProperty, List<KeyValuePair<int, int>> crossPCCObjectMappingList)
        {
            if (objProperty.Value == 0)
            {
                return null; //do not relink 0
            }
            if (importingPCC == destinationPCC && objProperty.Value < 0)
            {
                return null; //do not relink same-pcc imports.
            }
            int sourceObjReference = objProperty.Value;

            if (sourceObjReference > 0)
            {
                sourceObjReference--; //make 0 based for mapping.
            }
            if (sourceObjReference < 0)
            {
                sourceObjReference++; //make 0 based for mapping.
            }
            //if (objProperty.Name != null)
            //{
            //    Debug.WriteLine(objProperty.Name);
            //}
            KeyValuePair<int, int> mapping = crossPCCObjectMappingList.Where(pair => pair.Key == sourceObjReference).FirstOrDefault();
            var defaultKVP = default(KeyValuePair<int, int>); //struct comparison

            if (!mapping.Equals(defaultKVP))
            {
                //relink
                int newval = 0;
                if (mapping.Value > 0)
                {
                    newval = mapping.Value + 1; //reincrement
                }
                else if (mapping.Value < 0)
                {
                    newval = mapping.Value - 1; //redecrement
                }
                objProperty.Value = (newval);
                IEntry entry = destinationPCC.getEntry(newval);
                string s = "";
                if (entry != null)
                {
                    s = entry.GetFullPath;
                }
                Debug.WriteLine("Relink hit: " + sourceObjReference + objProperty.Name + ": " + s);
            }
            else if (objProperty.Value < 0) //It's an unmapped import
            {
                //objProperty is currently pointing to importingPCC as that is where we read the properties from
                int n = objProperty.Value;
                int origvalue = n;
                int importZeroIndex = Math.Abs(n) - 1;
                //Debug.WriteLine("Relink miss, attempting JIT relink on " + n + " " + rootNode.Text);
                if (n < 0 && importZeroIndex < importingPCC.ImportCount)
                {
                    //Get the original import
                    ImportEntry origImport = importingPCC.getImport(importZeroIndex);
                    string origImportFullName = origImport.GetFullPath;
                    //Debug.WriteLine("We should import " + origImport.GetFullPath);

                    ImportEntry crossImport = null;
                    string linkFailedDueToError = null;
                    try
                    {
                        crossImport = getOrAddCrossImport(origImportFullName, importingPCC, destinationPCC);
                    }
                    catch (Exception e)
                    {
                        //Error during relink
                        KFreonLib.Debugging.DebugOutput.StartDebugger("PCC Relinker");
                        DebugOutput.PrintLn("Exception occured during relink: ");
                        DebugOutput.PrintLn(ExceptionHandlerDialogWPF.FlattenException(e));
                        DebugOutput.PrintLn("You may want to consider discarding this sessions' changes as relinking was not able to properly finish.");
                        linkFailedDueToError = e.Message;
                    }
                    if (crossImport != null)
                    {
                        //cache item. Imports are stored +1, Exports-1. Someday I will go back and make this just 0 indexed
                        crossPCCObjectMappingList.Add(new KeyValuePair<int, int>(sourceObjReference, crossImport.UIndex + 1)); //add to mapping to speed up future relinks
                        objProperty.Value = crossImport.UIndex;
                        Debug.WriteLine("Relink hit: Dynamic CrossImport for " + origvalue + " " + importingPCC.getEntry(origvalue).GetFullPath + " -> " + objProperty.Value);

                    }
                    else
                    {
                        if (linkFailedDueToError != null)
                        {
                            Debug.WriteLine("Relink failed: CrossImport porting failed for " + objProperty.Name + " " + objProperty.Value + ": " + importingPCC.getEntry(origvalue).GetFullPath);
                            return "Relink failed for " + objProperty.Name + " " + objProperty.Value + ": " + linkFailedDueToError;
                        }
                        else
                        if (destinationPCC.getEntry(objProperty.Value) != null)
                        {
                            Debug.WriteLine("Relink failed: CrossImport porting failed for " + objProperty.Name + " " + objProperty.Value + ": " + importingPCC.getEntry(origvalue).GetFullPath);
                            return "Relink failed: CrossImport porting failed for " + objProperty.Name + " " + objProperty.Value + " " + destinationPCC.getEntry(objProperty.Value).GetFullPath;
                        }
                        else
                        {
                            return "Relink failed: New export does not exist - this is probably a bug in cross import code for " + objProperty.Name + " " + objProperty.Value;
                        }
                    }
                }
            }
            else
            {
                string path = importingPCC.getEntry(objProperty.Value) != null ? importingPCC.getEntry(objProperty.Value).GetFullPath : "Entry not found: " + objProperty.Value;
                Debug.WriteLine("Relink failed: " + objProperty.Name + " " + objProperty.Value + " " + path);
                return "Relink failed: " + objProperty.Name + " " + objProperty.Value + " " + path;
            }
            return null;
        }

        /// <summary>
        /// Attempts to relink unreal binary data to object pointers if they are part of the clone tree.
        /// It's gonna be an ugly mess.
        /// </summary>
        /// <param name="importpcc">PCC being imported from</param>
        private List<string> relinkBinaryObjects(IMEPackage importpcc)
        {
            List<string> relinkFailedReport = new List<string>();
            foreach (KeyValuePair<int, int> entry in crossPCCObjectMap)
            {
                if (entry.Key > 0)
                {
                    IExportEntry exp = Pcc.getExport(entry.Value);
                    byte[] binarydata = exp.getBinaryData();
                    if (binarydata.Length > 0)
                    {
                        //has binary data
                        //This is temporary until I find a more permanent style for relinking binary
                        try
                        {
                            switch (exp.ClassName)
                            {
                                case "WwiseEvent":
                                    {
                                        int count = BitConverter.ToInt32(binarydata, 0);
                                        for (int i = 0; i < count; i++)
                                        {
                                            int originalValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                            int currentObjectRef = unrealIndexToME3ExpIndexing(originalValue); //count + i * intsize
                                            int mapped;
                                            bool isMapped = crossPCCObjectMap.TryGetValue(currentObjectRef, out mapped);
                                            if (isMapped)
                                            {
                                                mapped = me3ExpIndexingToUnreal(mapped, originalValue < 0); //if the value is 0, it would have an error anyways.
                                                Debug.WriteLine("Binary relink hit for WwiseEvent Export " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + originalValue + " -> " + (mapped + 1));
                                                WriteMem(4 + (i * 4), binarydata, BitConverter.GetBytes(mapped));
                                                int newValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                                Debug.WriteLine(originalValue + " -> " + newValue);
                                            }
                                            else
                                            {
                                                Debug.WriteLine("Binary relink missed WwiseEvent Export " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + originalValue);
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: WwiseEvent referenced WwiseStream " + originalValue + " is not in the mapping tree and could not be relinked");
                                            }
                                        }
                                        exp.setBinaryData(binarydata);
                                    }
                                    break;
                                case "Class":
                                    {
                                        if (exp.FileRef.Game != importpcc.Game)
                                        {
                                            //Cannot relink against a different game.
                                            continue;
                                        }
                                        IExportEntry importingExp = importpcc.getExport(entry.Key);
                                        if (importingExp.ClassName != "Class")
                                        {
                                            continue; //the class was not actually set, so this is not really class.
                                        }

                                        //This is going to be pretty ugly
                                        try
                                        {
                                            byte[] newdata = importpcc.Exports[entry.Key].Data; //may need to rewrite first unreal header
                                            byte[] data = importpcc.Exports[entry.Key].Data;

                                            int offset = 0;
                                            int unrealExportIndex = BitConverter.ToInt32(data, offset);
                                            offset += 4;


                                            int superclassIndex = BitConverter.ToInt32(data, offset);
                                            if (superclassIndex < 0)
                                            {
                                                //its an import
                                                ImportEntry superclassImportEntry = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(superclassIndex))];
                                                ImportEntry newSuperclassValue = getOrAddCrossImport(superclassImportEntry.GetFullPath, importpcc, exp.FileRef);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newSuperclassValue.UIndex));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Superclass is an export in the source package and was not relinked.");
                                            }


                                            int unknown1 = BitConverter.ToInt32(data, offset);
                                            offset += 4;

                                            int classObjTree = BitConverter.ToInt32(data, offset); //Don't know if I need to do this.
                                            offset += 4;


                                            //I am not sure what these mean. However if Pt1&2 are 33/25, the following bytes that follow are extended.
                                            int headerUnknown1 = BitConverter.ToInt32(data, offset);
                                            Int64 ignoreMask = BitConverter.ToInt64(data, offset);
                                            offset += 8;

                                            Int16 labelOffset = BitConverter.ToInt16(data, offset);
                                            offset += 2;
                                            int skipAmount = 0x6;
                                            //Find end of script block. Seems to be 10 FF's.
                                            while (offset + skipAmount + 10 < data.Length)
                                            {
                                                //Debug.WriteLine("Cheecking at 0x"+(offset + skipAmount + 10).ToString("X4"));
                                                bool isEnd = true;
                                                for (int i = 0; i < 10; i++)
                                                {
                                                    byte b = data[offset + skipAmount + i];
                                                    if (b != 0xFF)
                                                    {
                                                        isEnd = false;
                                                        break;
                                                    }
                                                }
                                                if (isEnd)
                                                {
                                                    break;
                                                }
                                                else
                                                {
                                                    skipAmount++;
                                                }
                                            }

                                            int offsetEnd = offset + skipAmount + 10;
                                            offset += skipAmount + 10; //heuristic to find end of script
                                            uint stateMask = BitConverter.ToUInt32(data, offset);
                                            offset += 4;

                                            int localFunctionsTableCount = BitConverter.ToInt32(data, offset);
                                            offset += 4;
                                            bool isMapped;
                                            for (int i = 0; i < localFunctionsTableCount; i++)
                                            {
                                                int nameTableIndex = BitConverter.ToInt32(data, offset);
                                                int nameIndex = BitConverter.ToInt32(data, offset + 4);
                                                NameReference importingName = importpcc.getNameEntry(nameTableIndex);
                                                int newFuncName = exp.FileRef.FindNameOrAdd(importingName);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newFuncName));
                                                offset += 8;

                                                int functionObjectIndex = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                                int mapped;
                                                isMapped = crossPCCObjectMap.TryGetValue(functionObjectIndex, out mapped);
                                                if (isMapped)
                                                {
                                                    mapped = me3ExpIndexingToUnreal(mapped, functionObjectIndex < 0); //if the value is 0, it would have an error anyways.
                                                    WriteMem(offset, newdata, BitConverter.GetBytes(mapped));
                                                }
                                                else
                                                {
                                                    relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Local function[" + i + "] could not be remapped during porting: " + functionObjectIndex + " is not in the mapping tree and could not be relinked");
                                                }
                                                offset += 4;
                                            }

                                            int classMask = BitConverter.ToInt32(data, offset);
                                            offset += 4;
                                            if (importpcc.Game != MEGame.ME3)
                                            {
                                                offset += 1; //seems to be a blank byte here
                                            }

                                            int coreReference = BitConverter.ToInt32(data, offset);
                                            if (coreReference < 0)
                                            {
                                                //its an import
                                                ImportEntry outerclassReferenceImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(coreReference))];
                                                ImportEntry outerclassNewImport = getOrAddCrossImport(outerclassReferenceImport.GetFullPath, importpcc, exp.FileRef);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(outerclassNewImport.UIndex));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Outerclass is an export in the original package, not relinked.");
                                            }
                                            offset += 4;


                                            if (importpcc.Game == MEGame.ME3)
                                            {
                                                offset = ClassParser_RelinkComponentsTable(importpcc, exp, relinkFailedReport, ref newdata, offset);
                                                offset = ClassParser_ReadImplementsTable(importpcc, exp, relinkFailedReport, ref newdata, offset);
                                                int postComponentsNoneNameIndex = BitConverter.ToInt32(data, offset);
                                                int postComponentNoneIndex = BitConverter.ToInt32(data, offset + 4);
                                                string postCompName = importpcc.getNameEntry(postComponentsNoneNameIndex); //This appears to be unused in ME3, it is always None it seems.
                                                int newFuncName = exp.FileRef.FindNameOrAdd(postCompName);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newFuncName));
                                                offset += 8;

                                                int unknown4 = BitConverter.ToInt32(data, offset);
                                                offset += 4;
                                            }
                                            else
                                            {
                                                offset = ClassParser_ReadImplementsTable(importpcc, exp, relinkFailedReport, ref data, offset);
                                                offset = ClassParser_RelinkComponentsTable(importpcc, exp, relinkFailedReport, ref data, offset);

                                                int me12unknownend1 = BitConverter.ToInt32(data, offset);
                                                offset += 4;

                                                int me12unknownend2 = BitConverter.ToInt32(data, offset);
                                                offset += 4;
                                            }

                                            int defaultsClassLink = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                            int defClassLink;
                                            isMapped = crossPCCObjectMap.TryGetValue(defaultsClassLink, out defClassLink);
                                            if (isMapped)
                                            {
                                                defClassLink = me3ExpIndexingToUnreal(defClassLink, defaultsClassLink < 0); //if the value is 0, it would have an error anyways.
                                                WriteMem(offset, newdata, BitConverter.GetBytes(defClassLink));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: DefaultsClassLink cannot be currently automatically relinked by Binary Relinker. Please manually set this in Binary Editor");
                                            }
                                            offset += 4;

                                            if (importpcc.Game == MEGame.ME3)
                                            {
                                                int functionsTableCount = BitConverter.ToInt32(data, offset);
                                                offset += 4;

                                                for (int i = 0; i < functionsTableCount; i++)
                                                {
                                                    int functionsTableIndex = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                                    int mapped;
                                                    isMapped = crossPCCObjectMap.TryGetValue(functionsTableIndex, out mapped);
                                                    if (isMapped)
                                                    {
                                                        mapped = me3ExpIndexingToUnreal(mapped, functionsTableIndex < 0); //if the value is 0, it would have an error anyways.
                                                        WriteMem(offset, newdata, BitConverter.GetBytes(mapped));
                                                    }
                                                    else
                                                    {
                                                        if (functionsTableIndex < 0)
                                                        {
                                                            ImportEntry functionObjIndex = importpcc.Imports[Math.Abs(functionsTableIndex)];
                                                            ImportEntry newFunctionObjIndex = getOrAddCrossImport(functionObjIndex.GetFullPath, importpcc, exp.FileRef);
                                                            WriteMem(offset, newdata, BitConverter.GetBytes(newFunctionObjIndex.UIndex));
                                                        }
                                                        else
                                                        {
                                                            relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Full Functions List function[" + i + "] could not be remapped during porting: " + functionsTableIndex + " is not in the mapping tree and could not be relinked");

                                                        }
                                                    }
                                                    offset += 4;
                                                }
                                            }
                                            exp.Data = newdata;
                                        }
                                        catch (Exception ex)
                                        {
                                            relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Exception relinking: " + ex.Message);
                                        }
                                    }
                                    break;
                                default:
                                    continue;
                            }
                        }
                        catch (Exception e)
                        {
                            relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relinking failed: " + e.Message);
                        }
                        //Run an interpreter pass over it - we will find objectleafnodes and attempt to update the same offset in the destination file.
                        //BinaryInterpreter binaryrelinkInterpreter = new ME3Explorer.BinaryInterpreter(importpcc, importpcc.Exports[entry.Key], pcc, pcc.Exports[entry.Value], crossPCCObjectMapping);
                    }
                }
            }
            return relinkFailedReport;
        }

        private int unrealIndexToME3ExpIndexing(int sourceObjReference)
        {
            if (sourceObjReference > 0)
            {
                sourceObjReference--; //make 0 based for mapping.
            }

            if (sourceObjReference < 0)
            {
                sourceObjReference++; //make 0 based for mapping.
            }

            return sourceObjReference;
        }

        private int ClassParser_RelinkComponentsTable(IMEPackage importpcc, IExportEntry exp, List<string> relinkFailedReport, ref byte[] data, int offset)
        {
            if (importpcc.Game == MEGame.ME3)
            {
                int componentTableNameIndex = BitConverter.ToInt32(data, offset);
                int componentTableIndex = BitConverter.ToInt32(data, offset + 4);
                NameReference importingName = importpcc.getNameEntry(componentTableNameIndex);
                int newComponentTableName = exp.FileRef.FindNameOrAdd(importingName);
                WriteMem(offset, data, BitConverter.GetBytes(newComponentTableName));
                offset += 8;

                int componentTableCount = BitConverter.ToInt32(data, offset);
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    importingName = importpcc.getNameEntry(nameTableIndex);
                    int componentName = exp.FileRef.FindNameOrAdd(importingName);
                    WriteMem(offset, data, BitConverter.GetBytes(componentName));
                    offset += 8;

                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    int mapped;
                    bool isMapped = crossPCCObjectMap.TryGetValue(componentObjectIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, componentObjectIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        if (componentObjectIndex < 0)
                        {
                            ImportEntry componentObjectImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(componentObjectIndex))];
                            ImportEntry newComponentObjectImport = getOrAddCrossImport(componentObjectImport.GetFullPath, importpcc, exp.FileRef);
                            WriteMem(offset, data, BitConverter.GetBytes(newComponentObjectImport.UIndex));
                        }
                        else if (componentObjectIndex > 0) //we do not remap on 0 here in binary land
                        {
                            relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Component[" + i + "] could not be remapped during porting: " + componentObjectIndex + " is not in the mapping tree");
                        }
                    }
                    offset += 4;
                }
            }
            else
            {
                int componentTableCount = BitConverter.ToInt32(data, offset);
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);

                    offset += 8;

                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    if (componentObjectIndex != 0)
                    {
                        int mapped;
                        bool isMapped = crossPCCObjectMap.TryGetValue(componentObjectIndex, out mapped);
                        if (isMapped)
                        {
                            mapped = me3ExpIndexingToUnreal(mapped, componentObjectIndex < 0); //if the value is 0, it would have an error anyways.
                            WriteMem(offset, data, BitConverter.GetBytes(mapped));
                        }
                        else
                        {
                            if (componentObjectIndex < 0)
                            {
                                ImportEntry componentObjectImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(componentObjectIndex))];
                                ImportEntry newComponentObjectImport = getOrAddCrossImport(componentObjectImport.GetFullPath, importpcc, exp.FileRef);
                                WriteMem(offset, data, BitConverter.GetBytes(newComponentObjectImport.UIndex));
                            }
                            else
                            {
                                relinkFailedReport.Add("Binary Class Component[" + i + "] could not be remapped during porting: " + componentObjectIndex + " is not in the mapping tree");
                            }
                        }
                    }
                    offset += 4;
                }
            }
            return offset;
        }

        private int me3ExpIndexingToUnreal(int sourceObjReference, bool isImport = false)
        {
            if (sourceObjReference > 0)
            {
                sourceObjReference++; //make 1 based for mapping.
            }

            if (sourceObjReference < 0)
            {
                sourceObjReference--; //make 1 based for mapping.
            }

            //is 0: ???????
            if (sourceObjReference == 0)
            {
                if (isImport)
                {
                    sourceObjReference--;
                }
                else
                {
                    sourceObjReference++;
                }
            }

            return sourceObjReference;
        }

        private int ClassParser_ReadImplementsTable(IMEPackage importpcc, IExportEntry exp, List<string> relinkFailedReport, ref byte[] data, int offset)
        {
            if (importpcc.Game == MEGame.ME3)
            {
                int interfaceCount = BitConverter.ToInt32(data, offset);
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceIndex = BitConverter.ToInt32(data, offset);
                    int mapped;
                    bool isMapped = crossPCCObjectMap.TryGetValue(interfaceIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, interfaceIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        relinkFailedReport.Add("Binary Class Interface Index[" + i + "] could not be remapped during porting: " + interfaceIndex + " is not in the mapping tree");
                    }
                    offset += 4;

                    //propertypointer
                    interfaceIndex = BitConverter.ToInt32(data, offset);
                    isMapped = crossPCCObjectMap.TryGetValue(interfaceIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, interfaceIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        relinkFailedReport.Add("Binary Class Interface Index[" + i + "] could not be remapped during porting: " + interfaceIndex + " is not in the mapping tree");
                    }
                    offset += 4;
                }
            }
            else
            {
                int interfaceTableName = BitConverter.ToInt32(data, offset); //????
                NameReference importingName = importpcc.getNameEntry(interfaceTableName);
                int interfaceName = exp.FileRef.FindNameOrAdd(importingName);
                WriteMem(offset, data, BitConverter.GetBytes(interfaceName));
                offset += 8;

                int interfaceCount = BitConverter.ToInt32(data, offset);
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceNameIndex = BitConverter.ToInt32(data, offset);
                    importingName = importpcc.getNameEntry(interfaceNameIndex);
                    interfaceName = exp.FileRef.FindNameOrAdd(importingName);
                    WriteMem(offset, data, BitConverter.GetBytes(interfaceName));
                    offset += 8;
                }
            }
            return offset;
        }

        /// <summary>
        /// Adds an import from the importingPCC to the destinationPCC with the specified importFullName, or returns the existing one if it can be found. 
        /// This method will look at importingPCC's import upstream chain and check for the most downstream one's existence in destinationPCC, 
        /// including if none can be founc (in which case the entire upstream is copied). It will then create new imports to match the remaining 
        /// downstream ones and return the originally named import, however now located in destinationPCC.
        /// </summary>
        /// <param name="importFullName">GetFullPath() of an import from ImportingPCC</param>
        /// <param name="importingPCC">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <returns></returns>
        private ImportEntry getOrAddCrossImport(string importFullName, IMEPackage importingPCC, IMEPackage destinationPCC, int? forcedLinkIdx = null)
        {
            //This code is kind of ugly, sorry.

            //see if this import exists locally
            foreach (ImportEntry imp in destinationPCC.Imports)
            {
                if (imp.GetFullPath == importFullName)
                {
                    return imp;
                }
            }

            //Import doesn't exist, so we're gonna need to add it
            //But first we need to figure out what needs to be added upstream as links
            //Search upstream until we find something, or we can't get any more upstreams
            ImportEntry mostdownstreamimport = null;
            string[] importParts = importFullName.Split('.');

            if (!forcedLinkIdx.HasValue)
            {
                List<int> upstreamLinks = new List<int>(); //0 = top level, 1 = next level... n = what we wanted to import
                int upstreamCount = 1;

                ImportEntry upstreamImport = null;
                //get number of required upstream imports that do not yet exist
                while (upstreamCount < importParts.Count())
                {
                    string upstream = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                    foreach (ImportEntry imp in destinationPCC.Imports)
                    {
                        if (imp.GetFullPath == upstream)
                        {
                            upstreamImport = imp;
                            break;
                        }
                    }

                    if (upstreamImport != null)
                    {
                        //We found an upsteam import that already exists
                        break;
                    }
                    upstreamCount++;
                }

                IExportEntry donorUpstreamExport = null;
                if (upstreamImport == null)
                {
                    //We have to import the entire upstream chain
                    string fullobjectname = importParts[0];
                    ImportEntry donorTopLevelImport = null;
                    foreach (ImportEntry imp in importingPCC.Imports) //importing side info we will move to our dest pcc
                    {
                        if (imp.GetFullPath == fullobjectname)
                        {
                            donorTopLevelImport = imp;
                            break;
                        }
                    }

                    if (donorTopLevelImport == null)
                    {
                        //This is issue KinkoJiro had. It is aborting relinking at this step. Will need to find a way to
                        //work with exports as parents for imports which will block it.
                        //Update: This has been partially implemented.
                        Debug.WriteLine("No upstream import was found in the source file. It's probably an export: " + importFullName);
                        foreach (IExportEntry exp in destinationPCC.Exports) //importing side info we will move to our dest pcc
                        {
                            //Console.WriteLine(exp.GetFullPath);
                            if (exp.GetFullPath == fullobjectname)
                            {
                                // = imp;
                                //We will need to find a way to cross map this as this will block cross import mapping unless these exports already exist.
                                Debug.WriteLine("FOUND UPSTREAM, AS EXPORT!");
                                KFreonLib.Debugging.DebugOutput.StartDebugger("Package Editor Relinker");
                                KFreonLib.Debugging.DebugOutput.PrintLn("Error: Upstream item that is required is an export in the pcc to import from: " + fullobjectname);
                                donorUpstreamExport = exp;
                                upstreamCount--; //level 1 now from the top down
                                                 //Create new import with this as higher IDK
                                break;
                            }
                        }
                        if (donorUpstreamExport == null)
                        {
                            Debug.WriteLine("An error has occured. Could not find an upstream import or export for relinking: " + fullobjectname + " from " + Pcc.FileName);
                            return null;
                        }
                    }

                    if (donorUpstreamExport == null)
                    {
                        //Create new toplevel import and set that as the most downstream one. (top = bottom at this point)
                        int downstreamPackageFile = destinationPCC.FindNameOrAdd(Path.GetFileNameWithoutExtension(donorTopLevelImport.PackageFile));
                        int downstreamClassName = destinationPCC.FindNameOrAdd(donorTopLevelImport.ClassName);
                        int downstreamName = destinationPCC.FindNameOrAdd(fullobjectname);

                        mostdownstreamimport = new ImportEntry(destinationPCC);
                        // mostdownstreamimport.idxLink = downstreamLinkIdx; ??
                        mostdownstreamimport.idxClassName = downstreamClassName;
                        mostdownstreamimport.idxObjectName = downstreamName;
                        mostdownstreamimport.idxPackageFile = downstreamPackageFile;
                        destinationPCC.addImport(mostdownstreamimport); //Add new top level downstream import
                        upstreamImport = mostdownstreamimport;
                        upstreamCount--; //level 1 now from the top down
                                         //return null;
                    }
                }

                //Have an upstream import, now we need to add downstream imports.
                while (upstreamCount > 0)
                {
                    upstreamCount--;
                    string fullobjectname = String.Join(".", importParts, 0, importParts.Count() - upstreamCount);
                    ImportEntry donorImport = null;

                    //Get or create names for creating import and get upstream linkIdx
                    int downstreamName = destinationPCC.FindNameOrAdd(importParts[importParts.Count() - upstreamCount - 1]);
                    foreach (ImportEntry imp in importingPCC.Imports) //importing side info we will move to our dest pcc
                    {
                        if (imp.GetFullPath == fullobjectname)
                        {
                            donorImport = imp;
                            break;
                        }
                    }
                    if (donorImport == null)
                    {
                        throw new Exception("No suitable upstream import was found for porting - this may be an export in the source file that is referenced as a parent or dependency. You should import this object and its parents first. " + fullobjectname + "(as part of " + importFullName + ")");
                    }

                    int downstreamPackageFile = destinationPCC.FindNameOrAdd(Path.GetFileNameWithoutExtension(donorImport.PackageFile));
                    int downstreamClassName = destinationPCC.FindNameOrAdd(donorImport.ClassName);

                    mostdownstreamimport = new ImportEntry(destinationPCC);
                    mostdownstreamimport.idxLink = donorUpstreamExport == null ? upstreamImport.UIndex : donorUpstreamExport.UIndex;
                    mostdownstreamimport.idxClassName = downstreamClassName;
                    mostdownstreamimport.idxObjectName = downstreamName;
                    mostdownstreamimport.idxPackageFile = downstreamPackageFile;
                    destinationPCC.addImport(mostdownstreamimport);
                    upstreamImport = mostdownstreamimport;
                }
            }
            else
            {
                //get importing import
                ImportEntry importingImport = importingPCC.Imports.FirstOrDefault(x => x.GetFullPath == importFullName); //this shouldn't be null
                mostdownstreamimport = new ImportEntry(destinationPCC);
                mostdownstreamimport.idxLink = forcedLinkIdx.Value;
                mostdownstreamimport.idxClassName = destinationPCC.FindNameOrAdd(importingImport.ClassName);
                mostdownstreamimport.idxObjectName = destinationPCC.FindNameOrAdd(importingImport.ObjectName);
                mostdownstreamimport.idxPackageFile = destinationPCC.FindNameOrAdd(Path.GetFileNameWithoutExtension(importingImport.PackageFile));
                destinationPCC.addImport(mostdownstreamimport);
            }
            return mostdownstreamimport;
        }

        private void WriteMem(int pos, byte[] memory, byte[] dataToWrite)
        {
            for (int i = 0; i < dataToWrite.Length; i++)
                memory[pos + i] = dataToWrite[i];
        }
    }
}
