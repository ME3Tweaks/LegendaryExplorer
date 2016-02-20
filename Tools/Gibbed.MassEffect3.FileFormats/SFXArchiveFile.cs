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
using System.Collections.Generic;
using System.IO;
using Gibbed.IO;

namespace Gibbed.MassEffect3.FileFormats
{
    public class SFXArchiveFile
    {
        public Endian Endian;
        public List<uint> BlockSizes
            = new List<uint>();
        public List<SFXArchive.Entry> Entries
            = new List<SFXArchive.Entry>();
        public uint MaximumBlockSize;
        public SFXArchive.CompressionScheme CompressionScheme;

        public void Serialize(Stream output)
        {
            throw new NotImplementedException();
        }

        public void Deserialize(Stream input)
        {
            var magic = input.ReadValueU32(Endian.Little);
            if (magic != 0x53464152 && // SFAR
                magic.Swap() != 0x53464152)
            {
                throw new FormatException();
            }
            var endian = magic == 0x53464152 ? Endian.Little : Endian.Big;

            var version = input.ReadValueU32(endian);
            if (version != 0x00010000)
            {
                throw new FormatException();
            }

            var dataOffset = input.ReadValueU32(endian);
            var fileTableOffset = input.ReadValueU32(endian);
            var fileTableCount = input.ReadValueU32(endian);
            var blockSizeTableOffset = input.ReadValueU32(endian);
            this.MaximumBlockSize = input.ReadValueU32(endian);
            this.CompressionScheme = input
                .ReadValueEnum<SFXArchive.CompressionScheme>(endian);

            if (fileTableOffset != 0x20)
            {
                throw new FormatException();
            }

            if (this.MaximumBlockSize != 0x010000)
            {
                throw new FormatException();
            }

            /*
            if (this.CompressionScheme != SFXArchive.CompressionScheme.None &&
                this.CompressionScheme != SFXArchive.CompressionScheme.LZMA &&
                this.CompressionScheme != SFXArchive.CompressionScheme.LZX)
            {
                throw new FormatException();
            }
            */

            input.Seek(blockSizeTableOffset, SeekOrigin.Begin);

            var blockSizeTableSize = dataOffset - fileTableOffset;
            var blockSizeTableCount = blockSizeTableSize / 2;
            this.BlockSizes.Clear();
            for (uint i = 0; i < blockSizeTableCount; i++)
            {
                this.BlockSizes.Add(input.ReadValueU16(endian));
            }

            input.Seek(fileTableOffset, SeekOrigin.Begin);
            for (uint i = 0; i < fileTableCount; i++)
            {
// ReSharper disable UseObjectOrCollectionInitializer
                var entry = new SFXArchive.Entry();
// ReSharper restore UseObjectOrCollectionInitializer
                entry.NameHash = input.ReadFileNameHash();
                entry.BlockSizeIndex = input.ReadValueS32(endian);
                entry.UncompressedSize = input.ReadValueU32(endian);
                entry.UncompressedSize |= ((long)input.ReadValueU8()) << 32;
                entry.Offset = input.ReadValueU32(endian);
                entry.Offset |= ((long)input.ReadValueU8()) << 32;
                this.Entries.Add(entry);
            }
        }
    }
}
