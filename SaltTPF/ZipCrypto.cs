using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SaltTPF
{
    public static class ZipCrypto
    {
        public static byte[] tpfkey = {0x73, 0x2A, 0x63, 0x7D, 0x5F, 0x0A, 0xA6, 0xBD,
                0x7D, 0x65, 0x7E, 0x67, 0x61, 0x2A, 0x7F, 0x7F,
                0x74, 0x61, 0x67, 0x5B, 0x60, 0x70, 0x45, 0x74,
                0x5C, 0x22, 0x74, 0x5D, 0x6E, 0x6A, 0x73, 0x41,
                0x77, 0x6E, 0x46, 0x47, 0x77, 0x49, 0x0C, 0x4B,
                0x46, 0x6F };

        private static byte MagicByte
        {
            get
            {
                UInt16 t = (UInt16)((UInt16)(Keys[2] & 0xFFFF) | 2);
                return (byte)((t * (t ^ 1)) >> 8);
            }
        }

        static UInt32[] Keys;
        static CRC32 crcgen;

        public static void DecryptData(ZipReader.ZipEntry entry, byte[] block, int start, int count)
        {
            if (block == null || block.Length < count || count < 12)
                throw new ArgumentException("Invalid arguments for decryption");

            crcgen = new CRC32();
            InitCipher(tpfkey);

            DecryptBlock(block, start, 12);

            // KFreon: Apparently not required. Causes some TPF's to fail loading, but when commented out, TPF loads fine, so...
            /*if (block[11] != (byte)((entry.CRC >> 24) & 0xff) && (entry.BitFlag & 0x8) != 0x8)
                throw new FormatException("Incorrect password");*/

            DecryptBlock(block, start + 12, count - 12);
        }

        private static void InitCipher(byte[] password)
        {
            Keys = new UInt32[] { 305419896, 591751049, 878082192 };
            for (int i = 0; i < password.Length; i++)
            {
                UpdateKeys(password[i]);
            }
        }

        private static void UpdateKeys(byte byteval)
        {
            Keys[0] = (UInt32)crcgen.ComputeCrc32(Keys[0], byteval);
            Keys[1] += (byte)Keys[0];
            Keys[1] = Keys[1] * 134775813 + 1;
            Keys[2] = (UInt32)crcgen.ComputeCrc32(Keys[2], (byte)(Keys[1] >> 24));
        }

        private static void DecryptBlock(byte[] block, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++)
            {
                block[i] = (byte)(block[i] ^ MagicByte);
                UpdateKeys(block[i]);
            }
        }

        public static void EncryptData(Stream fs, byte[] comprBlock, uint CRC)
        {
            crcgen = new CRC32();
            InitCipher(tpfkey);

            Random randomiser = new Random();
            int val1 = randomiser.Next();
            int val2 = randomiser.Next();
            short val3 = (short)randomiser.Next();

            byte[] header = new byte[12];  // header size

            // Fill random header
            var rand1 = BitConverter.GetBytes(val1);
            var rand2 = BitConverter.GetBytes(val2);
            var rand3 = BitConverter.GetBytes(val3);
            var all = rand1.Concat(rand2).Concat(rand3).ToList();
            for (int i = 0; i < 10; i++)
                header[i] = all[i];

            // Add CRC
            header[10] = (byte)((CRC >> 16) & 0xff);
            header[11] = (byte)((CRC >> 24) & 0xff);

            //DecryptBlock(header, 0, 12);
            EncryptBlock(header, 0, 12);
            fs.Write(header, 0, 12);
            //DecryptBlock(comprBlock, 0, comprBlock.Length);
            EncryptBlock(comprBlock, 0, comprBlock.Length);
            fs.Write(comprBlock, 0, comprBlock.Length);
        }

        private static void EncryptBlock(byte[] block, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++)
            {
                byte C = block[i];
                block[i] = (byte)(block[i] ^ MagicByte);
                UpdateKeys(C);
            }
        }
    }
}
