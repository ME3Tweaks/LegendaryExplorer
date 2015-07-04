using ME3Data.DataTypes.ScriptTypes;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    public class ME3ByteCodeDecompiler
    {
        private ME3Struct DataContainer;

        private Byte[] Data { get { return DataContainer.DataScript; } }
        private int Index;

        private Byte CurrentByte { get { return Data[Index]; } }
        private Byte PopByte { get { return Data[Index++]; } }
        private Byte PeekByte { get { return Data[Index + 1]; } }
        private Byte PrevByte { get { return Data[Index - 1]; } }

        private bool CurrentIs()

        public ME3ByteCodeDecompiler(ME3Struct dataContainer)
        {
            DataContainer = dataContainer;
        }

        public CodeBody Decompile()
        {
            Index = 0;

            while (Index < Data.Length) //&& CurrentByte)
            {
                
            }

            return null;
        }

        public Statement DecompileStatement()
        {

        }
    }
}
