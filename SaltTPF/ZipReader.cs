using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace SaltTPF
{
    public class ZipReader
    {
        #region Definitions
        public struct EOFRecord
        {
            private UInt16 DiskNo;
            private UInt16 DiskOfCD;
            private UInt16 NumCDOnDisk;
            private UInt16 NumCDs;
            public UInt32 CDSize;
            public UInt32 CDOffset;
            public String Comment;

            public EOFRecord(Stream tpfstream, Int64 eofoff)
            {
                tpfstream.Seek(eofoff, SeekOrigin.Begin);
                int len = (int)(tpfstream.Length - eofoff);
                byte[] EOFBuff = BuffXOR(tpfstream, len);
                if (BitConverter.ToUInt32(EOFBuff, 0) != endofdirmagic)
                    throw new FormatException("Incorrect data passed to EOF constructor");
                DiskNo = BitConverter.ToUInt16(EOFBuff, 4);
                DiskOfCD = BitConverter.ToUInt16(EOFBuff, 6);
                NumCDOnDisk = BitConverter.ToUInt16(EOFBuff, 8);
                NumCDs = BitConverter.ToUInt16(EOFBuff, 10);
                CDSize = BitConverter.ToUInt32(EOFBuff, 12);
                CDOffset = BitConverter.ToUInt32(EOFBuff, 16);
                UInt16 commentlen = BitConverter.ToUInt16(EOFBuff, 20);
                char[] comm = new char[commentlen];
                for (int i = 0; i < commentlen; i++)
                    comm[i] = (char)EOFBuff[i + 22];
                Comment = new string(comm);
            }
        }

        public class ZipEntryFull : ZipEntry
        {
            protected UInt16 Buildvers;
            protected UInt16 DiskStart;
            protected UInt16 InternalAttr;
            protected UInt32 ExternalAttr;
            protected UInt32 FileOffset;
            public String Comment { get; protected set; }

            public ZipEntryFull(byte[] entry, ZipReader par)
                : base(par)
            {
                if (BitConverter.ToUInt32(entry, 0) != dirfileheadermagic)
                    throw new FormatException("Incorrect header");
                Buildvers = BitConverter.ToUInt16(entry, 4);
                Minvers = BitConverter.ToUInt16(entry, 6);
                BitFlag = BitConverter.ToUInt16(entry, 8);
                ComprMethod = BitConverter.ToUInt16(entry, 10);
                ModifyTime = BitConverter.ToUInt16(entry, 12);
                ModifyDate = BitConverter.ToUInt16(entry, 14);
                CRC = BitConverter.ToUInt32(entry, 16);
                ComprSize = BitConverter.ToUInt32(entry, 20);
                UncomprSize = BitConverter.ToUInt32(entry, 24);
                ushort namelen = BitConverter.ToUInt16(entry, 28);
                ushort extralen = BitConverter.ToUInt16(entry, 30);
                ushort commlen = BitConverter.ToUInt16(entry, 32);
                DiskStart = BitConverter.ToUInt16(entry, 34);
                InternalAttr = BitConverter.ToUInt16(entry, 36);
                ExternalAttr = BitConverter.ToUInt32(entry, 38);
                FileOffset = BitConverter.ToUInt32(entry, 42);

                char[] strbuild = new char[namelen];
                for (int i = 0; i < namelen; i++)
                    strbuild[i] = (char)entry[46 + i];
                Filename = new string(strbuild);

                Extra = new byte[extralen];
                for (int i = 0; i < extralen; i++)
                    Extra[i] = entry[46 + namelen + i];

                strbuild = new char[commlen];
                for (int i = 0; i < commlen; i++)
                    strbuild[i] = (char)entry[46 + namelen + extralen + i];
                Comment = new string(strbuild);
            }

            public byte[] Extract(bool Preview, String outname = null)
            {
                byte[] databuff;
                int dataoff = 30;
                if (Filename != null)
                    dataoff += Filename.Length;
                if (Extra != null)
                    dataoff += Extra.Length;
                using (FileStream tpf = new FileStream(_par._filename, FileMode.Open, FileAccess.Read))
                {
                    tpf.Seek(FileOffset, SeekOrigin.Begin);
                    databuff = ZipReader.BuffXOR(tpf, dataoff + (int)ComprSize + 16); // XOR the whole data block as well as the footer
                }

                // Check for correct header data and such
                ZipEntry fileentry = new ZipEntry(databuff);
                if (!fileentry.Compare(this))
                    throw new InvalidDataException("File header not as expected");
                if (BitConverter.ToUInt32(databuff, (int)ComprSize + dataoff) != datadescriptormagic)
                    throw new InvalidDataException("Footer not as expected");

                //ZipCrypto.DecryptData(this, databuff, dataoff, (int)ComprSize);
                KFreonZipCrypto crypto = new KFreonZipCrypto(this, databuff, dataoff, (int)ComprSize);
                databuff = crypto.GetBlocks();

                databuff = Deflate(databuff, 12 + dataoff, (int)ComprSize - 12);
                if (databuff.Length != UncomprSize)
                    throw new InvalidDataException("Deflation resulted in incorrect file size");
                CRC32 crcgen = new CRC32();
                if (crcgen.BlockChecksum(databuff, 0, (int)UncomprSize) != CRC)
                    throw new InvalidDataException("Checksums don't match");

                if (!Preview)
                {
                    outname = outname ?? Filename;
                    using (FileStream fs = new FileStream(outname, FileMode.Create, FileAccess.Write))
                    {
                        fs.Write(databuff, 0, (int)UncomprSize);
                    }
                    return null;
                }
                else
                    return databuff;
            }

            private byte[] Deflate(byte[] databuff, int start, int count)
            {
                using (MemoryStream decompr = new MemoryStream())
                {
                    using (MemoryStream cmpr = new MemoryStream(databuff, start, count))
                    {
                        using (DeflateStream deflator = new DeflateStream(cmpr, CompressionMode.Decompress))
                        {
                            deflator.CopyTo(decompr);
                        }
                    }
                    return decompr.ToArray();
                }
            }
        }

        public class ZipEntry
        {
            protected UInt16 Minvers;
            public UInt16 BitFlag { get; protected set; }
            protected UInt16 ComprMethod;
            protected UInt16 ModifyTime;
            protected UInt16 ModifyDate;
            public UInt32 CRC { get; protected set; }
            public UInt32 ComprSize { get; protected set; }
            public UInt32 UncomprSize { get; protected set; }
            public String Filename { get; protected set; }
            protected Byte[] Extra;
            protected ZipReader _par;
            protected UInt32 DataOffset;

            public ZipEntry(ZipReader par) { _par = par; }

            public ZipEntry(byte[] entry)
            {
                if (BitConverter.ToUInt32(entry, 0) != filemagic)
                    throw new FormatException("File header magic number incorrect");
                Minvers = BitConverter.ToUInt16(entry, 4);
                BitFlag = BitConverter.ToUInt16(entry, 6);
                ComprMethod = BitConverter.ToUInt16(entry, 8);
                ModifyTime = BitConverter.ToUInt16(entry, 10);
                ModifyDate = BitConverter.ToUInt16(entry, 12);
                CRC = BitConverter.ToUInt32(entry, 14);
                ComprSize = BitConverter.ToUInt32(entry, 18);
                UncomprSize = BitConverter.ToUInt32(entry, 22);

                ushort filelen = BitConverter.ToUInt16(entry, 26);
                ushort extralen = BitConverter.ToUInt16(entry, 28);

                char[] strbuild = new char[filelen];
                for (int i = 0; i < filelen; i++)
                {
                    strbuild[i] = (char)entry[30 + i];
                }
                Filename = new string(strbuild);

                if (extralen != 0)
                {
                    Extra = new byte[extralen];
                    Array.Copy(entry, 30 + filelen, Extra, 0, extralen);
                }
            }

            public bool Compare(ZipEntry entry)
            {
                if (this.Minvers != entry.Minvers)
                    return false;
                if (this.BitFlag != entry.BitFlag)
                    return false;
                if (this.ComprMethod != entry.ComprMethod)
                    return false;
                if (this.ModifyDate != entry.ModifyDate)
                    return false;
                if (this.ModifyTime != entry.ModifyTime)
                    return false;
                if (this.CRC != entry.CRC)
                    return false;
                if (this.ComprSize != entry.ComprSize)
                    return false;
                if (this.UncomprSize != entry.UncomprSize)
                    return false;
                return true;
            }
        }
        #endregion

        /* Magic numbers and constants */
        public const UInt32 filemagic = 0x04034B50;
        public const UInt32 datadescriptormagic = 0x08074B50;
        public const UInt32 dirfileheadermagic = 0x02014B50;
        public const UInt32 endofdirmagic = 0x06054B50;
        public const UInt32 tpfxor = 0x3FA43FA4;

        /* Public members */
        public List<ZipEntryFull> Entries;
        public String _filename;
        public EOFRecord EOFStrct;
		public string Description;
        public bool Scanned;

        /* Private members */
        private Int64 EOFRecordOff;

        /// <summary>
        /// Construct the zip object. Reads in the file list and uses it to populate the entries var
        /// </summary>
        /// <param name="filename">The file to read in</param>
        public ZipReader(String filename)
        {
            _filename = filename;

            using (FileStream fs = new FileStream(_filename, FileMode.Open, FileAccess.Read))
            {
                // The easiest way to do this is to just XOR the whole file, but let's try something a bit smarter
                // First read the first 4 bytes to do a prelim correctness test
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                if ((BitConverter.ToUInt32(buff, 0) ^ tpfxor) != filemagic)
                    throw new FormatException("Incorrect header");

                EOFRecordOff = FindEOFRecord(fs);
                EOFStrct = new EOFRecord(fs, EOFRecordOff);
                PopulateEntries(fs);
            }
        }

        /// <summary>
        /// This function attempts to find the location of the end of central directory record
        /// </summary>
        /// <param name="tpfstream">The stream to read</param>
        /// <returns>The offset</returns>
        private Int64 FindEOFRecord(Stream tpfstream)
        {
            tpfstream.Seek(-16, SeekOrigin.End); // The EOCDR is always at least 20 bytes. Let's go back 16, just to be safe
            tpfstream.Seek(-(tpfstream.Position % 4), SeekOrigin.Current); // Align the stream at a multiple of 4 for correct XOR
            while (tpfstream.Position > 0) // Safety catch
            {
                tpfstream.Seek(-16, SeekOrigin.Current); // Rewind by the buffer amount
                long tempoff = tpfstream.Position; // Record the offset
                int count = (tempoff < 19) ? (int)tempoff : 19; // Safety catch
                byte[] buff = new byte[count];
                tpfstream.Read(buff, 0, count);
                for (int i = 0; i < count - 3; i += 4) // XOR the buffer
                {
                    uint tempval = BitConverter.ToUInt32(buff, i);
                    tempval ^= tpfxor;
                    byte[] valbuff = BitConverter.GetBytes(tempval);
                    for (int j = 0; j < 4; j++)
                        buff[i + j] = valbuff[j];
                }
                uint tempxor = tpfxor;
                for (int i = count - 3; i < count; i++)
                {
                    buff[i] ^= (byte)(tempxor);
                    tempxor >>= 8;
                }
                for (int i = 0; i < count - 3; i++) // Loop through and check each value for the magic number
                {
                    uint tempval = BitConverter.ToUInt32(buff, i);
                    if (tempval == endofdirmagic)
                        return tempoff + i;
                }
                tpfstream.Seek(-19, SeekOrigin.Current); // Rewind to original read position
            }
            return -1; // Safety catch
        }

        /// <summary>
        /// Given a stream the function will return a byte array of length count that has been XORed with the TPF key
        /// </summary>
        /// <param name="buffstream">The stream to read. It should already be at the position to read from.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The XORed byte array</returns>
        internal static byte[] BuffXOR(Stream buffstream, int count)
        {
            if (buffstream.Position + count > buffstream.Length)
                throw new IndexOutOfRangeException("Filestream not long enough to XOR");
            byte[] buff = new byte[count];
            UInt32 tempxor = tpfxor;
            int mod = (int)buffstream.Position % 4;
            int buffpos = 0;
            if (mod != 0)
            {
                for (int i = 0; i < mod; i++)
                    tempxor >>= 8;
                for (int i = 0; i < (4 - mod); i++)
                {
                    buff[buffpos++] = (byte)((byte)buffstream.ReadByte() ^ (byte)tempxor);
                    tempxor >>= 8;
                }
                tempxor = tpfxor;
            }
            else
                mod = 4;
            int streampos = (int)buffstream.Position;
            int iters = (count - (4 - mod) - ((streampos + count - (4 - mod)) % 4)) / 4;
            for (int i = 0; i < iters; i++)
            {
                byte[] tempbyte = new byte[4];
                buffstream.Read(tempbyte, 0, 4);
                uint tempval = BitConverter.ToUInt32(tempbyte, 0);
                tempval ^= tpfxor;
                tempbyte = BitConverter.GetBytes(tempval);
                for (int j = 0; j < 4; j++)
                    buff[buffpos++] = tempbyte[j];
            }
            mod = 0;
            while (buffpos != count)
            {
                byte temp = (byte)buffstream.ReadByte();
                temp ^= (byte)tempxor;
                buff[buffpos++] = temp;
                tempxor >>= 8;
                mod++;
            }
            if (mod >= 4)
                throw new Exception();
            return buff;
        }

        /// <summary>
        /// Reads the Central Directory to populate the list of files
        /// </summary>
        /// <param name="buffstream">The Stream to read from</param>
        private void PopulateEntries(Stream buffstream)
        {
            buffstream.Seek(EOFStrct.CDOffset, SeekOrigin.Begin);
            Entries = new List<ZipEntryFull>();
            int count = 0;
            while (count < EOFStrct.CDSize)
            {
                byte[] buff = BuffXOR(buffstream, 34); // Read up until all the variable field lengths have been read
                int bufsize = BitConverter.ToUInt16(buff, 28) + BitConverter.ToUInt16(buff, 30) + BitConverter.ToUInt16(buff, 32) + 46; // Add them onto the total block size
                buffstream.Seek(-34, SeekOrigin.Current); // Rewind back to the start of the entry
                buff = BuffXOR(buffstream, bufsize); // Re-read the XOR
                ZipEntryFull entry = new ZipEntryFull(buff, this);
                Entries.Add(entry);
                count += bufsize;
            }
            if (buffstream.Position != EOFRecordOff)
                throw new FormatException("End of CD doesn't match position of EOFRecord");
        }
    }
}
