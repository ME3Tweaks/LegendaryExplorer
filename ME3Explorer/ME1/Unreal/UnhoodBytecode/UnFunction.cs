/*
 * This file is from the UnHood Project
 * https://github.com/yole/unhood
 * Modified by Mgamerz (2019)
 */

using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ME3Explorer.ME1.Unreal.UnhoodBytecode
{
    public class UnFunction : UnBytecodeOwner
    {
        private readonly string _name;
        private readonly FlagValues _flags;
        private readonly int _nativeIndex;
        private readonly int _operatorPrecedence;
        public StatementList Statements { get; set; }
        public List<BytecodeSingularToken> Tokens;

        internal UnFunction(IExportEntry export, string name, FlagValues flags, byte[] bytecode, int nativeIndex, int operatorPrecedence)
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

        internal string Name { get { return _name; } }
        internal int NativeIndex { get { return _nativeIndex; } }
        internal bool Native { get { return HasFlag("Native"); } }
        internal bool Event { get { return HasFlag("Event"); } }
        internal bool PreOperator { get { return HasFlag("PreOperator"); } }
        internal bool Operator { get { return HasFlag("Operator"); } }
        internal bool PostOperator { get { return Operator && _operatorPrecedence == 0; } }

        public List<Token> ScriptTokens = new List<Token>();

        public override void Decompile(TextBuilder result)
        {
            Decompile(result, true);
        }

        internal bool HasFlag(string name)
        {
            return _flags.HasFlag(name);
        }

        public string FunctionSignature = "";

        public void Decompile(TextBuilder result, bool createControlStatements)
        {
            result.Indent();
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
            result.Append(_self.ObjectName).Append("(");
            int paramCount = 0;
            var locals = new List<IExportEntry>();

            Tokens = new List<BytecodeSingularToken>();
            Statements = ReadBytecode();
            List<IExportEntry> childrenReversed = _self.FileRef.Exports.Where(x => x.idxLink == _self.UIndex).ToList();
            childrenReversed.Reverse();

            //Get local children of this function
            foreach (IExportEntry export in childrenReversed)
            {
                //Reading parameters info...
                if (export.ClassName.EndsWith("Property"))
                {
                    UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)BitConverter.ToUInt64(export.Data, 0x18);
                    if (ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.Parm) && !ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.ReturnParm))
                    {
                        if (paramCount > 0)
                        {
                            result.Append(", ");
                        }

                        if (export.ClassName == "ObjectProperty" || export.ClassName == "StructProperty")
                        {
                            var uindexOfOuter = BitConverter.ToInt32(export.Data, export.Data.Length - 4);
                            IEntry entry = export.FileRef.getEntry(uindexOfOuter);
                            if (entry != null)
                            {
                                result.Append(entry.ObjectName + " ");
                            }
                        }
                        else
                        {
                            result.Append(GetPropertyType(export) + " ");
                        }

                        result.Append(export.ObjectName);
                        paramCount++;

                        if (ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.OptionalParm) && Statements.Count > 0)
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
                    if (ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.ReturnParm))
                    {
                        break; //return param
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
            }
            result.Append(")");

            FunctionSignature = result.ToString();
            if (HasFlag("Defined"))
            {
                result.NewLine().Indent().Append("{").NewLine();
                result.PushIndent();
                foreach (var local in locals)
                {
                    result.Indent().Append("local ").Append(GetPropertyType(local)).Append(" ").Append(local.ObjectName).Append(";").NewLine();
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

        public static string GetPropertyType(IExportEntry exp)
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
                default:
                    return "???";
            }
        }

        private string GetReturnType()
        {
            var returnValue = _self.FileRef.Exports.SingleOrDefault(e => e.ObjectName == "ReturnValue" && e.idxLink == _self.UIndex);
            if (returnValue != null)
            {
                if (returnValue.ClassName == "ObjectProperty" || returnValue.ClassName == "StructProperty")
                {
                    var uindexOfOuter = BitConverter.ToInt32(returnValue.Data, returnValue.Data.Length - 4);
                    IEntry entry = returnValue.FileRef.getEntry(uindexOfOuter);
                    if (entry != null)
                    {
                        return entry.ObjectName;
                    }
                }
                return returnValue.ClassName;
            }
            return null;
        }
    }
}
