using ME3Explorer.Debugging;
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
using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Explorer
{
    public partial class PackageEditorWPF
    {
        /// <summary>
        /// Attempts to relink unreal property data using propertycollection when cross porting an export
        /// </summary>
        private List<string> relinkObjects2(IMEPackage importpcc)
        {
            var relinkResults = new List<string>();
            //relink each modified export

            //We must convert this to a list, as this list will be updated as imports are cross mapped during relinking.
            //This process speeds up same-relinks later.
            //This is a list because otherwise we would get a concurrent modification exception.
            //Since we only enumerate exports and append imports to this list we will not need to worry about recursive links
            //I am sure this won't come back to be a pain for me.
            var crossPCCObjectMappingList = new OrderedMultiValueDictionary<IEntry, IEntry>(crossPCCObjectMap);
            //can't be a foreach since we might append things to the list
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < crossPCCObjectMappingList.Count; i++)
            {
                KeyValuePair<IEntry, IEntry> kvp = crossPCCObjectMappingList[i];
                if (kvp.Key is ExportEntry sourceExportInOriginalFile)
                {
                    ExportEntry Value = (ExportEntry)kvp.Value;
                    PropertyCollection transplantProps = sourceExportInOriginalFile.GetProperties();
                    Debug.WriteLine($"Relinking items in destination export: {sourceExportInOriginalFile.GetFullPath}");
                    relinkResults.AddRange(relinkPropertiesRecursive(importpcc, Value, transplantProps, crossPCCObjectMappingList, ""));
                    Value.WriteProperties(transplantProps);
                }
            }

            crossPCCObjectMap.Clear();
            crossPCCObjectMap.AddRange(crossPCCObjectMappingList);
            return relinkResults;
        }

        private List<string> relinkPropertiesRecursive(IMEPackage importingPCC, ExportEntry relinkingExport, PropertyCollection transplantProps, OrderedMultiValueDictionary<IEntry, IEntry> crossPCCObjectMappingList, string debugPrefix)
        {
            var relinkResults = new List<string>();
            foreach (UProperty prop in transplantProps)
            {
                Debug.WriteLine($"{debugPrefix} Relink recursive on {prop.Name}");
                if (prop is StructProperty structProperty)
                {
                    relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, relinkingExport, structProperty.Properties, crossPCCObjectMappingList, debugPrefix + "-"));
                }
                else if (prop is ArrayProperty<StructProperty> structArrayProp)
                {
                    foreach (StructProperty arrayStructProperty in structArrayProp)
                    {
                        relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, relinkingExport, arrayStructProperty.Properties, crossPCCObjectMappingList, debugPrefix + "-"));
                    }
                }
                else if (prop is ArrayProperty<ObjectProperty> objArrayProp)
                {
                    foreach (ObjectProperty objProperty in objArrayProp)
                    {
                        int uIndex = objProperty.Value;
                        string result = relinkObjectProperty(importingPCC, relinkingExport, ref uIndex, objProperty.Name, crossPCCObjectMappingList, debugPrefix);
                        objProperty.Value = uIndex;
                        if (result != null)
                        {
                            relinkResults.Add(result);
                        }
                    }
                }
                else if (prop is ObjectProperty objectProperty)
                {
                    //relink
                    int uIndex = objectProperty.Value;
                    string result = relinkObjectProperty(importingPCC, relinkingExport, ref uIndex, objectProperty.Name, crossPCCObjectMappingList, debugPrefix);
                    objectProperty.Value = uIndex;
                    if (result != null)
                    {
                        relinkResults.Add(result);
                    }
                }
            }
            return relinkResults;
        }

        private string relinkObjectProperty(IMEPackage importingPCC, ExportEntry relinkingExport, ref int uIndex, string propertyName, OrderedMultiValueDictionary<IEntry, IEntry> crossPCCObjectMappingList, string debugPrefix)
        {
            if (uIndex == 0)
            {
                return null; //do not relink 0
            }
            if (importingPCC == relinkingExport.FileRef && uIndex < 0)
            {
                return null; //do not relink same-pcc imports.
            }
            int sourceObjReference = uIndex;

            Debug.WriteLine($"{debugPrefix} Relinking:{propertyName}");

            if (crossPCCObjectMappingList.TryGetValue(entry => entry.UIndex == sourceObjReference, out IEntry targetEntry))
            {
                //relink
                uIndex = targetEntry.UIndex;

                Debug.WriteLine($"{debugPrefix} Relink hit: {sourceObjReference}{propertyName} : {targetEntry.GetFullPath}");
            }
            else if (uIndex < 0) //It's an unmapped import
            {
                //objProperty is currently pointing to importingPCC as that is where we read the properties from
                int n = uIndex;
                int origvalue = n;
                //Debug.WriteLine("Relink miss, attempting JIT relink on " + n + " " + rootNode.Text);
                if (importingPCC.isImport(n))
                {
                    //Get the original import
                    ImportEntry origImport = importingPCC.getImport(n);
                    string origImportFullName = origImport.GetFullPath;
                    //Debug.WriteLine("We should import " + origImport.GetFullPath);

                    IEntry crossImport = null;
                    string linkFailedDueToError = null;
                    try
                    {
                        crossImport = getOrAddCrossImportOrPackage(origImportFullName, importingPCC, relinkingExport.FileRef);
                    }
                    catch (Exception e)
                    {
                        //Error during relink
                        DebugOutput.StartDebugger("PCC Relinker");
                        DebugOutput.PrintLn("Exception occured during relink: ");
                        DebugOutput.PrintLn(ExceptionHandlerDialogWPF.FlattenException(e));
                        DebugOutput.PrintLn("You may want to consider discarding this sessions' changes as relinking was not able to properly finish.");
                        linkFailedDueToError = e.Message;
                    }

                    if (crossImport != null)
                    {
                        crossPCCObjectMappingList.Add(origImport, crossImport); //add to mapping to speed up future relinks
                        uIndex = crossImport.UIndex;
                        Debug.WriteLine($"Relink hit: Dynamic CrossImport for {origvalue} {importingPCC.getEntry(origvalue).GetFullPath} -> {uIndex}");

                    }
                    else
                    {
                        string path = importingPCC.getEntry(uIndex) != null ? importingPCC.getEntry(uIndex).GetFullPath : "Entry not found: " + uIndex;

                        if (linkFailedDueToError != null)
                        {
                            Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.getEntry(origvalue).GetFullPath}");
                            return $"Relink failed for {propertyName} {uIndex} in export {path}({relinkingExport.UIndex}): {linkFailedDueToError}";
                        }

                        if (relinkingExport.FileRef.getEntry(uIndex) != null)
                        {
                            Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.getEntry(origvalue).GetFullPath}");
                            return $"Relink failed: CrossImport porting failed for {propertyName} {uIndex} {relinkingExport.FileRef.getEntry(uIndex).GetFullPath} in export {relinkingExport.GetFullPath}({relinkingExport.UIndex})";
                        }

                        return $"Relink failed: New export does not exist - this is probably a bug in cross import code for {propertyName} {uIndex} in export {relinkingExport.GetFullPath}({relinkingExport.UIndex})";
                    }
                }
            }
            else
            {
                //It's an export
                //Attempt lookup
                string indexedFullPath = importingPCC.getUExport(uIndex).GetIndexedFullPath;
                var existingExport = relinkingExport.FileRef.Exports.FirstOrDefault(x => x.GetIndexedFullPath == indexedFullPath);
                if (existingExport != null)
                {
                    Debug.WriteLine($"Relink hit [EXPERIMENTAL]: Existing export in file was found, linking to it:  {uIndex} {indexedFullPath} -> {existingExport.GetIndexedFullPath}");
                    uIndex = existingExport.UIndex;

                }
                else
                {
                    string path = importingPCC.getEntry(uIndex) != null ? importingPCC.getEntry(uIndex).GetFullPath : $"Entry not found: {uIndex}";
                    Debug.WriteLine($"Relink failed in {relinkingExport.ObjectName} {relinkingExport.UIndex}: {propertyName} {uIndex} {path}");
                    return $"Relink failed: {propertyName} {uIndex} in export {relinkingExport.GetFullPath}({relinkingExport.UIndex})";
                }
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
            var relinkFailedReport = new List<string>();
            var crossPCCObjectMappingList = new OrderedMultiValueDictionary<IEntry, IEntry>(crossPCCObjectMap);
            //can't be a foreach since we might append things to the list
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int index = 0; index < crossPCCObjectMappingList.Count; index++)
            {
                KeyValuePair<IEntry, IEntry> mapping = crossPCCObjectMappingList[index];
                if (mapping.Key is ExportEntry sourceexp)
                {
                    ExportEntry exp = (ExportEntry)mapping.Value;
                    try
                    {
                        if (ObjectBinary.From(exp) is ObjectBinary objBin)
                        {
                            List<(UIndex, string)> indices = objBin.GetUIndexes(exp.FileRef.Game);

                            foreach ((UIndex uIndex, string propName) in indices)
                            {
                                string result = relinkObjectProperty(importpcc, exp, ref uIndex.value, $"(Binary Property: {propName})", crossPCCObjectMappingList, "");
                                if (result != null)
                                {
                                    relinkFailedReport.Add(result);
                                }
                            }

                            exp.setBinaryData(objBin.ToBytes(exp.FileRef));
                            continue;
                        }

                        byte[] binarydata = exp.getBinaryData();
                        if (binarydata.Length > 0)
                        {
                            switch (exp.ClassName)
                            {
                                case "WwiseEvent":
                                {
                                    if (exp.FileRef.Game == MEGame.ME3)
                                    {
                                        int count = BitConverter.ToInt32(binarydata, 0);
                                        for (int i = 0; i < count; i++)
                                        {
                                            int originalValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));

                                            //This might throw an exception if it was invalid in the original file...
                                            bool isMapped = crossPCCObjectMappingList.TryGetValue(importpcc.getEntry(originalValue), out IEntry mappedValueInThisPackage);
                                            if (isMapped)
                                            {
                                                Debug.WriteLine("Binary relink hit for ME3 WwiseEvent Export " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + originalValue + " -> " + (mappedValueInThisPackage.UIndex));
                                                WriteMem(4 + (i * 4), binarydata, BitConverter.GetBytes(mappedValueInThisPackage.UIndex));
                                                int newValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                                Debug.WriteLine(originalValue + " -> " + newValue);
                                            }
                                            else
                                            {
                                                Debug.WriteLine("Binary relink missed ME3 WwiseEvent Export " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + originalValue);
                                                relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: WwiseEvent referenced WwiseStream " + originalValue + " is not in the mapping tree and could not be relinked");
                                            }
                                        }

                                        exp.setBinaryData(binarydata);
                                    }
                                    else if (exp.FileRef.Game == MEGame.ME2)
                                    {
                                        int parsingPos = 4;
                                        int linkCount = BitConverter.ToInt32(binarydata, parsingPos);
                                        parsingPos += 4;
                                        for (int i = 0; i < linkCount; i++)
                                        {
                                            int bankcount = BitConverter.ToInt32(binarydata, parsingPos);
                                            parsingPos += 4;
                                            for (int j = 0; j < bankcount; j++)
                                            {
                                                int bankRef = BitConverter.ToInt32(binarydata, parsingPos);
                                                bool isMapped = crossPCCObjectMappingList.TryGetValue(importpcc.getEntry(bankRef), out IEntry mappedValueInThisPackage);
                                                if (isMapped)
                                                {
                                                    Debug.WriteLine("Binary relink hit for ME2 WwiseEvent Bank Entry " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + bankRef + " -> " + (mappedValueInThisPackage.UIndex));
                                                    WriteMem(parsingPos, binarydata, BitConverter.GetBytes(mappedValueInThisPackage.UIndex));
                                                    int newValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                                    Debug.WriteLine(bankRef + " -> " + newValue);
                                                }
                                                else
                                                {
                                                    Debug.WriteLine("Binary relink missed ME2 WwiseEvent Bank Entry " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + bankRef);
                                                    relinkFailedReport.Add(exp.UIndex + " " + exp.GetInstancedFullPath + " binary relink error: ME2 WwiseEvent referenced WwiseBank " + bankRef + " is not in the mapping tree and could not be relinked");
                                                }

                                                parsingPos += 4;
                                            }

                                            int wwisestreamcount = BitConverter.ToInt32(binarydata, parsingPos);
                                            parsingPos += 4;
                                            for (int j = 0; j < wwisestreamcount; j++)
                                            {
                                                int wwiseStreamRef = BitConverter.ToInt32(binarydata, parsingPos);
                                                bool isMapped = crossPCCObjectMappingList.TryGetValue(importpcc.getEntry(wwiseStreamRef), out IEntry mappedValueInThisPackage);
                                                if (isMapped)
                                                {
                                                    Debug.WriteLine("Binary relink hit for ME2 WwiseEvent WwiseStream Entry " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + wwiseStreamRef + " -> " + (mappedValueInThisPackage.UIndex));
                                                    WriteMem(parsingPos, binarydata, BitConverter.GetBytes(mappedValueInThisPackage.UIndex));
                                                    int newValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                                    Debug.WriteLine(wwiseStreamRef + " -> " + newValue);
                                                }
                                                else
                                                {
                                                    Debug.WriteLine("Binary relink missed ME2 WwiseEvent Bank Entry " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + wwiseStreamRef);
                                                    relinkFailedReport.Add(exp.UIndex + " " + exp.GetInstancedFullPath + " binary relink error: ME2 WwiseEvent referenced WwiseStream " + wwiseStreamRef + " is not in the mapping tree and could not be relinked");
                                                }

                                                parsingPos += 4;
                                            }
                                        }

                                        exp.setBinaryData(binarydata);
                                    }
                                }
                                    break;
                                case "Class":
                                {
                                    if (exp.FileRef.Game != importpcc.Game)
                                    {
                                        //Cannot relink against a different game.
                                        continue;
                                    }

                                    ExportEntry importingExp = sourceexp;
                                    if (importingExp.ClassName != "Class")
                                    {
                                        continue; //the class was not actually set, so this is not really class.
                                    }

                                    //This is going to be pretty ugly
                                    try
                                    {
                                        byte[] newdata = sourceexp.Data; //may need to rewrite first unreal header
                                        byte[] data = sourceexp.Data;

                                        int offset = 0;
                                        int unrealExportIndex = BitConverter.ToInt32(data, offset);
                                        offset += 4;


                                        int superclassIndex = BitConverter.ToInt32(data, offset);
                                        if (superclassIndex < 0)
                                        {
                                            //its an import
                                            ImportEntry superclassImportEntry = importpcc.getImport(superclassIndex);
                                            IEntry newSuperclassValue = getOrAddCrossImportOrPackage(superclassImportEntry.GetFullPath, importpcc, exp.FileRef);
                                            WriteMem(offset, newdata, BitConverter.GetBytes(newSuperclassValue.UIndex));
                                        }
                                        else
                                        {
                                            relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: Superclass is an export in the source package and was not relinked.");
                                        }

                                        offset += 4;
                                        int unknown1 = BitConverter.ToInt32(data, offset);

                                        offset += 4;
                                        int childProbeUIndex = BitConverter.ToInt32(data, offset);
                                        if (childProbeUIndex != 0)
                                        { //Scoped
                                            if (crossPCCObjectMappingList.TryGetValue(importpcc.getEntry(childProbeUIndex), out IEntry mapped))
                                            {
                                                WriteMem(offset, newdata, BitConverter.GetBytes(mapped.UIndex));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: Child Probe UIndex could not be remapped during porting: " + childProbeUIndex + " is not in the mapping tree and could not be relinked");
                                            }
                                        }

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

                                        offset += skipAmount + 10; //heuristic to find end of script (ends with 10 FF's)
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
                                            WriteMem(offset, newdata, BitConverter.GetBytes(newFuncName)); //Need to convert to SirC way of doing it
                                            offset += 8;

                                            int functionObjectIndex = BitConverter.ToInt32(data, offset);

                                            //TODO: Add lookup
                                            if (crossPCCObjectMappingList.TryGetValue(importpcc.getEntry(functionObjectIndex), out IEntry mapped))
                                            {
                                                WriteMem(offset, newdata, BitConverter.GetBytes(mapped.UIndex));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: Local function[" + i + "] could not be remapped during porting: " + functionObjectIndex + " is not in the mapping tree and could not be relinked");
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
                                            IEntry outerclassNewImport = getOrAddCrossImportOrPackage(outerclassReferenceImport.GetFullPath, importpcc, exp.FileRef);
                                            WriteMem(offset, newdata, BitConverter.GetBytes(outerclassNewImport.UIndex));
                                        }
                                        else
                                        {
                                            relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: Outerclass is an export in the original package, not relinked.");
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

                                        int defaultsClassLink = BitConverter.ToInt32(data, offset);

                                        isMapped = crossPCCObjectMappingList.TryGetValue(importpcc.getEntry(defaultsClassLink), out IEntry defClassLink);
                                        if (isMapped)
                                        {
                                            WriteMem(offset, newdata, BitConverter.GetBytes(defClassLink.UIndex));
                                        }
                                        else
                                        {
                                            relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: DefaultsClassLink cannot be currently automatically relinked by Binary Relinker. Please manually set this in Binary Editor");
                                        }

                                        offset += 4;

                                        if (importpcc.Game == MEGame.ME3)
                                        {
                                            int functionsTableCount = BitConverter.ToInt32(data, offset);
                                            offset += 4;

                                            for (int i = 0; i < functionsTableCount; i++)
                                            {
                                                int functionsTableIndex = BitConverter.ToInt32(data, offset);
                                                if (crossPCCObjectMappingList.TryGetValue(importpcc.getEntry(functionsTableIndex), out IEntry mapped))
                                                {
                                                    WriteMem(offset, newdata, BitConverter.GetBytes(mapped.UIndex));
                                                }
                                                else
                                                {
                                                    if (functionsTableIndex < 0)
                                                    {
                                                        ImportEntry functionObjIndex = importpcc.getImport(functionsTableIndex);
                                                        IEntry newFunctionObjIndex = getOrAddCrossImportOrPackage(functionObjIndex.GetFullPath, importpcc, exp.FileRef);
                                                        WriteMem(offset, newdata, BitConverter.GetBytes(newFunctionObjIndex.UIndex));
                                                    }
                                                    else
                                                    {
                                                        relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: Full Functions List function[" + i + "] could not be remapped during porting: " + functionsTableIndex + " is not in the mapping tree and could not be relinked");
                                                    }
                                                }

                                                offset += 4;
                                            }
                                        }

                                        exp.Data = newdata;
                                    }
                                    catch (Exception ex)
                                    {
                                        relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: Exception relinking: " + ex.Message);
                                    }
                                }
                                    break;
                                case "Function":
                                    //Crazy experimental
                                {
                                    //Oh god
                                    Bytecode.RelinkFunctionForPorting(sourceexp, exp, relinkFailedReport, crossPCCObjectMappingList);
                                }
                                    break;
                                default:
                                    continue;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        relinkFailedReport.Add($"{exp.UIndex} {exp.GetFullPath} binary relinking failed due to exception: {e.Message}");
                    }
                }
            }

            crossPCCObjectMap.Clear();
            crossPCCObjectMap.AddRange(crossPCCObjectMappingList);
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

        private int ClassParser_RelinkComponentsTable(IMEPackage importpcc, ExportEntry exp, List<string> relinkFailedReport, ref byte[] data, int offset)
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

                    //TODO: Add lookup
                    if (crossPCCObjectMap.TryGetValue(importpcc.getEntry(componentObjectIndex), out IEntry mapped))
                    {
                        WriteMem(offset, data, BitConverter.GetBytes(mapped.UIndex));
                    }
                    else
                    {
                        if (componentObjectIndex < 0)
                        {
                            ImportEntry componentObjectImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(componentObjectIndex))];
                            IEntry newComponentObjectImport = getOrAddCrossImportOrPackage(componentObjectImport.GetFullPath, importpcc, exp.FileRef);
                            WriteMem(offset, data, BitConverter.GetBytes(newComponentObjectImport.UIndex));
                        }
                        else if (componentObjectIndex > 0) //we do not remap on 0 here in binary land
                        {
                            relinkFailedReport.Add(exp.UIndex + " " + exp.GetFullPath + " binary relink error: Component[" + i + "] could not be remapped during porting: " + componentObjectIndex + " is not in the mapping tree");
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
                        if (crossPCCObjectMap.TryGetValue(importpcc.getEntry(componentObjectIndex), out IEntry mapped))
                        {
                            WriteMem(offset, data, BitConverter.GetBytes(mapped.UIndex));
                        }
                        else
                        {
                            if (componentObjectIndex < 0)
                            {
                                ImportEntry componentObjectImport = importpcc.getImport(componentObjectIndex);
                                IEntry newComponentObjectImport = getOrAddCrossImportOrPackage(componentObjectImport.GetFullPath, importpcc, exp.FileRef);
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

        private int ClassParser_ReadImplementsTable(IMEPackage importpcc, ExportEntry exp, List<string> relinkFailedReport, ref byte[] data, int offset)
        {
            if (importpcc.Game == MEGame.ME3)
            {
                int interfaceCount = BitConverter.ToInt32(data, offset);
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    { //scoped
                        int interfaceIndex = BitConverter.ToInt32(data, offset);
                        if (crossPCCObjectMap.TryGetValue(importpcc.getEntry(interfaceIndex), out IEntry mapped))
                        {
                            WriteMem(offset, data, BitConverter.GetBytes(mapped.UIndex));
                        }
                        else
                        {
                            relinkFailedReport.Add("Binary Class Interface Index[" + i +
                                                   "] could not be remapped during porting: " + interfaceIndex +
                                                   " is not in the mapping tree");
                        }
                    }

                    offset += 4;

                    //propertypointer
                    {
                        int propertyPointerIndex = BitConverter.ToInt32(data, offset);

                        if (crossPCCObjectMap.TryGetValue(importpcc.getEntry(propertyPointerIndex), out IEntry mapped))
                        {
                            WriteMem(offset, data, BitConverter.GetBytes(mapped.UIndex));
                        }
                        else
                        {
                            relinkFailedReport.Add("Binary Class Interface Index[" + i +
                                                   "] could not be remapped during porting: " + propertyPointerIndex +
                                                   " is not in the mapping tree");
                        }
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
        /// This will add parent imports and packages as neccesary
        /// </summary>
        /// <param name="importFullName">GetFullPath() of an import from ImportingPCC</param>
        /// <param name="importingPCC">PCC to import imports from</param>
        /// <param name="destinationPCC">PCC to add imports to</param>
        /// <param name="forcedLink">force this as parent</param>
        /// <returns></returns>
        public static IEntry getOrAddCrossImportOrPackage(string importFullName, IMEPackage importingPCC, IMEPackage destinationPCC, int? forcedLink = null)
        {
            if (string.IsNullOrEmpty(importFullName))
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
            IEntry parent = getOrAddCrossImportOrPackage(string.Join(".", importParts.Take(importParts.Length - 1)), importingPCC, destinationPCC);


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
                    importExport(destinationPCC, exp, parent?.UIndex ?? 0, out ExportEntry package);
                    return package;
                }
            }

            throw new Exception($"Unable to add {importFullName} to file! Could not find it!");
        }

        private void WriteMem(int pos, byte[] memory, byte[] dataToWrite)
        {
            for (int i = 0; i < dataToWrite.Length; i++)
                memory[pos + i] = dataToWrite[i];
        }
    }
}
