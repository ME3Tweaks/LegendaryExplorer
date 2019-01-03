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
        private static readonly FlagSet _flagSet = new FlagSet("Final", "Defined", "Iterator", "Latent",
            "PreOperator", "Singular", "Net", "NetReliable",
            "Simulated", "Exec", "Native", "Event",
            "Operator", "Static", "Const", null,
            null, "Public", "Private", "Protected",
            "Delegate", "NetServer", "HasOutParms", "HasDefaults",
            "NetClient", "FuncInherit", "FuncOverrideMatch");

        public static Dictionary<int, UnFunction> tempNativeFunctions = new Dictionary<int, UnFunction>(); //This is a real hackjob... sorry...

        public static string ReadFunction(IExportEntry export)
        {
            tempNativeFunctions.Clear();
            for (int i = 0; i < export.FileRef.Exports.Count; i++)
            {
                var exp = export.FileRef.Exports[i];
                if (exp.ClassName == "Function")
                {
                    var function = ReadInstance(export.FileRef, new BinaryReader(new MemoryStream(exp.Data)), exp);
                    if (function.Native && !function.Event && function.NativeIndex != 0)
                    {
                        Debug.WriteLine("Native function: " + function.NativeIndex + " = " + exp.GetFullPath);
                        tempNativeFunctions[function.NativeIndex] = function;
                    }
                }
            }
            UnFunction func = ReadInstance(export.FileRef, new BinaryReader(new MemoryStream(export.Data)), export);
            TextBuilder tb = new TextBuilder();
            func.Decompile(tb);
            tempNativeFunctions.Clear();
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
