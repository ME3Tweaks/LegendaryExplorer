/*
 * This file is from the UnHood Project
 * https://github.com/yole/unhood
 * Modified by Mgamerz (2019)
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode
{
    public class UnFunction : UnBytecodeOwner
    {
        private readonly string _name;
        private readonly FlagValues _flags;
        private readonly int _nativeIndex;
        private readonly int _operatorPrecedence;
        public StatementList Statements { get; set; }
        public List<BytecodeSingularToken> Tokens;

        public UnFunction(ExportEntry export, string name, FlagValues flags, byte[] bytecode, int nativeIndex, int operatorPrecedence)
            : base(export, bytecode)
        {
            _name = name;
            _flags = flags;
            _nativeIndex = nativeIndex;
            _operatorPrecedence = operatorPrecedence;
        }

        public string GetFlags()
        {
            return _flags.GetFlagsString();
        }

        public byte[] Bytecode
        {
            get { return _bytecode; }
        }

        public string Name { get { return _name; } }
        public int NativeIndex { get { return _nativeIndex; } }
        public bool Native { get { return HasFlag("Native"); } }
        public bool Event { get { return HasFlag("Event"); } }
        public bool PreOperator { get { return HasFlag("PreOperator"); } }
        public bool Operator { get { return HasFlag("Operator"); } }
        public bool PostOperator { get { return Operator && _operatorPrecedence == 0; } }

        public List<Token> ScriptTokens = new List<Token>();

        public override void Decompile(TextBuilder result, bool parseSignature)
        {
            Decompile(result, true, parseSignature);
        }

        public bool HasFlag(string name)
        {
            return _flags.HasFlag(name);
        }

        public string FunctionSignature = "";

        public void Decompile(TextBuilder result, bool createControlStatements, bool parseSignature)
        {
            result.Indent();
            if (parseSignature)
            {
                if (Native)
                {
                    result.Append("native");
                    if (_nativeIndex > 0)
                        result.Append("(").Append(_nativeIndex).Append(")");
                    result.Append(" ");
                }

                _flags.Except("Native", "Event", "Delegate", "Defined", "Public", "HasDefaults", "HasOutParms").Each(f => result.Append(f.ToLower() + " "));

                if (HasFlag("Event"))
                    result.Append("event ");
                else if (HasFlag("Delegate"))
                    result.Append("delegate ");
                else
                    result.Append("function ");
                string type = GetReturnType();
                if (type != null)
                {
                    result.Append(type).Append(" ");
                }

                result.Append(_self.ObjectName.Instanced).Append("(");
            }

            int paramCount = 0;
            var locals = new List<ExportEntry>();

            Tokens = new List<BytecodeSingularToken>();
            Statements = ReadBytecode(out var bytecodeReader);

            // UI only
            // Man this code is bad
            // sorry
#if !DEBUG && !AZURE
            try
            {
#endif
            var s = bytecodeReader._reader;
            if (Export.ClassName == "State")
            {
                s.BaseStream.Skip(0x10); //Unknown
                var stateFlags = s.ReadInt32();
                var unknown = s.ReadInt16();
                var functionMapCount = s.ReadInt32();
                for (int i = 0; i < functionMapCount; i++)
                {
                    int spos = (int)s.BaseStream.Position;
                    var name = bytecodeReader.ReadName();
                    var entry = bytecodeReader.ReadEntryRef(out var _);
                    Statements.statements.Add(new Statement(spos, (int)s.BaseStream.Position, new NothingToken(spos, $"  {name} => {entry.InstancedFullPath}()"), bytecodeReader));
                }
            }
#if !DEBUG && !AZURE
            }
            catch (Exception)
            {
                Debug.WriteLine("Exception..");
            }
#endif
            NameReferences = bytecodeReader.NameReferences;
            EntryReferences = bytecodeReader.EntryReferences;
            //var childIdx = EndianReader.ToInt32(Export.DataReadOnly, 0x18, Export.FileRef.Endian);

            if (parseSignature)
            {
                try
                {
                    var childIdx = EndianReader.ToInt32(Export.DataReadOnly, 0x18, Export.FileRef.Endian);
                    var children = new List<ExportEntry>();
                    while (Export.FileRef.TryGetUExport(childIdx, out var parsingExp))
                    {
                        children.Add(parsingExp);
                        //childIdx = EndianReader.ToInt32(parsingExp.DataReadOnly, 0x10, Export.FileRef.Endian);
                        var nCdx = EndianReader.ToInt32(parsingExp.DataReadOnly, 0x10, Export.FileRef.Endian);
                        if (nCdx == childIdx || children.Any(x => x.UIndex == nCdx))
                        {
                            throw new Exception("Infinite loop detected while parsing function!");
                        }

                        childIdx = EndianReader.ToInt32(parsingExp.DataReadOnly, 0x10, Export.FileRef.Endian);
                    }

                    //Get local children of this function
                    foreach (ExportEntry export in children)
                    {
                        //Reading parameters info...
                        if (export.ClassName.EndsWith("Property"))
                        {
                            UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)EndianReader.ToUInt64(export.DataReadOnly, 0x18, export.FileRef.Endian);
                            //UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)EndianReader.ToUInt64(export.DataReadOnly, 0x18, export.FileRef.Endian);
                            if (ObjectFlagsMask.Has(UnrealFlags.EPropertyFlags.Parm) && !ObjectFlagsMask.Has(UnrealFlags.EPropertyFlags.ReturnParm))
                            {
                                if (paramCount > 0)
                                {
                                    result.Append(", ");
                                }

                                if (export.ClassName == "ObjectProperty" || export.ClassName == "StructProperty")
                                {
                                    //var uindexOfOuter = EndianReader.ToInt32(export.DataReadOnly, export.DataSize - 4, export.FileRef.Endian);
                                    var uindexOfOuter = EndianReader.ToInt32(export.DataReadOnly, export.DataSize - 4, export.FileRef.Endian);
                                    IEntry entry = export.FileRef.GetEntry(uindexOfOuter);
                                    if (entry != null)
                                    {
                                        result.Append($"{entry.ObjectName.Instanced} ");
                                    }
                                }
                                else
                                {
                                    result.Append($"{GetPropertyType(export)} ");
                                }

                                result.Append(export.ObjectName.Instanced);
                                paramCount++;

                                if (ObjectFlagsMask.Has(UnrealFlags.EPropertyFlags.OptionalParm) && Statements.Count > 0)
                                {
                                    if (Statements[0].Token is NothingToken)
                                        Statements.RemoveRange(0, 1);
                                    else if (Statements[0].Token is DefaultParamValueToken)
                                    {
                                        result.Append(" = ").Append(Statements[0].Token.ToString());
                                        Statements.RemoveRange(0, 1);
                                    }
                                }
                            }

                            if (ObjectFlagsMask.Has(UnrealFlags.EPropertyFlags.ReturnParm))
                            {
                                break; //return param
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    Debug.WriteLine(@"Exception parsing parameters of function - if relinking this is expected as values have not been corrected yet");
                }
            }

            //object instance = export.ReadInstance();
            //if (instance is UnClassProperty)
            //{
            //    var prop = (UnClassProperty)instance;
            //    if (prop.Parm)
            //    {
            //        if (!prop.ReturnParm)
            //        {
            //            if (paramCount > 0)
            //                result.Append(", ");

            //            prop.Flags.Except("Parm").Each(f => result.Append(f.ToLower() + " "));
            //            result.Append(prop.GetPropertyType()).Append(" ").Append(export.ObjectName);
            //            if (prop.OptionalParm && statements.Count > 0)
            //            {
            //                if (statements[0].Token is NothingToken)
            //                    statements.RemoveRange(0, 1);
            //                else if (statements[0].Token is DefaultParamValueToken)
            //                {
            //                    result.Append(" = ").Append(statements[0].Token.ToString());
            //                    statements.RemoveRange(0, 1);
            //                }
            //            }
            //            paramCount++;
            //        }
            //    }
            //    else
            //    {
            //        locals.Add(prop);
            //    }
            //}

            result.Append(")");

            FunctionSignature = result.ToString();
            if (HasFlag("Defined"))
            {
                result.NewLine().Indent().Append("{").NewLine();
                result.PushIndent();
                foreach (var local in locals)
                {
                    result.Indent().Append("local ").Append(GetPropertyType(local)).Append(" ").Append(local.ObjectName.Instanced).Append(";").NewLine();
                }
                result.PopIndent();   // will be pushed again in DecompileBytecode()
                DecompileBytecode(Statements, result, createControlStatements);
                result.Indent().Append("}").NewLine().NewLine();
            }
            else
            {
                result.Append(";").NewLine().NewLine();
            }
        }

        public Dictionary<long, IEntry> EntryReferences { get; set; }

        public Dictionary<long, NameReference> NameReferences { get; set; }

        public static string GetPropertyType(ExportEntry exp)
        {
            switch (exp.ClassName)
            {
                case "BoolProperty":
                    return "bool";
                case "NameProperty":
                    return "Name";
                case "IntProperty":
                    return "int";
                case "FloatProperty":
                    return "float";
                case "StrProperty":
                    return "String";
                case "ByteProperty":
                    return "byte";
                case "StringRefProperty":
                    return "stringref";
                case "ClassProperty":
                    return "UClass";
                case "ComponentProperty":
                    return "Component";
                case "ArrayProperty":
                    return "ArrayProperty";
                case "DelegateProperty":
                    return "delegate";
                case "InterfaceProperty":
                    return "interface";
                default:
                    Debug.WriteLine("Unknown property type for ME1 Function signature parsing: " + exp.ClassName);
                    return "???";
            }
        }

        private string GetReturnType()
        {
            //var childIdx = EndianReader.ToInt32(Export.DataReadOnly, 0x18, Export.FileRef.Endian);
            try
            {
                var childProbe = Export.Game.IsLEGame() ? 0x14 : 0x18;
                var childIdx = EndianReader.ToInt32(Export.DataReadOnly, childProbe, Export.FileRef.Endian);
                while (Export.FileRef.TryGetUExport(childIdx, out var parsingExp))
                {
                    var data = parsingExp.DataReadOnly;
                    if (parsingExp.ObjectName == "ReturnValue")
                    {
                        if (parsingExp.ClassName == "ObjectProperty" || parsingExp.ClassName == "StructProperty")
                        {
                            var uindexOfOuter = EndianReader.ToInt32(data, parsingExp.DataSize - 4, _self.FileRef.Endian);
                            IEntry entry = parsingExp.FileRef.GetEntry(uindexOfOuter);
                            if (entry != null)
                            {
                                return entry.ObjectName;
                            }
                        }

                        return parsingExp.ClassName;
                    }

                    var nCdx = EndianReader.ToInt32(data, 0x10, Export.FileRef.Endian);
                    if (nCdx == childIdx || (parsingExp.Parent.UIndex != Export.UIndex)) // not sure what second half of this if statement is for, but i'm not going not modify it
                    {
                        throw new Exception("Infinite loop detected while parsing function!");
                    }

                    childIdx = nCdx;
                }
            }
            catch (Exception e)
            {
                // Determining the return type may fail if we are relinking as the data has not yet been corrected.
                Debug.WriteLine($@"Error getting return type of func: {e.Message} - this may be intended if relinking is taking place");
            }

            return null;
        }
    }
}
