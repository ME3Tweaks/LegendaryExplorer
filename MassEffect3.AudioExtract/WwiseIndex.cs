using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect3.AudioExtract
{
	public class WwiseIndex : ISerializable
	{
		public List<Resource> Resources = new List<Resource>();
		public List<string> Strings = new List<string>();

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref Strings);
			stream.Serialize(ref Resources);
		}

		public static WwiseIndex Load(Stream input)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}

			uint magic = input.ReadValueU32(Endian.Little);
			if (magic != 0x58444957 &&
				magic.Swap() != 0x58444957)
			{
				throw new FormatException("invalid magic");
			}
			Endian endian = magic == 0x58444957 ? Endian.Little : Endian.Big;

			uint version = input.ReadValueU32(endian);
			if (version != 1)
			{
				throw new FormatException("unexpected version");
			}

			var reader = new FileReader(input, version, endian);

			var index = new WwiseIndex();
			index.Serialize(reader);
			return index;
		}

		public struct FileHash
		{
			public readonly uint A;
			public readonly uint B;
			public readonly uint C;
			public readonly uint D;

			public FileHash(uint a, uint b, uint c, uint d)
			{
				A = a;
				B = b;
				C = c;
				D = d;
			}

			public FileHash(byte[] bytes)
			{
				if (bytes.Length != 16)
				{
					throw new ArgumentException("must be 16 bytes", "bytes");
				}

				A = BitConverter.ToUInt32(bytes, 0).Swap();
				B = BitConverter.ToUInt32(bytes, 4).Swap();
				C = BitConverter.ToUInt32(bytes, 8).Swap();
				D = BitConverter.ToUInt32(bytes, 12).Swap();
			}

			public static FileHash Compute(byte[] bytes)
			{
				MD5 md5 = MD5.Create();
				return new FileHash(md5.ComputeHash(bytes));
			}

			public override string ToString()
			{
				return string.Format("{0:X8}{1:X8}{2:X8}{3:X8}",
					A,
					B,
					C,
					D);
			}

			public override bool Equals(object obj)
			{
				if (obj == null || obj.GetType() != GetType())
				{
					return false;
				}

				return (FileHash) obj == this;
			}

			public static bool operator !=(FileHash a, FileHash b)
			{
				return
					a.A != b.A ||
					a.B != b.B ||
					a.C != b.C ||
					a.D != b.D;
			}

			public static bool operator ==(FileHash a, FileHash b)
			{
				return
					a.A == b.A &&
					a.B == b.B &&
					a.C == b.C &&
					a.D == b.D;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					int hash = 17;
					hash = hash * 23 + A.GetHashCode();
					hash = hash * 23 + B.GetHashCode();
					hash = hash * 23 + C.GetHashCode();
					hash = hash * 23 + D.GetHashCode();
					return hash;
				}
			}
		}

		public class Instance : ISerializable
		{
			public int ActorIndex;
			public int FileIndex;
			public int GroupIndex;
			public bool IsPackage;
			public int LocaleIndex;
			public int NameIndex;
			public int Offset;
			public int PathIndex;
			public int Size;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref PathIndex);
				stream.Serialize(ref NameIndex);
				stream.Serialize(ref ActorIndex);
				stream.Serialize(ref GroupIndex);
				stream.Serialize(ref LocaleIndex);
				stream.Serialize(ref FileIndex);
				stream.Serialize(ref IsPackage);
				stream.Serialize(ref Offset);
				stream.Serialize(ref Size);
			}
		}

		public class Resource : ISerializable
		{
			public FileHash Hash;
			public List<Instance> Instances = new List<Instance>();

			public void Serialize(ISerializer stream)
			{
				if (stream.Mode == SerializeMode.Reading)
				{
					uint a = 0, b = 0, c = 0, d = 0;
					stream.Serialize(ref a);
					stream.Serialize(ref b);
					stream.Serialize(ref c);
					stream.Serialize(ref d);
					Hash = new FileHash(a, b, c, d);
				}
				else
				{
					throw new NotSupportedException();
				}

				stream.Serialize(ref Instances);
			}
		}
	}
}