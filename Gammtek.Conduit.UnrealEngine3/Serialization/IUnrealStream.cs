using System;
using System.IO;
using Gammtek.Conduit.UnrealEngine3.Core;

namespace Gammtek.Conduit.UnrealEngine3.Serialization
{
	public interface IUnrealStream : IDisposable
	{
		long LastPosition { get; set; }

		long Length { get; }

		string Name { get; }

		UnrealPackage Package { get; }

		long Position { get; set; }

		UnrealReader Reader { get; }

		uint Version { get; }

		string ParseName(int index);

		UObject ParseObject(int index);

		int Read(byte[] array, int offset, int count);

		byte ReadByte();

		float ReadFloat();

		int ReadIndex();

		short ReadInt16();

		long ReadInt64();

		int ReadInt32();

		int ReadNameIndex();

		int ReadNameIndex(out int n);

		UName ReadNameReference();

		UObject ReadObject();

		int ReadObjectIndex();

		string ReadText();

		ushort ReadUInt16();

		uint ReadUInt32();

		ulong ReadUInt64();

		long Seek(long offset, SeekOrigin origin = SeekOrigin.Begin);

		void Skip(int bytes);
	}
}
