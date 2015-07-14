using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    public partial class ME3ByteCodeDecompiler
    {
        public IntegerLiteral DecompileIntConst()
        {
            PopByte();

            var value = ReadInt32();

            StartPositions.Pop();
            return new IntegerLiteral(value, null, null);
        }

        public FloatLiteral DecompileFloatConst()
        {
            PopByte();

            var value = ReadFloat();

            StartPositions.Pop();
            return new FloatLiteral(value, null, null);
        }

        public StringLiteral DecompileStringConst()
        {
            PopByte();

            var value = ReadNullTerminatedString();

            StartPositions.Pop();
            return new StringLiteral(value, null, null);
        }

        /*
        public ObjectLiteral DecompileObjectConst()
        {
            PopByte();

            var value = ReadIndex();

            StartPositions.Pop();
            return new ObjectLiteral(value, null, null);
        } */

        public NameLiteral DecompileNameConst()
        {
            PopByte();

            var value = PCC.GetName(ReadNameRef());

            StartPositions.Pop();
            return new NameLiteral(value, null, null);
        }

        public IntegerLiteral DecompileByteConst()
        {
            PopByte();

            var value = ReadByte();

            StartPositions.Pop();
            return new IntegerLiteral(value, null, null);
        }

        public IntegerLiteral DecompileIntConstVal(int val)
        {
            PopByte();
            StartPositions.Pop();
            return new IntegerLiteral(val, null, null);
        }

        public BooleanLiteral DecompileBoolConstVal(bool val)
        {
            PopByte();
            StartPositions.Pop();
            return new BooleanLiteral(val, null, null);
        }
    }
}
