using System;
using System.Security.Cryptography;
using Gammtek.Conduit.Extensions;

namespace MassEffect3.FileFormats
{
	public struct FileNameHash
	{
		public readonly uint A;
		public readonly uint B;
		public readonly uint C;
		public readonly uint D;

		public FileNameHash(uint a, uint b, uint c, uint d)
		{
			A = a;
			B = b;
			C = c;
			D = d;
		}

		public FileNameHash(byte[] bytes)
		{
			if (bytes.Length != 16)
			{
				throw new ArgumentException("must be 16 bytes", nameof(bytes));
			}

			A = BitConverter.ToUInt32(bytes, 0).Swap();
			B = BitConverter.ToUInt32(bytes, 4).Swap();
			C = BitConverter.ToUInt32(bytes, 8).Swap();
			D = BitConverter.ToUInt32(bytes, 12).Swap();
		}

		private static char Sanitize(char c)
		{
			var s = (ushort) c;

			switch (s)
			{
				case 0x008C:
					return (char) 0x9C;
				case 0x009F:
					return (char) 0xFF;

				case 0x00D0:
				case 0x00DF:
				case 0x00F0:
				case 0x00F7:
					return c;
			}

			if ((c >= 'A' && c <= 'Z') ||
				(c >= 'À' && c <= 'Þ'))
			{
				return char.ToLowerInvariant(c);
			}

			return c;
		}

		public static FileNameHash Compute(string input)
		{
			var bytes = new byte[input.Length];
			for (var i = 0; i < input.Length; i++)
			{
				bytes[i] = (byte) Sanitize(input[i]);
			}

			var md5 = MD5.Create();
			return new FileNameHash(md5.ComputeHash(bytes));
		}

		public override string ToString()
		{
			return string.Format("{0:X8}{1:X8}{2:X8}{3:X8}",
				A, B, C, D);
		}

		public override bool Equals(object obj)
		{
			if (obj == null || obj.GetType() != GetType())
			{
				return false;
			}

			return (FileNameHash) obj == this;
		}

		public static bool operator !=(FileNameHash a, FileNameHash b)
		{
			return
				a.A != b.A ||
				a.B != b.B ||
				a.C != b.C ||
				a.D != b.D;
		}

		public static bool operator ==(FileNameHash a, FileNameHash b)
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
				var hash = 17;
				hash = hash * 23 + A.GetHashCode();
				hash = hash * 23 + B.GetHashCode();
				hash = hash * 23 + C.GetHashCode();
				hash = hash * 23 + D.GetHashCode();
				return hash;
			}
		}
	}
}