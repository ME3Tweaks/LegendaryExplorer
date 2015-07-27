using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaltTPF
{
    public class CRC32
    {
        private UInt32[] crc32Table;
        private const UInt32 dwPolynomial = 0xEDB88320;

        public CRC32()
        {
            crc32Table = new UInt32[256];
            unchecked
            {
                UInt32 dwCrc;
                byte i = 0;
                do
                {
                    dwCrc = i;
                    for (byte j = 8; j > 0; j--)
                    {
                        if ((dwCrc & 1) == 1)
                        {
                            dwCrc = (dwCrc >> 1) ^ dwPolynomial;
                        }
                        else
                        {
                            dwCrc >>= 1;
                        }
                    }
                    crc32Table[i] = dwCrc;
                    i++;
                } while (i != 0);
            }
        }

        public Int32 ComputeCrc32(UInt32 W, byte B)
        {
            return (Int32)(crc32Table[(W ^ B) & 0xFF] ^ (W >> 8));
        }

        public UInt32 BlockChecksum(byte[] buffer, int offset, int count, UInt32 seed = 0xffffffff)
        {
            UInt32 crc = seed;
            for (int i = offset; i < count; i++)
            {
                unchecked
                {
                    crc = (crc >> 8) ^ crc32Table[(buffer[i] ^ (crc & 0xFF))];
                }
            }
            return ~crc;
        }

        public UInt32 BlockChecksum(byte[] buffer)
        {
            return BlockChecksum(buffer, 0, buffer.Length);
        }
    }
}
