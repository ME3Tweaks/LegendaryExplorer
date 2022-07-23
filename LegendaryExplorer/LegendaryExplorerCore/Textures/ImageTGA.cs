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
        private void LoadImageTGA(MemoryStream stream)
        {
            int idLength = stream.ReadByte();

            int colorMapType = stream.ReadByte();
            if (colorMapType != 0)
                throw new Exception("indexed TGA not supported!");

            int imageType = stream.ReadByte();
            if (imageType != 2 && imageType != 10)
                throw new Exception("only RGB TGA supported!");

            bool compressed = imageType == 10;

            stream.SkipInt16(); // color map first entry index
            stream.SkipInt16(); // color map length
            stream.Skip(1); // color map entry size
            stream.SkipInt16(); // x origin
            stream.SkipInt16(); // y origin

            int imageWidth = stream.ReadInt16();
            int imageHeight = stream.ReadInt16();
            if (!BitOperations.IsPow2(imageWidth) || !BitOperations.IsPow2(imageHeight))
                throw new TextureSizeNotPowerOf2Exception();

            int imageDepth = stream.ReadByte();
            if (imageDepth != 32 && imageDepth != 24)
                throw new Exception("only 24 and 32 bits TGA supported!");

            int imageDesc = stream.ReadByte();
            if ((imageDesc & 0x10) != 0)
                throw new Exception("origin right not supported in TGA!");

            bool downToTop = (imageDesc & 0x20) == 0;

            stream.Skip(idLength);

            byte[] buffer = new byte[imageWidth * imageHeight * 4];
            int pos = downToTop ? imageWidth * (imageHeight - 1) * 4 : 0;
            int delta = downToTop ? -imageWidth * 4 * 2 : 0;
            if (compressed)
            {
                int count = 0, repeat = 0, w = 0, h = 0;
                for (; ; )
                {
                    if (count == 0 && repeat == 0)
                    {
                        byte code = (byte)stream.ReadByte();
                        if ((code & 0x80) != 0)
                            repeat = (code & 0x7F) + 1;
                        else
                            count = code + 1;
                    }
                    else
                    {
                        if (repeat != 0)
                        {
                            byte pixelR = (byte)stream.ReadByte();
                            byte pixelG = (byte)stream.ReadByte();
                            byte pixelB = (byte)stream.ReadByte();
                            byte pixelA;
                            if (imageDepth == 32)
                                pixelA = (byte)stream.ReadByte();
                            else
                                pixelA = 0xFF;
                            for (; w < imageWidth && repeat > 0; w++, repeat--)
                            {
                                buffer[pos++] = pixelR;
                                buffer[pos++] = pixelG;
                                buffer[pos++] = pixelB;
                                buffer[pos++] = pixelA;
                            }
                        }
                        else
                        {
                            for (; w < imageWidth && count > 0; w++, count--)
                            {
                                buffer[pos++] = (byte)stream.ReadByte();
                                buffer[pos++] = (byte)stream.ReadByte();
                                buffer[pos++] = (byte)stream.ReadByte();
                                if (imageDepth == 32)
                                    buffer[pos++] = (byte)stream.ReadByte();
                                else
                                    buffer[pos++] = 0xFF;
                            }
                        }
                    }

                    if (w == imageWidth)
                    {
                        w = 0;
                        pos += delta;
                        if (++h == imageHeight)
                            break;
                    }
                }
            }
            else
            {
                for (int h = 0; h < imageHeight; h++, pos += delta)
                {
                    for (int w = 0; w < imageWidth; w++)
                    {
                        buffer[pos++] = (byte)stream.ReadByte();
                        buffer[pos++] = (byte)stream.ReadByte();
                        buffer[pos++] = (byte)stream.ReadByte();
                        if (imageDepth == 32)
                            buffer[pos++] = (byte)stream.ReadByte();
                        else
                            buffer[pos++] = 0xFF;
                    }
                }
            }

            pixelFormat = PixelFormat.ARGB;
            MipMap mipmap = new MipMap(buffer, imageWidth, imageHeight, PixelFormat.ARGB);
            mipMaps.Add(mipmap);
        }
    }
}
