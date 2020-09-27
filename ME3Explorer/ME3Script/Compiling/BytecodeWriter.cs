using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Script.Language.ByteCode;
using ME3Script.Utilities;

namespace ME3Script.Compiling
{
    public class BytecodeWriter
    {

        protected readonly IMEPackage Pcc;

        protected BytecodeWriter(IMEPackage pcc)
        {
            Pcc = pcc;
        }

        public byte[] GetByteCode() => bytecode.ToArray();

        public int GetMemLength() => Position;

        protected ushort Position { get; private set; }
        private readonly List<byte> bytecode = new List<byte>();
        private readonly List<int> positions = new List<int>();

        private void IncrementPosition(int times = 1)
        {
            while (times-- > 0) positions.Add(Position++);
        }

        protected void WriteByte(byte b)
        {
            IncrementPosition();
            bytecode.Add(b);
        }

        protected void WriteBytes(byte[] bytes)
        {
            IncrementPosition(bytes.Length);
            bytecode.AddRange(bytes);
        }

        protected void WriteInt(int i) => WriteBytes(BitConverter.GetBytes(i));

        protected void WriteFloat(float f) => WriteBytes(BitConverter.GetBytes(f));

        protected void WriteUShort(ushort us) => WriteBytes(BitConverter.GetBytes(us));

        protected void WriteOpCode(OpCodes opCode) => WriteByte((byte)opCode);

        protected void WriteCast(ECast castToken) => WriteByte((byte)castToken);

        protected void WriteObjectRef(IEntry entry)
        {
            WriteInt(entry?.UIndex ?? 0);
            Position += 4;
        }

        protected NameReference StringToNameRef(string s)
        {
            int num = 0;
            int _Idx = s.LastIndexOf('_');
            if (_Idx > 0)
            {
                string numComponent = s.Substring(_Idx + 1);
                //if there's a leading zero, it's just part of the string
                if (numComponent.Length > 0 && numComponent[0] != '0' && int.TryParse(numComponent, NumberStyles.None, null, out num))
                {
                    s = s.Substring(0, _Idx);
                    num += 1;
                }
            }
            return new NameReference(s, num);
        }

        protected void WriteName(string fullName)
        {
            (string name, int number) = StringToNameRef(fullName);
            WriteInt(Pcc.FindNameOrAdd(name));
            WriteInt(number);
        }

        protected SkipPlaceholder WriteSkipPlaceholder() => new SkipPlaceholder(this);
        protected JumpPlaceholder WriteJumpPlaceholder(JumpType jumpType = JumpType.Break) => new JumpPlaceholder(this, jumpType);

        protected class SkipPlaceholder : IDisposable
        {
            private readonly int placeHolderIdx;
            private ushort startPos;
            private readonly BytecodeWriter Writer;

            public SkipPlaceholder(BytecodeWriter writer)
            {
                Writer = writer;
                placeHolderIdx = writer.bytecode.Count;
                writer.WriteByte(0);
                writer.WriteByte(0);
                ResetStart();
            }

            public void ResetStart()
            {
                startPos = Writer.Position;
            }

            public void End()
            {
                ushort skipSize = (ushort)(Writer.Position - startPos);
                Writer.bytecode.OverwriteRange(placeHolderIdx, BitConverter.GetBytes(skipSize));
            }

            public void Dispose() => End();
        }

        protected enum JumpType
        {
            Break,
            Continue,
            Case,
            Conditional
        }

        protected class JumpPlaceholder
        {
            private readonly int placeHolderIdx;
            private readonly BytecodeWriter Writer;

            public JumpType Type;

            public JumpPlaceholder(BytecodeWriter writer, JumpType type)
            {
                Writer = writer;
                Type = type;
                placeHolderIdx = writer.bytecode.Count;
                writer.WriteByte(0);
                writer.WriteByte(0);
            }

            public void End()
            {
                ushort jumpPos = Writer.Position;
                Writer.bytecode.OverwriteRange(placeHolderIdx, BitConverter.GetBytes(jumpPos));
            }
        }

        protected void WriteNativeOpCode(int nativeIndex)
        {
            if (nativeIndex < 256)
            {
                WriteByte((byte)nativeIndex);
            }
            else
            {
                WriteByte((byte)(0x70 + nativeIndex / 256));
                WriteByte((byte)(nativeIndex % 256));
            }
        }
    }
}
