using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ME3Explorer.ME1.Unreal.UnhoodBytecode;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.Classes
{
    public class Function
    {
        public IExportEntry export;
        //public IMEPackage pcc;
        public byte[] memory;
        public byte[] script;
        public int memsize;
        public int child;
        public int unk1;
        public int unk2;
        public int size;
        public int flagint;
        private FlagValues flags;
        public int nativeindex;

        public static readonly FlagSet _flags = new FlagSet("Final", "Defined", "Iterator", "Latent",
                                    "PreOperator", "Singular", "Net", "NetReliable",
                                    "Simulated", "Exec", "Native", "Event",
                                    "Operator", "Static", "Const", null,
                                    null, "Public", "Private", "Protected",
                                    "Delegate", "NetServer", "HasOutParms", "HasDefaults",
                                    "NetClient", "FuncInherit", "FuncOverrideMatch");
        internal string ScriptText; //This is not used but is referenced by PAckage Editor Classic, SCriptDB. Probably does not work since I refactored
        //the parsing script pretty heavily
        public string HeaderText;
        public List<Token> ScriptBlocks;

        internal List<BytecodeSingularToken> SingularTokenList { get; private set; }

        public Function()
        {
        }

        public Function(byte[] raw, IExportEntry export, int clippingSize = 32)
        {
            this.export = export;
            memory = raw;
            memsize = raw.Length;
            flagint = GetFlagInt();
            flags = new FlagValues(flagint, _flags);
            nativeindex = GetNatIdx();
            Deserialize(clippingSize);
        }

        internal bool HasFlag(string name)
        {
            return flags.HasFlag(name);
        }

        public string GetSignature()
        {
            string result = "";
            if (Native)
            {
                result += "native";
                if (GetNatIdx() > 0)
                    result += $"({GetNatIdx()})";
                result += " ";
            }

            flags.Except("Native", "Event", "Delegate", "Defined", "Public", "HasDefaults", "HasOutParms").Each(f => result += f.ToLower() + " ");

            if (HasFlag("Event"))
                result += "event ";
            else if (HasFlag("Delegate"))
                result += "delegate ";
            else
                result += "function ";
            string type = GetReturnType();
            if (type != null)
            {
                result += type + " ";
            }
            result += export.ObjectName + "(";
            int paramCount = 0;
            var locals = new List<IExportEntry>();

            //Tokens = new List<BytecodeSingularToken>();
            //Statements = ReadBytecode();
            List<IExportEntry> childrenReversed = export.FileRef.Exports.Where(x => x.idxLink == export.UIndex).ToList();
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
                            result += ", ";
                        }

                        if (export.ClassName == "ObjectProperty" || export.ClassName == "StructProperty")
                        {
                            var uindexOfOuter = BitConverter.ToInt32(export.Data, export.Data.Length - 4);
                            IEntry entry = export.FileRef.getEntry(uindexOfOuter);
                            if (entry != null)
                            {
                                result += entry.ObjectName + " ";
                            }
                        }
                        else
                        {
                            result += UnFunction.GetPropertyType(export) + " ";
                        }

                        result += export.ObjectName;
                        paramCount++;

                        //if (ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.OptionalParm) && Statements.Count > 0)
                        //{
                        //    if (Statements[0].Token is NothingToken)
                        //        Statements.RemoveRange(0, 1);
                        //    else if (Statements[0].Token is DefaultParamValueToken)
                        //    {
                        //        result += " = ").Append(Statements[0].Token.ToString());
                        //        Statements.RemoveRange(0, 1);
                        //    }
                        //}
                    }
                    if (ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.ReturnParm))
                    {
                        break; //return param
                    }
                }
            }
            result += ")";
            return result;
        }

        public int GetNatIdx()
        {

            return BitConverter.ToInt16(memory, memsize - 6);
        }

        public int GetFlagInt()
        {
            return BitConverter.ToInt32(memory, memsize - 4);
        }

        public string GetFlags()
        {
            return $"Flags ({flags.GetFlagsString()})";
        }

        public void Deserialize(int clippingSize = 32)
        {

            ReadHeader();
            script = new byte[memsize - clippingSize];
            for (int i = clippingSize; i < memsize; i++)
                script[i - clippingSize] = memory[i];
        }
        internal bool Native { get { return HasFlag("Native"); } }
        private string GetReturnType()
        {
            var returnValue = export.FileRef.Exports.SingleOrDefault(e => e.ObjectName == "ReturnValue" && e.idxLink == export.UIndex);
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

        public void ReadHeader()
        {
            int pos = 16;
            child = BitConverter.ToInt32(memory, pos) - 1;
            pos += 4;
            unk1 = BitConverter.ToInt32(memory, pos) - 1;
            pos += 4;
            unk2 = BitConverter.ToInt32(memory, pos);
            pos += 4;
            size = BitConverter.ToInt32(memory, pos);
            pos += 4;
        }

        public void ParseFunction()
        {
            HeaderText = "";
            HeaderText += "Childindex : " + child + "\n";
            HeaderText += "Unknown1 : " + unk1 + "\n";
            HeaderText += "Unknown2 : " + unk2 + "\n";
            HeaderText += "Script Size : " + size + "\n";
            HeaderText += GetFlags() + "\n";
            HeaderText += "Native Index: " + nativeindex;
            var parsedData = Bytecode.ParseBytecode(script, export.FileRef);
            ScriptBlocks = parsedData.Item1;
            SingularTokenList = parsedData.Item2;
        }
    }
}
