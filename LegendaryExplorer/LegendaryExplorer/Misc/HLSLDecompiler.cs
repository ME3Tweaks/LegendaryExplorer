using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.SharedUI.Converters;

namespace LegendaryExplorer.Misc
{
    /// <summary>
    /// Class for handling interaction with the 3DMIGOTO HLSL Decompiler for DirectX 11. This class is NOT thread safe.
    /// </summary>
    public partial class HLSLDecompiler
    {
        private const int MAX_SHADER_LEN = 65535; // 64KB

        [LibraryImport(@"HLSLDecompiler.dll")]
        private static partial int DecompileShader([In] byte[] shaderData, uint shaderDataLen, [Out] byte[] outStr, int buffSize, [MarshalAs(UnmanagedType.Bool)] bool includedCreatedBy);

        public static string DecompileShader(byte[] bytecode, bool includeCreatedBy)
        {
            if (bytecode == null)
                return "Bytecode not loaded for shader!";

            byte[] buffer = new byte[65535];
            var strLen = DecompileShader(bytecode, (uint)bytecode.Length, buffer, buffer.Length, includeCreatedBy);
            if (strLen == -1)
            {
                // Buffer too small;
                return "Buffer too small for decompile!";
            }
            var sr = Encoding.ASCII.GetString(buffer, 0, strLen);
            return sr;
        }
    }
}
