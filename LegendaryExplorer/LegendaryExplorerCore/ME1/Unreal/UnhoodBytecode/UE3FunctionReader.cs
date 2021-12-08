/*
 * This file is from the UnHood Project
 * https://github.com/yole/unhood
 * Modified by Mgamerz (2019)
 */

using System.IO;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode
{
    public static class UE3FunctionReader
    {
        public static readonly FlagSet _flagSet = new FlagSet("Final", "Defined", "Iterator", "Latent",
            "PreOperator", "Singular", "Net", "NetReliable",
            "Simulated", "Exec", "Native", "Event",
            "Operator", "Static", "Const", null,
            null, "Public", "Private", "Protected",
            "Delegate", "NetServer", "HasOutParms", "HasDefaults",
            "NetClient", "FuncInherit", "FuncOverrideMatch");

        //public static string ReadFunction(ExportEntry export)
        //{
        //    UnFunction func = ReadInstance(export.FileRef, new BinaryReader(new MemoryStream(export.Data)), export);
        //    TextBuilder tb = new TextBuilder();
        //    try
        //    {
        //        func.Decompile(tb);
        //    }
        //    catch (Exception e)
        //    {
        //        return "Error reading function: " + e.ToString();
        //    }
        //    return tb.ToString();
        //}

        public static UnFunction ReadState(ExportEntry export, byte[] dataOverride = null)
        {
            MemoryStream memoryStream = dataOverride == null ? export.GetReadOnlyBinaryStream() : new MemoryStream(dataOverride);

            using var reader = new EndianReader(memoryStream) { Endian = export.FileRef.Endian };
            if (dataOverride is not null)
            {
                reader.ReadBytes(export.IsClass ? 4 : 12); //netindex?, none
            }
            int super = reader.ReadInt32();
            int nextCompilingChainItem = reader.ReadInt32();
            reader.ReadBytes(12);
            int line = reader.ReadInt32(); //??
            int textPos = reader.ReadInt32(); //??
            int scriptSize = reader.ReadInt32();
            byte[] bytecode = reader.BaseStream.ReadFully(); //read the rest of the state
            return new UnFunction(export, "STATE", new FlagValues(0, _flagSet), bytecode, 0, 0);
        }

        /// <summary>
        /// Reads the function and returns a parsed object contianing information about the function
        /// Ported from Unhood (Modified by Mgamerz)
        /// </summary>
        /// <param name="export"></param>
        /// <returns></returns>
        public static UnFunction ReadFunction(ExportEntry export, byte[] dataOverride = null)
        {
            MemoryStream memoryStream = dataOverride == null ? export.GetReadOnlyBinaryStream() : new MemoryStream(dataOverride);

            using var reader = new EndianReader(memoryStream) { Endian = export.FileRef.Endian };
            if (dataOverride is not null)
            {
                reader.ReadBytes(12); //netindex?, none
            }
            int super = reader.ReadInt32();
            int nextCompilingChainItem = reader.ReadInt32();
            if (!export.Game.IsLEGame())
            {
                reader.ReadBytes(12);
                int line = reader.ReadInt32(); // UE3 Debugger leftovers
                int textPos = reader.ReadInt32(); // UE3 Debugger leftovers
            }
            else
            {
                reader.Skip(8); // ChildProbe, Line
            }

            int scriptSize = reader.ReadInt32();
            byte[] bytecode = reader.ReadBytes(scriptSize);
            int nativeIndex = reader.ReadInt16();
            int operatorPrecedence = reader.ReadByte();
            int functionFlags = reader.ReadInt32();
            if (export.Game.IsOTGame() && (functionFlags & _flagSet.GetMask("Net")) != 0)
            {
                reader.ReadInt16(); // repOffset
            }

            int friendlyNameIndex = reader.ReadInt32();
            reader.ReadInt32();
            return new UnFunction(export, export.FileRef.GetNameEntry(friendlyNameIndex),
                                  new FlagValues(functionFlags, _flagSet), bytecode, nativeIndex, operatorPrecedence);
        }
    }
}
