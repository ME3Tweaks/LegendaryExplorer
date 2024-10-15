using System.Runtime.InteropServices;
using System.Text;

namespace LegendaryExplorer.Misc
{
    /// <summary>
    /// Class for handling interaction with the 3DMIGOTO HLSL Decompiler for DirectX 11.
    /// </summary>
    public static partial class HLSLDecompiler
    {
        private static readonly object DecompileShaderLock = new(); 

        private const int MAX_SHADER_LEN = 65535; // 64KB

        [LibraryImport(@"HLSLDecompiler.dll")]
        private static partial int DecompileShader([In] byte[] shaderData, uint shaderDataLen, [Out] byte[] outStr, int buffSize, [MarshalAs(UnmanagedType.Bool)] bool includedCreatedBy);

        public static string DecompileShader(byte[] bytecode, bool includeCreatedBy)
        {
            if (bytecode == null)
                return "Bytecode not loaded for shader!";

            byte[] buffer = new byte[65535];
            int strLen;
            lock (DecompileShaderLock)
            {
                strLen = DecompileShader(bytecode, (uint)bytecode.Length, buffer, buffer.Length, includeCreatedBy);
            }
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
