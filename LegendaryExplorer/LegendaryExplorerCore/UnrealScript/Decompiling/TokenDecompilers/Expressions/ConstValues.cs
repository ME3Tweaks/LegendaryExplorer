using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript.Decompiling
{
    internal partial class ByteCodeDecompiler
    {
        private IntegerLiteral DecompileIntConst()
        {
            PopByte();

            var value = ReadInt32();

            StartPositions.Pop();
            return new IntegerLiteral(value);
        }
        private StringRefLiteral DecompileStringRefConst()
        {
            PopByte();

            var value = ReadInt32();

            StartPositions.Pop();
            return new StringRefLiteral(value);
        }

        private FloatLiteral DecompileFloatConst()
        {
            PopByte();

            var value = ReadFloat();

            StartPositions.Pop();
            return new FloatLiteral(value);
        }

        private StringLiteral DecompileStringConst()
        {
            PopByte();

            var value = ReadNullTerminatedString();

            StartPositions.Pop();
            return new StringLiteral(value);
        }

        
        private ObjectLiteral DecompileObjectConst()
        {
            PopByte();

            var value = ReadObject();

            StartPositions.Pop();
            return new ObjectLiteral(new NameLiteral(value.ClassName == "Class" || value.Parent == ContainingClass.Export ? value.ObjectName.Instanced : value.InstancedFullPath), new VariableType(value.ClassName));
        }

        private VectorLiteral DecompileVectorConst()
        {
            PopByte();
            var x = ReadFloat();
            var y = ReadFloat();
            var z = ReadFloat();

            StartPositions.Pop();
            return new VectorLiteral(x, y, z);
        }

        private RotatorLiteral DecompileRotationConst()
        {
            PopByte();
            var pitch = ReadInt32();
            var yaw = ReadInt32();
            var roll = ReadInt32();

            StartPositions.Pop();
            return new RotatorLiteral(pitch, yaw, roll);
        } 

        private NameLiteral DecompileNameConst()
        {
            PopByte();

            var value = ReadNameReference();

            StartPositions.Pop();
            return new NameLiteral(value.Instanced);
        }

        private IntegerLiteral DecompileByteConst(string numType)
        {
            PopByte();

            var value = ReadByte();

            StartPositions.Pop();
            return new IntegerLiteral(value) { NumType = numType };
        }

        private IntegerLiteral DecompileIntConstVal(int val)
        {
            PopByte();
            StartPositions.Pop();
            return new IntegerLiteral(val);
        }

        private BooleanLiteral DecompileBoolConstVal(bool val)
        {
            PopByte();
            StartPositions.Pop();
            return new BooleanLiteral(val);
        }
    }
}
