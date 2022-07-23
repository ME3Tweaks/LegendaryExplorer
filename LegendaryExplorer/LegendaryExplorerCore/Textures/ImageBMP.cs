/*
 * MassEffectModder
 *
 * Copyright (C) 2016-2017 Pawel Kolodziejski
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 *
 */

using System;
using System.IO;
using System.Numerics;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Textures
{
    public partial class Image
    {
        private const int BMP_TAG = 0x4D42;

        private static int getShiftFromMask(uint mask)
        {
            int shift = 0;

            if (mask == 0)
                return shift;

            while ((mask & 1) == 0)
            {
                mask >>= 1;
                shift++;
            }

            return shift;
        }

        private void LoadImageBMP(MemoryStream stream)
        {
            ushort tag = stream.ReadUInt16();
            if (tag != BMP_TAG)
                throw new Exception("not BMP header");

            stream.Skip(8);

            uint offsetData = stream.ReadUInt32();
            int headerSize = stream.ReadInt32();

            int imageWidth = stream.ReadInt32();
            int imageHeight = stream.ReadInt32();
            bool downToTop = true;
            if (imageHeight < 0)
            {
                imageHeight = -imageHeight;
                downToTop = false;
            }
            if (!BitOperations.IsPow2(imageWidth) || !BitOperations.IsPow2(imageHeight))
                throw new TextureSizeNotPowerOf2Exception();

            stream.Skip(2);

            int bits = stream.ReadUInt16();
            if (bits != 32 && bits != 24)
                throw new Exception("only 24 and 32 bits BMP supported!");

            bool hasAlphaMask = false;
            uint Rmask = 0xFF0000, Gmask = 0xFF00, Bmask = 0xFF, Amask = 0xFF000000;

            if (headerSize >= 40)
            {
                int compression = stream.ReadInt32();
                if (compression == 1 || compression == 2)
                    throw new Exception("compression not supported in BMP!");

                if (compression == 3)
                {
                    stream.Skip(20);
                    Rmask = stream.ReadUInt32();
                    Gmask = stream.ReadUInt32();
                    Bmask = stream.ReadUInt32();
                    if (headerSize >= 56)
                    {
                        Amask = stream.ReadUInt32();
                        hasAlphaMask = true;
                    }
                }

                stream.JumpTo(headerSize + 14);
            }

            int Rshift = getShiftFromMask(Rmask);
            int Gshift = getShiftFromMask(Gmask);
            int Bshift = getShiftFromMask(Bmask);
            int Ashift = getShiftFromMask(Amask);

            byte[] buffer = new byte[imageWidth * imageHeight * 4];
            int pos = downToTop ? imageWidth * (imageHeight - 1) * 4 : 0;
            int delta = downToTop ? -imageWidth * 4 * 2 : 0;
            for (int h = 0; h < imageHeight; h++)
            {
                for (int i = 0; i < imageWidth; i++)
                {
                    if (bits == 24)
                    {
                        buffer[pos++] = (byte)stream.ReadByte();
                        buffer[pos++] = (byte)stream.ReadByte();
                        buffer[pos++] = (byte)stream.ReadByte();
                        buffer[pos++] = 255;
                    }
                    else if (bits == 32)
                    {
                        uint p1 = (uint)stream.ReadByte();
                        uint p2 = (uint)stream.ReadByte();
                        uint p3 = (uint)stream.ReadByte();
                        uint p4 = (uint)stream.ReadByte();
                        uint pixel = p4 << 24 | p3 << 16 | p2 << 8 | p1;
                        buffer[pos++] = (byte)((pixel & Bmask) >> Bshift);
                        buffer[pos++] = (byte)((pixel & Gmask) >> Gshift);
                        buffer[pos++] = (byte)((pixel & Rmask) >> Rshift);
                        buffer[pos++] = (byte)((pixel & Amask) >> Ashift);
                    }
                }
                if (imageWidth % 4 != 0)
                    stream.Skip(4 - (imageWidth % 4));
                pos += delta;
            }

            if (bits == 32 && !hasAlphaMask)
            {
                bool hasAlpha = false;
                for (int i = 0; i < imageWidth * imageHeight; i++)
                {
                    if (buffer[4 * i + 3] != 0)
                    {
                        hasAlpha = true;
                        break;
                    }
                }

                if (!hasAlpha)
                {
                    for (int i = 0; i < imageWidth * imageHeight; i++)
                    {
                        buffer[4 * i + 3] = 255;
                    }
                }
            }

            pixelFormat = PixelFormat.ARGB;
            MipMap mipmap = new MipMap(buffer, imageWidth, imageHeight, PixelFormat.ARGB);
            mipMaps.Add(mipmap);
        }
    }
}
