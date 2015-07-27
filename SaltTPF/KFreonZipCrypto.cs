using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SaltTPF
{
    class KFreonZipCrypto
    {
        public byte[] tpfkey = {0x73, 0x2A, 0x63, 0x7D, 0x5F, 0x0A, 0xA6, 0xBD,
				0x7D, 0x65, 0x7E, 0x67, 0x61, 0x2A, 0x7F, 0x7F,
				0x74, 0x61, 0x67, 0x5B, 0x60, 0x70, 0x45, 0x74,
				0x5C, 0x22, 0x74, 0x5D, 0x6E, 0x6A, 0x73, 0x41,
				0x77, 0x6E, 0x46, 0x47, 0x77, 0x49, 0x0C, 0x4B,
				0x46, 0x6F };

        private byte MagicByte(UInt32[] Keys)
        {
                UInt16 t = (UInt16)((UInt16)(Keys[2] & 0xFFFF) | 2);
                return (byte)((t * (t ^ 1)) >> 8);
        }

        public byte[] Blocks;

        public byte[] GetBlocks()
        {
            return Blocks;
        }

        public KFreonZipCrypto(ZipReader.ZipEntry entry, byte[] block, int start, int count)
        {
            Blocks = DecryptData(entry, block, start, count);
        }

        public byte[] DecryptData(ZipReader.ZipEntry entry, byte[] block, int start, int count)
        {
            if (block == null || block.Length < count || count < 12)
                throw new ArgumentException("Invalid arguments for decryption");

            CRC32 crcgen = new CRC32();
            UInt32[] Keys = InitCipher(tpfkey, crcgen);

            DecryptBlock(block, start, 12, Keys, crcgen);

            if (block[11] != (byte)((entry.CRC >> 24) & 0xff) && (entry.BitFlag & 0x8) != 0x8)
                throw new FormatException("Incorrect password");

            DecryptBlock(block, start + 12, count - 12, Keys, crcgen);
            return block;
        }

        private UInt32[] InitCipher(byte[] password, CRC32 crcgen)
        {
            UInt32[] Keys = new UInt32[] { 305419896, 591751049, 878082192 };
            for (int i = 0; i < password.Length; i++)
            {
                Keys = UpdateKeys(password[i], Keys, crcgen);
            }
            return Keys;
        }

        private UInt32[] UpdateKeys(byte byteval, UInt32[] Keys, CRC32 crcgen)
        {
            Keys[0] = (UInt32)crcgen.ComputeCrc32(Keys[0], byteval);
            Keys[1] = Keys[1] + (byte)Keys[0];
            Keys[1] = Keys[1] * 0x08088405 + 1;
            Keys[2] = (UInt32)crcgen.ComputeCrc32(Keys[2], (byte)(Keys[1] >> 24));
            return Keys;
        }

        private void DecryptBlock(byte[] block, int offset, int count, UInt32[] Keys, CRC32 crcgen)
        {
            for (int i = offset; i < offset + count; i++)
            {
                block[i] = (byte)(block[i] ^ MagicByte(Keys));
                Keys = UpdateKeys(block[i], Keys, crcgen);
            }
        }
    }
}
