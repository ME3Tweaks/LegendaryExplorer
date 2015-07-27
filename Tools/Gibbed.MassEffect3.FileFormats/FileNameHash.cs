/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using Gibbed.IO;

namespace Gibbed.MassEffect3.FileFormats
{
    public struct FileNameHash : IComparable
    {
        public readonly uint A;
        public readonly uint B;
        public readonly uint C;
        public readonly uint D;
		
		//convert some characters, why?!?!
        public static char Sanitize(char c)
        {
            var s = (ushort)c;

            switch (s)
            {
                case 0x008C: return (char)0x9C;
                case 0x009F: return (char)0xFF;

                case 0x00D0:
                case 0x00DF:
                case 0x00F0:
                case 0x00F7: return c;
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
            for (int i = 0; i < input.Length; i++)
            {
                bytes[i] = (byte)Sanitize(input[i]);
                //bytes[i] = (byte)input[i];
            }

            var md5 = System.Security.Cryptography.MD5.Create();
            return new FileNameHash(md5.ComputeHash(bytes));
        }

        public FileNameHash(uint a, uint b, uint c, uint d)
        {
            this.A = a;
            this.B = b;
            this.C = c;
            this.D = d;
        }

        public FileNameHash(byte[] bytes)
        {
            if (bytes.Length != 16)
            {
                throw new ArgumentException("must be 16 bytes", "bytes");
            }

            this.A = BitConverter.ToUInt32(bytes, 0).Swap();
            this.B = BitConverter.ToUInt32(bytes, 4).Swap();
            this.C = BitConverter.ToUInt32(bytes, 8).Swap();
            this.D = BitConverter.ToUInt32(bytes, 12).Swap();
        }

        public override string ToString()
        {
            return string.Format("{0:X8}{1:X8}{2:X8}{3:X8}",
                this.A, this.B, this.C, this.D);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != this.GetType())
            {
                return false;
            }

            return (FileNameHash)obj == this;
        }
		
		public int CompareTo(object obj)
		{
   		    if(obj is FileNameHash)
			{
            	FileNameHash temp = (FileNameHash) obj;
				string a = this.ToString();
				string b = obj.ToString();
    			return a.CompareTo(b);
        	}
        	throw new ArgumentException();    
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

        public static bool operator <=(FileNameHash a, FileNameHash b)
        {
            return
                a.A <= b.A &&
                a.B <= b.B &&
                a.C <= b.C &&
                a.D <= b.D;
        }

        public static bool operator <(FileNameHash a, FileNameHash b)
        {
            return
                a.A < b.A &&
                a.B < b.B &&
                a.C < b.C &&
                a.D < b.D;
        }

        public static bool operator >=(FileNameHash a, FileNameHash b)
        {
            return
                a.A >= b.A &&
                a.B >= b.B &&
                a.C >= b.C &&
                a.D >= b.D;
        }

        public static bool operator >(FileNameHash a, FileNameHash b)
        {
            return
                a.A > b.A &&
                a.B > b.B &&
                a.C > b.C &&
                a.D > b.D;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + this.A.GetHashCode();
                hash = hash * 23 + this.B.GetHashCode();
                hash = hash * 23 + this.C.GetHashCode();
                hash = hash * 23 + this.D.GetHashCode();
                return hash;
            }
        }
    }
}
