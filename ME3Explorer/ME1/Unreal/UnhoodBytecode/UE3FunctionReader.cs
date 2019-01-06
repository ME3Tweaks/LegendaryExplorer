/*
 * This file is from the UnHood Project
 * https://github.com/yole/unhood
 * Modified by Mgamerz (2019)
 */

using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ME3Explorer.ME1.Unreal.UnhoodBytecode
{
    internal class UE3FunctionReader
    {
        public static readonly FlagSet _flagSet = new FlagSet("Final", "Defined", "Iterator", "Latent",
            "PreOperator", "Singular", "Net", "NetReliable",
            "Simulated", "Exec", "Native", "Event",
            "Operator", "Static", "Const", null,
            null, "Public", "Private", "Protected",
            "Delegate", "NetServer", "HasOutParms", "HasDefaults",
            "NetClient", "FuncInherit", "FuncOverrideMatch");

        public static string ReadFunction(IExportEntry export)
        {
            UnFunction func = ReadInstance(export.FileRef, new BinaryReader(new MemoryStream(export.Data)), export);
            TextBuilder tb = new TextBuilder();
            try
            {
                func.Decompile(tb);
            }
            catch (Exception e)
            {
                return "Error reading function: " + e.ToString();
            }
            return tb.ToString();
        }

        private static UnFunction ReadInstance(IMEPackage package, BinaryReader reader, IExportEntry export)
        {
            reader.ReadBytes(12);
            int super = reader.ReadInt32();
            int children = reader.ReadInt32();
            reader.ReadBytes(12);
            int line = reader.ReadInt32();
            int textPos = reader.ReadInt32();
            int scriptSize = reader.ReadInt32();
            byte[] bytecode = reader.ReadBytes(scriptSize);
            int nativeIndex = reader.ReadInt16();
            int operatorPrecedence = reader.ReadByte();
            int functionFlags = reader.ReadInt32();
            if ((functionFlags & _flagSet.GetMask("Net")) != 0)
            {
                reader.ReadInt16();  // repOffset
            }
            int friendlyNameIndex = reader.ReadInt32();
            reader.ReadInt32();
            return new UnFunction(export, package.getNameEntry(friendlyNameIndex),
                new FlagValues(functionFlags, _flagSet), bytecode, nativeIndex, operatorPrecedence);
        }
    }
}
