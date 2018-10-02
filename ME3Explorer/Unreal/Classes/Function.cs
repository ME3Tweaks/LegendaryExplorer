using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.Classes
{
    public class Function
    {
        public IMEPackage pcc;
        public byte[] memory;
        public byte[] script;
        public int memsize;
        public int child;
        public int unk1;
        public int unk2;
        public int size;
        public int flagint;
        public int nativeindex;

        public string[] flags = {   "Final", "Defined", "Iterator", "Latent", 
                                    "PreOperator", "Singular", "Net", "NetReliable", 
                                    "Simulated", "Exec", "Native", "Event", 
                                    "Operator", "Static", "Const", null,
                                    null, "Public", "Private", "Protected", 
                                    "Delegate", "NetServer", "HasOutParms", "HasDefaults", 
                                    "NetClient", "FuncInherit", "FuncOverrideMatch"};
        internal string ScriptText;
        public string HeaderText;
        public List<Token> ScriptBlocks;

        internal List<BytecodeSingularToken> SingularTokenList { get; private set; }

        public Function()
        {
        }

        public Function(byte[] raw, IMEPackage Pcc)
        {
            pcc = Pcc;
            memory = raw;
            memsize = raw.Length;
            flagint = GetFlagInt();
            nativeindex = GetNatIdx();
            Deserialize();
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
            string res = "Flags ( ";
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i] != null)
                {
                    int flag = 1 << i;
                    if ((flagint & flag)!=0)
                        res += flags[i] + " ";
                }
            }
            res += " )";
            return res;
        }


        public void Deserialize()
        {
            
            ReadHeader();
            script = new byte[memsize - 32];
            for (int i = 32; i < memsize; i++)
                script[i - 32] = memory[i];
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
            var parsedData = Bytecode.ParseBytecode(script, pcc);
            ScriptBlocks = parsedData.Item1;
            SingularTokenList = parsedData.Item2;
        }
    }
}
