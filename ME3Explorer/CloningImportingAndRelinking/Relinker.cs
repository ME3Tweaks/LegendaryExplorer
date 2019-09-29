using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer.Debugging;
using ME3Explorer.Packages;
using ME3Explorer.Pathfinding_Editor;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Explorer
{
    public static class Relinker
    {
        /// <summary>
        /// Attempts to relink unreal property data and object pointers in binary when cross porting an export
        /// </summary>
        public static List<string> RelinkAll(IDictionary<IEntry, IEntry> crossPccObjectMap, IMEPackage importpcc, bool importExportDependencies = false)
        {
            List<string> results = RelinkProperties(crossPccObjectMap, importpcc, importExportDependencies);
            results.AddRange(RelinkBinaryObjects(crossPccObjectMap, importpcc, importExportDependencies));
            return results;
        }

        /// <summary>
        /// Attempts to relink unreal property data using propertycollection when cross porting an export
        /// </summary>
        public static List<string> RelinkProperties(IDictionary<IEntry, IEntry> crossPccObjectMap, IMEPackage importpcc, bool importExportDependencies = false)
        {
            var relinkResults = new List<string>();
            //relink each modified export

            //We must convert this to a list, as this list will be updated as imports are cross mapped during relinking.
            //This process speeds up same-relinks later.
            //This is a list because otherwise we would get a concurrent modification exception.
            //Since we only enumerate exports and append imports to this list we will not need to worry about recursive links
            //I am sure this won't come back to be a pain for me.
            var crossPCCObjectMappingList = new OrderedMultiValueDictionary<IEntry, IEntry>(crossPccObjectMap);
            //can't be a foreach since we might append things to the list
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < crossPCCObjectMappingList.Count; i++)
            {
                KeyValuePair<IEntry, IEntry> kvp = crossPCCObjectMappingList[i];
                if (kvp.Key is ExportEntry sourceExportInOriginalFile)
                {
                    ExportEntry Value = (ExportEntry)kvp.Value;
                    PropertyCollection transplantProps = sourceExportInOriginalFile.GetProperties();
                    Debug.WriteLine($"Relinking items in destination export: {sourceExportInOriginalFile.FullPath}");
                    relinkResults.AddRange(relinkPropertiesRecursive(importpcc, Value, transplantProps, crossPCCObjectMappingList, "", importExportDependencies));
                    Value.WriteProperties(transplantProps);
                }
            }

            crossPccObjectMap.Clear();
            crossPccObjectMap.AddRange(crossPCCObjectMappingList);
            return relinkResults;
        }

        /// <summary>
        /// Attempts to relink unreal binary data to object pointers if they are part of the clone tree.
        /// It's gonna be an ugly mess.
        /// </summary>
        /// <param name="crossPccObjectMap"></param>
        /// <param name="importpcc">PCC being imported from</param>
        public static List<string> RelinkBinaryObjects(IDictionary<IEntry, IEntry> crossPccObjectMap, IMEPackage importpcc, bool importExportDependencies = false)
        {
            var relinkFailedReport = new List<string>();
            var crossPCCObjectMappingList = new OrderedMultiValueDictionary<IEntry, IEntry>(crossPccObjectMap);
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
                        if (exp.Game != importpcc.Game && (exp.IsClass || exp.ClassName == "State" || exp.ClassName == "Function"))
                        {
                            relinkFailedReport.Add($"{exp.UIndex} {exp.FullPath} binary relinking failed. Cannot port {exp.ClassName} between games!");
                            continue;
                        }

                        if (ObjectBinary.From(exp) is ObjectBinary objBin)
                        {
                            List<(UIndex, string)> indices = objBin.GetUIndexes(exp.FileRef.Game);

                            foreach ((UIndex uIndex, string propName) in indices)
                            {
                                string result = relinkUIndex(importpcc, exp, ref uIndex.value, $"(Binary Property: {propName})", crossPCCObjectMappingList, "",
                                                             importExportDependencies);
                                if (result != null)
                                {
                                    relinkFailedReport.Add(result);
                                }
                            }

                            //UStruct is abstract baseclass for Class, State, and Function
                            if (objBin is UStruct uStructBinary && uStructBinary.ScriptBytes.Length > 0)
                            {
                                if (exp.Game == MEGame.ME3)
                                {
                                    (List<Token> tokens, _) = Bytecode.ParseBytecode(uStructBinary.ScriptBytes, sourceexp);
                                    foreach (Token token in tokens)
                                    {
                                        relinkFailedReport.AddRange(RelinkToken(token, uStructBinary.ScriptBytes, sourceexp, exp, crossPCCObjectMappingList,
                                                                                importExportDependencies));
                                    }
                                }
                                else
                                {
                                    relinkFailedReport.Add($"{exp.UIndex} {exp.FullPath} binary relinking failed. {exp.ClassName} contains script, " +
                                                           $"which cannot be relinked for {exp.Game}");
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
                                //todo: make a WwiseEvent ObjectBinary class
                                case "WwiseEvent":
                                {
                                    void relinkAtPosition(int binaryPosition, string propertyName)
                                    {
                                        int uIndex = BitConverter.ToInt32(binarydata, binaryPosition);
                                        string relinkResult = relinkUIndex(importpcc, exp, ref uIndex, propertyName,
                                                                           crossPCCObjectMappingList, "", importExportDependencies);
                                        if (relinkResult is null)
                                        {
                                            binarydata.OverwriteRange(binaryPosition, BitConverter.GetBytes(uIndex));
                                        }
                                        else
                                        {
                                            relinkFailedReport.Add(relinkResult);
                                        }
                                    }

                                    if (exp.FileRef.Game == MEGame.ME3)
                                    {
                                        int count = BitConverter.ToInt32(binarydata, 0);
                                        for (int i = 0; i < count; i++)
                                        {
                                            relinkAtPosition(4 + (i * 4), $"(Binary Property: WwiseStreams[{i}])");
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
                                                relinkAtPosition(parsingPos, $"(Binary Property: link[{i}].WwiseBanks[{j}])");

                                                parsingPos += 4;
                                            }

                                            int wwisestreamcount = BitConverter.ToInt32(binarydata, parsingPos);
                                            parsingPos += 4;
                                            for (int j = 0; j < wwisestreamcount; j++)
                                            {
                                                relinkAtPosition(parsingPos, $"(Binary Property: link[{i}].WwiseStreams[{j}])");

                                                parsingPos += 4;
                                            }
                                        }

                                        exp.setBinaryData(binarydata);
                                    }
                                }
                                    break;
                                default:
                                    continue;
                            }
                        }
                    }
                    catch (Exception e) when (!App.IsDebug)
                    {
                        relinkFailedReport.Add($"{exp.UIndex} {exp.FullPath} binary relinking failed due to exception: {e.Message}");
                    }
                }
            }

            crossPccObjectMap.Clear();
            crossPccObjectMap.AddRange(crossPCCObjectMappingList);
            return relinkFailedReport;
        }

        private static List<string> relinkPropertiesRecursive(IMEPackage importingPCC, ExportEntry relinkingExport, PropertyCollection transplantProps,
                                                              OrderedMultiValueDictionary<IEntry, IEntry> crossPCCObjectMappingList, string debugPrefix,
                                                              bool importExportDependencies = false)
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
                        string result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, objProperty.Name, crossPCCObjectMappingList, debugPrefix, importExportDependencies);
                        objProperty.Value = uIndex;
                        if (result != null)
                        {
                            relinkResults.Add(result);
                        }
                    }
                }
                else if (prop is ObjectProperty objectProperty)
                {
                    int uIndex = objectProperty.Value;
                    string result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, objectProperty.Name, crossPCCObjectMappingList, debugPrefix, importExportDependencies);
                    objectProperty.Value = uIndex;
                    if (result != null)
                    {
                        relinkResults.Add(result);
                    }
                }
                else if (prop is DelegateProperty delegateProp)
                {
                    int uIndex = delegateProp.Value.Object;
                    string result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, delegateProp.Name, crossPCCObjectMappingList, debugPrefix, importExportDependencies);
                    delegateProp.Value = new ScriptDelegate(uIndex, delegateProp.Value.FunctionName);
                    if (result != null)
                    {
                        relinkResults.Add(result);
                    }
                }
            }
            return relinkResults;
        }

        private static string relinkUIndex(IMEPackage importingPCC, ExportEntry relinkingExport, ref int uIndex, string propertyName,
                                           OrderedMultiValueDictionary<IEntry, IEntry> crossPCCObjectMappingList, string debugPrefix, bool importExportDependencies = false)
        {
            if (uIndex == 0)
            {
                return null; //do not relink 0
            }

            IMEPackage destinationPcc = relinkingExport.FileRef;
            if (importingPCC == destinationPcc && uIndex < 0)
            {
                return null; //do not relink same-pcc imports.
            }
            int sourceObjReference = uIndex;

            Debug.WriteLine($"{debugPrefix} Relinking:{propertyName}");

            if (crossPCCObjectMappingList.TryGetValue(entry => entry.UIndex == sourceObjReference, out IEntry targetEntry))
            {
                //relink
                uIndex = targetEntry.UIndex;

                Debug.WriteLine($"{debugPrefix} Relink hit: {sourceObjReference}{propertyName} : {targetEntry.FullPath}");
            }
            else if (uIndex < 0) //It's an unmapped import
            {
                //objProperty is currently pointing to importingPCC as that is where we read the properties from
                int n = uIndex;
                int origvalue = n;
                //Debug.WriteLine("Relink miss, attempting JIT relink on " + n + " " + rootNode.Text);
                if (importingPCC.IsImport(n))
                {
                    //Get the original import
                    ImportEntry origImport = importingPCC.GetImport(n);
                    string origImportFullName = origImport.FullPath;
                    //Debug.WriteLine("We should import " + origImport.GetFullPath);

                    IEntry crossImport = null;
                    string linkFailedDueToError = null;
                    try
                    {
                        crossImport = EntryImporter.GetOrAddCrossImportOrPackage(origImportFullName, importingPCC, destinationPcc);
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
                        Debug.WriteLine($"Relink hit: Dynamic CrossImport for {origvalue} {importingPCC.GetEntry(origvalue).FullPath} -> {uIndex}");

                    }
                    else
                    {
                        string path = importingPCC.GetEntry(uIndex) != null ? importingPCC.GetEntry(uIndex).FullPath : "Entry not found: " + uIndex;
                        if (linkFailedDueToError != null)
                        {
                            Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.GetEntry(origvalue).FullPath}");
                            return $"Relink failed for {propertyName} {uIndex} in export {path}({relinkingExport.UIndex}): {linkFailedDueToError}";
                        }

                        if (destinationPcc.GetEntry(uIndex) != null)
                        {
                            Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.GetEntry(origvalue).FullPath}");
                            return $"Relink failed: CrossImport porting failed for {propertyName} {uIndex} {destinationPcc.GetEntry(uIndex).FullPath} in export {relinkingExport.FullPath}({relinkingExport.UIndex})";
                        }

                        return $"Relink failed: New export does not exist - this is probably a bug in cross import code for {propertyName} {uIndex} in export {relinkingExport.FullPath}({relinkingExport.UIndex})";
                    }
                }
            }
            else
            {
                //It's an export
                //Attempt lookup
                ExportEntry sourceExport = importingPCC.GetUExport(uIndex);
                string fullPath = sourceExport.FullPath;
                int indexValue = sourceExport.indexValue;
                var existingExport = destinationPcc.Exports.FirstOrDefault(x => x.FullPath == fullPath && indexValue == x.indexValue);
                if (existingExport != null)
                {
                    Debug.WriteLine($"Relink hit [EXPERIMENTAL]: Existing export in file was found, linking to it:  " +
                                    $"{uIndex} {sourceExport.InstancedFullPath} -> {existingExport.InstancedFullPath}");
                    uIndex = existingExport.UIndex;

                }
                else if (importExportDependencies)
                {
                    ExportEntry importedExport = EntryImporter.ImportExport(destinationPcc, sourceExport,
                                                                            EntryImporter.GetOrAddCrossImportOrPackage(sourceExport.ParentFullPath, importingPCC,
                                                                                                                       destinationPcc)?.UIndex ?? 0, true);
                    crossPCCObjectMappingList[sourceExport] = importedExport;
                    uIndex = importedExport.UIndex;
                }
                else
                {
                    string path = importingPCC.GetEntry(uIndex)?.FullPath ?? $"Entry not found: {uIndex}";
                    Debug.WriteLine($"Relink failed in {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} {uIndex} {path}");
                    return $"Relink failed: {propertyName} {uIndex} in export {relinkingExport.FullPath}({relinkingExport.UIndex})";
                }
            }

            return null;
        }

        private static List<string> RelinkFunction(ExportEntry sourceExport, ExportEntry destinationExport,
                                                            OrderedMultiValueDictionary<IEntry, IEntry> crossFileRefObjectMap, bool importExportDependencies = false)
        {
            var relinkFailedReport = new List<string>();
            //Copy function bytes
            byte[] binaryData = sourceExport.getBinaryData();
            byte[] script = binaryData.Slice(0x14, binaryData.Length - 0x14);

            //Perform relink
            (List<Token> topLevelTokens, _) = Bytecode.ParseBytecode(script, sourceExport);
            foreach (Token t in topLevelTokens)
            {
                relinkFailedReport.AddRange(RelinkToken(t, script, sourceExport, destinationExport, crossFileRefObjectMap, importExportDependencies));
            }

            binaryData.OverwriteRange(0x14, script);

            relinkAtPosition(0, "(Binary Property: SuperClass)");
            relinkAtPosition(4, "(Binary Property: NextItemInLoadingChain)");
            relinkAtPosition(8, "(Binary Property: ChildListStart)");

            destinationExport.setBinaryData(binaryData);
            return relinkFailedReport;

            void relinkAtPosition(int binaryPosition, string propertyName)
            {
                int uIndex = BitConverter.ToInt32(binaryData, binaryPosition);
                string relinkResult = relinkUIndex(sourceExport.FileRef, destinationExport, ref uIndex, propertyName,
                                                   crossFileRefObjectMap, "", importExportDependencies);
                if (relinkResult is null)
                {
                    binaryData.OverwriteRange(binaryPosition, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    relinkFailedReport.Add(relinkResult);
                }
            }
        }

        private static List<string> RelinkToken(Token t, byte[] script, ExportEntry sourceExport, ExportEntry destinationExport,
                                                OrderedMultiValueDictionary<IEntry, IEntry> crossFileRefObjectMap, bool importExportDependencies = false)
        {
            var relinkFailedReport = new List<string>();
            Debug.WriteLine($"Attempting function relink on token at position {t.pos}. Number of listed relinkable items {t.inPackageReferences.Count}");

            foreach ((int pos, int type, int value) in t.inPackageReferences)
            {
                switch (type)
                {
                    case Token.INPACKAGEREFTYPE_NAME:
                        int newValue = destinationExport.FileRef.FindNameOrAdd(sourceExport.FileRef.GetNameEntry(value));
                        Debug.WriteLine($"Function relink hit @ 0x{t.pos + pos:X6}, cross ported a name: {sourceExport.FileRef.GetNameEntry(value)}");
                        script.OverwriteRange(pos, BitConverter.GetBytes(newValue));
                        break;
                    case Token.INPACKAGEREFTYPE_ENTRY:
                        relinkAtPosition(pos, value, $"(Script at @ 0x{t.pos + pos:X6}: {t.text})");
                        break;
                }
            }

            return relinkFailedReport;

            void relinkAtPosition(int binaryPosition, int uIndex, string propertyName)
            {
                string relinkResult = relinkUIndex(sourceExport.FileRef, destinationExport, ref uIndex, propertyName,
                                                   crossFileRefObjectMap, "", importExportDependencies);
                if (relinkResult is null)
                {
                    script.OverwriteRange(binaryPosition, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    relinkFailedReport.Add(relinkResult);
                }
            }
        }
    }
}