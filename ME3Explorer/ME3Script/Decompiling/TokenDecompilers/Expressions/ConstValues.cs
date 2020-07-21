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

        
        public Expression/*ObjectLiteral*/ DecompileObjectConst() // TODO: properly
        {
            PopByte();

            var value = ReadObject();

            StartPositions.Pop();
            //return new ObjectLiteral(value, null, null);
            return new SymbolReference(null, null, null, value.ClassName + "'" + value.ObjectName + "'");
        }

        public Expression/*VectorLiteral*/ DecompileVectorConst() // TODO: properly
        {
            PopByte();
            var X = ReadFloat();
            var Y = ReadFloat();
            var Z = ReadFloat();

            StartPositions.Pop();
            var str = "vect(" + X + ", " + Y + ", " + Z + ")";
            return new SymbolReference(null, null, null, str);
        }

        public Expression/*RotationLiteral*/ DecompileRotationConst() // TODO: properly
        {
            PopByte();
            var Pitch = ReadInt32();
            var Yaw = ReadInt32();
            var Roll = ReadInt32();

            StartPositions.Pop();
            var str = "rot(0x" + Pitch.ToString("X8") + ", 0x" + Yaw.ToString("X8") + ", 0x" + Roll.ToString("X8") + ")";
            return new SymbolReference(null, null, null, str);
        } 

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
