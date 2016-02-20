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

// is this code terrible?
// yup!

using System.Collections;
using System.Text;

namespace Gibbed.MassEffect3.FileFormats.Huffman
{
    public static class Decoder
    {
        public static string Decode(Pair[] tree, BitArray data, int offset, int maxLength)
        {
            var sb = new StringBuilder();

            var start = tree.Length - 1;
            while (true)
            {
                int node = start;
                do
                {
                    node = data[offset] == false ?
                        tree[node].Left :
                        tree[node].Right;
                    offset++;
                }
                while (node >= 0);

                var c = (ushort)(-1 - node);
                if (c == 0)
                {
                    break;
                }

                sb.Append((char)c);
                if (sb.Length >= maxLength)
                {
                    break;
                }
            }

            return sb.ToString();
        }
    }
}
