using ME3Data.DataTypes.ScriptTypes;
using ME3Data.Utility;
using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    public partial class ME3ByteCodeDecompiler : ObjectReader
    {
        private ME3Struct DataContainer;

        private Byte CurrentByte { get { return _data[Position]; } } // TODO: meaningful error handling here..
        private Byte PopByte() { return ReadByte(); }
        private Byte PeekByte { get { return Position < Size ? _data[Position + 1] : (byte)0; } }
        private Byte PrevByte { get { return Position > 0 ? _data[Position - 1] : (byte)0; } }

        private bool CurrentIs(StandardByteCodes val)
        {
            return CurrentByte == (byte)val;
        }

        public ME3ByteCodeDecompiler(ME3Struct dataContainer)
            :base(dataContainer.ByteScript)
        {
            DataContainer = dataContainer;
        }

        public CodeBody Decompile()
        {
            Position = DataContainer.ByteScriptSize - DataContainer.DataScriptSize;
            var statememnts = new List<Statement>();

            while (Position < Size && !CurrentIs(StandardByteCodes.EndOfScript))
            {
                var current = DecompileStatement();
                if (current == null)
                    return null; // ERROR!

                statememnts.Add(current);
            }

            return null;
        }
    }
}
