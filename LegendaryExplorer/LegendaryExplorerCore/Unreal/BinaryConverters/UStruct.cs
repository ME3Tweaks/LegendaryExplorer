﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

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

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            if (Export.ClassName is "Function" or "State")
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

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            base.ForEachUIndex(game, in action);
            Unsafe.AsRef(action).Invoke(ref Children, "ChildListStart");
            
            if (Export.ClassName is not "ScriptStruct")
            {
                if (Export.Game == MEGame.ME3 || Export.Game.IsLEGame())
                {
                    try
                    {
                        (List<Token> tokens, _) = Bytecode.ParseBytecode(ScriptBytes, Export);
                        foreach (var t in tokens)
                        {
                            var refs = t.inPackageReferences.Where(x => x.type == Token.INPACKAGEREFTYPE_ENTRY);
                            foreach ((int position, int type, int value) in refs)
                            {
                                int temp = value;
                                Unsafe.AsRef(action).Invoke(ref temp, $"Reference inside of function at 0x{position:X}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error decompiling function {Export.InstancedFullPath}: {e.Message}");
                    }
                }
                else if (!Export.IsClass)
                {
                    try
                    {
                        var func = Export.ClassName == "State" ? UE3FunctionReader.ReadState(Export) : UE3FunctionReader.ReadFunction(Export);
                        func.Decompile(new TextBuilder(), false, false); //parse bytecode without signature (it does not contain entry refs)
                        var entryRefs = func.EntryReferences;
                        foreach ((long key, IEntry value) in entryRefs)
                        {
                            int temp = value.UIndex;
                            Unsafe.AsRef(action).Invoke(ref temp, $"Reference inside of function at 0x{key:X}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Error decompiling function {Export.InstancedFullPath}: {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///  Rebuilds the compiling chain of children for this struct. Items with this entry as the parent will participate in the class. Don't use this on functions.
        /// </summary>
        /// <param name="relinkChildrenStructs">Recursively relink children for all structs that are decendants of this struct</param>
        public void UpdateChildrenChain(bool relinkChildrenStructs = false)
        {
            if (this is UFunction)
            {
                //UpdateChildrenChain not yet working for function exports, use function compilation instead
                return;
            }
            var children = Export.GetChildren<ExportEntry>().ToList();
            for (int i = children.Count - 1; i >= 0; --i)
            {
                var c = children[i];
                if (ObjectBinary.From(c) is UField uf)
                {
                    uf.Next = i == 0 ? 0 : children[i - 1].UIndex;
                    if (relinkChildrenStructs && uf is UStruct st)
                    {
                        st.UpdateChildrenChain(true);
                    }
                    c.WriteBinary(uf);
                }
                else
                {
                    Debug.WriteLine($"Can't link non UField {c.InstancedFullPath}");
                }
            }
            Children = children.Any() ? children[^1].UIndex : 0;
        }
    }
}
