using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Language.ByteCode;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Compiling
{
    public class BytecodeWriter
    {
        protected readonly IMEPackage Pcc;
        protected readonly MEGame Game;
        private readonly byte extNativeIndex;

        protected ushort Position { get; private set; }
        private readonly List<byte> bytecode = [];

        protected BytecodeWriter(IMEPackage pcc)
        {
            Pcc = pcc;
            Game = pcc.Game;
            extNativeIndex = (byte)(Game.IsGame3() ? 0x70 : 0x60);
        }

        protected byte[] GetByteCode() => [.. bytecode];

        protected int GetMemLength() => Position;

        protected void WriteByte(byte b)
        {
            Position += 1;
            bytecode.Add(b);
        }

        protected void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            Position += (ushort)bytes.Length;
            bytecode.AddRange(bytes);
        }

        protected void WriteInt(int i)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            MemoryMarshal.Write(bytes, i);
            WriteBytes(bytes);
        }

        protected void WriteFloat(float f)
        {
            Span<byte> bytes = stackalloc byte[sizeof(float)];
            MemoryMarshal.Write(bytes, f);
            WriteBytes(bytes);
        }

        protected void WriteUShort(ushort us)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ushort)];
            MemoryMarshal.Write(bytes, us);
            WriteBytes(bytes);
        }

        protected void WriteOpCode(OpCodes opCode) => WriteByte((byte)opCode);

        protected void WriteCast(ECast castToken) => WriteByte((byte)castToken);

        protected void WriteObjectRef(IEntry entry)
        {
            WriteInt(entry?.UIndex ?? 0);
            if (Game >= MEGame.ME3)
            {
                Position += 4;
            }
        }

        protected void WriteName(string fullName)
        {
            (string name, int number) = NameReference.FromInstancedString(fullName);
            WriteInt(Pcc.FindNameOrAdd(name));
            WriteInt(number);
        }

        protected SkipPlaceholder WriteSkipPlaceholder() => new(this);
        protected JumpPlaceholder WriteJumpPlaceholder(JumpType jumpType = JumpType.Break) => new(this, jumpType);

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

            public void SetExplicit(ushort val)
            {
                Writer.bytecode.OverwriteRange(placeHolderIdx, BitConverter.GetBytes(val));
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

            public readonly JumpType Type;

            public JumpPlaceholder(BytecodeWriter writer, JumpType type)
            {
                Writer = writer;
                Type = type;
                placeHolderIdx = writer.bytecode.Count;
                writer.WriteByte(0);
                writer.WriteByte(0);
            }

            public void End(ushort? position = null)
            {
                ushort jumpPos = position ?? Writer.Position;
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
                WriteByte((byte)(extNativeIndex + nativeIndex / 256));
                WriteByte((byte)(nativeIndex % 256));
            }
        }
    }
}
