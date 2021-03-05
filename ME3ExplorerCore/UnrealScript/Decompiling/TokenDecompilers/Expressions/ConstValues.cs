using ME3Script.Language.Tree;
using ME3Script.Utilities;

namespace ME3Script.Decompiling
{
    public partial class ByteCodeDecompiler
    {
        public IntegerLiteral DecompileIntConst()
        {
            PopByte();

            var value = ReadInt32();

            StartPositions.Pop();
            return new IntegerLiteral(value);
        }
        public StringRefLiteral DecompileStringRefConst()
        {
            PopByte();

            var value = ReadInt32();

            StartPositions.Pop();
            return new StringRefLiteral(value);
        }

        public FloatLiteral DecompileFloatConst()
        {
            PopByte();

            var value = ReadFloat();

            StartPositions.Pop();
            return new FloatLiteral(value);
        }

        public StringLiteral DecompileStringConst()
        {
            PopByte();

            var value = ReadNullTerminatedString();

            StartPositions.Pop();
            return new StringLiteral(value);
        }

        
        public ObjectLiteral DecompileObjectConst()
        {
            PopByte();

            var value = ReadObject();

            StartPositions.Pop();
            return new ObjectLiteral(new NameLiteral(value.ClassName == "Class" || value.Parent == ContainingClass.Export ? value.ObjectName.Instanced : value.InstancedFullPath), new VariableType(value.ClassName));
        }

        public VectorLiteral DecompileVectorConst()
        {
            PopByte();
            var x = ReadFloat();
            var y = ReadFloat();
            var z = ReadFloat();

            StartPositions.Pop();
            return new VectorLiteral(x, y, z);
        }

        public RotatorLiteral DecompileRotationConst()
        {
            PopByte();
            var pitch = ReadInt32();
            var yaw = ReadInt32();
            var roll = ReadInt32();

            StartPositions.Pop();
            return new RotatorLiteral(pitch, yaw, roll);
        } 

        public NameLiteral DecompileNameConst()
        {
            PopByte();

            var value = ReadNameReference();

            StartPositions.Pop();
            return new NameLiteral(value.Instanced);
        }

        public IntegerLiteral DecompileByteConst(string numType)
        {
            PopByte();

            var value = ReadByte();

            StartPositions.Pop();
            return new IntegerLiteral(value) { NumType = numType };
        }

        public IntegerLiteral DecompileIntConstVal(int val)
        {
            PopByte();
            StartPositions.Pop();
            return new IntegerLiteral(val);
        }

        public BooleanLiteral DecompileBoolConstVal(bool val)
        {
            PopByte();
            StartPositions.Pop();
            return new BooleanLiteral(val);
        }
    }
}
