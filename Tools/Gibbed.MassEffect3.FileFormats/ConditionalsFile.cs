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
using System.IO;
using Gibbed.IO;

namespace Gibbed.MassEffect3.FileFormats
{
    public class ConditionalsFile
    {
        public Endian Endian;
        public uint Version;

        public void Serialize(Stream output)
        {
            throw new NotImplementedException();
        }

        public void Deserialize(Stream input)
        {
            var magic = input.ReadValueU32(Endian.Little);
            if (magic != 0x434F4E44 && // COND
                magic.Swap() != 0x434F4E44)
            {
                throw new FormatException();
            }
            var endian = magic == 0x434F4E44 ? Endian.Little : Endian.Big;

            var version = input.ReadValueU32(endian);
            if (version != 1)
            {
                throw new FormatException();
            }
            this.Version = version;

            var unknown08 = input.ReadValueU16(endian);
            var count = input.ReadValueU16(endian);

            var ids = new int[count];
            var offsets = new uint[count];
            for (ushort i = 0; i < count; i++)
            {
                ids[i] = input.ReadValueS32(endian);
                offsets[i] = input.ReadValueU32(endian);
            }

            for (ushort i = 0; i < count; i++)
            {
                var id = ids[i];
                var offset = offsets[i];

                input.Seek(offset, SeekOrigin.Begin);

                var flags = input.ReadValueU8();

                var valueType = (Conditionals.ValueType)((flags & 0x0F) >> 0);
                var opType = (Conditionals.OpType)((flags & 0xF0) >> 4);

                if (valueType == Conditionals.ValueType.Bool)
                {
                    switch (opType)
                    {
                        default:
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
            }

            //throw new NotImplementedException();

            this.Endian = endian;
        }
    }
}
