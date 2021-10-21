using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Collections.ObjectModel;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{

    public static class Relinker
    {
        /// <summary>
        /// Attempts to relink unreal property data and object pointers in binary when cross porting an export
        /// </summary>
        public static List<EntryStringPair> RelinkAll(IDictionary<IEntry, IEntry> crossPccObjectMap, bool importExportDependencies = false)
        {
            var relinkReport = new List<EntryStringPair>();
            //relink each modified export

            //We must convert this to a list, as this list will be updated as imports are cross mapped during relinking.
            //This process speeds up same-relinks later.
            //This is a list because otherwise we would get a concurrent modification exception.
            //Since we only enumerate exports and append imports to this list we will not need to worry about recursive links
            //I am sure this won't come back to be a pain for me.

            // Used for quick mapping lookups
            var crossPackageMap = new ListenableDictionary<IEntry, IEntry>(crossPccObjectMap);

            // Used to perform a full relink
            var mappingList = crossPackageMap.ToList();

            crossPackageMap.OnDictionaryChanged += (sender, args) =>
            {
                if (args.Type == DictChangeType.AddItem)
                {
                    mappingList.Add(new KeyValuePair<IEntry, IEntry>(args.Key, args.Value));
                    //Debug.WriteLine($"Adding relink mapping {args.Key.ObjectName} {args.Key.UIndex} -> {args.Value.UIndex}");
                }
            };
            //can't be a foreach since we might append things to the list
            // ReSharper disable once ForCanBeConvertedToForeach

            // Used for forcing further relinks
            int i = 0;
            while (i < mappingList.Count)
            {
                var entryMap = mappingList[i];
                if (entryMap.Key is ExportEntry sourceExport && entryMap.Value is ExportEntry relinkingExport)
                {
                    Relink(sourceExport, relinkingExport, crossPackageMap, relinkReport, importExportDependencies);
                }
                i++;

                // Potential way to work around OrderedMultiValueDictionary performance issues - if the concat on listenabledictionary doesn't work
                // Have we reached the end of the current pass? If so, recreate the list. This means we only have to generate the list
                // a few times instead of thousands
                // If we are at end pass this won't make a difference
                //if (i == mappingList.Count && crossPackageMap.Count != mappingList.Count)
                //{
                //    mappingList = crossPackageMap.ToList();
                //}
            }
            crossPccObjectMap.ReplaceAll(crossPccObjectMap);
            return relinkReport;
        }

        public static void Relink(ExportEntry sourceExport, ExportEntry relinkingExport, IDictionary<IEntry, IEntry> crossPCCObjectMappingList,
            List<EntryStringPair> relinkReport, bool importExportDependencies = false)
        {
            IMEPackage sourcePcc = sourceExport.FileRef;

            // Relink header (component map)
            if (relinkingExport.HasComponentMap && relinkingExport.ComponentMap.Count > 0)
            {
                OrderedMultiValueDictionary<NameReference, int> newComponentMap = new OrderedMultiValueDictionary<NameReference, int>();
                foreach (var cmk in sourceExport.ComponentMap)
                {
                    // This code makes a lot of assumptions, like how components are always directly below the current export
                    var nameIndex = relinkingExport.FileRef.FindNameOrAdd(cmk.Key.Name);
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport.FileRef.GetUExport(cmk.Value + 1), relinkingExport.FileRef, relinkingExport, true, out var newComponent);

                    newComponentMap.Add(new KeyValuePair<NameReference, int>(cmk.Key, newComponent.UIndex - 1)); // TODO: Relink the 
                }

                relinkingExport.ComponentMap = newComponentMap;
            }



            byte[] prePropBinary = relinkingExport.GetPrePropBinary();
            //Relink stack
            if (relinkingExport.HasStack)
            {

                int uIndex = BitConverter.ToInt32(prePropBinary, 0);
                var relinkResult = relinkUIndex(sourceExport.FileRef, relinkingExport, ref uIndex, "Stack: Node",
                                                   crossPCCObjectMappingList, "", importExportDependencies);
                if (relinkResult is null)
                {
                    prePropBinary.OverwriteRange(0, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    relinkReport.Add(relinkResult);
                }

                uIndex = BitConverter.ToInt32(prePropBinary, 4);
                relinkResult = relinkUIndex(sourceExport.FileRef, relinkingExport, ref uIndex, "Stack: StateNode",
                                            crossPCCObjectMappingList, "", importExportDependencies);
                if (relinkResult is null)
                {
                    prePropBinary.OverwriteRange(4, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    relinkReport.Add(relinkResult);
                }
            }
            //Relink Component's TemplateOwnerClass
            else if (relinkingExport.TemplateOwnerClassIdx is var toci and >= 0)
            {

                int uIndex = BitConverter.ToInt32(prePropBinary, toci);
                var relinkResult = relinkUIndex(sourceExport.FileRef, relinkingExport, ref uIndex, "TemplateOwnerClass",
                                                crossPCCObjectMappingList, "", importExportDependencies);
                if (relinkResult is null)
                {
                    prePropBinary.OverwriteRange(toci, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    relinkReport.Add(relinkResult);
                }
            }

            //Relink Properties
            // NOTES: this used to be relinkingExport, not source, Changed near end of jan 2021 - Mgamerz - Due to ported items possibly not having way to reference original items
            PropertyCollection props = sourceExport.GetProperties();
            bool removedProperties = false;
            if (sourcePcc.Game != relinkingExport.Game && props.Count > 0)
            {
                props = EntryPruner.RemoveIncompatibleProperties(sourcePcc, props, sourceExport.ClassName, relinkingExport.Game, ref removedProperties);
                if (removedProperties)
                {
                    relinkReport.Add(new EntryStringPair(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.InstancedFullPath}: Some properties were removed from this object because they do not exist in {relinkingExport.Game}!"));
                }
            }
            relinkPropertiesRecursive(sourcePcc, relinkingExport, props, crossPCCObjectMappingList, "", relinkReport, importExportDependencies);

            //Relink Binary
            try
            {
                if (relinkingExport.Game != sourcePcc.Game && (relinkingExport.IsClass || relinkingExport.ClassName is "State" or "Function"))
                {
                    relinkReport.Add(new EntryStringPair(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.InstancedFullPath} binary relinking failed. Cannot port {relinkingExport.ClassName} between games!"));
                }
                else if (ObjectBinary.From(relinkingExport) is ObjectBinary objBin)
                {

                    // This doesn't work on functions! Finding the children through the probe doesn't work

                    if (objBin.Export is {ClassName: "State"})
                    {
                        // We can't relink labeltable as it depends on none
                        // Use the source export instead
                        objBin = ObjectBinary.From(sourceExport);
                    }

                    
                    List<(UIndex, string)> indices = objBin.GetUIndexes(relinkingExport.FileRef.Game);
                    
                    foreach ((UIndex uIndex, string propName) in indices)
                    {
                        var result = relinkUIndex(sourcePcc, relinkingExport, ref uIndex.value, $"(Binary Property: {propName})",
                            crossPCCObjectMappingList, "", importExportDependencies);
                        if (result != null)
                        {
                            relinkReport.Add(result);
                        }
                    }

                    //UStruct is abstract baseclass for Class, State, and Function, and can have script in it
                    if (objBin is UStruct uStructBinary && uStructBinary.ScriptBytes.Length > 0)
                    {
                        if (relinkingExport.Game == MEGame.ME3 || relinkingExport.Game.IsLEGame())
                        {
                            (List<Token> tokens, _) = Bytecode.ParseBytecode(uStructBinary.ScriptBytes, sourceExport);
                            foreach (Token token in tokens)
                            {
                                RelinkToken(token, uStructBinary.ScriptBytes, sourceExport, relinkingExport,
                                    crossPCCObjectMappingList, relinkReport, importExportDependencies);
                            }
                        }
                        else
                        {
                            var func = sourceExport.ClassName == "State" ? UE3FunctionReader.ReadState(sourceExport) : UE3FunctionReader.ReadFunction(sourceExport);
                            func.Decompile(new TextBuilder(), false, false); //parse bytecode
                            var nameRefs = func.NameReferences;
                            var entryRefs = func.EntryReferences;
                            foreach ((long position, NameReference nameRef) in nameRefs)
                            {
                                if (position < uStructBinary.ScriptBytes.Length)
                                {
                                    RelinkNameReference(nameRef.Name, position, uStructBinary.ScriptBytes, relinkingExport);
                                }
                            }

                            foreach ((long position, IEntry entry) in entryRefs)
                            {
                                if (position < uStructBinary.ScriptBytes.Length)
                                {
                                    RelinkUnhoodEntryReference(entry, position, uStructBinary.ScriptBytes, sourceExport, relinkingExport,
                                         crossPCCObjectMappingList, relinkReport, importExportDependencies);
                                }
                            }
                        }
                    }
                    relinkingExport.WritePrePropsAndPropertiesAndBinary(prePropBinary, props, objBin);
                    return;
                }
            }
            catch (Exception e) when (!LegendaryExplorerCoreLib.IsDebug)
            {
                relinkReport.Add(new EntryStringPair(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.InstancedFullPath} binary relinking failed due to exception: {e.Message}"));
            }

            relinkingExport.WritePrePropsAndProperties(prePropBinary, props, removedProperties ? relinkingExport.propsEnd() : sourceExport.propsEnd());
        }

        private static void relinkPropertiesRecursive(IMEPackage importingPCC, ExportEntry relinkingExport, PropertyCollection transplantProps,
                                                              IDictionary<IEntry, IEntry> crossPCCObjectMappingList, string prefix, List<EntryStringPair> relinkResults,
                                                              bool importExportDependencies = false)
        {
            foreach (Property prop in transplantProps)
            {
                //Debug.WriteLine($"{prefix} Relink recursive on {prop.Name}");
                if (prop is StructProperty structProperty)
                {
                    relinkPropertiesRecursive(importingPCC, relinkingExport, structProperty.Properties, crossPCCObjectMappingList,
                        $"{prefix}{structProperty.Name}.", relinkResults, importExportDependencies);
                }
                else if (prop is ArrayProperty<StructProperty> structArrayProp)
                {
                    for (int i = 0; i < structArrayProp.Count; i++)
                    {
                        StructProperty arrayStructProperty = structArrayProp[i];
                        relinkPropertiesRecursive(importingPCC, relinkingExport, arrayStructProperty.Properties, crossPCCObjectMappingList,
                                                                         $"{prefix}{arrayStructProperty.Name}[{i}].", relinkResults, importExportDependencies);
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
        }

        private static EntryStringPair relinkUIndex(IMEPackage importingPCC, ExportEntry relinkingExport, ref int uIndex, string propertyName,
                                           IDictionary<IEntry, IEntry> crossPCCObjectMappingList, string prefix, bool importExportDependencies = false)
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
            //if (sourceObjReference == -1009)
            //    Debugger.Break();
            //Debug.WriteLine($"{prefix} Relinking:{propertyName}");
            if (crossPCCObjectMappingList.TryGetValue(importingPCC.GetEntry(uIndex), out IEntry targetEntry))
            {
                //relink
                uIndex = targetEntry.UIndex;

                //Debug.WriteLine($"{prefix} Relink hit: {sourceObjReference}{propertyName} : {targetEntry.InstancedFullPath}");
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
                    string origImportFullName = origImport.InstancedFullPath; //used to be just FullPath - but some imports are indexed!
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
                        // Debug.WriteLine($"Relink hit: Dynamic CrossImport for {origvalue} {importingPCC.GetEntry(origvalue).InstancedFullPath} -> {uIndex}");

                    }
                    else
                    {
                        string path = importingPCC.GetEntry(uIndex) != null ? importingPCC.GetEntry(uIndex).InstancedFullPath : "Entry not found: " + uIndex;
                        if (linkFailedDueToError != null)
                        {
                            Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.GetEntry(origvalue).InstancedFullPath}");
                            return new EntryStringPair(relinkingExport, $"Relink failed for {prefix}{propertyName} {uIndex} in export {path}({relinkingExport.UIndex}): {linkFailedDueToError}");
                        }

                        if (destinationPcc.GetEntry(uIndex) != null)
                        {
                            Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.GetEntry(origvalue).InstancedFullPath}");
                            return new EntryStringPair(relinkingExport, $"Relink failed: CrossImport porting failed for {prefix}{propertyName} {uIndex} {destinationPcc.GetEntry(uIndex).InstancedFullPath} in export {relinkingExport.InstancedFullPath}({relinkingExport.UIndex})");
                        }

                        return new EntryStringPair(relinkingExport, $"Relink failed: New export does not exist - this is probably a bug in cross import code for {prefix}{propertyName} {uIndex} in export {relinkingExport.InstancedFullPath}({relinkingExport.UIndex})");
                    }
                }
            }
            else
            {
                bool importingFromGlobalFile = false;
                //It's an export
                //Attempt lookup
                ExportEntry sourceExport = importingPCC.GetUExport(uIndex);
                string instancedFullPath = sourceExport.InstancedFullPath;
                string sourceFilePath = sourceExport.FileRef.FilePath;
                if (EntryImporter.IsSafeToImportFrom(sourceFilePath, destinationPcc.Game))
                {
                    importingFromGlobalFile = true;
                    instancedFullPath = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{instancedFullPath}";
                }

                IEntry existingEntry = destinationPcc.FindEntry(instancedFullPath);

                if (existingEntry != null)
                {
#if DEBUG
                    if (existingEntry.InstancedFullPath.StartsWith(UnrealPackageFile.TrashPackageName))
                    {
                        // RELINKED TO TRASH!
                        Debugger.Break();
                    }
#endif
                    //Debug.WriteLine($"Relink hit [EXPERIMENTAL]: Existing entry in file was found, linking to it:  {uIndex} {sourceExport.InstancedFullPath} -> {existingEntry.InstancedFullPath}");
                    uIndex = existingEntry.UIndex;

                }
                else if (importExportDependencies)
                {
                    if (importingFromGlobalFile)
                    {
                        uIndex = EntryImporter.GetOrAddCrossImportOrPackageFromGlobalFile(sourceExport.InstancedFullPath, importingPCC, destinationPcc, crossPCCObjectMappingList).UIndex;
                    }
                    else
                    {
                        IEntry parent = null;
                        if (sourceExport.Parent != null && !crossPCCObjectMappingList.TryGetValue(sourceExport.Parent, out parent))
                        {
                            parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceExport.ParentInstancedFullPath, importingPCC, destinationPcc, true, crossPCCObjectMappingList);
                        }
                        ExportEntry importedExport = EntryImporter.ImportExport(destinationPcc, sourceExport, parent?.UIndex ?? 0, true, crossPCCObjectMappingList);
                        uIndex = importedExport.UIndex;
                    }
                }
                else
                {
                    string path = importingPCC.GetEntry(uIndex)?.InstancedFullPath ?? $"Entry not found: {uIndex}";
                    Debug.WriteLine($"Relink failed in {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} {uIndex} {path}");
                    return new EntryStringPair(relinkingExport, $"Relink failed: {prefix}{propertyName} {uIndex} in export {relinkingExport.InstancedFullPath}({relinkingExport.UIndex})");
                }
            }

            return null;
        }


        private static void RelinkUnhoodEntryReference(IEntry entry, long position, byte[] script, ExportEntry sourceExport, ExportEntry destinationExport,
                                            IDictionary<IEntry, IEntry> crossFileRefObjectMap, List<EntryStringPair> relinkFailedReport, bool importExportDependencies = false)
        {
            //Debug.WriteLine($"Attempting function relink on token entry reference {entry.FullPath} at position {position}");

            int uIndex = entry.UIndex;
            var relinkResult = relinkUIndex(sourceExport.FileRef, destinationExport, ref uIndex, $"Entry {entry.InstancedFullPath} at 0x{position:X8}",
                crossFileRefObjectMap, "", importExportDependencies);
            if (relinkResult is null)
            {
                script.OverwriteRange((int)position, BitConverter.GetBytes(uIndex));
            }
            else
            {
                relinkFailedReport.Add(relinkResult);
            }
        }

        private static void RelinkToken(Token t, byte[] script, ExportEntry sourceExport, ExportEntry destinationExport,
                                                IDictionary<IEntry, IEntry> crossFileRefObjectMap, List<EntryStringPair> relinkFailedReport,
        bool importExportDependencies = false)
        {
            //Debug.WriteLine($"Attempting function relink on token at position {t.pos}. Number of listed relinkable items {t.inPackageReferences.Count}");

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