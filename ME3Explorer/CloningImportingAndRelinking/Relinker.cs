using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer.ME1.Unreal.UnhoodBytecode;
using ME3Explorer.Packages;
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
        public static List<ListDialog.EntryItem> RelinkAll(IDictionary<IEntry, IEntry> crossPccObjectMap, bool importExportDependencies = false)
        {
            var relinkReport = new List<ListDialog.EntryItem>();
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
                (IEntry src, IEntry dest) = crossPCCObjectMappingList[i];
                if (src is ExportEntry sourceExport && dest is ExportEntry relinkingExport)
                {
                    relinkReport.AddRange(Relink(sourceExport, relinkingExport, crossPCCObjectMappingList, importExportDependencies));
                }
            }

            crossPccObjectMap.Clear();
            crossPccObjectMap.AddRange(crossPCCObjectMappingList);
            return relinkReport;
        }

        public static List<ListDialog.EntryItem> Relink(ExportEntry sourceExport, ExportEntry relinkingExport, OrderedMultiValueDictionary<IEntry, IEntry> crossPCCObjectMappingList, bool importExportDependencies = false)
        {
            var relinkFailedReport = new List<ListDialog.EntryItem>();
            IMEPackage sourcePcc = sourceExport.FileRef;
            //Relink stack
            if (relinkingExport.HasStack)
            {
                byte[] stack = relinkingExport.GetStack();

                int uIndex = BitConverter.ToInt32(stack, 0);
                var relinkResult = relinkUIndex(sourceExport.FileRef, relinkingExport, ref uIndex, "Stack: Node",
                                                   crossPCCObjectMappingList, "", importExportDependencies);
                if (relinkResult is null)
                {
                    stack.OverwriteRange(0, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    relinkFailedReport.Add(relinkResult);
                }

                uIndex = BitConverter.ToInt32(stack, 4);
                relinkResult = relinkUIndex(sourceExport.FileRef, relinkingExport, ref uIndex, "Stack: StateNode",
                                            crossPCCObjectMappingList, "", importExportDependencies);
                if (relinkResult is null)
                {
                    stack.OverwriteRange(4, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    relinkFailedReport.Add(relinkResult);
                }

                relinkingExport.SetStack(stack);
            }

            //Relink Properties
            PropertyCollection transplantProps = sourceExport.GetProperties();
            relinkFailedReport.AddRange(relinkPropertiesRecursive(sourcePcc, relinkingExport, transplantProps, crossPCCObjectMappingList, "", importExportDependencies));
            relinkingExport.WriteProperties(transplantProps);

            //Relink Binary
            try
            {
                if (relinkingExport.Game != sourcePcc.Game && (relinkingExport.IsClass || relinkingExport.ClassName == "State" || relinkingExport.ClassName == "Function"))
                {
                    relinkFailedReport.Add(new ListDialog.EntryItem(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.FullPath} binary relinking failed. Cannot port {relinkingExport.ClassName} between games!"));
                    return relinkFailedReport;
                }

                if (ObjectBinary.From(relinkingExport) is ObjectBinary objBin)
                {
                    List<(UIndex, string)> indices = objBin.GetUIndexes(relinkingExport.FileRef.Game);

                    foreach ((UIndex uIndex, string propName) in indices)
                    {
                        var result = relinkUIndex(sourcePcc, relinkingExport, ref uIndex.value, $"(Binary Property: {propName})", crossPCCObjectMappingList, "",
                                                     importExportDependencies);
                        if (result != null)
                        {
                            relinkFailedReport.Add(result);
                        }
                    }

                    //UStruct is abstract baseclass for Class, State, and Function, and can have script in it
                    if (objBin is UStruct uStructBinary && uStructBinary.ScriptBytes.Length > 0)
                    {
                        if (relinkingExport.Game == MEGame.ME3)
                        {
                            (List<Token> tokens, _) = Bytecode.ParseBytecode(uStructBinary.ScriptBytes, sourceExport);
                            foreach (Token token in tokens)
                            {
                                relinkFailedReport.AddRange(RelinkToken(token, uStructBinary.ScriptBytes, sourceExport, relinkingExport, crossPCCObjectMappingList,
                                                                        importExportDependencies));
                            }
                        }
                        else
                        {
                            var func = sourceExport.ClassName == "State" ? UE3FunctionReader.ReadState(sourceExport) : UE3FunctionReader.ReadFunction(sourceExport);
                            func.Decompile(new TextBuilder(), false); //parse bytecode
                            var nameRefs = func.NameReferences;
                            var entryRefs = func.EntryReferences;
                            foreach (var nr in nameRefs)
                            {
                                if (nr.Key < uStructBinary.ScriptBytes.Length)
                                {
                                    RelinkNameReference(nr.Value.Name, nr.Key, uStructBinary.ScriptBytes, relinkingExport);
                                }
                            }

                            foreach (var ef in entryRefs)
                            {
                                if (ef.Key < uStructBinary.ScriptBytes.Length)
                                {
                                    relinkFailedReport.AddRange(RelinkUnhoodEntryReference(ef.Value, ef.Key, uStructBinary.ScriptBytes, sourceExport, relinkingExport, crossPCCObjectMappingList,
                                        importExportDependencies));
                                }
                            }

                            //relinkFailedReport.Add(new ListDialog.EntryItem(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.FullPath} binary relinking failed. {relinkingExport.ClassName} contains script, " +
                            //                                                                 $"which cannot be relinked for {relinkingExport.Game}"));
                        }
                    }

                    relinkingExport.SetBinaryData(objBin.ToBytes(relinkingExport.FileRef, relinkingExport.DataOffset + relinkingExport.propsEnd()));
                    return relinkFailedReport;
                }

                byte[] binarydata = relinkingExport.GetBinaryData();

                if (binarydata.Length > 0)
                {
                    switch (relinkingExport.ClassName)
                    {
                        //todo: make a WwiseEvent ObjectBinary class
                        case "WwiseEvent":
                            {
                                void relinkAtPosition(int binaryPosition, string propertyName)
                                {
                                    int uIndex = BitConverter.ToInt32(binarydata, binaryPosition);
                                    var relinkResult = relinkUIndex(sourcePcc, relinkingExport, ref uIndex, propertyName,
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

                                if (relinkingExport.FileRef.Game == MEGame.ME3)
                                {
                                    int count = BitConverter.ToInt32(binarydata, 0);
                                    for (int j = 0; j < count; j++)
                                    {
                                        relinkAtPosition(4 + (j * 4), $"(Binary Property: WwiseStreams[{j}])");
                                    }

                                    relinkingExport.SetBinaryData(binarydata);
                                }
                                else if (relinkingExport.FileRef.Game == MEGame.ME2)
                                {
                                    int parsingPos = 4;
                                    int linkCount = BitConverter.ToInt32(binarydata, parsingPos);
                                    parsingPos += 4;
                                    for (int j = 0; j < linkCount; j++)
                                    {
                                        int bankcount = BitConverter.ToInt32(binarydata, parsingPos);
                                        parsingPos += 4;
                                        for (int k = 0; k < bankcount; k++)
                                        {
                                            relinkAtPosition(parsingPos, $"(Binary Property: link[{j}].WwiseBanks[{k}])");

                                            parsingPos += 4;
                                        }

                                        int wwisestreamcount = BitConverter.ToInt32(binarydata, parsingPos);
                                        parsingPos += 4;
                                        for (int k = 0; k < wwisestreamcount; k++)
                                        {
                                            relinkAtPosition(parsingPos, $"(Binary Property: link[{j}].WwiseStreams[{k}])");

                                            parsingPos += 4;
                                        }
                                    }

                                    relinkingExport.SetBinaryData(binarydata);
                                }
                            }
                            break;
                        case "DominantDirectionalLightComponent":
                        case "SphericalHarmonicLightComponent":
                        case "DominantPointLightComponent":
                        case "StaticLightCollectionActor":
                        case "DominantSpotLightComponent":
                        case "DirectionalLightComponent":
                        case "StaticMeshCollectionActor":
                        case "TerrainWeightMapTexture":
                        case "PhysicsAssetInstance":
                        case "PointLightComponent":
                        case "ShadowMapTexture2D":
                        case "SpotLightComponent":
                        case "LightMapTexture2D":
                        case "SkyLightComponent":
                        case "TextureFlipBook":
                        case "BrushComponent":
                        case "FaceFXAnimSet":
                        case "TextureMovie":
                        case "AnimSequence":
                        case "RB_BodySetup":
                        case "TextureCube":
                        case "FaceFXAsset":
                        case "MorphTarget":
                        case "ShadowMap1D":
                        case "WwiseStream":
                        case "WwiseBank":
                        case "Texture2D":
                        case "ForceFeedbackWaveform":
                            //these classes have binary but do not need relinking
                            break;
                        default:
                            if (binarydata.Any(b => b != 0))
                            {
                                relinkFailedReport.Add(new ListDialog.EntryItem(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.FullPath} has unparsed binary. " +
                                                                                                 $"This binary may contain items that need to be relinked. Come to the Discord server " +
                                                                                                 $"(click ME3Tweaks logo in main window for invite) and ask devs to parse this class."));
                            }

                            break;
                    }
                }
            }
            catch (Exception e) when (!App.IsDebug)
            {
                relinkFailedReport.Add(new ListDialog.EntryItem(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.FullPath} binary relinking failed due to exception: {e.Message}"));
            }

            return relinkFailedReport;
        }

        private static List<ListDialog.EntryItem> relinkPropertiesRecursive(IMEPackage importingPCC, ExportEntry relinkingExport, PropertyCollection transplantProps,
                                                              OrderedMultiValueDictionary<IEntry, IEntry> crossPCCObjectMappingList, string prefix,
                                                              bool importExportDependencies = false)
        {
            var relinkResults = new List<ListDialog.EntryItem>();
            foreach (UProperty prop in transplantProps)
            {
                //Debug.WriteLine($"{prefix} Relink recursive on {prop.Name}");
                if (prop is StructProperty structProperty)
                {
                    relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, relinkingExport, structProperty.Properties, crossPCCObjectMappingList,
                                                                     $"{prefix}{structProperty.Name}.", importExportDependencies));
                }
                else if (prop is ArrayProperty<StructProperty> structArrayProp)
                {
                    for (int i = 0; i < structArrayProp.Count; i++)
                    {
                        StructProperty arrayStructProperty = structArrayProp[i];
                        relinkResults.AddRange(relinkPropertiesRecursive(importingPCC, relinkingExport, arrayStructProperty.Properties, crossPCCObjectMappingList,
                                                                         $"{prefix}{arrayStructProperty.Name}[{i}].", importExportDependencies));
                    }
                }
                else if (prop is ArrayProperty<ObjectProperty> objArrayProp)
                {
                    foreach (ObjectProperty objProperty in objArrayProp)
                    {
                        int uIndex = objProperty.Value;
                        var result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, objProperty.Name, crossPCCObjectMappingList, prefix, importExportDependencies);
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
                    var result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, objectProperty.Name, crossPCCObjectMappingList, prefix, importExportDependencies);
                    objectProperty.Value = uIndex;
                    if (result != null)
                    {
                        relinkResults.Add(result);
                    }
                }
                else if (prop is DelegateProperty delegateProp)
                {
                    int uIndex = delegateProp.Value.Object;
                    var result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, delegateProp.Name, crossPCCObjectMappingList, prefix, importExportDependencies);
                    delegateProp.Value = new ScriptDelegate(uIndex, delegateProp.Value.FunctionName);
                    if (result != null)
                    {
                        relinkResults.Add(result);
                    }
                }
            }
            return relinkResults;
        }

        private static ListDialog.EntryItem relinkUIndex(IMEPackage importingPCC, ExportEntry relinkingExport, ref int uIndex, string propertyName,
                                           OrderedMultiValueDictionary<IEntry, IEntry> crossPCCObjectMappingList, string prefix, bool importExportDependencies = false)
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

            //Debug.WriteLine($"{prefix} Relinking:{propertyName}");

            if (crossPCCObjectMappingList.TryGetValue(entry => entry.UIndex == sourceObjReference, out IEntry targetEntry))
            {
                //relink
                uIndex = targetEntry.UIndex;

                //Debug.WriteLine($"{prefix} Relink hit: {sourceObjReference}{propertyName} : {targetEntry.FullPath}");
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
                        linkFailedDueToError = e.Message;
                    }

                    if (crossImport != null)
                    {
                        crossPCCObjectMappingList.Add(origImport, crossImport); //add to mapping to speed up future relinks
                        uIndex = crossImport.UIndex;
                        // Debug.WriteLine($"Relink hit: Dynamic CrossImport for {origvalue} {importingPCC.GetEntry(origvalue).FullPath} -> {uIndex}");

                    }
                    else
                    {
                        string path = importingPCC.GetEntry(uIndex) != null ? importingPCC.GetEntry(uIndex).FullPath : "Entry not found: " + uIndex;
                        if (linkFailedDueToError != null)
                        {
                            Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.GetEntry(origvalue).FullPath}");
                            return new ListDialog.EntryItem(relinkingExport, $"Relink failed for {prefix}{propertyName} {uIndex} in export {path}({relinkingExport.UIndex}): {linkFailedDueToError}");
                        }

                        if (destinationPcc.GetEntry(uIndex) != null)
                        {
                            Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.GetEntry(origvalue).FullPath}");
                            return new ListDialog.EntryItem(relinkingExport, $"Relink failed: CrossImport porting failed for {prefix}{propertyName} {uIndex} {destinationPcc.GetEntry(uIndex).FullPath} in export {relinkingExport.FullPath}({relinkingExport.UIndex})");
                        }

                        return new ListDialog.EntryItem(relinkingExport, $"Relink failed: New export does not exist - this is probably a bug in cross import code for {prefix}{propertyName} {uIndex} in export {relinkingExport.FullPath}({relinkingExport.UIndex})");
                    }
                }
            }
            else
            {
                bool importingFromGlobalFile = false;
                //It's an export
                //Attempt lookup
                ExportEntry sourceExport = importingPCC.GetUExport(uIndex);
                string fullPath = sourceExport.InstancedFullPath;
                string sourceFilePath = sourceExport.FileRef.FilePath;
                if (EntryImporter.IsSafeToImportFrom(sourceFilePath, destinationPcc.Game))
                {
                    importingFromGlobalFile = true;
                    fullPath = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{fullPath}";
                }
                IEntry existingEntry = destinationPcc.Exports.FirstOrDefault(x => x.InstancedFullPath == fullPath);
                existingEntry ??= destinationPcc.Imports.FirstOrDefault(x => x.InstancedFullPath == fullPath);
                if (existingEntry != null)
                {
                    //Debug.WriteLine($"Relink hit [EXPERIMENTAL]: Existing entry in file was found, linking to it:  {uIndex} {sourceExport.InstancedFullPath} -> {existingEntry.InstancedFullPath}");
                    uIndex = existingEntry.UIndex;

                }
                else if (importExportDependencies)
                {
                    if (importingFromGlobalFile)
                    {
                        uIndex = EntryImporter.GetOrAddCrossImportOrPackageFromGlobalFile(sourceExport.FullPath, importingPCC, destinationPcc, crossPCCObjectMappingList).UIndex;
                    }
                    else
                    {
                        if (!crossPCCObjectMappingList.TryGetValue(sourceExport.Parent, out IEntry parent))
                        {
                            parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceExport.ParentFullPath, importingPCC, destinationPcc, true, crossPCCObjectMappingList);
                        }
                        ExportEntry importedExport = EntryImporter.ImportExport(destinationPcc, sourceExport, parent?.UIndex ?? 0, true, crossPCCObjectMappingList);
                        uIndex = importedExport.UIndex;
                    }
                }
                else
                {
                    string path = importingPCC.GetEntry(uIndex)?.FullPath ?? $"Entry not found: {uIndex}";
                    Debug.WriteLine($"Relink failed in {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} {uIndex} {path}");
                    return new ListDialog.EntryItem(relinkingExport, $"Relink failed: {prefix}{propertyName} {uIndex} in export {relinkingExport.FullPath}({relinkingExport.UIndex})");
                }
            }

            return null;
        }


        private static List<ListDialog.EntryItem> RelinkUnhoodEntryReference(IEntry entry, long position, byte[] script, ExportEntry sourceExport, ExportEntry destinationExport,
                                            OrderedMultiValueDictionary<IEntry, IEntry> crossFileRefObjectMap, bool importExportDependencies = false)
        {
            var relinkFailedReport = new List<ListDialog.EntryItem>();
            Debug.WriteLine($"Attempting function relink on token entry reference {entry.FullPath} at position {position}");

            int uIndex = entry.UIndex;
            var relinkResult = relinkUIndex(sourceExport.FileRef, destinationExport, ref uIndex, $"Entry {entry.FullPath} at 0x{position:X8}",
                crossFileRefObjectMap, "", importExportDependencies);
            if (relinkResult is null)
            {
                script.OverwriteRange((int)position, BitConverter.GetBytes(uIndex));
            }
            else
            {
                relinkFailedReport.Add(relinkResult);
            }

            return relinkFailedReport;
        }

        private static List<ListDialog.EntryItem> RelinkToken(Token t, byte[] script, ExportEntry sourceExport, ExportEntry destinationExport,
                                                OrderedMultiValueDictionary<IEntry, IEntry> crossFileRefObjectMap, bool importExportDependencies = false)
        {
            var relinkFailedReport = new List<ListDialog.EntryItem>();
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
                var relinkResult = relinkUIndex(sourceExport.FileRef, destinationExport, ref uIndex, propertyName,
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

        /// <summary>
        /// This returns nothing as you cannot fail to relink a name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="position"></param>
        /// <param name="data"></param>
        /// <param name="destinationExport"></param>
        private static void RelinkNameReference(string name, long position, byte[] data, ExportEntry destinationExport)
        {
            data.OverwriteRange((int)position, BitConverter.GetBytes(destinationExport.FileRef.FindNameOrAdd(name)));
        }
    }
}