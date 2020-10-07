using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Script;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;

namespace ME3Explorer.PackageEditor.Experiments
{
    /// <summary>
    /// Class for SirCxyrtyx experimental code
    /// </summary>
    public class PackageEditorExperimentsS
    {

        class OpcodeInfo
        {
            public readonly HashSet<string> PropTypes = new HashSet<string>();
            public readonly HashSet<string> PropLocations = new HashSet<string>();

            public readonly List<(string filePath, int uIndex, int position)> Usages =
                new List<(string filePath, int uIndex, int position)>();
        }
        public static void ScanStuff(PackageEditorWPF pewpf)
        {
            //var filePaths = MELoadedFiles.GetOfficialFiles(MEGame.ME3);//.Concat(MELoadedFiles.GetOfficialFiles(MEGame.ME2));//.Concat(MELoadedFiles.GetOfficialFiles(MEGame.ME1));
            //var filePaths = MELoadedFiles.GetAllFiles(game);
            /*"Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc" */
            var filePaths = new[] { "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc" }.Select(f => Path.Combine(ME3Directory.cookedPath, f));
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
                //preload base files for faster scanning
                using var baseFiles = MEPackageHandler.OpenMEPackages(EntryImporter.FilesSafeToImportFrom(MEGame.ME3)
                    .Select(f => Path.Combine(ME3Directory.cookedPath, f)));
                baseFiles.Add(
                    MEPackageHandler.OpenMEPackage(Path.Combine(ME3Directory.cookedPath, "BIOP_MP_COMMON.pcc")));

                foreach (string filePath in filePaths)
                {
                    //ScanShaderCache(filePath);
                    //ScanMaterials(filePath);
                    //ScanStaticMeshComponents(filePath);
                    //ScanLightComponents(filePath);
                    //ScanLevel(filePath);
                    //if (findClass(filePath, "ShaderCache", true)) break;
                    //findClassesWithBinary(filePath);
                    ScanScripts2(filePath);
                    //if (interestingExports.Count > 0)
                    //{
                    //    break;
                    //}
                    //if (resolveImports(filePath)) break;
                }
            }).ContinueWithOnUIThread(prevTask =>
            {
                pewpf.IsBusy = false;
                interestingExports.Add(new EntryStringPair(null, string.Join("\n", extraInfo)));
                var listDlg = new ListDialog(interestingExports, "Interesting Exports", "", pewpf)
                {
                    DoubleClickEntryHandler = entryItem =>
                    {
                        if (entryItem?.Entry is IEntry entryToSelect)
                        {
                            PackageEditorWPF p = new PackageEditorWPF();
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
                            exp.SetBinaryData(ObjectBinary.From(exp));
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
                                interestingExports.Add(new EntryStringPair(null,
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
                                interestingExports.Add(new EntryStringPair(null, $"{i}: {filePath}"));
                                return;
                            }

                            binData.Skip(normalParams * 29);

                            int unrealVersion = binData.ReadInt32();
                            int licenseeVersion = binData.ReadInt32();
                            if (unrealVersion != 684 || licenseeVersion != 194)
                            {
                                interestingExports.Add(new EntryStringPair(null,
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
                        interestingExports.Add(new EntryStringPair(null, $"{filePath}\n{exception}"));
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
                foreach (ExportEntry exp in pcc.Exports.Reverse().Where(exp => exp.ClassName == "Function" && exp.Parent.ClassName == "Class" && !exp.GetBinaryData<UFunction>().FunctionFlags.Has(FunctionFlags.Native)))
                {
                    try
                    {
                        var originalData = exp.Data;
                        (_, string originalScript) = ME3ScriptCompiler.DecompileExport(exp);
                        (ASTNode ast, MessageLog log) = ME3ScriptCompiler.CompileFunction(exp, originalScript);
                        if (ast == null || log.AllErrors.Count > 0)
                        {
                            interestingExports.Add(exp);
                            continue;
                        }

                        if (!originalData.SequenceEqual(exp.Data))
                        {
                            interestingExports.Add(exp);
                            comparisonDict.Add($"{exp.UIndex} {exp.FileRef.FilePath}", (originalData, exp.Data));
                            //File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "original.bin"), originalData);
                            //File.WriteAllBytes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "new.bin"), exp.Data);
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

    }
}
