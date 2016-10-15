using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace SaltTPF
{
    public static class ZipWriter
    {
        private class WriterZipEntry
        {
            public UInt16 Time;
            public UInt16 Date;
            public UInt32 CRC32;
            public UInt32 ComprLen;
            public UInt32 UncLen;
            public long MainOffset;
            public String Filename;

            public WriterZipEntry() { }
        }

        public const UInt16 MinVers = 0x14;
        //public const UInt16 BitFlag = 0x9; // Set: Encrypted, Defer CRC - Texmod uses this one, but ours won't work if we do...
        public const UInt16 BitFlag = 0x1;
        public const UInt16 ComprMethod = 0x8;  // Deflate (default)
        public static uint tpfxor = 0x3FA43FA4;
        public const UInt32 ExternalAttr = 0;
        //public const UInt32 ExternalAttr = 0x81B40020;  // Texmod uses this one but ours won't work if we do...
        static CRC32 crc = new CRC32();

        static WriterZipEntry BuildEntry(Stream ms, string file, Func<byte[]> dataGetter)
        {
            bool FileOnDisk = dataGetter == null;

            WriterZipEntry entry = new WriterZipEntry();
            entry.MainOffset = ms.Position;
            entry.Filename = FileOnDisk ? Path.GetFileName(file) : file;

            //Header
            ms.Write(BitConverter.GetBytes(ZipReader.filemagic), 0, 4);
            ms.Write(BitConverter.GetBytes(MinVers), 0, 2);
            ms.Write(BitConverter.GetBytes(BitFlag), 0, 2);
            ms.Write(BitConverter.GetBytes(ComprMethod), 0, 2);

            //Date/time
            DateTime date = FileOnDisk ? new FileInfo(file).LastWriteTime : DateTime.Now;
            ushort secs = 0;
            ushort mins = 0;
            ushort hours = 0;

            ushort day = 0;
            ushort month = 0;
            ushort year = 0;

            secs = (ushort)date.Second;
            mins = (ushort)date.Minute;
            hours = (ushort)date.Hour;

            ushort datetime = (ushort)(secs / 2);
            datetime |= (ushort)(mins >> 5);
            datetime |= (ushort)(hours >> 11);
            ms.Write(BitConverter.GetBytes(datetime), 0, 2);
            entry.Time = datetime;

            day = (ushort)date.Day;
            month = (ushort)date.Month;
            year = (ushort)date.Year;

            datetime = day;
            datetime &= (ushort)(month >> 5);
            datetime &= (ushort)(year >> 9);
            ms.Write(BitConverter.GetBytes(datetime), 0, 2);
            entry.Date = datetime;

            byte[] comprData;
            Stream dataStream = null;
            if (FileOnDisk)
                dataStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            else
                dataStream = new MemoryStream(dataGetter());

            using (dataStream)
            {
                byte[] buff = new byte[dataStream.Length];
                dataStream.Read(buff, 0, buff.Length);

                // create and write local header
                entry.CRC32 = crc.BlockChecksum(buff);
                entry.UncLen = (UInt32)dataStream.Length;

                ms.Write(BitConverter.GetBytes(entry.CRC32), 0, 4);

                ms.Seek(4, SeekOrigin.Current); // Skip compressed size
                ms.Write(BitConverter.GetBytes(entry.UncLen), 0, 4);

                if (entry.Filename.ToLower().Contains("meresults"))
                    entry.Filename = "texmod.def";

                ms.Write(BitConverter.GetBytes((ushort)entry.Filename.Length), 0, 2);
                ms.Write(BitConverter.GetBytes((ushort)0), 0, 2);  // extra

                // Filename
                foreach (char c in entry.Filename)
                    ms.WriteByte((byte)c);

                dataStream.Seek(0, SeekOrigin.Begin); // Rewind to compress entry
                using (MemoryStream ms2 = new MemoryStream())
                {
                    using (DeflateStream deflator = new DeflateStream(ms2, CompressionMode.Compress))
                        dataStream.CopyTo(deflator);

                    comprData = ms2.ToArray();
                }
                entry.ComprLen = (uint)comprData.Length + 12;  // 12 is crypt header
                ZipCrypto.EncryptData(ms, comprData, entry.CRC32); // Encrypt and write data
            }

            // Footer
            ms.Write(BitConverter.GetBytes(ZipReader.datadescriptormagic), 0, 4);
            ms.Write(BitConverter.GetBytes(entry.CRC32), 0, 4);
            ms.Write(BitConverter.GetBytes(entry.ComprLen), 0, 4);
            ms.Write(BitConverter.GetBytes(entry.UncLen), 0, 4);

            // Go back and write compressed length
            ms.Seek(entry.MainOffset + 18, SeekOrigin.Begin);
            ms.Write(BitConverter.GetBytes(entry.ComprLen), 0, 4);

            // Go to end for next entry
            ms.Seek(0, SeekOrigin.End);
            return entry;
        }

        static void WriteGlobalEntryHeader(ZipWriter.WriterZipEntry entry, Stream ms)
        {
            ms.Write(BitConverter.GetBytes(entry.Time), 0, 2);
            ms.Write(BitConverter.GetBytes(entry.Date), 0, 2);
            ms.Write(BitConverter.GetBytes(entry.CRC32), 0, 4);
            ms.Write(BitConverter.GetBytes(entry.ComprLen), 0, 4);
            ms.Write(BitConverter.GetBytes(entry.UncLen), 0, 4);
            ms.Write(BitConverter.GetBytes((ushort)entry.Filename.Length), 0, 2);
            ms.Write(BitConverter.GetBytes(0), 0, 4); // 0 for extra field, 0 for comment
            ms.Write(BitConverter.GetBytes(0), 0, 4); // 0 for disk no, 0 for internal attributes
            ms.Write(BitConverter.GetBytes(ExternalAttr), 0, 4);
            ms.Write(BitConverter.GetBytes((int)entry.MainOffset), 0, 4);
            foreach (char c in entry.Filename)
                ms.WriteByte((byte)c);
        }

        static void BuildTPF(Stream ms, string Author, string Comment, List<WriterZipEntry> Entries)
        {
            uint cdlen = 0;
            uint cdpos = (uint)ms.Position;
            for (int i = 0; i < Entries.Count; i++)
            {
                // Create and write Full entry header
                ms.Write(BitConverter.GetBytes(ZipReader.dirfileheadermagic), 0, 4);
                ms.Write(BitConverter.GetBytes(0), 0, 2);
                ms.Write(BitConverter.GetBytes(ZipWriter.MinVers), 0, 2);
                ms.Write(BitConverter.GetBytes(ZipWriter.BitFlag), 0, 2);
                ms.Write(BitConverter.GetBytes(ZipWriter.ComprMethod), 0, 2);

                ZipWriter.WriterZipEntry entry = Entries[i];
                WriteGlobalEntryHeader(entry, ms);
            }
            cdlen = (uint)(ms.Position - cdpos);

            // EOF Record
            ms.Write(BitConverter.GetBytes(ZipReader.endofdirmagic), 0, 4);
            ms.Write(BitConverter.GetBytes(0), 0, 4);  // Disk No = 0, // Start disk = 0
            ms.Write(BitConverter.GetBytes((ushort)Entries.Count), 0, 2);
            ms.Write(BitConverter.GetBytes((ushort)Entries.Count), 0, 2);
            ms.Write(BitConverter.GetBytes(cdlen), 0, 4);
            ms.Write(BitConverter.GetBytes(cdpos), 0, 4);

            // KFreon: This is the created by/comment section. Seems to be required for Texmod.
            string createdByANDComment = Author + "\r\n" + Comment;  // KFreon: Newline required by Texmod to seperate author and comment.
            byte[] bytes = Encoding.Default.GetBytes(createdByANDComment);
            ms.Write(BitConverter.GetBytes(createdByANDComment.Length), 0, 2);
            ms.Write(bytes, 0, bytes.Length);


            long streamlen = ms.Length;
            ms.Seek(0, SeekOrigin.Begin); // XOR the file
            // Why this way? Shouldn't be done like this
            while (ms.Position < ms.Length)
            {
                int count = (ms.Position + 10000 <= ms.Length) ? 10000 : (int)(ms.Length - ms.Position);

                byte[] buff2 = BuffXOR(ms, count);
                ms.Seek(-count, SeekOrigin.Current);
                ms.Write(buff2, 0, count);
            }
        }

        public static void Repack(String filename, List<String> files, string Author = "", string Comment = "")
        {
            List<WriterZipEntry> Entries = new List<WriterZipEntry>();
            foreach (String file in files)
            {
                if (!File.Exists(file))
                    throw new FileNotFoundException(Path.GetFileName(file) + " was not found");
            }

            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                foreach (String file in files)
                {
                    var entry = BuildEntry(fs, file, null);
                    Entries.Add(entry);
                }

                BuildTPF(fs, Author, Comment, Entries);
            }
        }

        public static void Repack(string destination, List<Tuple<string, Func<byte[]>>> FilenamesAndDataGetters, string Author = "", string Comment = "")
        {
            List<WriterZipEntry> Entries = new List<WriterZipEntry>();
            using (MemoryStream ms = new MemoryStream())
            {
                foreach (var tex in FilenamesAndDataGetters)
                {
                    var entry = BuildEntry(ms, tex.Item1, tex.Item2);
                    Entries.Add(entry);
                }

                BuildTPF(ms, Author, Comment, Entries);
                ms.Seek(0, SeekOrigin.Begin);
                using (FileStream fs = new FileStream(destination, FileMode.Create))
                    ms.CopyTo(fs);
            }
        }

        private static byte[] BuffXOR(Stream buffstream, int count)
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
    }
}
