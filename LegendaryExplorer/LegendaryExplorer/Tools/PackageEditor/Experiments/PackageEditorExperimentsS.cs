using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ClosedXML.Excel;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.GameInterop;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Tools.ScriptDebugger;
using LegendaryExplorer.UnrealExtensions.Classes;
using LegendaryExplorerCore;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.UDK;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using LegendaryExplorerCore.UnrealScript;
using LegendaryExplorerCore.UnrealScript.Compiling;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Decompiling;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Parsing;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using SharpDX.D3DCompiler;
using static LegendaryExplorer.Tools.ScriptDebugger.DebuggerInterface;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

#pragma warning disable CS8321 //unused function warning

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class for SirCxyrtyx experimental code
    /// </summary>
    public static class PackageEditorExperimentsS
    {
        public static IEnumerable<string> EnumerateOfficialFiles(params MEGame[] games)
        {
            foreach (MEGame game in games)
            {
                var filePaths = MELoadedFiles.GetOfficialFiles(game);
                //preload base files for faster scanning
                using var baseFiles = MEPackageHandler.OpenMEPackages(EntryImporter.FilesSafeToImportFrom(game)
                                                                                   .Select(f => Path.Combine(MEDirectories.GetCookedPath(game), f)));
                if (game is MEGame.ME3)
                {
                    baseFiles.Add(MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "BIOP_MP_COMMON.pcc")));
                }

                foreach (string filePath in filePaths)
                {
                    yield return filePath;
                }
            }
        }

        public enum GhidraTypeKind
        {
            Normal,
            Array,
            Pointer
        }

        public record GhidraProperty(string Name, string TypeName, int Offset, int ElementSize, GhidraTypeKind TypeKind = GhidraTypeKind.Normal, int NumElements = 1, int BitOffset = 0);
        public record GhidraStruct(string Name, int Size, string Super, List<GhidraProperty> Properties);
        public record GhidraEnum(string Name, List<string> Values);

        public static async void GenerateGhidraStructInsertionScript(PackageEditorWindow pewpf)
        {
            pewpf.IsBusy = true;
            pewpf.BusyText = "Generating Ghidra Scripts";

            try
            {
                foreach (MEGame game in new[] { MEGame.LE1, MEGame.LE2, MEGame.LE3 })
                {
                    if (!GameController.TryGetMEProcess(game, out Process meProcess)) continue;
                    var structDict = new Dictionary<string, GhidraStruct>
                    {
                        ["TArray"] = new("TArray", 16, null, new List<GhidraProperty>
                        {
                            new("Data", "pointer", 0, 8),
                            new("Count", "int", 8, 4),
                            new("Max", "int", 12, 4),
                        }),
                        ["FString"] = new("FString", 16, null, new List<GhidraProperty>
                        {
                            new("Data", "wchar_t", 0, 8, GhidraTypeKind.Pointer),
                            new("Count", "int", 8, 4),
                            new("Max", "int", 12, 4),
                        }),
                        ["FScriptDelegate"] = new("FScriptDelegate", 16, null, new List<GhidraProperty>
                        {
                            new("Object", "UObject", 0, 8, GhidraTypeKind.Pointer),
                            new("FunctionName", "FName", 8, 4)
                        }),
                        ["FScriptInterface"] = new("FScriptInterface", 16, null, new List<GhidraProperty>
                        {
                            new("Object", "UObject", 0, 8, GhidraTypeKind.Pointer),
                            new("Interface", "pointer", 8, 4)
                        }),
                    };
                    //this must be created on the main thread!
                    var debugger = new DebuggerInterface(game, meProcess);
                    try
                    {
                        await debugger.WaitForAttach();
                        await debugger.WaitForBreak();

                        foreach (NObject nObject in debugger.IterateGObjects())
                        {
                            if (nObject is NClass nClass && nClass.ClassFlags.Has(EClassFlags.Native))
                            {
                                AddStruct(nClass);
                            }
                            else if (nObject is NScriptStruct nScriptStruct && (nScriptStruct.StructFlags.Has(ScriptStructFlags.Native) || nScriptStruct.Outer is NClass structOuter && structOuter.ClassFlags.Has(EClassFlags.Native)))
                            {
                                AddStruct(nScriptStruct);
                            }

                            void AddStruct(NStruct nStruct)
                            {
                                string name = nStruct.CPlusPlusName(game);
                                if (structDict.ContainsKey(name))
                                {
                                    return;
                                }
                                var props = new List<GhidraProperty>();

                                if (nStruct is not NClass || !GlobalUnrealObjectInfo.IsA(nStruct.Name, "Interface", game))
                                {
                                    for (NField child = nStruct.FirstChild; child is not null; child = child.Next)
                                    {
                                        if (child is NScriptStruct nScriptStruct)
                                        {
                                            AddStruct(nScriptStruct);
                                        }
                                        else if (child is NProperty nProp)
                                        {
                                            (string typename, GhidraTypeKind typeKind) = GetTypeName(nProp);
                                            (string, GhidraTypeKind) GetTypeName(NProperty nProperty)
                                            {
                                                (string typename, GhidraTypeKind typeKind) = nProperty switch
                                                {
                                                    NArrayProperty => (null, GhidraTypeKind.Array),
                                                    NBioMask4Property => ("byte", GhidraTypeKind.Normal),
                                                    NBoolProperty => ("bool", GhidraTypeKind.Normal),
                                                    NByteProperty => ("byte", GhidraTypeKind.Normal),
                                                    NClassProperty => ("UClass", GhidraTypeKind.Pointer),
                                                    NDelegateProperty => ("FScriptDelegate", GhidraTypeKind.Normal),
                                                    NFloatProperty => ("float", GhidraTypeKind.Normal),
                                                    NInterfaceProperty => ("FScriptInterface", GhidraTypeKind.Normal),
                                                    NIntProperty => ("int", GhidraTypeKind.Normal),
                                                    NMapProperty => ("FMap_Mirror", GhidraTypeKind.Normal),
                                                    NNameProperty => ("FName", GhidraTypeKind.Normal),
                                                    //NComponentProperty
                                                    NObjectProperty nObjectProperty => (nObjectProperty.PropertyClass.CPlusPlusName(game), GhidraTypeKind.Pointer),
                                                    NStringRefProperty => ("int", GhidraTypeKind.Normal),
                                                    NStrProperty => ("FString", GhidraTypeKind.Normal),
                                                    NStructProperty nStructProperty => (nStructProperty.Struct.CPlusPlusName(game), GhidraTypeKind.Normal),
                                                    _ => throw new ArgumentOutOfRangeException(nameof(nProperty))
                                                };
                                                typename = typename switch
                                                {
                                                    "FPointer" => "pointer",
                                                    "FQWord" => "qword",
                                                    _ => typename
                                                };
                                                return (typename, typeKind);
                                            }

                                            int arrayDim = nProp.GetRealArrayDim();
                                            arrayDim = arrayDim == 0 ? 1 : arrayDim;
                                            NameReference nPropName = nProp.Name;
                                            int nPropOffset = nProp.Offset;
                                            int nPropElementSize = nProp.ElementSize;
                                            int bitOffset = 0;
                                            if (nProp is NBoolProperty nBoolProp)
                                            {
                                                bitOffset = BitOperations.TrailingZeroCount(nBoolProp.BitMask);
                                            }
                                            if (nProp is NArrayProperty nArrayProp)
                                            {
                                                (string innerPropName, GhidraTypeKind innerTypeKind) = GetTypeName(nArrayProp.InnerProperty);
                                                if (innerPropName is "FPointer")
                                                {
                                                    innerPropName = "pointer";
                                                }
                                                string fullInnerName = innerPropName;
                                                if (innerTypeKind is GhidraTypeKind.Pointer)
                                                {
                                                    fullInnerName += "*";
                                                }
                                                typename = $"TArray<{fullInnerName}>";
                                                if (!structDict.ContainsKey(typename))
                                                {
                                                    structDict[typename] = new GhidraStruct(typename, 16, null, new List<GhidraProperty>
                                                {
                                                    //incorrect technically, as this should be a pointer to the innerProp kind. Will be fixed up in the ghidra script
                                                    new("Data", innerPropName, 0, 8, innerTypeKind),
                                                    new("Count", "int", 8, 4),
                                                    new("Max", "int", 12, 4),
                                                });
                                                }
                                            }
                                            props.Add(new GhidraProperty(nPropName, typename, nPropOffset, nPropElementSize, typeKind, arrayDim, bitOffset));
                                        }
                                    }
                                }

                                var ghidraStruct = new GhidraStruct(name, nStruct.PropertySize, (nStruct.Super as NStruct)?.CPlusPlusName(game), props);
                                structDict[name] = ghidraStruct;
                            }
                        }
                    }
                    finally
                    {
                        debugger.Detach();
                        debugger.Dispose();
                    }

                    await using var fileStream = new FileStream(Path.Combine(AppDirectories.ExecFolder, $"{game}ImportStructs.java"), FileMode.Create);
                    //await using var textWriter = new StreamWriter(fileStream);
                    //await textWriter.WriteAsync(JsonConvert.SerializeObject(structDict, Formatting.Indented));
                    //continue;
                    using var writer = new CodeWriter(fileStream);
                    writer.WriteLine(
                        $@"//Imports {game} structure definitions
//@author SirCxyrtyx
//@category Data Types
//@keybinding 
//@menupath 
//@toolbar 

import ghidra.app.script.GhidraScript;
import ghidra.program.model.util.*;
import ghidra.program.model.reloc.*;
import ghidra.program.model.data.*;
import ghidra.program.model.block.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.scalar.*;
import ghidra.program.model.mem.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.lang.*;
import ghidra.program.model.pcode.*;
import ghidra.program.model.address.*;
import java.util.*;"
                    );
                    GhidraStruct[][] chunks = structDict.Values.Chunk(100).ToArray();
                    writer.WriteBlock($"public class {game}ImportStructs extends GhidraScript", () =>
                    {
                        writer.WriteLine();
                        writer.WriteLine("private static final String CATEGORY = \"/SDK_Test\";");
                        writer.WriteLine();
                        writer.WriteLine("//Can be either DataTypeConflictHandler.REPLACE_HANDLER or DataTypeConflictHandler.KEEP_HANDLER");
                        writer.WriteLine("private static final DataTypeConflictHandler CONFLICT_HANDLER = DataTypeConflictHandler.REPLACE_HANDLER;");
                        writer.WriteLine();
                        writer.WriteBlock("public void run() throws Exception", () =>
                        {
                            writer.WriteLine("DataTypeManager typeMan = currentProgram.getDataTypeManager();");
                            writer.WriteLine("var structDict = new HashMap<String, DataType>();");
                            writer.WriteLine("var categoryPath = new CategoryPath(CATEGORY);");
                            writer.WriteLine("var dwordType = typeMan.getDataType(new CategoryPath(\"/\"), \"dword\");");
                            foreach (string primitive in new[]{"pointer", "int", "byte", "byte", "float", "wchar_t", "qword"})
                            {
                                writer.WriteLine($"structDict.put(\"{primitive}\", typeMan.getDataType(new CategoryPath(\"/\"), \"{primitive}\"));");
                            }
                            writer.WriteLine();
                            writer.WriteLine("var fName = new StructureDataType(categoryPath, \"FName\", 8, typeMan);");
                            writer.WriteLine("fName.insertBitFieldAt(0, 4, 0, dwordType, 29, \"Offset\" , null);");
                            writer.WriteLine("fName.insertBitFieldAt(0, 4, 29, dwordType, 3, \"Chunk\" , null);");
                            writer.WriteLine("fName.replaceAtOffset(4, typeMan.getDataType(new CategoryPath(\"/\"), \"int\"), 4, \"Number\", null);");
                            writer.WriteLine("typeMan.addDataType(fName, CONFLICT_HANDLER);");
                            writer.WriteLine("structDict.put(\"FName\", fName);");
                            writer.WriteLine();
                            writer.WriteLine("//This is chunked because java has a limit on how large methods can be");
                            for (int i = 0; i < chunks.Length; i++)
                            {
                                writer.WriteLine($"createStructs{i}(structDict, typeMan, categoryPath);");
                            }
                            writer.WriteLine();
                            for (int i = 0; i < chunks.Length; i++)
                            {
                                writer.WriteLine($"populateStructs{i}(structDict, typeMan, dwordType);");
                            }
                        });
                        for (int i = 0; i < chunks.Length; i++)
                        {
                            GhidraStruct[] chunk = chunks[i];
                            writer.WriteBlock($"private void createStructs{i}(HashMap<String, DataType> structDict, DataTypeManager typeMan, CategoryPath categoryPath) throws Exception", () =>
                            {
                                foreach (GhidraStruct ghidraStruct in chunk)
                                {
                                    writer.WriteLine($"structDict.put(\"{ghidraStruct.Name}\", new StructureDataType(categoryPath, \"{ghidraStruct.Name}\", {ghidraStruct.Size}, typeMan));");
                                }
                            });
                        }
                        for (int i = 0; i < chunks.Length; i++)
                        {
                            GhidraStruct[] chunk = chunks[i];
                            writer.WriteBlock($"private void populateStructs{i}(HashMap<String, DataType> structDict, DataTypeManager typeMan, DataType dwordType) throws Exception", () =>
                            {
                                //populate the structs and add them to the type manager
                                writer.WriteLine("StructureDataType struct = null;");
                                foreach ((string structName, int structSize, string superName, List<GhidraProperty> properties) in chunk)
                                {
                                    writer.WriteLine($"struct = (StructureDataType)structDict.get(\"{structName}\");");
                                    if (structName.StartsWith("TArray<"))
                                    {
                                        string typeGetter = $"structDict.get(\"{properties[0].TypeName}\")";
                                        if (properties[0].TypeKind is GhidraTypeKind.Pointer)
                                        {
                                            typeGetter = $"typeMan.getPointer({typeGetter})";
                                        }
                                        writer.WriteLine($"struct.replaceAtOffset(0, typeMan.getPointer({typeGetter}), 8, \"Data\", null);");
                                        writer.WriteLine("struct.replaceAtOffset(8, structDict.get(\"int\"), 4, \"Count\", null);");
                                        writer.WriteLine("struct.replaceAtOffset(12, structDict.get(\"int\"), 4, \"Max\", null);");
                                        continue;
                                    }
                                    if (superName is not null)
                                    {
                                        GhidraStruct superStruct = structDict[superName];
                                        writer.WriteLine($"struct.replaceAtOffset(0, structDict.get(\"{superStruct.Name}\"), {superStruct.Size}, \"Super\", null);");
                                    }
                                    foreach ((string propName, string typeName, int offset, int elementSize, GhidraTypeKind ghidraTypeKind, int numElements, int bitOffset) in properties)
                                    {
                                        if (typeName == "bool")
                                        {
                                            if (numElements > 1)
                                            {
                                                Debugger.Break();
                                            }
                                            writer.WriteLine($"struct.insertBitFieldAt({offset}, 4, {bitOffset}, dwordType, 1, \"{propName}\" , null);");
                                        }
                                        else
                                        {
                                            string typeGetter = $"structDict.get(\"{typeName}\")";
                                            if (ghidraTypeKind is GhidraTypeKind.Pointer)
                                            {
                                                typeGetter = $"typeMan.getPointer({typeGetter})";
                                            }
                                            int fullSize = elementSize;
                                            if (numElements > 1)
                                            {
                                                fullSize *= numElements;
                                                typeGetter = $"new ArrayDataType({typeGetter}, {numElements}, {elementSize}, typeMan)";
                                            }
                                            writer.WriteLine($"struct.replaceAtOffset({offset}, {typeGetter}, {fullSize}, \"{propName}\", null);");
                                        }
                                    }
                                    writer.WriteLine("typeMan.addDataType(struct, CONFLICT_HANDLER);");
                                }
                            });
                        }
                    });
                }
            }
            finally
            {
                pewpf.IsBusy = false;
            }
        }

        public static async void GenerateGhidraEnumInsertionScript(PackageEditorWindow pewpf)
        {
            pewpf.IsBusy = true;
            pewpf.BusyText = "Generating Ghidra Enum Scripts";

            try
            {
                foreach (MEGame game in new[] { MEGame.LE1, MEGame.LE2, MEGame.LE3 })
                {
                    if (!GameController.TryGetMEProcess(game, out Process meProcess)) continue;
                    var enumDict = new Dictionary<string, GhidraEnum>();
                    //this must be created on the main thread!
                    var debugger = new DebuggerInterface(game, meProcess);
                    try
                    {
                        await debugger.WaitForAttach();
                        await debugger.WaitForBreak();

                        foreach (NObject nObject in debugger.IterateGObjects())
                        {
                            if (nObject is NClass nClass && nClass.ClassFlags.Has(EClassFlags.Native))
                            {
                                for (NField child = nClass.FirstChild; child is not null; child = child.Next)
                                {
                                    if (child is NEnum nEnum)
                                    {
                                        string enumName = nEnum.Name.Instanced;
                                        if (!enumDict.ContainsKey(enumName))
                                        {
                                            enumDict.Add(enumName, new GhidraEnum(enumName, nEnum.Names.Select(fName => debugger.GetNameReference(fName).Instanced).ToList()));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        debugger.Detach();
                        debugger.Dispose();
                    }

                    await using var fileStream = new FileStream(Path.Combine(AppDirectories.ExecFolder, $"{game}ImportEnums.java"), FileMode.Create);
                    //await using var textWriter = new StreamWriter(fileStream);
                    //await textWriter.WriteAsync(JsonConvert.SerializeObject(structDict, Formatting.Indented));
                    //continue;
                    using var writer = new CodeWriter(fileStream);
                    writer.WriteLine(
                        $@"//Imports {game} enum definitions
//@author SirCxyrtyx
//@category Data Types
//@keybinding 
//@menupath 
//@toolbar 

import ghidra.app.script.GhidraScript;
import ghidra.program.model.util.*;
import ghidra.program.model.reloc.*;
import ghidra.program.model.data.*;
import ghidra.program.model.block.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.scalar.*;
import ghidra.program.model.mem.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.lang.*;
import ghidra.program.model.pcode.*;
import ghidra.program.model.address.*;
import java.util.*;"
                    );
                    GhidraEnum[][] chunks = enumDict.Values.Chunk(100).ToArray();
                    writer.WriteBlock($"public class {game}ImportEnums extends GhidraScript", () =>
                    {
                        writer.WriteLine();
                        writer.WriteLine("private static final String CATEGORY = \"/SDK_Test\";");
                        writer.WriteLine();
                        writer.WriteLine("//Can be either DataTypeConflictHandler.REPLACE_HANDLER or DataTypeConflictHandler.KEEP_HANDLER");
                        writer.WriteLine("private static final DataTypeConflictHandler CONFLICT_HANDLER = DataTypeConflictHandler.REPLACE_HANDLER;");
                        writer.WriteLine();
                        writer.WriteBlock("public void run() throws Exception", () =>
                        {
                            writer.WriteLine("DataTypeManager typeMan = currentProgram.getDataTypeManager();");
                            writer.WriteLine("var structDict = new HashMap<String, DataType>();");
                            writer.WriteLine("var categoryPath = new CategoryPath(CATEGORY);");
                            writer.WriteLine();
                            writer.WriteLine();
                            writer.WriteLine("//This is chunked because java has a limit on how large methods can be");
                            for (int i = 0; i < chunks.Length; i++)
                            {
                                writer.WriteLine($"createEnums{i}(typeMan, categoryPath);");
                            }
                        });
                        for (int i = 0; i < chunks.Length; i++)
                        {
                            GhidraEnum[] chunk = chunks[i];
                            writer.WriteBlock($"private void createEnums{i}(DataTypeManager typeMan, CategoryPath categoryPath) throws Exception", () =>
                            {
                                writer.WriteLine("EnumDataType enumDef;");
                                foreach (GhidraEnum ghidraEnum in chunk)
                                {
                                    writer.WriteLine($"enumDef = new EnumDataType(categoryPath, \"{ghidraEnum.Name}\", 1, typeMan);");
                                    for (int j = 0; j < ghidraEnum.Values.Count; j++)
                                    {
                                        writer.WriteLine($"enumDef.add(\"{ghidraEnum.Values[j]}\", {j});");
                                    }
                                    writer.WriteLine("typeMan.addDataType(enumDef, CONFLICT_HANDLER);");
                                }
                            });
                        }
                    });
                }
            }
            finally
            {
                pewpf.IsBusy = false;
                MessageBox.Show("Done!");
            }
        }

        public static void PortShadowMaps(PackageEditorWindow pewpf)
        {
            var pcc = pewpf.Pcc;

            pewpf.IsBusy = true;
            pewpf.BusyText = $"Porting Shadow Maps from OT file.";
            if (pcc?.Game is not MEGame.LE3)
            {
                MessageBox.Show("This is only designed to work on LE3!");
                return;
            }

            string fileName = Path.GetFileName(pcc.FilePath);
            if (!MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3).TryGetValue(fileName, out string otFilePath))
            {
                MessageBox.Show($"Could not find ME3 version of {fileName}!");
                return;
            }
            Task.Run(() =>
            {
                var levelExport = pcc.FindExport("TheWorld.PersistentLevel");
                var levelBin = levelExport.GetBinaryData<Level>();
                levelBin.TextureToInstancesMap.RemoveAll(kvp => pcc.GetEntry(kvp.Key) is ExportEntry exp && exp.ClassName.CaseInsensitiveEquals("ShadowMapTexture2D"));
                var shadowMapsToTrash = pcc.Exports.Where(exp => exp.ClassName.CaseInsensitiveEquals("ShadowMapTexture2D")).ToList();
                EntryPruner.TrashEntriesAndDescendants(shadowMapsToTrash);

                using IMEPackage otPcc = MEPackageHandler.OpenME3Package(otFilePath);
                foreach (int uIndex in levelBin.Actors)
                {
                    if (pcc.GetEntry(uIndex) is ExportEntry actor)
                    {
                        if (actor.ClassName.CaseInsensitiveEquals("StaticMeshActor") && actor.GetProperty<ObjectProperty>("StaticMeshComponent") is ObjectProperty smcProp && smcProp.ResolveToEntry(pcc) is ExportEntry smcExp)
                        {
                            var otsmcExp = otPcc.FindExport(smcExp.InstancedFullPath);

                            PortShadowMap(otsmcExp, smcExp);
                        }
                        else if (actor.ClassName.CaseInsensitiveEquals("StaticMeshCollectionActor") && actor.GetProperty<ArrayProperty<ObjectProperty>>("StaticMeshComponents") is { } smcArray)
                        {
                            foreach (ObjectProperty objProp in smcArray)
                            {
                                if (objProp.ResolveToEntry(pcc) is ExportEntry { ObjectName.Instanced: var smcName } smcExport && otPcc.Exports.FirstOrDefault(exp => exp.ObjectName.Instanced.CaseInsensitiveEquals(smcName)) is ExportEntry otsmcExport)
                                {
                                    PortShadowMap(otsmcExport, smcExport);
                                }
                            }
                        }
                    }
                }

                var otLevelExport = otPcc.FindExport("TheWorld.PersistentLevel");
                var otLevelTexToInst = otLevelExport.GetBinaryData<Level>().TextureToInstancesMap;
                foreach ((int key, StreamableTextureInstanceList value) in otLevelTexToInst)
                {
                    if (otPcc.GetEntry(key) is ExportEntry exp && exp.ClassName.CaseInsensitiveEquals("ShadowMapTexture2D"))
                    {
                        levelBin.TextureToInstancesMap.Add(pcc.FindExport(exp.InstancedFullPath).UIndex, value);
                    }
                }

                levelExport.WriteBinary(levelBin);
                levelExport.WriteProperty(otLevelExport.GetProperty<FloatProperty>("ShadowmapTotalSize"));

                void PortShadowMap(ExportEntry otsmcExp, ExportEntry smcExp)
                {
                    if (otsmcExp.GetProperty<ArrayProperty<StructProperty>>("IrrelevantLights") is { } irrelevantLightsProp)
                    {
                        smcExp.WriteProperty(irrelevantLightsProp);
                    }

                    var otLodData = otsmcExp.GetBinaryData<StaticMeshComponent>().LODData;
                    if (otLodData.IsEmpty())
                    {
                        return;
                    }
                    var otShadowMaps = otLodData[0].ShadowMaps;
                    if (otShadowMaps.IsEmpty())
                    {
                        return;
                    }

                    var smcBin = smcExp.GetBinaryData<StaticMeshComponent>();
                    if (smcBin.LODData.Length != 1)
                    {
                        Debugger.Break();
                    }

                    var shadowMaps = smcBin.LODData[0].ShadowMaps;
                    if (shadowMaps.Length > 1)
                    {
                        Debugger.Break();
                    }

                    if (shadowMaps.Length == 1)
                    {
                        EntryPruner.TrashEntryAndDescendants(pcc.GetEntry(shadowMaps[0]));
                    }

                    var rop = new RelinkerOptionsPackage
                    {
                        Cache = null, // Maintains original behavior of this func (09/30/2021: Change to RelinkerOptionsPackage)
                        ImportExportDependencies = true,
                    };

                    var results = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies,
                        otPcc.GetEntry(otShadowMaps[0]), pcc, smcExp, true, rop, out IEntry leShadowMap);
                    if (results?.Count > 0)
                    {
                        Debugger.Break();
                    }

                    smcBin.LODData[0].ShadowMaps = new[] { leShadowMap.UIndex };
                    smcExp.WriteBinary(smcBin);
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                pewpf.IsBusy = false;
                MessageBox.Show("Done!");
            });
        }

        class ClassProbeInfo
        {
            public EProbeFunctions ProbeMask;
            public HashSet<string> Functions;
        }

        //Does not work!
        public static void CalculateProbeNames(PackageEditorWindow pewpf)
        {
            const MEGame game = MEGame.LE3;
            pewpf.IsBusy = true;
            pewpf.BusyText = $"Calculating Probe functions for {game}";
            Task.Run(() =>
            {
                var classDict = new CaseInsensitiveDictionary<ClassProbeInfo>();
                foreach (string filePath in EnumerateOfficialFiles(game))
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    foreach (ExportEntry export in pcc.Exports.Where(exp => exp.IsClass))
                    {
                        if (classDict.ContainsKey(export.ObjectName.Instanced))
                        {
                            continue;
                        }
                        var objBin = export.GetBinaryData<UClass>();
                        var info = new ClassProbeInfo
                        {
                            ProbeMask = objBin.ProbeMask,
                            Functions = new HashSet<string>(objBin.VirtualFunctionTable.Length)
                        };
                        foreach (int uIndex in objBin.VirtualFunctionTable)
                        {
                            if (pcc.GetEntry(uIndex) is IEntry entry)
                            {
                                info.Functions.Add(entry.ObjectName);
                            }
                        }
                        classDict.Add(export.ObjectName.Instanced, info);
                    }
                }

                var sb = new StringBuilder();
                foreach (EProbeFunctions probeFunc in Enums.GetValues<EProbeFunctions>())
                {
                    HashSet<string> funcSet = null;
                    var exclusionSet = new HashSet<string>();
                    foreach ((string className, ClassProbeInfo info) in classDict)
                    {
                        if (info.ProbeMask.Has(probeFunc))
                        {
                            if (funcSet == null)
                            {
                                funcSet = new HashSet<string>();
                                funcSet.UnionWith(info.Functions);
                            }
                            else
                            {
                                funcSet.IntersectWith(info.Functions);
                            }
                        }
                        else
                        {
                            exclusionSet.UnionWith(info.Functions);
                        }
                    }

                    funcSet ??= new HashSet<string>();
                    funcSet.ExceptWith(exclusionSet);
                    sb.Append(probeFunc.ToString());
                    sb.Append(": ");
                    sb.AppendJoin(", ", funcSet);
                    sb.AppendLine();
                }
                File.WriteAllText(@"D:\Exports\LE3ProbeFuncs.txt", sb.ToString());
            }).ContinueWithOnUIThread(prevTask =>
            {
                //the base files will have been in memory for so long at this point that they take a looong time to clear out automatically, so force it.
                MemoryAnalyzer.ForceFullGC(true);
                pewpf.IsBusy = false;
                MessageBox.Show("Done!");
            });
        }

        public static void ReSerializeAllObjectBinary(PackageEditorWindow pewpf)
        {
            pewpf.IsBusy = true;
            pewpf.BusyText = "Re-serializing all binary in LE1";
            var interestingExports = new List<EntryStringPair>();
            var comparisonDict = new Dictionary<string, (byte[] original, byte[] newData)>();
            var classesMissingObjBin = new List<string>();
            Task.Run(() =>
            {
                foreach (string filePath in EnumerateOfficialFiles(MEGame.LE1/*, MEGame.LE2, MEGame.LE3*/))
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    foreach (ExportEntry export in pcc.Exports)
                    {
                        try
                        {
                            if (ObjectBinary.From(export) is ObjectBinary bin)
                            {
                                var original = export.Data;
                                export.WriteBinary(bin);
                                if (!export.DataReadOnly.SequenceEqual(original))
                                {
                                    interestingExports.Add(export);
                                    comparisonDict.Add($"{export.UIndex} {export.FileRef.FilePath}", (original, export.Data));
                                }
                            }
                            else
                            {
                                // Binary class is not defined for this
                                // Check to make sure there is in fact no binary so 
                                // we aren't missing anything
                                if (export.propsEnd() != export.DataSize && !classesMissingObjBin.Contains(export.ClassName))
                                {
                                    classesMissingObjBin.Add(export.ClassName);
                                    interestingExports.Add(new EntryStringPair($"{export.ClassName} Export has data after properties but no objectbinary class exists for this "));
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            interestingExports.Add(new EntryStringPair(export, e.Message));
                        }
                    }

                    // Uncomment this if you don't want it to do a lot before stopping
                    //if (interestingExports.Count >= 2)
                    //{
                    //    return;
                    //}
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                //the base files will have been in memory for so long at this point that they take a looong time to clear out automatically, so force it.
                MemoryAnalyzer.ForceFullGC(true);
                pewpf.IsBusy = false;
                var listDlg = new ListDialog(interestingExports, "Interesting Exports", "", pewpf)
                {
                    DoubleClickEntryHandler = entryItem =>
                    {
                        if (entryItem?.Entry is IEntry entryToSelect)
                        {
                            var p = new PackageEditorWindow();
                            p.Show();
                            p.LoadFile(entryToSelect.FileRef.FilePath, entryToSelect.UIndex);
                            p.Activate();
                            if (comparisonDict.TryGetValue($"{entryToSelect.UIndex} {entryToSelect.FileRef.FilePath}", out (byte[] original, byte[] newData) val))
                            {
                                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "original.bin"), val.original);
                                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "new.bin"), val.newData);
                            }
                        }
                    }
                };
                listDlg.Show();
            });
        }

        public static void ReSerializeAllObjectBinaryInFile(PackageEditorWindow pewpf)
        {
            pewpf.IsBusy = true;
            pewpf.BusyText = "Re-serializing all binary in file";
            var interestingExports = new List<EntryStringPair>();
            var comparisonDict = new Dictionary<string, (byte[] original, byte[] newData)>();
            var classesMissingObjBin = new List<string>();
            Task.Run(() =>
            {
                var sw = new Stopwatch();
                sw.Start();
                foreach (ExportEntry export in pewpf.Pcc.Exports)
                {
                    try
                    {
                        if (ObjectBinary.From(export) is ObjectBinary bin)
                        {
                            var original = export.Data;
                            export.WriteBinary(bin);
                            if (!export.DataReadOnly.SequenceEqual(original))
                            {
                                interestingExports.Add(export);
                                comparisonDict.Add($"{export.UIndex} {export.FileRef.FilePath}", (original, export.Data));
                            }
                        }
                        else
                        {
                            // Binary class is not defined for this
                            // Check to make sure there is in fact no binary so 
                            // we aren't missing anything
                            if (export.propsEnd() != export.DataSize && !classesMissingObjBin.Contains(export.ClassName))
                            {
                                classesMissingObjBin.Add(export.ClassName);
                                interestingExports.Add(new EntryStringPair($"{export.ClassName} Export has data after properties but no objectbinary class exists for this "));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        interestingExports.Add(new EntryStringPair(export, e.Message));
                    }
                }
                sw.Stop();
                interestingExports.Insert(0, new EntryStringPair($"{sw.ElapsedMilliseconds}ms"));
            }).ContinueWithOnUIThread(prevTask =>
            {
                //the base files will have been in memory for so long at this point that they take a looong time to clear out automatically, so force it.
                MemoryAnalyzer.ForceFullGC(true);
                pewpf.IsBusy = false;
                var listDlg = new ListDialog(interestingExports, "Interesting Exports", "", pewpf)
                {
                    DoubleClickEntryHandler = entryItem =>
                    {
                        if (entryItem?.Entry is IEntry entryToSelect)
                        {
                            var p = new PackageEditorWindow();
                            p.Show();
                            p.LoadFile(entryToSelect.FileRef.FilePath, entryToSelect.UIndex);
                            p.Activate();
                            if (comparisonDict.TryGetValue($"{entryToSelect.UIndex} {entryToSelect.FileRef.FilePath}", out (byte[] original, byte[] newData) val))
                            {
                                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "original.bin"), val.original);
                                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "new.bin"), val.newData);
                            }
                        }
                    }
                };
                listDlg.Show();
            });
        }

        public static void ReSerializeAllProperties(PackageEditorWindow pewpf)
        {
            pewpf.IsBusy = true;
            pewpf.BusyText = "Re-serializing all properties in LE";
            var interestingExports = new List<EntryStringPair>();
            Task.Run(() =>
            {
                foreach (string filePath in EnumerateOfficialFiles(MEGame.LE1, MEGame.LE2, MEGame.LE3))
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    foreach (ExportEntry export in pcc.Exports)
                    {
                        try
                        {
                            var original = export.Data;
                            PropertyCollection props = export.GetProperties();
                            export.WriteProperties(props);
                            if (!export.DataReadOnly.SequenceEqual(original))
                            {
                                interestingExports.Add(export);
                            }
                        }
                        catch (Exception e)
                        {
                            interestingExports.Add(new EntryStringPair(export, e.Message));
                        }
                    }

                    //if (interestingExports.Count >= 2)
                    //{
                    //    return;
                    //}
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                //the base files will have been in memory for so long at this point that they take a looong time to clear out automatically, so force it.
                MemoryAnalyzer.ForceFullGC(true);
                pewpf.IsBusy = false;
                var listDlg = new ListDialog(interestingExports, "Interesting Exports", "", pewpf)
                {
                    DoubleClickEntryHandler = entryItem =>
                    {
                        if (entryItem?.Entry is IEntry entryToSelect)
                        {
                            var p = new PackageEditorWindow();
                            p.Show();
                            p.LoadFile(entryToSelect.FileRef.FilePath, entryToSelect.UIndex);
                            p.Activate();
                        }
                    }
                };
                listDlg.Show();
            });
        }

        record CompressionData(string filePath, int compressedSize, int uncompressedSize);

        public static void CompileCompressionStats(PackageEditorWindow pewpf)
        {
            pewpf.IsBusy = true;
            pewpf.BusyText = "Compiling statistics on file compression";
            var zLibData = new List<CompressionData>();
            var lzoData = new List<CompressionData>();
            var oodleData = new List<CompressionData>();
            Task.Run(() =>
            {
                foreach (MEGame game in new[] { MEGame.LE1, MEGame.LE2, MEGame.LE3 })//, MEGame.ME1, MEGame.ME2, MEGame.ME3})
                {
                    var filePaths = MELoadedFiles.GetOfficialFiles(game);
                    foreach (string filePath in filePaths)
                    {
                        using var fs = File.OpenRead(filePath);
                        EndianReader raw = EndianReader.SetupForPackageReading(fs);

                        #region Header

                        raw.SkipInt32(); //skip magic as we have already read it
                        var versionLicenseePacked = raw.ReadUInt32();

                        raw.ReadInt32();
                        int foldernameStrLen = raw.ReadInt32();
                        if (foldernameStrLen > 0)
                            fs.ReadStringLatin1Null(foldernameStrLen);
                        else
                            fs.ReadStringUnicodeNull(foldernameStrLen * -2);

                        var Flags = (UnrealFlags.EPackageFlags)raw.ReadUInt32();

                        if ((game == MEGame.ME3 || game == MEGame.LE3)
                         && Flags.HasFlag(UnrealFlags.EPackageFlags.Cooked))
                        {
                            raw.ReadInt32();
                        }

                        var NameCount = raw.ReadInt32();
                        var NameOffset = raw.ReadInt32();
                        var ExportCount = raw.ReadInt32();
                        var ExportOffset = raw.ReadInt32();
                        var ImportCount = raw.ReadInt32();
                        var ImportOffset = raw.ReadInt32();

                        if (game.IsLEGame() || (game != MEGame.ME1))
                        {
                            raw.ReadInt32();
                        }

                        if (game.IsLEGame() || game == MEGame.ME3)
                        {
                            raw.ReadInt32();
                            raw.ReadInt32(); //ImportGuidsCount always 0
                            raw.ReadInt32(); //ExportGuidsCount always 0
                            raw.ReadInt32(); //ThumbnailTableOffset always 0
                        }

                        raw.ReadGuid();

                        uint generationsTableCount = raw.ReadUInt32();
                        if (generationsTableCount > 0)
                        {
                            generationsTableCount--;
                            raw.ReadInt32();
                            raw.ReadInt32();
                            raw.ReadInt32();
                        }
                        //should never be more than 1 generation, but just in case
                        raw.Skip(generationsTableCount * 12);

                        raw.SkipInt32(); //engineVersion          Like unrealVersion and licenseeVersion, these 2 are determined by what game this is,
                        raw.SkipInt32(); //cookedContentVersion   so we don't have to read them in

                        if ((game == MEGame.ME2 || game == MEGame.ME1)) //PS3 on ME3 engine
                        {
                            raw.SkipInt32(); //always 0
                            raw.SkipInt32(); //always 47699
                            raw.ReadInt32();
                            raw.SkipInt32(); //always 1 in ME1, always 1966080 in ME2
                        }

                        raw.ReadInt32(); // Build 
                        raw.ReadInt32(); // Branch - always -1 in ME1 and ME2, always 145358848 in ME3

                        if (game == MEGame.ME1)
                        {
                            raw.SkipInt32(); //always -1
                        }

                        #endregion

                        //COMPRESSION AND COMPRESSION CHUNKS
                        var compressionType = (UnrealPackageFile.CompressionType)raw.ReadUInt32();

                        if (compressionType is UnrealPackageFile.CompressionType.None)
                        {
                            continue;
                        }

                        var NumChunks = raw.ReadInt32();
                        int compressedSize = 0;
                        int uncompressedSize = 0;
                        for (int i = 0; i < NumChunks; i++)
                        {
                            raw.ReadInt32();
                            uncompressedSize += raw.ReadInt32();
                            raw.ReadInt32();
                            compressedSize += raw.ReadInt32();
                        }

                        switch (compressionType)
                        {
                            case UnrealPackageFile.CompressionType.Zlib:
                                zLibData.Add(new CompressionData(filePath, compressedSize, uncompressedSize));
                                break;
                            case UnrealPackageFile.CompressionType.LZO:
                                lzoData.Add(new CompressionData(filePath, compressedSize, uncompressedSize));
                                break;
                            case UnrealPackageFile.CompressionType.OodleLeviathan:
                                oodleData.Add(new CompressionData(filePath, compressedSize, uncompressedSize));
                                break;
                        }
                    }
                }

                var xl = new XLWorkbook();
                var oodleWS = xl.AddWorksheet("Oodle");
                var lzoWS = xl.AddWorksheet("lzo");
                var zlibWS = xl.AddWorksheet("zlib");
                foreach ((List<CompressionData> data, IXLWorksheet ws) in new[] { oodleData, lzoData, zLibData }.Zip(new[] { oodleWS, lzoWS, zlibWS }))
                {
                    ws.Cell(1, 1).SetValue("Compressed Size");
                    ws.Cell(1, 2).SetValue("Uncompressed Size");
                    ws.Cell(1, 3).SetValue("Path");
                    int i = 2;
                    foreach ((string filePath, int compressedSize, int uncompressedSize) in data)
                    {
                        ws.Cell(i, 1).SetValue(compressedSize);
                        ws.Cell(i, 2).SetValue(uncompressedSize);
                        ws.Cell(i, 3).SetValue(filePath);
                        ++i;
                    }
                }
                xl.SaveAs(Path.Combine(AppDirectories.ExecFolder, "CompressionStats.xlsx"));
            }).ContinueWithOnUIThread(prevTask =>
            {
                pewpf.IsBusy = false;
            });
        }

        public static void ScanPackageHeader(PackageEditorWindow pewpf)
        {
            pewpf.IsBusy = true;
            pewpf.BusyText = "Scanning Package Headers";
            var buildData = new Dictionary<MEGame, HashSet<int>>();
            var branchData = new Dictionary<MEGame, HashSet<int>>();
            Task.Run(() =>
            {
                foreach (MEGame game in new[] { MEGame.LE1, MEGame.LE2, MEGame.LE3 })//, MEGame.ME1, MEGame.ME2, MEGame.ME3})
                {
                    var buildSet = new HashSet<int>();
                    buildData.Add(game, buildSet);
                    var branchSet = new HashSet<int>();
                    branchData.Add(game, branchSet);
                    var filePaths = MELoadedFiles.GetOfficialFiles(game);
                    foreach (string filePath in filePaths)
                    {
                        using var fs = File.OpenRead(filePath);
                        EndianReader raw = EndianReader.SetupForPackageReading(fs);

                        #region Header

                        raw.SkipInt32(); //skip magic as we have already read it
                        var versionLicenseePacked = raw.ReadUInt32();

                        raw.ReadInt32();
                        int foldernameStrLen = raw.ReadInt32();
                        if (foldernameStrLen > 0)
                            fs.ReadStringLatin1Null(foldernameStrLen);
                        else
                            fs.ReadStringUnicodeNull(foldernameStrLen * -2);

                        var Flags = (UnrealFlags.EPackageFlags)raw.ReadUInt32();

                        if ((game == MEGame.ME3 || game == MEGame.LE3)
                         && Flags.HasFlag(UnrealFlags.EPackageFlags.Cooked))
                        {
                            raw.ReadInt32();
                        }

                        var NameCount = raw.ReadInt32();
                        var NameOffset = raw.ReadInt32();
                        var ExportCount = raw.ReadInt32();
                        var ExportOffset = raw.ReadInt32();
                        var ImportCount = raw.ReadInt32();
                        var ImportOffset = raw.ReadInt32();

                        if (game.IsLEGame() || (game != MEGame.ME1))
                        {
                            raw.ReadInt32();
                        }

                        if (game.IsLEGame() || game == MEGame.ME3)
                        {
                            raw.ReadInt32();
                            raw.ReadInt32(); //ImportGuidsCount always 0
                            raw.ReadInt32(); //ExportGuidsCount always 0
                            raw.ReadInt32(); //ThumbnailTableOffset always 0
                        }

                        raw.ReadGuid();

                        uint generationsTableCount = raw.ReadUInt32();
                        if (generationsTableCount > 0)
                        {
                            generationsTableCount--;
                            raw.ReadInt32();
                            raw.ReadInt32();
                            raw.ReadInt32();
                        }
                        //should never be more than 1 generation, but just in case
                        raw.Skip(generationsTableCount * 12);

                        raw.SkipInt32(); //engineVersion          Like unrealVersion and licenseeVersion, these 2 are determined by what game this is,
                        raw.SkipInt32(); //cookedContentVersion   so we don't have to read them in

                        if ((game == MEGame.ME2 || game == MEGame.ME1)) //PS3 on ME3 engine
                        {
                            raw.SkipInt32(); //always 0
                            raw.SkipInt32(); //always 47699
                            raw.ReadInt32();
                            raw.SkipInt32(); //always 1 in ME1, always 1966080 in ME2
                        }

                        int build = raw.ReadInt32(); // Build 
                        int branch = raw.ReadInt32(); // Branch - always -1 in ME1 and ME2, always 145358848 in ME3
                        buildSet.Add(build);
                        branchSet.Add(branch);
                        continue;

                        if (game == MEGame.ME1)
                        {
                            raw.SkipInt32(); //always -1
                        }

                        #endregion

                        //COMPRESSION AND COMPRESSION CHUNKS
                        var compressionType = (UnrealPackageFile.CompressionType)raw.ReadUInt32();

                        if (compressionType is UnrealPackageFile.CompressionType.None)
                        {
                            continue;
                        }

                        var NumChunks = raw.ReadInt32();
                        int compressedSize = 0;
                        int uncompressedSize = 0;
                        for (int i = 0; i < NumChunks; i++)
                        {
                            raw.ReadInt32();
                            uncompressedSize += raw.ReadInt32();
                            raw.ReadInt32();
                            compressedSize += raw.ReadInt32();
                        }
                    }
                }

                return (branchData, buildData);
            }).ContinueWithOnUIThread(prevTask =>
            {
                pewpf.IsBusy = false;
                new ListDialog(new[]
                {
                    $"build:\n {string.Join('\n', prevTask.Result.buildData.Select(kvp => $"{kvp.Key}: {string.Join(',', kvp.Value)}"))}",
                    $"branc:\n {string.Join('\n', prevTask.Result.branchData.Select(kvp => $"{kvp.Key}: {string.Join(',', kvp.Value)}"))}",
                }, "", "", pewpf).Show();
            });
        }

        class OpcodeInfo
        {
            public readonly HashSet<string> PropTypes = new();
            public readonly HashSet<string> PropLocations = new();

            public readonly List<(string filePath, int uIndex, int position)> Usages = new();
        }
        public static void ScanStuff(PackageEditorWindow pewpf)
        {
            ////test pcc deserialization time
            //string pccPath = MELoadedFiles.GetFilesLoadedInGame(MEGame.LE3)["SFXGame.pcc"];
            //for (int i = 0; i < 200; i++)
            //{
            //    MEPackageHandler.OpenMEPackage(pccPath, forceLoadFromDisk: true);
            //}
            //return;
            //var game = MEGame.LE3;
            //var filePaths = MELoadedFiles.GetOfficialFiles(game);//.Concat(MELoadedFiles.GetOfficialFiles(MEGame.ME2));//.Concat(MELoadedFiles.GetOfficialFiles(MEGame.ME1));
            //var filePaths = MELoadedFiles.GetAllFiles(game);
            /*"Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc" */
            //var filePaths = new[] { "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc" }.Select(f => Path.Combine(ME3Directory.CookedPCPath, f));
            var interestingExports = new List<EntryStringPair>();
            var foundClasses = new HashSet<string>(); //new HashSet<string>(BinaryInterpreterWPF.ParsableBinaryClasses);
            var foundProps = new Dictionary<string, string>();

            var unkOpcodes = new List<int>();//Enumerable.Range(0x5B, 8).ToList();
            unkOpcodes.Add(0);
            unkOpcodes.Add(1);
            var unkOpcodesInfo = unkOpcodes.ToDictionary(i => i, i => new OpcodeInfo());
            var comparisonDict = new Dictionary<string, (byte[] original, byte[] newData)>();

            var extraInfo = new HashSet<string>();

            pewpf.IsBusy = true;
            pewpf.BusyText = "Scanning";
            Task.Run(() =>
            {
                foreach (MEGame game in new[] { MEGame.LE3, MEGame.LE2, MEGame.LE1, /*MEGame.ME3, MEGame.ME2, MEGame.ME1*/ })
                {
                    //preload base files for faster scanning
                    using (DisposableCollection<IMEPackage> baseFiles = MEPackageHandler.OpenMEPackages(EntryImporter.FilesSafeToImportFrom(game)
                               .Select(f => Path.Combine(MEDirectories.GetCookedPath(game), f))))
                    using (var packageCache = new PackageCache {CacheMaxSize = 20})
                    {
                        packageCache.InsertIntoCache(baseFiles);
                        if (game == MEGame.ME3)
                        {
                            baseFiles.Add(MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "BIOP_MP_COMMON.pcc")));
                        }

                        foreach (string filePath in EnumerateOfficialFiles(game))
                        {
                            //ScanShaderCache(filePath);
                            //ScanMaterials(filePath);
                            //ScanStaticMeshComponents(filePath);
                            //ScanLightComponents(filePath);
                            //ScanLevel(filePath);
                            //if (findClass(filePath, "ShaderCache", true)) break;
                            //findClassesWithBinary(filePath);
                            //ScanScripts2(filePath);
                            //RecompileAllFunctions(filePath);
                            //RecompileAllStates(filePath);
                            //RecompileAllDefaults(filePath, packageCache);
                            RecompileAllPropsOfNonScriptExports(filePath, packageCache);
                            //RecompileAllStructs(filePath, packageCache);
                            //RecompileAllEnums(filePath, packageCache);
                            //RecompileAllClasses(filePath, packageCache);
                        }
                    }
                    //the base files will have been in memory for so long at this point that they take a looong time to clear out automatically, so force it.
                    MemoryAnalyzer.ForceFullGC();
                    if (interestingExports.Any())
                    {
                        return;
                    }
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                pewpf.IsBusy = false;
                if (extraInfo.Count > 0)
                {
                    interestingExports.Add(new EntryStringPair(string.Join("\n", extraInfo)));
                }
                var listDlg = new ListDialog(interestingExports, $" {interestingExports.Count} Interesting Exports", "", pewpf)
                {
                    DoubleClickEntryHandler = entryItem =>
                    {
                        if (entryItem?.Entry is IEntry entryToSelect)
                        {
                            var p = new PackageEditorWindow();
                            p.Show();
                            p.LoadFile(entryToSelect.FileRef.FilePath, entryToSelect.UIndex);
                            p.Activate();
                            p = new PackageEditorWindow();
                            p.Show();
                            p.LoadFile(entryToSelect.FileRef.FilePath, entryToSelect.UIndex);
                            if (comparisonDict.TryGetValue($"{entryToSelect.UIndex} {entryToSelect.FileRef.FilePath}", out (byte[] original, byte[] newData) val))
                            {
                                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "original.bin"), val.original);
                                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "new.bin"), val.newData);
                            }
                        }
                    }
                };
                listDlg.Show();
            });

            #region extra scanning functions

            bool findClass(string filePath, string className, bool withBinary = false)
            {
                Debug.WriteLine($" {filePath}");
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath))
                {
                    //if (!pcc.IsCompressed) return false;

                    var exports = pcc.Exports.Where(exp => !exp.IsDefaultObject && exp.IsA(className));
                    foreach (ExportEntry exp in exports)
                    {
                        try
                        {
                            //Debug.WriteLine($"{exp.UIndex}: {filePath}");
                            var originalData = exp.Data;
                            exp.WriteBinary(ObjectBinary.From(exp));
                            var newData = exp.Data;
                            if (!originalData.SequenceEqual(newData))
                            {
                                interestingExports.Add(exp);
                                File.WriteAllBytes(
                                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                        "original.bin"), originalData);
                                File.WriteAllBytes(
                                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                        "new.bin"), newData);
                                return true;
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            interestingExports.Add(new EntryStringPair(exp, $"{exception}"));
                            return true;
                        }
                    }
                }

                return false;
            }

            void findClassesWithBinary(string filePath)
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath))
                {
                    foreach (ExportEntry exp in pcc.Exports.Where(exp => !exp.IsDefaultObject))
                    {
                        try
                        {
                            if (!foundClasses.Contains(exp.ClassName) && exp.propsEnd() < exp.DataSize)
                            {
                                if (ObjectBinary.From(exp) != null)
                                {
                                    foundClasses.Add(exp.ClassName);
                                }
                                else if (exp.GetBinaryData().Any(b => b != 0))
                                {
                                    foundClasses.Add(exp.ClassName);
                                    interestingExports.Add(exp);
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                        }
                    }
                }
            }

            void ScanShaderCache(string filePath)
            {
                using (IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath))
                {
                    ExportEntry shaderCache = pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache");
                    if (shaderCache == null) return;
                    int oldDataOffset = shaderCache.DataOffset;

                    try
                    {
                        MemoryStream binData = new MemoryStream(shaderCache.Data);
                        binData.JumpTo(shaderCache.propsEnd() + 1);

                        int nameList1Count = binData.ReadInt32();
                        binData.Skip(nameList1Count * 12);

                        int namelist2Count = binData.ReadInt32(); //namelist2
                        binData.Skip(namelist2Count * 12);

                        int shaderCount = binData.ReadInt32();
                        for (int i = 0; i < shaderCount; i++)
                        {
                            binData.Skip(24);
                            int nextShaderOffset = binData.ReadInt32() - oldDataOffset;
                            binData.Skip(14);
                            if (binData.ReadInt32() != 1111577667) //CTAB
                            {
                                interestingExports.Add(new EntryStringPair((IEntry)null,
                                    $"{binData.Position - 4}: {filePath}"));
                                return;
                            }

                            binData.JumpTo(nextShaderOffset);
                        }

                        int vertexFactoryMapCount = binData.ReadInt32();
                        binData.Skip(vertexFactoryMapCount * 12);

                        int materialShaderMapCount = binData.ReadInt32();
                        for (int i = 0; i < materialShaderMapCount; i++)
                        {
                            binData.Skip(16);

                            int switchParamCount = binData.ReadInt32();
                            binData.Skip(switchParamCount * 32);

                            int componentMaskParamCount = binData.ReadInt32();
                            //if (componentMaskParamCount != 0)
                            //{
                            //    interestingExports.Add($"{i}: {filePath}");
                            //    return;
                            //}

                            binData.Skip(componentMaskParamCount * 44);

                            int normalParams = binData.ReadInt32();
                            if (normalParams != 0)
                            {
                                interestingExports.Add(new EntryStringPair((IEntry)null, $"{i}: {filePath}"));
                                return;
                            }

                            binData.Skip(normalParams * 29);

                            int unrealVersion = binData.ReadInt32();
                            int licenseeVersion = binData.ReadInt32();
                            if (unrealVersion != 684 || licenseeVersion != 194)
                            {
                                interestingExports.Add(new EntryStringPair((IEntry)null,
                                    $"{binData.Position - 8}: {filePath}"));
                                return;
                            }

                            int nextMaterialShaderMapOffset = binData.ReadInt32() - oldDataOffset;
                            binData.JumpTo(nextMaterialShaderMapOffset);
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        interestingExports.Add(new EntryStringPair((IEntry)null, $"{filePath}\n{exception}"));
                    }
                }
            }

            void ScanScripts(string filePath)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                foreach (ExportEntry exp in pcc.Exports.Where(exp => !exp.IsDefaultObject))
                {
                    try
                    {
                        if ((exp.ClassName == "State" || exp.ClassName == "Function") &&
                            ObjectBinary.From(exp) is UStruct uStruct)
                        {
                            byte[] data = exp.Data;
                            (_, List<BytecodeSingularToken> tokens) = Bytecode.ParseBytecode(uStruct.ScriptBytes, exp);
                            foreach (var token in tokens)
                            {
                                if (token.CurrentStack.Contains("UNKNOWN") || token.OpCodeString.Contains("UNKNOWN"))
                                {
                                    interestingExports.Add(exp);
                                }

                                if (unkOpcodes.Contains(token.OpCode))
                                {
                                    int refUIndex = EndianReader.ToInt32(data, token.StartPos + 1, pcc.Endian);
                                    IEntry entry = pcc.GetEntry(refUIndex);
                                    if (entry != null && (entry.ClassName == "ByteProperty"))
                                    {
                                        var info = unkOpcodesInfo[token.OpCode];
                                        info.Usages.Add(pcc.FilePath, exp.UIndex, token.StartPos);
                                        info.PropTypes.Add(refUIndex switch
                                        {
                                            0 => "Null",
                                            _ when entry != null => entry.ClassName,
                                            _ => "Invalid"
                                        });
                                        if (entry != null)
                                        {
                                            if (entry.Parent == exp)
                                            {
                                                info.PropLocations.Add("Local");
                                            }
                                            else if (entry.Parent == (exp.Parent.ClassName == "State" ? exp.Parent.Parent : exp.Parent))
                                            {
                                                info.PropLocations.Add("ThisClass");
                                            }
                                            else if (entry.Parent.ClassName == "Function")
                                            {
                                                info.PropLocations.Add("OtherFunction");
                                            }
                                            else if (exp.Parent.IsA(entry.Parent.ObjectName))
                                            {
                                                info.PropLocations.Add("AncestorClass");
                                            }
                                            else
                                            {
                                                info.PropLocations.Add("OtherClass");
                                            }
                                        }
                                    }

                                }
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                    }
                }
            }

            void ScanScripts2(string filePath)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                var fileLib = new FileLib(pcc);
                if (fileLib.Initialize())
                {
                    foreach (ExportEntry exp in pcc.Exports.Reverse().Where(exp => exp.ClassName == "Function" && exp.Parent.ClassName == "Class" && !exp.GetBinaryData<UFunction>().FunctionFlags.Has(EFunctionFlags.Native)))
                    {
                        if (exp.Parent.ObjectName == "SFXSeqAct_ScreenShake")
                        {
                            continue;
                        }
                        try
                        {
                            var originalData = exp.Data;
                            (_, string originalScript) = UnrealScriptCompiler.DecompileExport(exp, fileLib);
                            (ASTNode ast, MessageLog log) = UnrealScriptCompiler.CompileFunction(exp, originalScript, fileLib);
                            if (log.HasErrors)
                            {
                                interestingExports.Add(exp);
                                continue;
                            }

                            if (!fileLib.ReInitializeFile())
                            {
                                interestingExports.Add(new EntryStringPair(exp, $"{pcc.FilePath} failed to re-initialize after compiling {$"#{exp.UIndex}",-9}"));
                                return;
                            }
                            if (!originalData.SequenceEqual(exp.Data))
                            {
                                interestingExports.Add(exp);
                                comparisonDict.Add($"{exp.UIndex} {exp.FileRef.FilePath}", (originalData, exp.Data));
                                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "original.bin"), originalData);
                                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "new.bin"), exp.Data);
                                continue;
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                            return;
                        }
                    }
                }
                else
                {
                    interestingExports.Add(new EntryStringPair($"{pcc.FilePath} failed to compile!"));
                }
            }

            void RecompileAllFunctions(string filePath)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                var fileLib = new FileLib(pcc);
                if (fileLib.Initialize())
                {
                    foreach (ExportEntry exp in pcc.Exports.Where(exp => exp.ClassName == "Function"))
                    {
                        string instancedFullPath = exp.InstancedFullPath;
                        if (foundClasses.Contains(instancedFullPath))
                        {
                            continue;
                        }

                        foundClasses.Add(instancedFullPath);
                        try
                        {
                            //var originalData = exp.Data;
                            (_, string originalScript) = UnrealScriptCompiler.DecompileExport(exp, fileLib);
                            (ASTNode ast, MessageLog log) = UnrealScriptCompiler.CompileFunction(exp, originalScript, fileLib);
                            if (ast == null || log.HasErrors)
                            {
                                interestingExports.Add(exp);
                            }
                            if (!fileLib.ReInitializeFile())
                            {
                                interestingExports.Add(new EntryStringPair(exp, $"{pcc.FilePath} failed to re-initialize after compiling {$"#{exp.UIndex}",-9}"));
                                return;
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                            return;
                        }
                    }
                }
                else
                {
                    interestingExports.Add(new EntryStringPair($"{pcc.FilePath} failed to compile!"));
                }
            }

            void RecompileAllStates(string filePath)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                var fileLib = new FileLib(pcc);

                foreach (ExportEntry exp in pcc.Exports.Where(exp => exp.ClassName == "State"))
                {
                    string instancedFullPath = exp.InstancedFullPath;
                    if (foundClasses.Contains(instancedFullPath))
                    {
                        continue;
                    }

                    foundClasses.Add(instancedFullPath);
                    try
                    {
                        if (fileLib.Initialize())
                        {
                            var originalData = exp.Data;
                            (_, string originalScript) = UnrealScriptCompiler.DecompileExport(exp, fileLib);
                            (ASTNode ast, MessageLog log) = UnrealScriptCompiler.CompileState(exp, originalScript, fileLib);
                            if (ast == null || log.HasErrors)
                            {
                                interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\nCompilation failed!"));
                            }
                            else if (!originalData.AsSpan().SequenceEqual(exp.DataReadOnly))
                            {
                                comparisonDict[$"{exp.UIndex} {exp.FileRef.FilePath}"] = (originalData, exp.Data);
                                interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\nRecompilation does not match!"));
                            }
                            if (!fileLib.ReInitializeFile())
                            {
                                interestingExports.Add(new EntryStringPair(exp, $"{pcc.FilePath} failed to re-initialize after compiling {$"#{exp.UIndex}",-9}"));
                                return;
                            }
                        }
                        else
                        {
                            interestingExports.Add(new EntryStringPair($"{pcc.FilePath} failed to compile!"));
                            return;
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                        return;
                    }
                }
            }

            void RecompileAllDefaults(string filePath, PackageCache packageCache = null)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                var fileLib = new FileLib(pcc);

                foreach (ExportEntry exp in pcc.Exports.Where(exp => exp.IsDefaultObject && exp.Class is ExportEntry))
                {
                    string instancedFullPath = exp.InstancedFullPath;
                    if (foundClasses.Contains(instancedFullPath))
                    {
                        continue;
                    }

                    foundClasses.Add(instancedFullPath);
                    try
                    {
                        if (fileLib.Initialize())
                        {
                            (_, string script) = UnrealScriptCompiler.DecompileExport(exp, fileLib);
                            (ASTNode ast, MessageLog log) = UnrealScriptCompiler.CompileDefaultProperties(exp, script, fileLib, packageCache);
                            if (ast is not DefaultPropertiesBlock || log.HasErrors)
                            {
                                interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {pcc.FilePath}\nfailed to parse defaults!"));
                                return;
                            }

                            if (!fileLib.ReInitializeFile())
                            {
                                interestingExports.Add(new EntryStringPair(exp, $"{pcc.FilePath} failed to re-initialize after compiling {$"#{exp.UIndex}",-9}"));
                                return;
                            }
                            if (exp.EntryHasPendingChanges || exp.GetChildren().Any(entry => entry.EntryHasPendingChanges && entry.ClassName is not "ForceFeedbackWaveform") || pcc.FindEntry(UnrealPackageFile.TrashPackageName) is not null)
                            {
                                interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\nRecompilation does not match!"));
                            }
                        }
                        else
                        {
                            interestingExports.Add(new EntryStringPair($"{pcc.FilePath} failed to compile!"));
                            return;
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                        return;
                    }
                }
            }

            void RecompileAllPropsOfNonScriptExports(string filePath, PackageCache packageCache = null)
            {
                try
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    ExportEntry firstExport = pcc.Exports.FirstOrDefault();
                    string src = UnrealScriptCompiler.DecompileBulkProps(pcc, out MessageLog log, packageCache);
                    if (src is null || log.HasErrors)
                    {
                        interestingExports.Add(new EntryStringPair(firstExport, $"{pcc.FilePath} failed to decompile props!"));
                        return;
                    }
                    log = UnrealScriptCompiler.CompileBulkPropertiesFile(src, pcc, packageCache);
                    if (log.HasErrors || log.HasLexErrors)
                    {
                        interestingExports.Add(new EntryStringPair(firstExport, $"{pcc.FilePath} failed to recompile props!"));
                        return;
                    }
                    if (pcc.IsModified)
                    {
                        interestingExports.Add(new EntryStringPair(firstExport, $"{pcc.FilePath} was modified!"));
                        return;
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    interestingExports.Add(new EntryStringPair($"{filePath}\n{exception}"));
                }
            }

            void RecompileAllStructs(string filePath, PackageCache packageCache = null)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                var fileLib = new FileLib(pcc);

                for (int i = 0; i < pcc.ExportCount; i++)
                {
                    ExportEntry exp = pcc.Exports[i];
                    if (exp.ClassName is "ScriptStruct")
                    {
                        string instancedFullPath = exp.InstancedFullPath;
                        if (!foundClasses.Add(instancedFullPath))
                        {
                            continue;
                        }

                        if (exp.GetBinaryData<UScriptStruct>().StructFlags.Has(ScriptStructFlags.Native))
                        {
                            continue;
                        }
                        try
                        {
                            if (fileLib.Initialize())
                            {
                                (_, string script) = UnrealScriptCompiler.DecompileExport(exp, fileLib);
                                (ASTNode ast, MessageLog log) = UnrealScriptCompiler.CompileStruct(exp, script, fileLib, packageCache);
                                if (ast is not Struct || log.HasErrors)
                                {
                                    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {pcc.FilePath}\nfailed to parse defaults!"));
                                    return;
                                }

                                if (!fileLib.ReInitializeFile())
                                {
                                    interestingExports.Add(new EntryStringPair(exp, $"{pcc.FilePath} failed to re-initialize after compiling {$"#{exp.UIndex}",-9}"));
                                    return;
                                }
                                if (exp.EntryHasPendingChanges || exp.GetAllDescendants().Any(entry => entry.EntryHasPendingChanges))
                                {
                                    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\nRecompilation does not match!"));
                                }
                            }
                            else
                            {
                                interestingExports.Add(new EntryStringPair($"{pcc.FilePath} failed to compile!"));
                                return;
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                            return;
                        }
                    }
                }
            }

            void RecompileAllEnums(string filePath, PackageCache packageCache = null)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                var fileLib = new FileLib(pcc);

                for (int i = 0; i < pcc.ExportCount; i++)
                {
                    ExportEntry exp = pcc.Exports[i];
                    if (exp.ClassName is "Enum")
                    {
                        string instancedFullPath = exp.InstancedFullPath;
                        if (foundClasses.Contains(instancedFullPath))
                        {
                            continue;
                        }

                        foundClasses.Add(instancedFullPath);
                        try
                        {
                            if (fileLib.Initialize())
                            {
                                (_, string script) = UnrealScriptCompiler.DecompileExport(exp, fileLib);
                                (ASTNode ast, MessageLog log) = UnrealScriptCompiler.CompileEnum(exp, script, fileLib, packageCache);
                                if (ast is not Enumeration || log.HasErrors)
                                {
                                    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {pcc.FilePath}\nfailed to parse defaults!"));
                                    return;
                                }

                                if (!fileLib.ReInitializeFile())
                                {
                                    interestingExports.Add(new EntryStringPair(exp, $"{pcc.FilePath} failed to re-initialize after compiling {$"#{exp.UIndex}",-9}"));
                                    return;
                                }
                                if (exp.EntryHasPendingChanges)
                                {
                                    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\nRecompilation does not match!"));
                                }
                            }
                            else
                            {
                                interestingExports.Add(new EntryStringPair($"{pcc.FilePath} failed to compile!"));
                                return;
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                            return;
                        }
                    }
                }
            }

            void RecompileAllClasses(string filePath, PackageCache packageCache = null)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                var fileLib = new FileLib(pcc);

                for (int i = 0; i < pcc.ExportCount; i++)
                {
                    ExportEntry exp = pcc.Exports[i];
                    if (exp.IsClass)
                    {
                        string instancedFullPath = exp.InstancedFullPath;
                        if (foundClasses.Contains(instancedFullPath))
                        {
                            continue;
                        }

                        try
                        {
                            if (fileLib.Initialize(packageCache))
                            {
                                (ASTNode ast, string script) = UnrealScriptCompiler.DecompileExport(exp, fileLib, packageCache);
                                if (!((Class)ast).IsFullyDefined)
                                {
                                    continue;
                                }
                                foundClasses.Add(instancedFullPath);
                                var log = new MessageLog();
                                //(ast, _) = UnrealScriptCompiler.CompileOutlineAST(script, "Class", log, pcc.Game);
                                //if (ast is not Class classAST || log.HasErrors)
                                //{
                                //    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {pcc.FilePath}\nfailed to parse class!"));
                                //    return;
                                //}

                                //UnrealScriptCompiler.CompileNewClassAST(pcc, classAST, log, fileLib, out bool vfTableChanged);
                                //if (log.HasErrors)
                                //{
                                //    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {pcc.FilePath}\nfailed to parse class!"));
                                //    return;
                                //}
                                //if (vfTableChanged)
                                //{
                                //    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {pcc.FilePath}\nVTableChanged!"));
                                //    return;
                                //}

                                (ast, log) = UnrealScriptCompiler.CompileClass(pcc, script, fileLib, exp, exp.Parent, packageCache);
                                if (ast is not Class || log.HasErrors)
                                {
                                    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {pcc.FilePath}\nfailed to parse class!"));
                                    //return;
                                }

                                if (!fileLib.ReInitializeFile())
                                {
                                    interestingExports.Add(new EntryStringPair(exp, $"{pcc.FilePath} failed to re-initialize after compiling {$"#{exp.UIndex}",-9}"));
                                    return;
                                }
                                //if (exp.EntryHasPendingChanges )//|| exp.GetAllDescendants().Any(entry => entry.EntryHasPendingChanges))
                                //{
                                //    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\nRecompilation does not match!"));
                                //}
                                //if (pcc.FindEntry(UnrealPackageFile.TrashPackageName) is not null)
                                //{
                                //    interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\nTrashed an export! Aborting compilation for file."));
                                //    return;
                                //}
                            }
                            else
                            {
                                interestingExports.Add(new EntryStringPair($"{pcc.FilePath} failed to compile!"));
                                return;
                            }
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine(exception);
                            interestingExports.Add(new EntryStringPair(exp, $"{exp.UIndex}: {filePath}\n{exception}"));
                            return;
                        }
                    }
                }
            }

            bool resolveImports(string filePath)
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);

                //pre-load associated files
                var gameFiles = MELoadedFiles.GetFilesLoadedInGame(MEGame.ME3);
                using var associatedFiles = MEPackageHandler.OpenMEPackages(EntryImporter
                    .GetPossibleAssociatedFiles(pcc)
                    .Select(fileName => gameFiles.TryGetValue(fileName, out string path) ? path : null)
                    .Where(File.Exists));

                var filesSafeToImportFrom = EntryImporter.FilesSafeToImportFrom(pcc.Game)
                    .Select(Path.GetFileNameWithoutExtension).ToList();
                Debug.WriteLine(filePath);
                foreach (ImportEntry import in pcc.Imports.Where(imp =>
                    !filesSafeToImportFrom.Contains(imp.FullPath.Split('.')[0])))
                {
                    try
                    {
                        if (EntryImporter.ResolveImport(import) is ExportEntry exp)
                        {
                            extraInfo.Add(Path.GetFileName(exp.FileRef.FilePath));
                        }
                        else
                        {
                            interestingExports.Add(import);
                            return true;
                        }
                    }
                    catch (Exception exception)
                    {
                        interestingExports.Add(new EntryStringPair(import,
                            $"{$"#{import.UIndex}",-9} {import.FileRef.FilePath}\n{exception}"));
                        return true;
                    }
                }

                return false;
            }

            #endregion
        }

        public static void CreateDynamicLighting(IMEPackage Pcc, bool silent = false)
        {
            foreach (ExportEntry exp in Pcc.Exports.Where(exp => (exp.IsA("MeshComponent") && exp.Parent.IsA("StaticMeshActorBase")) || (exp.IsA("BrushComponent") && !exp.Parent.IsA("Volume"))))
            {
                PropertyCollection props = exp.GetProperties();
                if (props.GetProp<ObjectProperty>("StaticMesh")?.Value != 11483 &&
                    (props.GetProp<BoolProperty>("bAcceptsLights")?.Value == false ||
                     props.GetProp<BoolProperty>("CastShadow")?.Value == false))
                {
                    // shadows/lighting has been explicitly forbidden, don't mess with it.
                    continue;
                }

                props.AddOrReplaceProp(new BoolProperty(false, "bUsePreComputedShadows"));
                props.AddOrReplaceProp(new BoolProperty(false, "bBioForcePreComputedShadows"));
                props.AddOrReplaceProp(new BoolProperty(false, "bCastDynamicShadow"));
                //props.AddOrReplaceProp(new BoolProperty(true, "CastShadow"));
                //props.AddOrReplaceProp(new BoolProperty(true, "bAcceptsDynamicDominantLightShadows"));
                props.AddOrReplaceProp(new BoolProperty(true, "bAcceptsLights"));
                //props.AddOrReplaceProp(new BoolProperty(false, "bAcceptsDynamicLights"));

                var lightingChannels = props.GetProp<StructProperty>("LightingChannels") ??
                                       new StructProperty("LightingChannelContainer", false,
                                           new BoolProperty(true, "bIsInitialized"))
                                       {
                                           Name = "LightingChannels"
                                       };
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Static"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "CompositeDynamic"));
                props.AddOrReplaceProp(lightingChannels);

                exp.WriteProperties(props);
            }
            //fix interpactors to be dynamic
            foreach (ExportEntry exp in Pcc.Exports.Where(exp => exp.IsA("MeshComponent") && exp.Parent.IsA("DynamicSMActor")))
            {
                PropertyCollection props = exp.GetProperties();
                if (props.GetProp<ObjectProperty>("StaticMesh")?.Value != 11483 &&
                    (props.GetProp<BoolProperty>("bAcceptsLights")?.Value == false ||
                     props.GetProp<BoolProperty>("CastShadow")?.Value == false))
                {
                    // shadows/lighting has been explicitly forbidden, don't mess with it.
                    continue;
                }

                props.AddOrReplaceProp(new BoolProperty(false, "bUsePreComputedShadows"));
                props.AddOrReplaceProp(new BoolProperty(false, "bBioForcePreComputedShadows"));

                var lightingChannels = props.GetProp<StructProperty>("LightingChannels") ??
                                       new StructProperty("LightingChannelContainer", false,
                                           new BoolProperty(true, "bIsInitialized"))
                                       {
                                           Name = "LightingChannels"
                                       };
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));
                props.AddOrReplaceProp(lightingChannels);

                exp.WriteProperties(props);
            }

            foreach (ExportEntry exp in Pcc.Exports.Where(exp => exp.IsA("LightComponent")))
            {
                PropertyCollection props = exp.GetProperties();
                //props.AddOrReplaceProp(new BoolProperty(true, "bCanAffectDynamicPrimitivesOutsideDynamicChannel"));
                //props.AddOrReplaceProp(new BoolProperty(true, "bForceDynamicLight"));

                var lightingChannels = props.GetProp<StructProperty>("LightingChannels") ??
                                       new StructProperty("LightingChannelContainer", false,
                                           new BoolProperty(true, "bIsInitialized"))
                                       {
                                           Name = "LightingChannels"
                                       };
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Static"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "CompositeDynamic"));
                props.AddOrReplaceProp(lightingChannels);

                exp.WriteProperties(props);
            }

            if (!silent)
                MessageBox.Show("Done!");
        }

        public static void ConvertAllDialogueToSkippable(PackageEditorWindow pewpf)
        {
            var gameString = InputComboBoxDialog.GetValue(pewpf,
                            "Select which game's files you want converted to having skippable dialogue",
                            "Game selector", new[] { "ME1", "ME2", "ME3", "LE1", "LE2", "LE3" }, "LE1");
            if (Enum.TryParse(gameString, out MEGame game) && MessageBoxResult.Yes ==
                MessageBox.Show(pewpf,
                    $"WARNING! This will edit every dialogue-containing file in {gameString}, including in DLCs and installed mods. Do you want to begin?",
                    "", MessageBoxButton.YesNo))
            {
                pewpf.IsBusy = true;
                pewpf.BusyText = $"Making all {gameString} dialogue skippable";
                Task.Run(() =>
                {
                    foreach (string file in MELoadedFiles.GetAllFiles(game))
                    {
                        using IMEPackage pcc = MEPackageHandler.OpenMEPackage(file);
                        bool hasConv = false;
                        foreach (ExportEntry conv in pcc.Exports.Where(exp => exp.ClassName == "BioConversation"))
                        {
                            hasConv = true;
                            PropertyCollection props = conv.GetProperties();
                            if (props.GetProp<ArrayProperty<StructProperty>>("m_EntryList") is
                                ArrayProperty<StructProperty> entryList)
                            {
                                foreach (StructProperty entryNode in entryList)
                                {
                                    entryNode.Properties.AddOrReplaceProp(new BoolProperty(true, "bSkippable"));
                                }
                            }

                            if (props.GetProp<ArrayProperty<StructProperty>>("m_ReplyList") is
                                ArrayProperty<StructProperty> replyList)
                            {
                                foreach (StructProperty entryNode in replyList)
                                {
                                    entryNode.Properties.AddOrReplaceProp(new BoolProperty(false, "bUnskippable"));
                                }
                            }

                            conv.WriteProperties(props);
                        }

                        if (hasConv)
                            pcc.Save();
                    }
                }).ContinueWithOnUIThread(prevTask =>
                {
                    pewpf.IsBusy = false;
                    MessageBox.Show(pewpf, "Done!");
                });
            }
        }

        public static void DumpAllShaders(IMEPackage Pcc)
        {
            if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is ExportEntry shaderCacheExport)
            {
                var dlg = new CommonOpenFileDialog("Pick a folder to save Shaders to.")
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true
                };
                if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    var shaderCache = ObjectBinary.From<ShaderCache>(shaderCacheExport);
                    foreach (Shader shader in shaderCache.Shaders.Values)
                    {
                        string shaderType = shader.ShaderType;
                        string pathWithoutInvalids = Path.Combine(dlg.FileName,
                            $"{shaderType.GetPathWithoutInvalids()} - {shader.Guid}.txt");
                        File.WriteAllText(pathWithoutInvalids,
                            ShaderBytecode.FromStream(new MemoryStream(shader.ShaderByteCode))
                                .Disassemble());
                    }

                    MessageBox.Show("Done!");
                }
            }
        }

        public static void DumpMaterialShaders(ExportEntry matExport)
        {
            //var dlg = new CommonOpenFileDialog("Pick a folder to save Shaders to.")
            //{
            //    IsFolderPicker = true,
            //    EnsurePathExists = true
            //};
            //if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            //{
            //    var matInst = new MaterialInstanceConstant(matExport);
            //    matInst.GetShaders();
            //    foreach (Shader shader in matInst.Shaders)
            //    {
            //        string shaderType = shader.ShaderType;
            //        string pathWithoutInvalids;
            //        //pathWithoutInvalids = Path.Combine(dlg.FileName, $"{shaderType.GetPathWithoutInvalids()} - {shader.Guid} - OFFICIAL.txt");
            //        //File.WriteAllText(pathWithoutInvalids,
            //        //                  SharpDX.D3DCompiler.ShaderBytecode.FromStream(new MemoryStream(shader.ShaderByteCode))
            //        //                         .Disassemble());

            //        pathWithoutInvalids = Path.Combine(dlg.FileName, $"{shaderType.GetPathWithoutInvalids()} - {shader.Guid}.txt");// - SirCxyrtyx.txt");
            //        File.WriteAllText(pathWithoutInvalids, shader.ShaderDisassembly);
            //    }

            //    MessageBox.Show("Done!");
            //}

        }

        public static void ReserializeExport(ExportEntry export)
        {
            PropertyCollection props = export.GetProperties();
            ObjectBinary bin = ObjectBinary.From(export) ?? export.GetBinaryData();
            byte[] original = export.Data;

            export.WriteProperties(props);

            EndianReader ms = new EndianReader(new MemoryStream()) { Endian = export.FileRef.Endian };
            ms.Writer.Write(export.Data, 0, export.propsEnd());
            bin.WriteTo(ms.Writer, export.FileRef, export.DataOffset);

            byte[] changed = ms.ToArray();
            //export.Data = changed;
            if (original.SequenceEqual(changed))
            {
                MessageBox.Show("reserialized identically!");
            }
            else
            {
                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "original.bin"), original);
                File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "new.bin"), changed);
                if (original.Length != changed.Length)
                {
                    MessageBox.Show($"Differences detected: Lengths are not the same. Original {original.Length}, Reserialized {changed.Length}");
                }
                else
                {
                    for (int i = 0; i < Math.Min(changed.Length, original.Length); i++)
                    {
                        if (original[i] != changed[i])
                        {
                            MessageBox.Show($"Differences detected: Bytes differ first at 0x{i:X8}");
                            break;
                        }
                    }
                }
            }
        }

        public static void RunPropertyCollectionTest(PackageEditorWindow pewpf)
        {
            var filePaths = /*MELoadedFiles.GetOfficialFiles(MEGame.ME3).Concat*/
                            (MELoadedFiles.GetOfficialFiles(MEGame.ME2)).Concat(MELoadedFiles.GetOfficialFiles(MEGame.ME1));

            pewpf.IsBusy = true;
            pewpf.BusyText = "Scanning";
            Task.Run(() =>
            {
                foreach (string filePath in filePaths)
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                    Debug.WriteLine(filePath);
                    foreach (ExportEntry export in pcc.Exports)
                    {
                        try
                        {
                            byte[] originalData = export.Data;
                            PropertyCollection props = export.GetProperties();
                            export.WriteProperties(props);
                            byte[] resultData = export.Data;
                            if (!originalData.SequenceEqual(resultData))
                            {
                                string userFolder = Path.Combine(@"C:\Users", Environment.UserName);
                                File.WriteAllBytes(Path.Combine(userFolder, $"c.bin"), resultData);
                                File.WriteAllBytes(Path.Combine(userFolder, $"o.bin"), originalData);
                                return (filePath, export.UIndex);
                            }
                        }
                        catch
                        {
                            return (filePath, export.UIndex);
                        }
                    }
                }

                return (null, 0);
            }).ContinueWithOnUIThread(prevTask =>
            {
                pewpf.IsBusy = false;
                (string filePath, int uIndex) = prevTask.Result;
                if (filePath == null)
                {
                    MessageBox.Show(pewpf, "No errors occured!");
                }
                else
                {
                    pewpf.LoadFile(filePath, uIndex);
                    MessageBox.Show(pewpf, $"Error at #{uIndex} in {filePath}!");
                }
            });
        }

        public static void UDKifyTest(PackageEditorWindow pewpf)
        {
            // TODO: IMPLEMENT IN LEX

            IMEPackage Pcc = pewpf.Pcc;
            string udkPath = UDKDirectory.DefaultGamePath;
            if (udkPath == null || !Directory.Exists(udkPath))
            {
                var udkDlg = new CommonOpenFileDialog(@"Select your UDK\Custom folder");
                udkDlg.IsFolderPicker = true;
                var result = udkDlg.ShowDialog();

                if (result is not CommonFileDialogResult.Ok || string.IsNullOrWhiteSpace(udkDlg.FileName) || !Directory.Exists(udkDlg.FileName))
                    return;
                udkPath = udkDlg.FileName;
                LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory = udkPath;
                UDKDirectory.ReloadDefaultGamePath();
            }

            string persistentLevelFileName = Path.GetFileNameWithoutExtension(Pcc.FilePath);

            pewpf.IsBusy = true;
            pewpf.BusyText = $"Converting {persistentLevelFileName}";
            Task.Run(() =>
            {
                string persistentPath = ConvertToUDK.GenerateUDKFileForLevel(Pcc); 
                var levelFiles = new List<string>();
                if (Pcc is MEPackage mePackage)
                {
                    var loadedFiles = MELoadedFiles.GetFilesLoadedInGame(Pcc.Game);
                    foreach (string additionalPackage in mePackage.AdditionalPackagesToCook)
                    {
                        if (loadedFiles.TryGetValue($"{additionalPackage}.pcc", out string filePath))
                        {
                            pewpf.BusyText = $"Converting {additionalPackage}";
                            using IMEPackage levPcc = MEPackageHandler.OpenMEPackage(filePath);
                            levelFiles.Add(ConvertToUDK.GenerateUDKFileForLevel(levPcc));
                        }
                    }

                    using IMEPackage persistentUDK = MEPackageHandler.OpenUDKPackage(persistentPath);
                    IEntry levStreamingClass =
                        persistentUDK.GetEntryOrAddImport("Engine.LevelStreamingAlwaysLoaded", "Class");
                    IEntry theWorld = persistentUDK.Exports.First(exp => exp.ClassName == "World");
                    int i = 1;
                    int firstLevStream = persistentUDK.ExportCount;
                    foreach (string fileName in levelFiles.Select(Path.GetFileNameWithoutExtension))
                    {
                        persistentUDK.AddExport(new ExportEntry(persistentUDK, theWorld, new NameReference("LevelStreamingAlwaysLoaded", i), properties: new PropertyCollection
                        {
                            new NameProperty(fileName, "PackageName"),
                            CommonStructs.ColorProp(System.Drawing.Color.FromArgb(255, (byte)(i % 256), (byte)((255 - i) % 256), (byte)(i * 7 % 256)), "DrawColor")
                        })
                        {
                            Class = levStreamingClass
                        });
                        i++;
                    }

                    var streamingLevelsProp = new ArrayProperty<ObjectProperty>("StreamingLevels");
                    for (int j = firstLevStream; j < persistentUDK.ExportCount; j++)
                    {
                        streamingLevelsProp.Add(new ObjectProperty(j));
                    }

                    persistentUDK.Exports.First(exp => exp.ClassName == "WorldInfo")
                        .WriteProperty(streamingLevelsProp);
                    persistentUDK.Save();
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                if (prevTask.IsFaulted)
                {
                    new ExceptionHandlerDialog(prevTask.Exception).ShowDialog();
                }
                pewpf.IsBusy = false;
            });
        }

        public static void MakeME1TextureFileList(PackageEditorWindow pewpf)
        {
            var filePaths = MELoadedFiles.GetOfficialFiles(MEGame.ME1).ToList();

            pewpf.IsBusy = true;
            pewpf.BusyText = "Scanning";
            Task.Run(() =>
            {
                var textureFiles = new HashSet<string>();
                foreach (string filePath in filePaths)
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);

                    foreach (ExportEntry export in pcc.Exports)
                    {
                        try
                        {
                            if (export.IsTexture() && !export.IsDefaultObject)
                            {
                                List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(export, null);
                                foreach (Texture2DMipInfo mip in mips)
                                {
                                    if (mip.storageType == StorageTypes.extLZO ||
                                        mip.storageType == StorageTypes.extZlib ||
                                        mip.storageType == StorageTypes.extUnc)
                                    {
                                        var fullPath = filePaths.FirstOrDefault(x =>
                                            Path.GetFileName(x).Equals(mip.TextureCacheName,
                                                StringComparison.InvariantCultureIgnoreCase));
                                        if (fullPath != null)
                                        {
                                            var baseIdx = fullPath.LastIndexOf("CookedPC");
                                            textureFiles.Add(fullPath.Substring(baseIdx));
                                            break;
                                        }
                                        else
                                        {
                                            throw new FileNotFoundException(
                                                $"Externally referenced texture file not found in game: {mip.TextureCacheName}.");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e.Message);
                        }
                    }
                }

                return textureFiles;
            }).ContinueWithOnUIThread(prevTask =>
            {
                pewpf.IsBusy = false;
                List<string> files = prevTask.Result.OrderBy(s => s).ToList();
                File.WriteAllText(Path.Combine(AppDirectories.ExecFolder, "ME1TextureFiles.json"),
                    JsonConvert.SerializeObject(files, Formatting.Indented));
                ListDialog dlg = new ListDialog(files, "", "ME1 files with externally referenced textures", pewpf);
                dlg.Show();
            });
        }

        public static void BuildNativeTable(PackageEditorWindow pewpf)
        {
            //pewpf.IsBusy = true;
            //pewpf.BusyText = "Building Native Tables";
            //Task.Run(() =>
            //{
            //    foreach (MEGame game in new[] {  MEGame.UDK })
            //    {
            //        string scriptPath = UDKDirectory.ScriptPath;
            //        var entries = new List<(int, string)>();
            //        foreach (string fileName in FileLib.BaseFileNames(game))
            //        {
            //            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(Path.Combine(scriptPath, fileName));
            //            foreach (ExportEntry export in pcc.Exports.Where(exp => exp.ClassName == "Function"))
            //            {
            //                var func = export.GetBinaryData<UFunction>();
            //                ushort nativeIndex = func.NativeIndex;
            //                if (nativeIndex > 0)
            //                {
            //                    NativeType type = NativeType.Function;
            //                    if (func.FunctionFlags.Has(EFunctionFlags.PreOperator))
            //                    {
            //                        type = NativeType.PreOperator;
            //                    }
            //                    else if (func.FunctionFlags.Has(EFunctionFlags.Operator))
            //                    {
            //                        var nextItem = func.Children;
            //                        int paramCount = 0;
            //                        while (export.FileRef.TryGetUExport(nextItem, out ExportEntry nextChild))
            //                        {
            //                            var objBin = ObjectBinary.From(nextChild);
            //                            switch (objBin)
            //                            {
            //                                case UProperty uProperty:
            //                                    if (uProperty.PropertyFlags.HasFlag(UnrealFlags.EPropertyFlags.ReturnParm))
            //                                    {
            //                                    }
            //                                    else if (uProperty.PropertyFlags.HasFlag(UnrealFlags.EPropertyFlags.Parm))
            //                                    {
            //                                        paramCount++;
            //                                    }
            //                                    nextItem = uProperty.Next;
            //                                    break;
            //                                default:
            //                                    nextItem = 0;
            //                                    break;
            //                            }
            //                        }

            //                        type = paramCount == 1 ? NativeType.PostOperator : NativeType.Operator;
            //                    }

            //                    string name = func.FriendlyName;
            //                    if (game is MEGame.ME3 or MEGame.LE3)
            //                    {
            //                        name = export.ObjectName;
            //                    }
            //                    entries.Add(nativeIndex, $"{{ 0x{nativeIndex:X}, new {nameof(NativeTableEntry)} {{ {nameof(NativeTableEntry.Name)}=\"{name}\", " +
            //                                             $"{nameof(NativeTableEntry.Type)}={nameof(NativeType)}.{type}, {nameof(NativeTableEntry.Precedence)}={func.OperatorPrecedence}}} }},");
            //                }
            //            }
            //        }

            //        using var fileStream = new FileStream(Path.Combine(AppDirectories.ExecFolder, $"{game}NativeTable.cs"), FileMode.Create);
            //        using var writer = new CodeWriter(fileStream);
            //        writer.WriteLine("using System.Collections.Generic;");
            //        writer.WriteLine();
            //        writer.WriteBlock("namespace LegendaryExplorerCore.UnrealScript.Decompiling", () =>
            //        {
            //            writer.WriteBlock($"public partial class {nameof(ByteCodeDecompiler)}", () =>
            //            {
            //                if (game is MEGame.ME3 or MEGame.LE3)
            //                {
            //                    writer.WriteLine("//TODO: Names need fixing for operators with symbols in name");
            //                }
            //                writer.WriteLine($"public static readonly Dictionary<int, {nameof(NativeTableEntry)}> {game}NativeTable = new() ");
            //                writer.WriteLine("{");
            //                writer.IncreaseIndent();

            //                foreach ((_, string entry) in entries.OrderBy(tup => tup.Item1))
            //                {
            //                    writer.WriteLine(entry);
            //                }

            //                writer.DecreaseIndent();
            //                writer.WriteLine("};");
            //            });

            //        });
            //    }
            //}).ContinueWithOnUIThread(prevTask =>
            //{
            //    if (prevTask.IsFaulted)
            //    {
            //        new ExceptionHandlerDialog(prevTask.Exception).ShowDialog();
            //    }
            //    pewpf.IsBusy = false;
            //});
        }
        class CodeWriter : IDisposable
        {
            private readonly StreamWriter writer;
            private byte indent;
            private bool writingLine;
            private readonly string indentString;

            public CodeWriter(Stream stream, string indentString = "    ")
            {
                writer = new StreamWriter(stream);
                this.indentString = indentString;
            }

            public void IncreaseIndent(byte amount = 1)
            {
                indent += amount;
            }

            public void DecreaseIndent(byte amount = 1)
            {
                if (amount > indent)
                {
                    throw new InvalidOperationException("Cannot have a negative indent!");
                }
                indent -= amount;
            }

            public void WriteIndent()
            {
                for (int i = 0; i < indent; i++)
                {
                    writer.Write(indentString);
                }
            }

            public void Write(string text)
            {
                if (!writingLine)
                {
                    WriteIndent();
                    writingLine = true;
                }
                writer.Write(text);
            }

            public void WriteLine(string line)
            {
                if (!writingLine)
                {
                    WriteIndent();
                }
                writingLine = false;
                writer.WriteLine(line);
            }

            public void WriteLine()
            {
                writingLine = false;
                writer.WriteLine();
            }

            public void WriteBlock(string header, Action contents)
            {
                WriteLine(header);
                WriteLine("{");
                IncreaseIndent();
                contents();
                DecreaseIndent();
                WriteLine("}");
            }

            public void Dispose()
            {
                writer.Dispose();
            }
        }

        public static void DumpSound(PackageEditorWindow packEd)
        {
            if (InputComboBoxDialog.GetValue(packEd, "Choose game:", "Game to dump sound for", new[] { "ME3", "ME2", "LE3", "LE2" }, "LE3") is string gameStr &&
                Enum.TryParse(gameStr, out MEGame game)
                &&
             InputComboBoxDialog.GetValue(packEd, "Choose language:", "Choose language", new[] { MELocalization.INT, MELocalization.FRA, MELocalization.DEU, MELocalization.ITA, MELocalization.POL }, MELocalization.INT.ToString()) is string loc &&
                Enum.TryParse(loc, out MELocalization localization))
            {
                var languagePrefixes = new List<string>();
                switch (localization)
                {
                    case MELocalization.INT:
                        languagePrefixes.Add("en_us");
                        languagePrefixes.Add("en-us");
                        break;
                    case MELocalization.FRA:
                        languagePrefixes.Add("fr_fr");
                        languagePrefixes.Add("fr-fr");
                        break;
                    case MELocalization.DEU:
                        languagePrefixes.Add("de_de");
                        languagePrefixes.Add("de-de");
                        break;
                    case MELocalization.ITA:
                        languagePrefixes.Add("it_it");
                        languagePrefixes.Add("it-it");
                        break;
                    case MELocalization.POL:
                        languagePrefixes.Add("pl-pl");
                        // No ME3 loc
                        break;
                }

                string tag = PromptDialog.Prompt(packEd, "Character tag:", defaultValue: "player_f", selectText: true);
                if (string.IsNullOrWhiteSpace(tag))
                {
                    return;
                }
                var dlg = new CommonOpenFileDialog("Pick a folder to save WAVs to.")
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true
                };
                if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return;
                }

                string outFolder = dlg.FileName;
                var filePaths = MELoadedFiles.GetOfficialFiles(game);
                packEd.IsBusy = true;
                packEd.BusyText = "Scanning";
                Task.Run(() =>
                {
                    //preload base files for faster scanning
                    using var baseFiles = MEPackageHandler.OpenMEPackages(EntryImporter.FilesSafeToImportFrom(game)
                                                                                       .Select(f => Path.Combine(MEDirectories.GetCookedPath(game), f)));
                    if (game is MEGame.ME3)
                    {
                        baseFiles.Add(MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "BIOP_MP_COMMON.pcc")));
                    }

                    foreach (string filePath in filePaths)
                    {
                        using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);
                        foreach (ExportEntry export in pcc.Exports.Where(exp => exp.ClassName == "WwiseStream"))
                        {
                            if (export.ObjectNameString.Split(',') is string[] { Length: > 1 } parts && languagePrefixes.Contains(parts[0]) && parts[1] == tag)
                            {
                                string fileName = Path.Combine(outFolder, $"{export.ObjectNameString}.wav");
                                using var fs = new FileStream(fileName, FileMode.Create);
                                Stream wavStream = export.GetBinaryData<WwiseStream>().CreateWaveStream();
                                wavStream.SeekBegin();
                                wavStream.CopyTo(fs);
                            }
                        }
                    }
                }).ContinueWithOnUIThread(prevTask =>
                {
                    packEd.IsBusy = false;
                    MessageBox.Show("Done");
                });
            }
        }

        private record StringMEGamePair(string str, MEGame game);

        public static void DumpShaderTypes(PackageEditorWindow pewpf)
        {
            var shaderTypes = new HashSet<string>();
            var shaderLocs = new Dictionary<StringMEGamePair, (string, int)>();

            var shadersToFind = new HashSet<string>
            {
                "FShadowDepthNoPSVertexShader",
                "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy",
                "TBasePassVertexShaderFPointLightLightMapPolicyFNoDensityPolicy",
                "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFConstantDensityPolicy",
                "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy",
                "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy",
                "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFSphereDensityPolicy",
                "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConstantDensityPolicy",
                "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy",
                "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy",
                "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFSphereDensityPolicy",
                "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFConstantDensityPolicy",
                "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFLinearHalfspaceDensityPolicy",
                "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy",
                "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFSphereDensityPolicy",
                "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConstantDensityPolicy",
                "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFLinearHalfspaceDensityPolicy",
                "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy",
                "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFSphereDensityPolicy"
            };

            pewpf.IsBusy = true;
            pewpf.BusyText = "Scanning";
            Task.Run(() =>
            {
                foreach (MEGame game in new[] { MEGame.ME2, MEGame.ME3, MEGame.LE1, MEGame.LE2, MEGame.LE3 })
                {
                    var filePaths = MELoadedFiles.GetOfficialFiles(game);
                    //preload base files for faster scanning
                    using var baseFiles = MEPackageHandler.OpenMEPackages(EntryImporter.FilesSafeToImportFrom(game)
                                                                                       .Select(f => Path.Combine(MEDirectories.GetCookedPath(game), f)));
                    if (game is MEGame.ME3)
                    {
                        baseFiles.Add(MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.CookedPCPath, "BIOP_MP_COMMON.pcc")));
                    }

                    foreach (string filePath in filePaths)
                    {
                        using IMEPackage pcc = MEPackageHandler.OpenMEPackage(filePath);

                        if (pcc.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is ExportEntry scEntry)
                        {
                            int entryDataOffset = scEntry.DataOffset;
                            var binData = new MemoryStream(scEntry.Data);
                            binData.Seek(scEntry.propsEnd() + 1, SeekOrigin.Begin);

                            int nameList1Count = binData.ReadInt32();
                            binData.Seek(nameList1Count * 12, SeekOrigin.Current);

                            if (game is MEGame.ME3 || game.IsLEGame())
                            {
                                int namelist2Count = binData.ReadInt32();//namelist2
                                binData.Seek(namelist2Count * 12, SeekOrigin.Current);
                            }

                            if (game is MEGame.ME1)
                            {
                                int vertexFactoryMapCount = binData.ReadInt32();
                                binData.Seek(vertexFactoryMapCount * 12, SeekOrigin.Current);
                            }

                            int shaderCount = binData.ReadInt32();
                            for (int i = 0; i < shaderCount; i++)
                            {
                                string shaderType = binData.ReadNameReference(pcc);
                                //shaderTypes.Add(shaderType);
                                if (shadersToFind.Contains(shaderType))
                                {
                                    shaderLocs.TryAdd(new StringMEGamePair(shaderType, game), (pcc.FilePath, i));
                                }
                                binData.Seek(16, SeekOrigin.Current);
                                int nextShaderOffset = binData.ReadInt32() - entryDataOffset;
                                binData.Seek(nextShaderOffset, SeekOrigin.Begin);
                            }
                        }
                    }
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                //the base files will have been in memory for so long at this point that they take a looong time to clear out automatically, so force it.
                MemoryAnalyzer.ForceFullGC();
                pewpf.IsBusy = false;

                //var list = shaderTypes.ToList();
                //list.Sort();
                //string scriptFile = Path.Combine("ShaderTypes.txt");
                //scriptFile = Path.GetFullPath(scriptFile);
                //File.WriteAllText(scriptFile, string.Join('\n', list));
                //Process.Start("notepad++", $"\"{scriptFile}\"");

                using var fileStream = new FileStream(Path.GetFullPath($"ShaderLocs.txt"), FileMode.Create);
                using var writer = new CodeWriter(fileStream);
                foreach ((var shaderTypeMEGamePair, (string filePath, int index)) in shaderLocs)
                {
                    writer.WriteBlock($"{shaderTypeMEGamePair.str} : {shaderTypeMEGamePair.game}", () =>
                    {
                        writer.WriteLine($"{index}\t\t{filePath}");
                    });
                }
            });
        }

        public static void RecompileAll(PackageEditorWindow pew)
        {
            if (pew.Pcc is { Platform: MEPackage.GamePlatform.PC } && pew.Pcc.Game != MEGame.UDK)
            {
                var exportsWithDecompilationErrors = new List<EntryStringPair>();
                var fileLib = new FileLib(pew.Pcc);
                if (!fileLib.Initialize())
                {
                    exportsWithDecompilationErrors.Add(new EntryStringPair("Filelib failed to initialize!"));
                }
                for (int i = 0; i < pew.Pcc.ExportCount; i++)
                {
                    ExportEntry export = pew.Pcc.Exports[i];
                    if (export.ClassName is "ScriptStruct")
                    {
                        try
                        {
                            (_, string script) = UnrealScriptCompiler.DecompileExport(export, fileLib);
                            (ASTNode ast, MessageLog log) = UnrealScriptCompiler.CompileStruct(export, script, fileLib);
                            if (ast is not Struct s || log.HasErrors)
                            {
                                throw new Exception();
                            }
                            if (!fileLib.ReInitializeFile())
                            {
                                exportsWithDecompilationErrors.Add(new EntryStringPair(export, $"{pew.Pcc.FilePath} failed to re-initialize after compiling {$"#{export.UIndex}",-9}"));
                                return;
                            }
                        }
                        catch 
                        {
                            exportsWithDecompilationErrors.Add(new EntryStringPair(export, "Compilation Error!"));
                            break;
                        }
                    }
                }
                //foreach (ExportEntry export in pew.Pcc.Exports.Where(exp => exp.IsClass))
                //{
                //    try
                //    {
                //        (_, string script) = UnrealScriptCompiler.DecompileExport(export, fileLib);
                //        (ASTNode ast, MessageLog log, _) = UnrealScriptCompiler.CompileAST(script, export.ClassName, export.Game);
                //        if (ast is not Class c|| log.HasErrors)
                //        {
                //            throw new Exception();
                //        }

                //        //foreach (State state in c.States)
                //        //{
                //        //    ast = UnrealScriptCompiler.CompileNewStateBodyAST(export, state, log, fileLib);
                //        //    if (ast is not State || log.HasErrors)
                //        //    {
                //        //        throw new Exception();
                //        //    }
                //        //}

                //        //foreach (Function function in c.Functions)
                //        //{
                //        //    ast = UnrealScriptCompiler.CompileNewFunctionBodyAST(export, function, log, fileLib);
                //        //    if (ast is not Function || log.HasErrors)
                //        //    {
                //        //        throw new Exception();
                //        //    }
                //        //}

                //        ast = UnrealScriptCompiler.CompileDefaultPropertiesAST(export, c.DefaultProperties, log, fileLib);
                //        if (ast is not DefaultPropertiesBlock || log.HasErrors)
                //        {
                //            throw new Exception();
                //        }
                //    }
                //    catch (Exception e)
                //    {
                //        exportsWithDecompilationErrors.Add(new EntryStringPair(export, "Compilation Error!"));
                //        break;
                //    }
                //}

                var dlg = new ListDialog(exportsWithDecompilationErrors, "Compilation errors", "", pew)
                {
                    DoubleClickEntryHandler = pew.GetEntryDoubleClickAction()
                };
                dlg.Show();
            }
        }

        public static void CompileLooseClassFolder(PackageEditorWindow pew)
        {
            var folderPicker = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select folder with .uc files"
            };
            if (folderPicker.ShowDialog(pew) is not CommonFileDialogResult.Ok) return;
            string classFolder = folderPicker.FileName;

            string gameString = InputComboBoxDialog.GetValue(pew, "Choose a game to compile for:",
                "Create new package file", new[] { "LE3", "LE2", "LE1", "ME3", "ME2", "ME1", "UDK" }, "LE3");
            if (!Enum.TryParse(gameString, out MEGame game)) return;

            var dlg = new SaveFileDialog
            {
                Filter = game switch
                {
                    MEGame.UDK => GameFileFilters.UDKFileFilter,
                    MEGame.ME1 => GameFileFilters.ME1SaveFileFilter,
                    MEGame.ME2 => GameFileFilters.ME3ME2SaveFileFilter,
                    MEGame.ME3 => GameFileFilters.ME3ME2SaveFileFilter,
                    _ => GameFileFilters.LESaveFileFilter
                },
                CustomPlaces = AppDirectories.GameCustomPlaces,
                InitialDirectory = classFolder
            };
            if (dlg.ShowDialog() != true) return;
            string packagePath = dlg.FileName;

            pew.SetBusy("Compiling loose classes");
            Task.Run(() =>
            {
                var classes = new List<UnrealScriptCompiler.LooseClass>();
                foreach (string ucFilePath in Directory.EnumerateFiles(classFolder, "*.uc", SearchOption.AllDirectories))
                {
                    string source = File.ReadAllText(ucFilePath);
                    //files must be named Classname.uc
                    classes.Add(new UnrealScriptCompiler.LooseClass(Path.GetFileNameWithoutExtension(ucFilePath), source));
                }

                using IMEPackage pcc = MEPackageHandler.CreateAndOpenPackage(packagePath, game);

                //in real use, you would want a way to associate classes with the right packages (e.g. SFXGameContent).
                //Perhaps this could be encoded in the directory structure of the loose files?
                var looseClassPackages = new List<UnrealScriptCompiler.LooseClassPackage>
                {
                    new("CompiledClasses", classes)
                };

                MessageLog log = UnrealScriptCompiler.CompileLooseClasses(pcc, looseClassPackages, MissingObjectResolver, vtableDonorGetter: VTableDonorGetter);

                if (log.HasErrors)
                {
                    return (object)log;
                }

                pcc.Save();
                return packagePath;

                //a real resolver might want to pull in donors, or at least keep track of which exports had been stubbed
                static IEntry MissingObjectResolver(IMEPackage pcc, string instancedPath)
                {
                    ExportEntry export = null;
                    foreach (string name in instancedPath.Split('.'))
                    {
                        export = ExportCreator.CreatePackageExport(pcc, NameReference.FromInstancedString(name), export);
                    }
                    return export;
                }

                //terrible function, there are better ways of implementing it.
                List<string> VTableDonorGetter(string className)
                {
                    var classInfo = GlobalUnrealObjectInfo.GetClassOrStructInfo(game, className);
                    if (classInfo is not null)
                    {
                        string path = Path.Combine(MEDirectories.GetBioGamePath(game), classInfo.pccPath);
                        if (File.Exists(path))
                        {
                            IMEPackage partialLoad = MEPackageHandler.UnsafePartialLoad(path, exp => exp.IsClass && exp.ObjectName == className);
                            foreach (ExportEntry export in partialLoad.Exports)
                            {
                                if (export.IsClass && export.ObjectName == className)
                                {
                                    var obj = export.GetBinaryData<UClass>();
                                    return obj.VirtualFunctionTable.Select(uIdx => partialLoad.GetEntry(uIdx).ObjectName.Name).ToList();
                                }
                            }
                        }
                    }
                    return null;
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                pew.EndBusy();
                switch (prevTask.Result)
                {
                    case MessageLog log:
                        new ListDialog(log.AllErrors.Select(msg => msg.ToString()), "Errors occured while compiling loose classes", "", pew).Show();
                        break;
                    case string path:
                        pew.LoadFile(path);
                        break;
                }
            });
        }

        public static void DumpClassSource(PackageEditorWindow pew)
        {
            var pcc = pew.Pcc;
            if (pcc is null)
            {
                return;
            }

            var folderPicker = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                EnsurePathExists = true,
                Title = "Select folder to dump .uc files"
            };
            if (folderPicker.ShowDialog(pew) is not CommonFileDialogResult.Ok) return;
            string folderPath = folderPicker.FileName;
            pew.SetBusy("Dumping source code");
            Task.Run(() =>
            {
                var fileLib = new FileLib(pcc);
                if (!fileLib.Initialize())
                {
                    return "Failed to initialize FileLib";
                }
                int numFiles = 0;
                foreach (ExportEntry classExport in pew.Pcc.Exports.Where(exp => exp.IsClass))
                {
                    try
                    {
                        (ASTNode ast, string script) = UnrealScriptCompiler.DecompileExport(classExport, fileLib);
                        var cls = (Class)ast;
                        if (!cls.IsFullyDefined)
                        {
                            continue;
                        }
                        File.WriteAllText(Path.Combine(folderPath, $"{cls.Name}.uc"), script);
                        numFiles++;
                    }
                    catch (Exception exception)
                    {
                        return exception.FlattenException();
                    }
                }
                return $"Dumped {numFiles} files.";
            }).ContinueWithOnUIThread(prevTask =>
            {
                pew.EndBusy();
                MessageBox.Show(prevTask.Result);
            });
        }

        public static void RegenCachedPhysBrushData(PackageEditorWindow pew)
        {
            var pcc = pew.Pcc;
            if (pcc is null || !pcc.Game.IsLEGame() || !pew.TryGetSelectedExport(out ExportEntry brushComponentExport) || brushComponentExport.ClassName != "BrushComponent")
            {
                MessageBox.Show(pew, "Must have a BrushComponent selected in an LE pcc.");
                return;
            }
            var bin = brushComponentExport.GetBinaryData<BrushComponent>();
            bin.RegenCachedPhysBrushData();
            brushComponentExport.WriteBinary(bin);
            MessageBox.Show(pew, "Regenerated!");
        }
    }
}
