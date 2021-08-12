using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class UStruct : UField
    {
        public UIndex Children;
        private int Line; //ME1/ME2
        private int TextPos; //ME1/ME2
        public int ScriptBytecodeSize; //ME3, LE
        public int ScriptStorageSize;
        public byte[] ScriptBytes;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            if (sc.Game is MEGame.ME1 or MEGame.ME2 && sc.Pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
            }
            sc.Serialize(ref Children);
            if (sc.Game <= MEGame.ME2 && sc.Pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref Line);
                sc.Serialize(ref TextPos);
            }

            if (sc.Game is MEGame.ME3 || sc.Game.IsLEGame() || sc.Pcc.Platform == MEPackage.GamePlatform.PS3)
            {
                sc.Serialize(ref ScriptBytecodeSize);
            }
            if (sc.IsSaving)
            {
                ScriptStorageSize = ScriptBytes.Length;
            }
            sc.Serialize(ref ScriptStorageSize);
            if (sc.Game is MEGame.ME1 or MEGame.ME2 )
            {
                ScriptBytecodeSize = ScriptStorageSize;
            }
            sc.Serialize(ref ScriptBytes, ScriptStorageSize);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((Children, "ChildListStart"));

            if (Export.ClassName is "Function" or "State")
            {
                if (Export.Game == MEGame.ME3 || Export.Game.IsLEGame())
                {
                    try
                    {
                        (List<Token> tokens, _) = Bytecode.ParseBytecode(ScriptBytes, Export);
                        foreach (var t in tokens)
                        {
                            {
                                var refs = t.inPackageReferences.Where(x => x.type == Token.INPACKAGEREFTYPE_ENTRY);
                                uIndices.AddRange(refs.Select(x => (new UIndex(x.value), $"Reference inside of function at 0x{x.position:X}")));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error decompiling function {Export.InstancedFullPath}: {e.Message}");
                    }
                }
                else
                {
                    try
                    {
                        var func = Export.ClassName == "State" ? UE3FunctionReader.ReadState(Export) : UE3FunctionReader.ReadFunction(Export);
                        func.Decompile(new TextBuilder(), false, false); //parse bytecode without signature (it does not contain entry refs)
                        var entryRefs = func.EntryReferences;
                        uIndices.AddRange(entryRefs.Select(x =>
                            (new UIndex(x.Value.UIndex), $"Reference inside of function at 0x{x.Key:X}")));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error decompiling function {Export.InstancedFullPath}: {e.Message}");
                    }
                }
            }

            return uIndices;
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            if (Export.ClassName == "Function" || Export.ClassName == "State")
            {
                if (Export.Game is MEGame.ME3 || Export.Game.IsLEGame())
                {
                    try
                    {
                        (List<Token> tokens, _) = Bytecode.ParseBytecode(ScriptBytes, Export);
                        foreach (var t in tokens)
                        {
                            {
                                var refs = t.inPackageReferences.Where(x => x.type == Token.INPACKAGEREFTYPE_NAME);
                                names.AddRange(refs.Select(x => (new NameReference(Export.FileRef.GetNameEntry(x.value)), $"Name inside of function at 0x{x.position:X}")));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error decompiling function {Export.InstancedFullPath}: {e.Message}");
                    }
                }
                else
                {
                    try
                    {
                        var func = Export.ClassName == "State" ? UE3FunctionReader.ReadState(Export) : UE3FunctionReader.ReadFunction(Export);
                        func.Decompile(new TextBuilder(), false, false); //parse bytecode without signature (it does not contain entry refs)
                        var entryRefs = func.NameReferences;
                        names.AddRange(entryRefs.Select(x => (x.Value, $"Name inside of function at 0x{x.Key:X}")));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error decompiling function {Export.InstancedFullPath}: {e.Message}");
                    }
                }
            }

            return names;
        }
    }
}
