using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal.Classes
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

        public static readonly FlagSet _me3flags = new FlagSet("Final", "Defined", "Iterator", "Latent",
                                                            "PreOperator", "Singular", "Net", "NetReliable",
                                                            "Simulated", "Exec", "Native", "Event",
                                                            "Operator", "Static", "HasOptionalParms", "Const",
                                                            null, "Public", "Private", "Protected",
                                                            "Delegate", "NetServer", "HasOutParms", "HasDefaults",
                                                            "NetClient", "FuncInherit", "FuncOverrideMatch");

        public List<Token> ScriptBlocks;

        public List<BytecodeSingularToken> SingularTokenList { get; private set; }

        public Function()
        {
        }

        public Function(byte[] raw, ExportEntry export)
        {
            this.export = export;
            
            //need to make a temp copy to support the "Preview Script Changes" functionality in bytecode editor
            if (ObjectBinary.From(export.CreateTempCopyWithNewData(raw)) is not UStruct objBin)
            {
                throw new Exception($"Cannot parse export of class {export.ClassName}!");
            }
            
            memory = raw;
            memsize = raw.Length;
            flagint = (int)((objBin as UFunction)?.FunctionFlags ?? 0);
            flags = new FlagValues(flagint, export.Game == MEGame.ME3 || export.Game.IsLEGame()  ? _me3flags : _flags);
            nativeindex = (objBin as UFunction)?.NativeIndex ?? 0;
            ReadHeader();
            script = objBin.ScriptBytes;
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
                    if (nativeindex > 0)
                        result += $"({nativeindex})";
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

            // OLD CODE
            //List<ExportEntry> childrenReversed = export.FileRef.Exports.Where(x => x.idxLink == export.UIndex).ToList();
            //childrenReversed.Reverse();

            // NEW CODE
            if (!export.IsClass)
            {
                List<ExportEntry> childrenReversed = new List<ExportEntry>();
                var childIdx = EndianReader.ToInt32(export.DataReadOnly, 0x14, export.FileRef.Endian);
                while (export.FileRef.TryGetUExport(childIdx, out var parsingExp))
                {
                    childrenReversed.Add(parsingExp);
                    childIdx = EndianReader.ToInt32(parsingExp.DataReadOnly, 0x10, export.FileRef.Endian);
                }

                //Get local children of this function
                foreach (ExportEntry export in childrenReversed)
                {
                    //Reading parameters info...
                    if (export.ClassName.EndsWith("Property"))
                    {
                        UnrealFlags.EPropertyFlags ObjectFlagsMask = (UnrealFlags.EPropertyFlags)EndianReader.ToUInt64(export.DataReadOnly, 0x18, export.FileRef.Endian);
                        if (ObjectFlagsMask.Has(UnrealFlags.EPropertyFlags.Parm) && !ObjectFlagsMask.Has(UnrealFlags.EPropertyFlags.ReturnParm))
                        {
                            if (paramCount > 0)
                            {
                                result += ", ";
                            }

                            if (export.ClassName is "ObjectProperty" or "StructProperty")
                            {
                                var uindexOfOuter = EndianReader.ToInt32(export.DataReadOnly, export.DataSize - 4, export.FileRef.Endian);
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

                            //if (ObjectFlagsMask.Has(UnrealFlags.EPropertyFlags.OptionalParm) && Statements.Count > 0)
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
                        if (ObjectFlagsMask.Has(UnrealFlags.EPropertyFlags.ReturnParm))
                        {
                            break; //return param
                        }
                    }
                }
                result += ")";
            }
            return result;
        }

        public string GetFlags()
        {
            return $"Flags ({flags.GetFlagsString()})";
        }

        internal bool Native => HasFlag("Native");

        private string GetReturnType()
        {
            // NEW CODE
            if (!export.IsClass)
            {
                var childIdx = EndianReader.ToInt32(export.DataReadOnly, 0x14, export.FileRef.Endian);

                while (export.FileRef.TryGetUExport(childIdx, out var parsingExp))
                {
                    var data = parsingExp.DataReadOnly;
                    if (parsingExp.ObjectName == "ReturnValue")
                    {
                        if (parsingExp.ClassName is "ObjectProperty" or "StructProperty")
                        {
                            var uindexOfOuter = EndianReader.ToInt32(data, parsingExp.DataSize - 4,
                                export.FileRef.Endian);
                            IEntry entry = parsingExp.FileRef.GetEntry(uindexOfOuter);
                            if (entry != null)
                            {
                                return entry.ObjectName;
                            }
                        }

                        return parsingExp.ClassName;
                    }
                    childIdx = EndianReader.ToInt32(data, 0x10, export.FileRef.Endian);
                }
            }

            return null;

            // OLD CODE
            //var returnValue = export.FileRef.Exports.SingleOrDefault(e => e.ObjectName == "ReturnValue" && e.idxLink == export.UIndex);
            //if (returnValue != null)
            //{
            //    if (returnValue.ClassName == "ObjectProperty" || returnValue.ClassName == "StructProperty")
            //    {
            //        var uindexOfOuter = EndianReader.ToInt32(returnValue.Data, returnValue.Data.Length - 4, export.FileRef.Endian);
            //        IEntry entry = returnValue.FileRef.GetEntry(uindexOfOuter);
            //        if (entry != null)
            //        {
            //            return entry.ObjectName;
            //        }
            //    }
            //    return returnValue.ClassName;
            //}
            //return null;
        }

        public void ReadHeader()
        {
            int pos = export.IsClass ? 4 : 12;
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
            int byteCodeStartPos = export.Game is MEGame.UDK ? export.IsClass ? 0x28 : 0x30 : export.IsClass ? 0x18 : 0x20;
            var parsedData = Bytecode.ParseBytecode(script, export);
            ScriptBlocks = parsedData.Item1;
            SingularTokenList = parsedData.Item2;

            // Calculate memory offsets
            List<int> objRefPositions = ScriptBlocks.SelectMany(tok => tok.inPackageReferences)
                .Where(tup => tup.type == Token.INPACKAGEREFTYPE_ENTRY)
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
                var diskPos = t.pos - byteCodeStartPos;
                if (diskPos >= 0 && diskPos < DiskToMemPosMap.Length)
                {
                    t.memPos = DiskToMemPosMap[diskPos];
                }
            }
        }

        public int[] DiskToMemPosMap { get; set; }
    }
}
