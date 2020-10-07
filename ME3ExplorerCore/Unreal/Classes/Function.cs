using System;
using System.Collections.Generic;
using System.Linq;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.ME1.Unreal.UnhoodBytecode;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Unreal.Classes
{
    public class Function
    {
        public ExportEntry export;
        //public IMEPackage pcc;
        public byte[] memory;
        public byte[] script;
        public int memsize;
        public int NextItemInLoadingChain;
        public int FunctionSuperclass;
        public int ChildProbeStart;
        public int DiskSize;
        public int MemorySize;
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
        public string ScriptText; //This is not used but is referenced by PAckage Editor Classic, SCriptDB. Probably does not work since I refactored
        //the parsing script pretty heavily
        public string HeaderText;
        public List<Token> ScriptBlocks;

        public List<BytecodeSingularToken> SingularTokenList { get; private set; }

        public Function()
        {
        }

        public Function(byte[] raw, ExportEntry export, int clippingSize = 32)
        {
            this.export = export;
            memory = raw;
            memsize = raw.Length;
            flagint = GetFlagInt();
            flags = new FlagValues(flagint, _flags);
            nativeindex = GetNatIdx();
            Deserialize(clippingSize);
        }

        public bool HasFlag(string name)
        {
            return flags.HasFlag(name);
        }

        public string GetSignature()
        {
            string result = "";
            if (export.ClassName == "Function")
            {
                if (Native)
                {
                    result += "native";
                    if (GetNatIdx() > 0)
                        result += $"({GetNatIdx()})";
                    result += " ";
                }

                flags.Except("Native", "Event", "Delegate", "Defined", "Public", "HasDefaults", "HasOutParms").Each(f => result += f.ToLower() + " ");
            }

            if (HasFlag("Event"))
                result += "event ";
            else if (HasFlag("Delegate"))
                result += "delegate ";
            else
                result += export.ClassName.ToLower() + " ";
            string type = GetReturnType();
            if (type != null)
            {
                result += type + " ";
            }
            result += export.ObjectName.Instanced + "(";
            int paramCount = 0;
            var locals = new List<ExportEntry>();

            //Tokens = new List<BytecodeSingularToken>();
            //Statements = ReadBytecode();
            List<ExportEntry> childrenReversed = export.FileRef.Exports.Where(x => x.idxLink == export.UIndex).ToList();
            childrenReversed.Reverse();

            //Get local children of this function
            foreach (ExportEntry export in childrenReversed)
            {
                //Reading parameters info...
                if (export.ClassName.EndsWith("Property"))
                {
                    UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)EndianReader.ToUInt64(export.Data, 0x18, export.FileRef.Endian);
                    if (ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.Parm) && !ObjectFlagsMask.HasFlag(UnrealFlags.EPropertyFlags.ReturnParm))
                    {
                        if (paramCount > 0)
                        {
                            result += ", ";
                        }

                        if (export.ClassName == "ObjectProperty" || export.ClassName == "StructProperty")
                        {
                            var uindexOfOuter = EndianReader.ToInt32(export.Data, export.Data.Length - 4, export.FileRef.Endian);
                            IEntry entry = export.FileRef.GetEntry(uindexOfOuter);
                            if (entry != null)
                            {
                                result += entry.ObjectName.Instanced + " ";
                            }
                        }
                        else
                        {
                            result += UnFunction.GetPropertyType(export) + " ";
                        }

                        result += export.ObjectName.Instanced;
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
            var nativeBackOffset = export.FileRef.Game < MEGame.ME3 ? 7 : 6;
            return EndianReader.ToInt16(memory, memsize - nativeBackOffset, export.FileRef.Endian);

        }

        public int GetFlagInt()
        {
            return EndianReader.ToInt32(memory, export.ClassName == "Function" ? memsize - 4 : memsize - 10, export.FileRef.Endian);
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
                    var uindexOfOuter = EndianReader.ToInt32(returnValue.Data, returnValue.Data.Length - 4, export.FileRef.Endian);
                    IEntry entry = returnValue.FileRef.GetEntry(uindexOfOuter);
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
            int pos = 12;
            FunctionSuperclass = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            pos += 4;
            NextItemInLoadingChain = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            pos += 4;
            ChildProbeStart = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            pos += 4;
            DiskSize = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            pos += 4;
            MemorySize = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            pos += 4;
        }

        public void ParseFunction()
        {
            HeaderText = "";
            if (export.FileRef.TryGetEntry(FunctionSuperclass, out var _fs))
            {
                HeaderText += $"Function Superclass: {_fs.UIndex} {_fs.InstancedFullPath}\n";
            }
            if (export.FileRef.TryGetEntry(NextItemInLoadingChain, out var _ni))
            {
                HeaderText += $"Next Item in loading chain: {_ni.UIndex} {_ni.InstancedFullPath}\n";
            }
            if (export.FileRef.TryGetEntry(ChildProbeStart, out var _cps)){
                HeaderText += $"Child Probe Start: {_cps.UIndex} {_cps.InstancedFullPath}\n";
            }
            HeaderText += $"Script Disk Size: {DiskSize}\n";
            HeaderText += $"Script Memory Size: {MemorySize}\n";
            HeaderText += $"Native Index: {nativeindex}\n";
            HeaderText += GetSignature();
            HeaderText += "\n";

            var parsedData = Bytecode.ParseBytecode(script, export);
            ScriptBlocks = parsedData.Item1;
            SingularTokenList = parsedData.Item2;

            // Calculate memory offsets
            List<int> objRefPositions = ScriptBlocks.SelectMany(tok => tok.inPackageReferences)
                .Where(tup => tup.type == Unreal.Token.INPACKAGEREFTYPE_ENTRY)
                .Select(tup => tup.position).ToList();
            int calculatedLength = DiskSize + 4 * objRefPositions.Count;

            DiskToMemPosMap = new int[DiskSize];
            int iDisk = 0;
            int iMem = 0;
            foreach (int objRefPosition in objRefPositions)
            {
                while (iDisk < objRefPosition + 4)
                {
                    DiskToMemPosMap[iDisk] = iMem;
                    iDisk++;
                    iMem++;
                }
                iMem += 4;
            }
            while (iDisk < DiskSize)
            {
                DiskToMemPosMap[iDisk] = iMem;
                iDisk++;
                iMem++;
            }

            foreach (Token t in ScriptBlocks)
            {
                var diskPos = t.pos - 32;
                if (diskPos >= 0 && diskPos < DiskToMemPosMap.Length)
                {
                    t.memPos = DiskToMemPosMap[diskPos];
                }
            }
        }

        public int[] DiskToMemPosMap { get; set; }
    }
}
