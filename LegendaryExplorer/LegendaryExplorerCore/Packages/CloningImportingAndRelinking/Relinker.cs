using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Collections;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{
    /// <summary>
    /// Specifies options used for relinking, as well as containing objects that can be passed through the entry porting process without having to modify method signatures.
    /// </summary>
    public class RelinkerOptionsPackage
    {
#if DEBUG
        /// <summary>
        /// Used to denote a unique version of this object in the debugger
        /// </summary>
        public Guid aDebuggingGuid = Guid.NewGuid();
#endif

        /// <summary>
        /// The mapping of source package entries to target package entries. Items in this dictionary will be relinked, and the dictionary will be populated as relinking occurs. Supply your own if you're doing a targeted relink, or let the relinker automatically build this
        /// </summary>
        public ListenableDictionary<IEntry, IEntry> CrossPackageMap = [];

        /// <summary>
        /// Whether objects an export depends on should be imported during a relink.
        /// This will cause all dependencies of dependencies to also be ported in, as well as the parents of those dependencies so they can be fully qualified paths.
        /// </summary>
        public bool ImportExportDependencies { get; set; } = true;

        /// <summary>
        /// The object database that will be used for donating from the target game. This is only used if IsCrossGame is true. Passing this as null will still allow cross game, but donors will not be used.
        /// </summary>
        public ObjectInstanceDB TargetGameDonorDB { get; set; }

        /// <summary>
        /// If this relinker operation is across two different games
        /// </summary>
        public bool IsCrossGame { get; set; }

        /// <summary>
        /// The results of the relink. If this is empty, everything was OK, otherwise warnings and errors will populate this list.
        /// </summary>
        public List<EntryStringPair> RelinkReport { get; set; } = [];

        /// <summary>
        /// Package Cache that can be used to open packages. Can speed up performance if many packages have to be opened in succession.
        /// </summary>
        public PackageCache Cache { get; set; }

        /// <summary>
        /// When porting out of globally loaded files (like SFXGame), imports will be generated for relinked objects instead of porting exports.
        /// </summary>
        public bool GenerateImportsForGlobalFiles { get; set; } = true;

        /// <summary>
        /// When porting imports across files, resolve import in target first. If import fails to resolve, port the resolved export from the source instead
        /// </summary>
        public bool PortImportsMemorySafe { get; set; }

        /// <summary>
        /// When porting exports, attempt to resolve as import in the target file first, if it resolves, port it as an import instead
        /// </summary>
        public bool PortExportsAsImportsWhenPossible { get; set; }

        /// <summary>
        /// When using certain porting options, and the item being ported is a package, setting this to false will only port the package, not the children of it
        /// </summary>
        public bool ImportChildrenOfPackages { get; set; } = true;

        /// <summary>
        /// The path to the root of the game for this relinker option - this is only used if you are overriding the default path of the game, so this is used mostly with ME3Tweaks Mod Manager
        /// </summary>
        public string GamePathOverride { get; set; }

        /// <summary>
        /// Custom resolver that can be used to help find imports in the destination package
        /// </summary>
        public Func<string, PackageCache, IMEPackage> DestinationCustomImportFileResolver { get; set; }

        /// <summary>
        /// Custom resolver that can be used to help find imports in the source package
        /// </summary>
        public Func<string, PackageCache, IMEPackage> SourceCustomImportFileResolver { get; set; }

        /// <summary>
        /// Allows different classnames to exist when relinking and an object is looked up. <br/>
        /// This can happen in vanilla bioware files (e.g. material and texture with same IFP). <br/>
        /// Package resynthesis uses this so relink re-uses existing data
        /// Typically you want this to be false unless you know what you're doing.
        /// </summary>
        public bool RelinkAllowDifferingClassesInRelink { get; set; }

        /// <summary>
        /// If imports that resolve to be in localized files should be imported as exports when <see cref="PortImportsMemorySafe"/> is set to true. This often is undesirable, bringing INT lines into non-localized files.
        /// </summary>
        public bool PortLocalizationImportsMemorySafe { get; set; }

        /// <summary>
        /// Invoked when an error occurs during porting. Can be null.
        /// </summary>
        public Action<string> ErrorOccurredCallback;

        /// <summary>
        /// Blank constructor (left here for breakpoints)
        /// </summary>
        public RelinkerOptionsPackage()
        {
            // Commented out 11/20/2023 - might break crossgen
            // Cache = new PackageCache();
        }

        /// <summary>
        /// Constructor that takes an existing <see cref="PackageCache"/>
        /// </summary>
        public RelinkerOptionsPackage(PackageCache cache)
        {
            // 11/20/2023: Initialize an empty package cache
            Cache = cache ?? new PackageCache();
        }

        /// <summary>
        /// When importing an export that is a donor, and the donor is found, this can be set to override how the donor export is processed into the destination package. Return the newly created export.
        /// </summary>
        public Func<IMEPackage, ExportEntry, RelinkerOptionsPackage, IEntry> CustomDonorImporter { get; set; }

        /// <summary>
        /// USED BY ENTRYEXPORTER - Set to false to disable the import to export resolution. Use if you know you need imports and not exports.
        /// </summary>
        public bool CheckImportsWhenExportingToPackage { get; set; } = true;

        /// <summary>
        /// When relinking only objects within the same package, setting this to true will force imports to be considered for relink. This is useful for repointing import references to different objects.
        /// </summary>
        public bool ForceSamePackageImportRelink { get; set; }
    }

    public static class Relinker
    {
        /// <summary>
        /// Attempts to relink unreal property data and object pointers in binary when cross porting an export. Access the results from the RelinkerOptionsPackage's RelinkReport property.
        /// </summary>
        public static void RelinkAll(RelinkerOptionsPackage rop)
        {
            //relink each modified export

            //We must convert this to a list, as this list will be updated as imports are cross mapped during relinking.
            //This process speeds up same-relinks later.
            //This is a list because otherwise we would get a concurrent modification exception.
            //Since we only enumerate exports and append imports to this list we will not need to worry about recursive links
            //I am sure this won't come back to be a pain for me.

            // Used to perform a full relink. Items will be added to this list so they can be processed at the end
            var mappingList = rop.CrossPackageMap.ToList();

            rop.CrossPackageMap.OnDictionaryChanged += (sender, args) =>
            {
                if (args.Type == DictChangeType.AddItem)
                {
                    mappingList.Add(new KeyValuePair<IEntry, IEntry>(args.Key, args.Value));
                    //Debug.WriteLine($"ROP {rop.aDebuggingGuid} -  Adding relink mapping {args.Key.ObjectName} {args.Key.UIndex} -> {args.Value.UIndex}");
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
                    Relink(sourceExport, relinkingExport, rop);
                }
                i++;
            }

            // If porting cross game, functions need to be recompiled
            if (rop.IsCrossGame)
            {
                var functionsToRelink = rop.CrossPackageMap.Keys.OfType<ExportEntry>().Where(x => x.ClassName == "Function").ToList();
                if (functionsToRelink.Any())
                {
                    UnrealScriptOptionsPackage usopSource = new UnrealScriptOptionsPackage()
                    {
                        Cache = TieredPackageCache.GetGlobalPackageCache(rop.CrossPackageMap.First().Key.Game).ChainNewCache(), // Cross game source will use its own cache
                        CustomFileResolver = rop.SourceCustomImportFileResolver,
                    };

                    UnrealScriptOptionsPackage usopDest = new UnrealScriptOptionsPackage()
                    {
                        Cache = rop.Cache,
                        CustomFileResolver = rop.DestinationCustomImportFileResolver,
                        GamePathOverride = rop.GamePathOverride
                    };


                    //functionsToRelink has exports from potentially multiple files. This creates the minimum number of FileLibs needed
                    var sourceFileLibs = new Dictionary<IMEPackage, FileLib>();
                    bool sourceOK = true;



                    foreach (ExportEntry funcToRelink in functionsToRelink)
                    {
                        if (!sourceFileLibs.ContainsKey(funcToRelink.FileRef))
                        {
                            var sourceLib = new FileLib(funcToRelink.FileRef);
                            // We use different USOP objects depending on what game we are initializing the FileLib for. If a
                            // class is donated from the target game we use the dest one. If there is no donor, it might be coming cross
                            // game and we want to use the source game instead.
                            sourceOK &= sourceLib.Initialize(funcToRelink.FileRef.Game == rop.CrossPackageMap.First().Key.Game ? usopSource : usopDest);
                            if (!sourceOK)
                            {
                                rop.RelinkReport.Add(new EntryStringPair(funcToRelink, $"{funcToRelink.UIndex} {funcToRelink.InstancedFullPath} function relinking failed. Could not initialize the FileLib! This will likely be unusable. {string.Join("\n", sourceLib.InitializationLog.AllErrors.Select(x => x.Message))}"));
                                break;
                            }
                            sourceFileLibs[funcToRelink.FileRef] = sourceLib;
                        }
                    }

                    var destPcc = rop.CrossPackageMap[functionsToRelink[0]].FileRef;
                    FileLib destFL = new FileLib(destPcc);
                    var destOK = destFL.Initialize(usopDest);

                    if (sourceOK && destOK)
                    {
                        foreach (var f in functionsToRelink)
                        {
                            // crossgen debug
                            int origBCBIdx = -1;
                            var sourcePcc = f.FileRef;
                            if (sourcePcc.Game == MEGame.ME1)
                            {
                                origBCBIdx = sourcePcc.findName("BIOC_Base");
                                sourcePcc.replaceName(origBCBIdx, "SFXGame");

                                // Todo: Other renamed packages like BIOG_Strategic"AI" -> SFXStrategic"AI"
                            }

                            var targetFuncEntry = rop.CrossPackageMap[f];
                            if (targetFuncEntry is ImportEntry)
                            {
                                continue; // This was converted to an import and does not need recompiled
                            }

                            var targetFuncExp = targetFuncEntry as ExportEntry;
#if DEBUG
                            // DEBUGGING
                            var debugTargetEntry = rop.CrossPackageMap[f];
#endif

                            var sourceInfo = UnrealScriptCompiler.DecompileExport(f, sourceFileLibs[f.FileRef], usopSource);
                            //    var targetFunc = ObjectBinary.From<UFunction>(targetFuncExp);
                            //    targetFunc.ScriptBytes = new byte[0]; // Zero out function
                            //    targetFuncExp.WriteBinary(targetFunc);

                            // Debug.WriteLine($"Recompiling function after cross game porting: {targetFuncExp.InstancedFullPath}");

                            (_, MessageLog log) = UnrealScriptCompiler.CompileFunction(targetFuncExp, sourceInfo.text, destFL, usopDest);
                            if (log.AllErrors.Any())
                            {
                                rop.RelinkReport.Add(new EntryStringPair(targetFuncExp, $"{targetFuncExp.UIndex} {targetFuncExp.InstancedFullPath} binary relinking failed. Could not recompile function. Errors: {string.Join("\n", log.AllErrors.Select(x => x.Message))}"));
                            }

                            if (origBCBIdx >= 0)
                            {
                                // Restore BIOC_Base
                                sourcePcc.replaceName(origBCBIdx, "BIOC_Base");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Relinks an export to a new export, likely in another package.
        /// </summary>
        /// <param name="sourceExport">The source export that was ported, containing the original data</param>
        /// <param name="relinkingExport">The export that will be updated with new references</param>
        /// <param name="rop">Option package for relinking</param>
        public static void Relink(ExportEntry sourceExport, ExportEntry relinkingExport, RelinkerOptionsPackage rop)
        {
            IMEPackage sourcePcc = sourceExport.FileRef;
            // Relink header (component map)
            // When porting to a game newer than ME2 might want to just strip this out. As I don't think that engine version uses this anymore
            if (relinkingExport.HasComponentMap && relinkingExport.ComponentMap.Count > 0)
            {
                var newComponentMap = new UMultiMap<NameReference, int>();
                foreach ((NameReference componentName, int componentIndex) in sourceExport.ComponentMap)
                {
                    // 04/07/2024 - Remove temp cross package mapping and have this call skip its internal relink step since this call is already in a relink - Mgamerz
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceExport.FileRef.GetUExport(componentIndex + 1), relinkingExport.FileRef, relinkingExport, false, rop, out var newComponent);
                    newComponentMap.Add(componentName, newComponent.UIndex - 1); // TODO: Relink the 
                }
                relinkingExport.ComponentMap = newComponentMap;
            }

            byte[] prePropBinary = relinkingExport.GetPrePropBinary();
            //Relink stack
            if (relinkingExport.HasStack)
            {
                int uIndex = BitConverter.ToInt32(prePropBinary, 0);
                var relinkResult = relinkUIndex(sourceExport.FileRef, relinkingExport, ref uIndex, "Stack: Node", "", rop);
                if (relinkResult is null)
                {
                    prePropBinary.OverwriteRange(0, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    rop.RelinkReport.Add(relinkResult);
                }

                uIndex = BitConverter.ToInt32(prePropBinary, 4);
                relinkResult = relinkUIndex(sourceExport.FileRef, relinkingExport, ref uIndex, "Stack: StateNode", "", rop);
                if (relinkResult is null)
                {
                    prePropBinary.OverwriteRange(4, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    rop.RelinkReport.Add(relinkResult);
                }
            }
            //Relink Component's TemplateOwnerClass
            else if (relinkingExport.TemplateOwnerClassIdx is var toci and >= 0)
            {
                int uIndex = BitConverter.ToInt32(prePropBinary, toci);
                var relinkResult = relinkUIndex(sourceExport.FileRef, relinkingExport, ref uIndex, "TemplateOwnerClass", "", rop);
                if (relinkResult is null)
                {
                    prePropBinary.OverwriteRange(toci, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    rop.RelinkReport.Add(relinkResult);
                }
            }

            //Relink Properties
            // NOTES: this used to be relinkingExport, not source, Changed near end of jan 2021 - Mgamerz - Due to ported items possibly not having way to reference original items
            PropertyCollection props = sourceExport.GetProperties();
            bool removedProperties = false;
            if (sourcePcc.Game != relinkingExport.Game && props.Count > 0)
            {
                if (!sourceExport.IsDefaultObject)
                {
                    EntryImporter.ApplyCrossGamePropertyFixes(sourceExport, relinkingExport.FileRef, props);
                    props = EntryPruner.RemoveIncompatibleProperties(sourcePcc, props, sourceExport.ClassName, relinkingExport.Game, ref removedProperties);
                    if (removedProperties)
                    {
                        rop.RelinkReport.Add(new EntryStringPair(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.InstancedFullPath}: Some properties were removed from this object because they do not exist in {relinkingExport.Game}!"));
                    }
                }
            }

            relinkPropertiesRecursive(sourcePcc, relinkingExport, props, "", rop);

            //Relink Binary
            try
            {
                // crossgen-v disabled .IsClass sept 20 2021 - mgamerz
                //if (relinkingExport.Game != sourcePcc.Game && (/*relinkingExport.IsClass || */relinkingExport.ClassName is "State" /*or "Function"*/))
                //{
                //    rop.RelinkReport.Add(new EntryStringPair(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.InstancedFullPath} binary relinking failed. Cannot port {relinkingExport.ClassName} between games!"));
                //}
                //else
                if (ObjectBinary.From(relinkingExport) is ObjectBinary objBin)
                {
                    // This doesn't work on functions! Finding the children through the probe doesn't work

                    if (objBin.Export is { ClassName: "State" })
                    {
                        // We can't relink labeltable as it depends on none
                        // Use the source export instead
                        objBin = ObjectBinary.From(sourceExport);
                    }
                    else if (relinkingExport.Game != sourcePcc.Game && objBin is UFunction uf)
                    {
                        uf.ScriptBytes = []; // This needs zero'd out so it doesn't try to relink anything. The relink will occur on the second pass
                    }

                    objBin.ForEachUIndex(relinkingExport.FileRef.Game, new RelinkingAction(sourcePcc, relinkingExport, rop));
                    if (relinkingExport.Game != sourcePcc.Game && objBin is UFunction uf2)
                    {
                        // This forces data to copy over that may have been referenced in the script data, such as another class in the same file. 
                        // It won't be relinked till later though
                        ObjectBinary.From(sourceExport).ForEachUIndex(sourceExport.FileRef.Game, new RelinkingAction(sourcePcc, relinkingExport, rop));
                    }

                    //UStruct is abstract baseclass for Class, State, and Function, and can have script in it
                    if (objBin is UStruct uStructBinary && uStructBinary.ScriptBytes.Length > 0)
                    {
                        if (relinkingExport.Game == MEGame.ME3 || relinkingExport.Game.IsLEGame())
                        {
                            (List<Token> tokens, _) = Bytecode.ParseBytecode(uStructBinary.ScriptBytes, sourceExport);
                            foreach (Token token in tokens)
                            {
                                RelinkToken(token, uStructBinary.ScriptBytes, sourceExport, relinkingExport, rop);
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
                                    RelinkUnhoodEntryReference(entry, position, uStructBinary.ScriptBytes, sourceExport, relinkingExport, rop);
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
                rop.RelinkReport.Add(new EntryStringPair(relinkingExport, $"{relinkingExport.UIndex} {relinkingExport.InstancedFullPath} binary relinking failed due to exception: {e.Message}"));
            }

            relinkingExport.WritePrePropsAndProperties(prePropBinary, props, removedProperties || sourceExport.Game != relinkingExport.Game ? relinkingExport.propsEnd() : sourceExport.propsEnd());
        }

        private readonly struct RelinkingAction : IUIndexAction
        {
            private readonly IMEPackage ImportingPcc;
            private readonly ExportEntry RelinkingExport;
            private readonly RelinkerOptionsPackage Rop;

            public RelinkingAction(IMEPackage importingPcc, ExportEntry relinkingExport, RelinkerOptionsPackage rop)
            {
                ImportingPcc = importingPcc;
                RelinkingExport = relinkingExport;
                Rop = rop;
            }

            public void Invoke(ref int uIndex, string propName)
            {
                var result = relinkUIndex(ImportingPcc, RelinkingExport, ref uIndex, $"(Binary Property: {propName})", "", Rop);
                if (result != null)
                {
                    Rop.RelinkReport.Add(result);
                }
            }
        }

        /// <summary>
        /// Relinks a property collection, from the importingPCC to the relinkingExport.
        /// </summary>
        /// <param name="importingPCC"></param>
        /// <param name="relinkingExport"></param>
        /// <param name="transplantProps"></param>
        /// <param name="prefix"></param>
        /// <param name="rop"></param>
        private static void relinkPropertiesRecursive(IMEPackage importingPCC, ExportEntry relinkingExport, PropertyCollection transplantProps, string prefix, RelinkerOptionsPackage rop)
        {
            foreach (Property prop in transplantProps)
            {
                //Debug.WriteLine($"{prefix} Relink recursive on {prop.Name}");
                if (prop is StructProperty structProperty)
                {
                    relinkPropertiesRecursive(importingPCC, relinkingExport, structProperty.Properties, $"{prefix}{structProperty.Name}.", rop);
                }
                else if (prop is ArrayProperty<StructProperty> structArrayProp)
                {
                    for (int i = 0; i < structArrayProp.Count; i++)
                    {
                        StructProperty arrayStructProperty = structArrayProp[i];
                        relinkPropertiesRecursive(importingPCC, relinkingExport, arrayStructProperty.Properties, $"{prefix}{arrayStructProperty.Name}[{i}].", rop);
                    }
                }
                else if (prop is ArrayProperty<ObjectProperty> objArrayProp)
                {
                    foreach (ObjectProperty objProperty in objArrayProp)
                    {
                        int uIndex = objProperty.Value;
                        var result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, objProperty.Name, prefix, rop);
                        objProperty.Value = uIndex;
                        if (result != null)
                        {
                            rop.RelinkReport.Add(result);
                        }
                    }
                }
                else if (prop is ObjectProperty objectProperty)
                {
                    int uIndex = objectProperty.Value;
                    var result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, objectProperty.Name, prefix, rop);
                    objectProperty.Value = uIndex;
                    if (result != null)
                    {
                        rop.RelinkReport.Add(result);
                    }
                }
                else if (prop is DelegateProperty delegateProp)
                {
                    int uIndex = delegateProp.Value.ContainingObjectUIndex;
                    var result = relinkUIndex(importingPCC, relinkingExport, ref uIndex, delegateProp.Name, prefix, rop);
                    delegateProp.Value = new ScriptDelegate(uIndex, delegateProp.Value.FunctionName);
                    if (result != null)
                    {
                        rop.RelinkReport.Add(result);
                    }
                }
            }
        }

        /// <summary>
        /// Relinks a property collection, from the sourcePcc to the destinationPcc. This should only be used standalone, not as part of a full relink operation!
        /// </summary>
        /// <param name="sourcePcc">Package file we are relinking from</param>
        /// <param name="destinationPcc">Destination to relink into. If null, we will not relink - but we will still report if we have object properties.</param>
        /// <param name="props">The collection to relink</param>
        /// <param name="rop">Relinker options package to configure relink behavior</param>
        public static void RelinkPropertyCollection(IMEPackage sourcePcc, IMEPackage destinationPcc, PropertyCollection props, RelinkerOptionsPackage rop, out bool hasObjectProperties)
        {
            hasObjectProperties = false;
            foreach (Property prop in props)
            {
                //Debug.WriteLine($"{prefix} Relink recursive on {prop.Name}");
                if (prop is StructProperty structProperty)
                {
                    RelinkPropertyCollection(sourcePcc, destinationPcc, structProperty.Properties, rop, out var hasObjectProperties2);
                    hasObjectProperties |= hasObjectProperties2;
                }
                else if (prop is ArrayProperty<StructProperty> structArrayProp)
                {
                    for (int i = 0; i < structArrayProp.Count; i++)
                    {
                        StructProperty arrayStructProperty = structArrayProp[i];
                        RelinkPropertyCollection(sourcePcc, destinationPcc, arrayStructProperty.Properties, rop, out var hasObjectProperties2);
                        hasObjectProperties |= hasObjectProperties2;
                    }
                }
                else if (prop is ArrayProperty<ObjectProperty> objArrayProp)
                {
                    hasObjectProperties = true;
                    foreach (ObjectProperty objProperty in objArrayProp)
                    {
                        int uIndex = objProperty.Value;
                        var result = RelinkSingle(sourcePcc, destinationPcc, ref uIndex, rop);
                        objProperty.Value = uIndex;
                        if (!result)
                            Debugger.Break();
                    }
                }
                else if (prop is ObjectProperty objectProperty)
                {
                    if (objectProperty.Value != 0)
                    {
                        // For purposes of object properties if the value is 0 we don't need a relink.
                        hasObjectProperties = true;
                    }

                    int uIndex = objectProperty.Value;
                    var result = RelinkSingle(sourcePcc, destinationPcc, ref uIndex, rop);
                    objectProperty.Value = uIndex;
                    if (!result)
                        Debugger.Break();
                }
                else if (prop is DelegateProperty delegateProp)
                {
                    if (delegateProp.Value.ContainingObjectUIndex != 0)
                    {
                        // For purposes of delegate properties if the value is 0 we don't need a relink.
                        hasObjectProperties = true;
                    }
                    int uIndex = delegateProp.Value.ContainingObjectUIndex;
                    var result = RelinkSingle(sourcePcc, destinationPcc, ref uIndex, rop);
                    delegateProp.Value = new ScriptDelegate(uIndex, delegateProp.Value.FunctionName);
                    if (!result)
                        Debugger.Break();
                }
            }
        }

        private static bool RelinkSingle(IMEPackage sourcePackage, IMEPackage destPackage, ref int uIndex, RelinkerOptionsPackage rop)
        {
            if (destPackage == null)
                return true; // We are only testing if we have object properties. We do not perform a relink.
            if (uIndex == 0)
                return true; // We're fine.


            var sourceEntry = sourcePackage.GetEntry(uIndex);
            // See if it's in the target.
            var targetEntry = destPackage.FindEntry(sourceEntry.InstancedFullPath, sourceEntry.ClassName);
            if (targetEntry != null)
            {
                uIndex = targetEntry.UIndex;
                return true;
            }

            // Must be ported over.
            var parent = EntryExporter.PortParents(sourceEntry, destPackage, cache: rop.Cache);
            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceEntry,
                destPackage, parent, true, rop, out var newItem);
            uIndex = newItem.UIndex; // Relinked
            return true;
        }

        /// <summary>
        /// Relinks a uIndex that represents an import in the original package - that is, the UIndex is less than zero.
        /// </summary>
        /// <param name="importingPCC"></param>
        /// <param name="relinkingExport"></param>
        /// <param name="uIndex"></param>
        /// <param name="rop"></param>
        /// <returns></returns>
        private static EntryStringPair relinkImportUIndex(IMEPackage importingPCC, ExportEntry relinkingExport, ref int uIndex, string propertyName, string prefix, RelinkerOptionsPackage rop)
        {
            //objProperty is currently pointing to importingPCC as that is where we read the properties from
            int n = uIndex;
            int origvalue = n;
            //Debug.WriteLine("Relink miss, attempting JIT relink on " + n + " " + rootNode.Text);
            if (importingPCC.IsImport(n))
            {
                //Get the original import
                ImportEntry importFullName = importingPCC.GetImport(n);

                // 12/26/2023 - See if it's already in the file
                var existingEntry = relinkingExport.FileRef.FindEntry(importFullName.InstancedFullPath, importFullName.ClassName);
                if (existingEntry != null)
                {
                    uIndex = existingEntry.UIndex;
                    return null; // Relinked import to existing item in the file
                }

                string originalInstancedFullPath = importFullName.InstancedFullPath; //used to be just FullPath - but some imports are indexed!
                                                                                     //Debug.WriteLine("We should import " + origImport.GetFullPath);

                string DONOTEDIT_OriginalInstancedFullPath = originalInstancedFullPath;
                // CROSSGEN-V
                // Imports are not reliable across games (or even across a single game)
                // Check to see if this import is safe to import from,
                // if not take the export instead. Might use some disk space but maybe with better algorithm
                // We can identify master/persistent files in ME2+ and also inspect those.
                if (rop.IsCrossGame && rop.TargetGameDonorDB != null
                                    && !EntryImporter.IsSafeToImportFrom($"{importFullName.GetRootName()}.{(relinkingExport.Game == MEGame.ME1 ? "u" : "pcc")}",
                                        relinkingExport.FileRef))
                {
                    // Find an export version instead that we can import
                    var candidates = rop.TargetGameDonorDB.GetFilesContainingObject(originalInstancedFullPath, relinkingExport.FileRef.Localization);
                    if (candidates?.Any() != true)
                    {
                        // Ruh Roh
                        // Debug.WriteLine($@"No candidates for export substitution of an unsafe import: {originalInstancedFullPath}, we will port this as an import, but it may not work!");
                    }
                    else
                    {
                        bool continueConvertingToExport = true; // Should we just leave this as an import?
                        bool isForcedExport = false; // If we should append the package name to the path - but only if continueConvertingToExport = false

                        // Map the relative paths onto the game directory
                        candidates = candidates.ConvertAll(x => Path.Combine(MEDirectories.GetDefaultGamePath(relinkingExport.Game), x));

                        if (candidates.Any(x => EntryImporter.IsSafeToImportFrom(Path.GetFileName(x), relinkingExport.FileRef))) // Some things are in multiple files, like things in startup files.
                        {
                            // It's been moved, we need to change how we import to it.
                            // Depending on if it's ForcedExport or not changes how we reference it
                            continueConvertingToExport = false; // Leave as an import
                        }

                        // See if cache has any of the packages open
                        IMEPackage newSourcePackage;
                        bool closePackageOnCompletion = true;
                        if (rop.Cache != null)
                        {
                            // See if any packages are already open to avoid wasting memory
                            newSourcePackage = rop.Cache.GetFirstCachedPackage(candidates);
                            for (int index = 0; index < candidates.Count && newSourcePackage == null; index++)
                            {
                                // If db has missing file this enumerates to find the correct one
                                newSourcePackage = rop.Cache.GetCachedPackage(candidates[index], true); // Open package in the cache
                            }

                            closePackageOnCompletion = false;
                        }
                        else
                        {
                            // Just pick the first file
                            newSourcePackage = MEPackageHandler.OpenMEPackage(candidates[0]);
                        }

                        // See if target export is ForcedExport
                        var targetTest = newSourcePackage.FindExport(originalInstancedFullPath);
                        //if (targetTest == null && originalInstancedFullPath.StartsWith($"{Path.GetFileNameWithoutExtension(canddiates[0])}."))
                        //{
                        //    // Import starts with 'SFXGame.' or the like, which almost guarantees this is not a forced export...
                        //    originalInstancedFullPath = originalInstancedFullPath
                        //}
                        if (targetTest != null)
                        {
                            isForcedExport = true;
                        }

                        if (continueConvertingToExport)
                        {
                            //if (originalInstancedFullPath.Contains("EngineMaterials"))
                            //    Debugger.Break();
                            // Debug.WriteLine($@"Redirecting relink of import {originalInstancedFullPath} to pull export from {newSourcePackage.FilePath} instead");

                            // Have to kind of hack it to work
                            var newSourceUIndex = newSourcePackage.FindExport(importingPCC.GetEntry(uIndex).InstancedFullPath).UIndex;

                            var result = relinkExportUIndex(newSourcePackage, relinkingExport, ref newSourceUIndex, propertyName, prefix, rop);

                            uIndex = newSourceUIndex; // Assign the ported value from the new package source onto the original one we're going to write back to the dest package

                            if (closePackageOnCompletion)
                                newSourcePackage.Dispose();

                            return result;
                        }
                    }
                }

                // END CROSSGEN-V

                if (rop.PortImportsMemorySafe && !rop.IsCrossGame)
                {
                    if (importFullName.HasParent)
                    {
                        var parentTest = importFullName.Parent;
                        if (relinkingExport.FileRef.FindEntry(parentTest.InstancedFullPath) == null)
                        {
                            // We need to port the parent first

                            // Build the parent stack in order from top to bottom.
                            Stack<IEntry> parentStack = [];
                            var parentPointer = parentTest;
                            while (parentPointer != null)
                            {
                                parentStack.Push(parentPointer);
                                parentPointer = parentPointer.Parent;
                            }

                            while (parentStack.Count > 0)
                            {
                                var parentToEnsure = parentStack.Pop();
                                int pUindex = parentToEnsure.UIndex;
                                relinkUIndex(importingPCC, relinkingExport, ref pUindex, "Parent", null, rop);
                            }
                        }
                    }
                    ImportEntry testImport = new ImportEntry(relinkingExport.FileRef, importFullName);
                    var resolved = EntryImporter.ResolveImport(testImport, rop.Cache, fileResolver: rop.DestinationCustomImportFileResolver);
                    if (resolved == null)
                    {
                        // We failed to resolve the import in the destination. Does it resolve in the source?
                        var resolvedSource = EntryImporter.ResolveImport(importFullName, rop.Cache, fileResolver: rop.SourceCustomImportFileResolver);
                        // If port localizations memory safe is true, we don't bother checking the localization. Otherwise we port only if hte localization is None to prevent bringing in localized content.
                        if (resolvedSource != null && (rop.PortLocalizationImportsMemorySafe || resolvedSource.FileRef.Localization == MELocalization.None))
                        {
                            Debug.WriteLine($@"Failed to resolve import in destination package: {testImport.InstancedFullPath}. Porting as export instead");
                            // Todo: We probably need to support porting in from things like BIOG files due to ForcedExport.
                            var importedExport = EntryImporter.ImportExport(relinkingExport.FileRef, resolvedSource, testImport.Parent?.UIndex ?? 0, rop);
                            // Debug.WriteLine($@"Memory safe porting: Redirected import {importedExport.InstancedFullPath} to export from {resolvedSource.FileRef.FileNameNoExtension}");

                            if (!rop.CrossPackageMap.ContainsKey(importFullName))
                                rop.CrossPackageMap.Add(importFullName, importedExport); //add to mapping to speed up future relinks
                            uIndex = importedExport.UIndex;
                            // Debug.WriteLine($"Relink hit: Dynamic CrossImport for {origvalue} {importingPCC.GetEntry(origvalue).InstancedFullPath} -> {uIndex}");
                            return null; // OK
                        }
                        else
                        {
                            Debug.WriteLine($@"Failed to resolve import in destination package: {testImport.InstancedFullPath}. Not porting as export, will port as import");
                        }
                    }
                }

                IEntry crossImport = null;
                string linkFailedDueToError = null;
                try
                {
                    crossImport = EntryImporter.GetOrAddCrossImportOrPackage(originalInstancedFullPath, importingPCC, relinkingExport.FileRef, rop);
                }
                catch (Exception e)
                {
                    //Error during relink
                    linkFailedDueToError = e.Message;
                }

                if (crossImport != null)
                {
                    if (!rop.CrossPackageMap.ContainsKey(importFullName))
                    {
                        // Debug.WriteLine($"Adding to cross map: {importFullName}");
                        rop.CrossPackageMap.Add(importFullName, crossImport); //add to mapping to speed up future relinks
                    }
                    uIndex = crossImport.UIndex;
                    // Debug.WriteLine($"Relink hit: Dynamic CrossImport for {origvalue} {importingPCC.GetEntry(origvalue).InstancedFullPath} -> {uIndex}");
                    return null; // OK
                }
                else
                {
                    string path = importingPCC.GetEntry(uIndex) != null ? importingPCC.GetEntry(uIndex).InstancedFullPath : "Entry not found: " + uIndex;
                    if (linkFailedDueToError != null)
                    {
                        Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.GetEntry(origvalue).InstancedFullPath}");
                        return new EntryStringPair(relinkingExport, $"Relink failed for {prefix}{propertyName} {uIndex} in export {path}({relinkingExport.UIndex}): {linkFailedDueToError}");
                    }

                    if (relinkingExport.FileRef.GetEntry(uIndex) != null)
                    {
                        Debug.WriteLine($"Relink failed: CrossImport porting failed for {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} ({uIndex}): {importingPCC.GetEntry(origvalue).InstancedFullPath}");
                        return new EntryStringPair(relinkingExport, $"Relink failed: CrossImport porting failed for {prefix}{propertyName} {uIndex} {relinkingExport.FileRef.GetEntry(uIndex).InstancedFullPath} in export {relinkingExport.InstancedFullPath}({relinkingExport.UIndex})");
                    }

                    return new EntryStringPair(relinkingExport, $"Relink failed: New export does not exist - this is probably a bug in cross import code for {prefix}{propertyName} {uIndex} in export {relinkingExport.InstancedFullPath}({relinkingExport.UIndex})");
                }
            }

            return new EntryStringPair(relinkingExport, $"Relink failed: Provided value is negative but is not an import for {prefix}{propertyName} in export {relinkingExport.UIndex} {relinkingExport.InstancedFullPath}: {uIndex}");
        }

        /// <summary>
        /// Relinks the specified uIndex (the index in the source importingPCC that now exists in the relinkingExport's data, but needs repointed to the right data) to the correct new UIndex.
        /// </summary>
        /// <param name="importingPCC"></param>
        /// <param name="relinkingExport"></param>
        /// <param name="uIndex"></param>
        /// <param name="propertyName"></param>
        /// <param name="prefix"></param>
        /// <param name="rop">Option package for relinking</param>
        /// <returns></returns>
        private static EntryStringPair relinkUIndex(IMEPackage importingPCC, ExportEntry relinkingExport, ref int uIndex, string propertyName, string prefix, RelinkerOptionsPackage rop)
        {
            if (uIndex == 0)
            {
                return null; //do not relink 0
            }

            IMEPackage destinationPcc = relinkingExport.FileRef;
            if (!rop.ForceSamePackageImportRelink && importingPCC == destinationPcc && uIndex < 0)
            {
                return null; //do not relink same-pcc imports.
            }

            // Leave the following 4 lines for debugging
            //int sourceObjReference = uIndex;
            //if (sourceObjReference == 287)
            //    Debugger.Break();
            //Debug.WriteLine($"{prefix} Relinking:{propertyName}");
            if (rop.CrossPackageMap.TryGetValue(importingPCC.GetEntry(uIndex), out IEntry targetEntry))
            {
                //relink
                uIndex = targetEntry.UIndex;

                //Debug.WriteLine($"{prefix} Relink hit: {sourceObjReference}{propertyName} : {targetEntry.InstancedFullPath}");
            }
            else if (uIndex < 0) //It's an unmapped import
            {
                return relinkImportUIndex(importingPCC, relinkingExport, ref uIndex, propertyName, prefix, rop);
            }
            else
            {
                // It's an export
                return relinkExportUIndex(importingPCC, relinkingExport, ref uIndex, propertyName, prefix, rop);
            }
            return null;
        }

        /// <summary>
        /// Relinks a uIndex entry reference within an ExportEntry.
        /// </summary>
        /// <param name="importingPCC"></param>
        /// <param name="relinkingExport"></param>
        /// <param name="uIndex"></param>
        /// <param name="propertyName"></param>
        /// <param name="prefix"></param>
        /// <param name="rop"></param>
        /// <returns></returns>
        private static EntryStringPair relinkExportUIndex(IMEPackage importingPCC, ExportEntry relinkingExport, ref int uIndex, string propertyName, string prefix, RelinkerOptionsPackage rop)
        {
            bool importingFromGlobalFile = false;
            //It's an export
            //Attempt lookup
            ExportEntry sourceExport = importingPCC.GetUExport(uIndex);
            string instancedFullPath = sourceExport.InstancedFullPath;
            string sourceFilePath = sourceExport.FileRef.FilePath;

            // Typically global files are not ForceExport'd 
            // which means objects in them will sit at the root instead of under a package export
            if (rop.GenerateImportsForGlobalFiles && EntryImporter.IsSafeToImportFrom(sourceFilePath, relinkingExport.FileRef))
            {
                importingFromGlobalFile = true;
                instancedFullPath = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.{instancedFullPath}";
            }

            // 08/22/2024 - Change from only-class matching when allowing differing ROP option was set,
            // to checking if it's a material interface if not set, since you can (most times) swap these.
            // This addresses duplicate objects being ported in and then not being relinked if the source object
            // is referenced multiple times and gets relinked in as a different object. We only do material interface
            // because it's fairly easy to swap; others could probably be implemented here and in ImportExport().
            // To allow all items to substitute, use the ROP's RelinkAllowDifferingClassesInRelink item.
            IEntry existingEntry = FindExistingEntry(instancedFullPath, relinkingExport, sourceExport, rop);

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
            else if (rop.ImportExportDependencies)
            {
                if (importingFromGlobalFile)
                {
                    // We are porting out of a global loaded file like SFXGame - generate imports
                    uIndex = EntryImporter.GenerateEntryForGlobalFileExport(sourceExport.InstancedFullPath, importingPCC, relinkingExport.FileRef, rop).UIndex;
                }
                else
                {
                    IEntry parent = null;
                    if (sourceExport.Parent != null && !rop.CrossPackageMap.TryGetValue(sourceExport.Parent, out parent))
                    {
                        //if (sourceExport.Parent is ExportEntry parExp)
                        //{
                        //    // Parent is export
                        //    // How to find parent UIndex from here if it might not yet exist?

                        //    // Note: This doesn't work if it's nested deeper than one link we can find. Might be best to put this in a loop to ensure parent creation?

                        //    // Port parents recursively

                        //    var parParLink = parExp.Parent != null ? relinkingExport.FileRef.FindEntry(parExp.ParentInstancedFullPath) : null; // This is pretty weak...
                        //    parent = relinkingExport.FileRef.FindEntry(parExp.InstancedFullPath) ?? EntryImporter.ImportExport(relinkingExport.FileRef, parExp, parParLink?.UIndex ?? 0, true, crossPCCObjectMappingList, targetGameDB: targetGameDonorDB);
                        //}
                        //else
                        //{
                        //Parent is import
                        parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceExport.ParentInstancedFullPath, importingPCC, relinkingExport.FileRef, rop);

                        if (parent != null)
                        {
                            // The destination IFP may have changed
                            // We should check again if it exists at this destination IFP or we will add a duplicate
                            var newIFP = parent.InstancedFullPath + '.' + sourceExport.ObjectName.Instanced;
                            existingEntry = FindExistingEntry(newIFP, relinkingExport, sourceExport, rop);
                            if (existingEntry != null)
                            {
                                // Relink to existing object
                                uIndex = existingEntry.UIndex;
                                return null;
                            }
                        }
                        //}
                    }

                    if (rop.PortExportsAsImportsWhenPossible && !sourceExport.InstancedFullPath.StartsWith(@"TheWorld."))
                    {
                        // Try convert to import
                        var testImport = new ImportEntry(sourceExport, parent?.UIndex ?? 0, relinkingExport.FileRef);
                        if (EntryImporter.TryResolveImport(testImport, out var resolve, cache: rop.Cache, fileResolver: rop.DestinationCustomImportFileResolver))
                        {
                            relinkingExport.FileRef.AddImport(testImport);
                            uIndex = testImport.UIndex;
                            // Debug.WriteLine($"Redirected importable export {relinkingExport.InstancedFullPath} to import from {resolved.FileRef.FilePath}");
                            return null;
                        }
                    }

                    var importedExport = EntryImporter.ImportExport(relinkingExport.FileRef, sourceExport, parent?.UIndex ?? 0, rop);
                    if (!importedExport.InstancedFullPath.CaseInsensitiveEquals(sourceExport.InstancedFullPath))
                    {
                        // This needs to be suppressed if we are doing replace export with another
                        // as IFP will likely be different
                        //Debugger.Break();
                    }
                    uIndex = importedExport.UIndex;
                }
            }
            else
            {
                string path = importingPCC.GetEntry(uIndex)?.InstancedFullPath ?? $"Entry not found: {uIndex}";
                Debug.WriteLine($"Relink failed in {relinkingExport.ObjectName.Instanced} {relinkingExport.UIndex}: {propertyName} {uIndex} {path}");
                return new EntryStringPair(relinkingExport, $"Relink failed: {prefix}{propertyName} {uIndex} in export {relinkingExport.InstancedFullPath}({relinkingExport.UIndex})");
            }

            return null;
        }

        /// <summary>
        /// Shared code for finding existing entry in package, using lookup rules
        /// </summary>
        /// <returns></returns>
        private static IEntry FindExistingEntry(string instancedFullPath, ExportEntry relinkingExport, ExportEntry sourceExport, RelinkerOptionsPackage rop)
        {
            IEntry existingEntry = relinkingExport.FileRef.FindEntry(instancedFullPath);

            if (existingEntry != null)
            {
                if (!existingEntry.ClassName.CaseInsensitiveEquals(sourceExport.ClassName) && !rop.RelinkAllowDifferingClassesInRelink)
                {
                    bool allowSub = false;
                    // Allow substituting BioSWF -> GFxMovieInfo when porting out of ME1
                    if (existingEntry.Game > MEGame.ME1 && sourceExport.Game == MEGame.ME1 && existingEntry.IsA("GFxMovieInfo") && sourceExport.IsA("BioSWF"))
                    {
                        allowSub = true;
                    }
                    // Allow substituting materials. This prevents a lot of other bugs versus allowing anything to 
                    // be substituted since we can control it here.
                    else if (existingEntry.IsA("MaterialInterface") && sourceExport.IsA("MaterialInterface"))
                    {
                        allowSub = true;
                    }

                    if (!allowSub)
                    {
                        existingEntry = null;
                    }
                }
            }
            return existingEntry;
        }

        private static void RelinkUnhoodEntryReference(IEntry entry, long position, byte[] script, ExportEntry sourceExport, ExportEntry destinationExport, RelinkerOptionsPackage rop)
        {
            //Debug.WriteLine($"Attempting function relink on token entry reference {entry.FullPath} at position {position}");

            int uIndex = entry.UIndex;
            var relinkResult = relinkUIndex(sourceExport.FileRef, destinationExport, ref uIndex, $"Entry {entry.InstancedFullPath} at 0x{position:X8}", "", rop);
            if (relinkResult is null)
            {
                script.OverwriteRange((int)position, BitConverter.GetBytes(uIndex));
            }
            else
            {
                rop.RelinkReport.Add(relinkResult);
            }
        }

        private static void RelinkToken(Token t, byte[] script, ExportEntry sourceExport, ExportEntry destinationExport, RelinkerOptionsPackage rop)
        {
            //Debug.WriteLine($"Attempting function relink on token at position {t.pos}. Number of listed relinkable items {t.inPackageReferences.Count}");

            foreach ((int pos, int type, int value) in t.inPackageReferences)
            {
                switch (type)
                {
                    case Token.INPACKAGEREFTYPE_NAME:
                        int newValue = destinationExport.FileRef.FindNameOrAdd(sourceExport.FileRef.GetNameEntry(value));
                        //Debug.WriteLine($"Function relink hit @ 0x{t.pos + pos:X6}, cross ported a name: {sourceExport.FileRef.GetNameEntry(value)}");
                        script.OverwriteRange(pos, BitConverter.GetBytes(newValue));
                        break;
                    case Token.INPACKAGEREFTYPE_ENTRY:
                        relinkAtPosition(pos, value, $"(Script at @ 0x{t.pos + pos:X6}: {t.text})", rop);
                        break;
                }
            }

            void relinkAtPosition(int binaryPosition, int uIndex, string propertyName, RelinkerOptionsPackage ropLocal)
            {
                var relinkResult = relinkUIndex(sourceExport.FileRef, destinationExport, ref uIndex, propertyName, "", ropLocal);
                if (relinkResult is null)
                {
                    script.OverwriteRange(binaryPosition, BitConverter.GetBytes(uIndex));
                }
                else
                {
                    rop.RelinkReport.Add(relinkResult);
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

        /// <summary>
        /// Relinks objects to different ones within the same package. Will not generate new exports or imports.
        /// </summary>
        /// <param name="package">Package to operate on</param>
        /// <param name="relinkMap">List of entries that you are repointing to different objects. The remaining objects will have a relink performed to update references automatically.</param>
        public static void RelinkSamePackage(IMEPackage package, ListenableDictionary<IEntry, IEntry> relinkMap)
        {

            foreach (var exp in package.Exports)
            {
                // Relink to self - this makes it so this export will have relink performed on it for the rest of the map contents
                relinkMap.TryAdd(exp, exp);
            }

            foreach (var imp in package.Imports)
            {
                // Relink to self - this makes it so this export will have relink performed on it for the rest of the map contents
                relinkMap.TryAdd(imp, imp);
            }

            Relinker.RelinkAll(new RelinkerOptionsPackage() { CrossPackageMap = relinkMap, ForceSamePackageImportRelink = true});
        }
    }
}