using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace LegendaryExplorerCore.Shaders
{
    /// <summary>
    /// Class for handling interaction with the 3DMIGOTO HLSL Decompiler for DirectX 11.
    /// </summary>
    public static partial class HLSLDecompiler
    {
        private static readonly Lock DecompileShaderLock = new();

        private const int MAX_SHADER_LEN = 65535; // 64KB

        //[LibraryImport(@"HLSLDecompiler.dll")]
        [LibraryImport(@"HLSLDecompiler2.dll")]
        //private static partial int DecompileShader([In] byte[] shaderData, uint shaderDataLen, [Out] byte[] outStr, int buffSize, [MarshalAs(UnmanagedType.Bool)] bool includedCreatedBy);
        //private static partial int DecompileShader([In] byte[] shaderData, uint shaderDataLen, [Out] byte[] outStr, int buffSize);
        private static partial int DecompileShader([In] byte[] shaderData, uint shaderDataLen, [Out] byte[] outStr, int buffSize);
        public static string DecompileShader(byte[] bytecode, bool includeCreatedBy, int bufferSize = 65535)
        {
            if (bytecode == null)
                return "Bytecode not loaded for shader!";

            byte[] buffer = new byte[bufferSize];
            int resCode;
            lock (DecompileShaderLock)
            {
                // strLen = DecompileShader(bytecode, (uint)bytecode.Length, buffer, buffer.Length, includeCreatedBy);
                resCode = DecompileShader(bytecode, (uint)bytecode.Length, buffer, buffer.Length);
            }

            /*
             * •	0 — Success. outStr contains the null-terminated decompiled HLSL text.
•	Positive value — Buffer too small. The returned value is the required buffer size (including null terminator). Call again with a larger buffer.
•	-1 — Invalid input (null or zero-length shader data).
•	-2 — Decompilation failed.
•	-3 — Unhandled exception during decompilation.
            */

            if (resCode == -1)
            {
                // Buffer too small;
                return "Invalid input";
            }
            if (resCode == -2)
            {
                return "Decompilation failed";
            }
            if (resCode == -3)
            {
                return "Unhandled exception during decompilation";
            }
            if (resCode > 0)
            {
                return DecompileShader(bytecode, includeCreatedBy, resCode);
            }

            var sr = Encoding.ASCII.GetString(buffer, 0, buffer.Length);
            return sr;
        }
    }
}
