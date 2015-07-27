using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace ME2Explorer.DLC_Crack
{
    class DataProtection
    {
        public static byte[] Encrypt(byte[] data, byte[] magic)
        {
            Native.DataBlob blob;
            Native.DataBlob blob3;
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            blob.Size = data.Length;
            blob.Data = handle.AddrOfPinnedObject();
            bool flag = false;
            if (magic != null)
            {
                Native.DataBlob blob2;
                GCHandle handle2 = GCHandle.Alloc(magic, GCHandleType.Pinned);
                blob2.Size = magic.Length;
                blob2.Data = handle2.AddrOfPinnedObject();
                flag = Native.CryptProtectData(ref blob, null, ref blob2, IntPtr.Zero, IntPtr.Zero, 1, out blob3);
                handle2.Free();
            }
            else
            {
                flag = Native.CryptProtectData(ref blob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out blob3);
            }
            handle.Free();
            byte[] destination = null;
            if (flag)
            {
                destination = new byte[blob3.Size];
                if ((blob3.Size > 0) && (blob3.Data != IntPtr.Zero))
                {
                    Marshal.Copy(blob3.Data, destination, 0, blob3.Size);
                }
            }
            else
            {
                destination = null;
            }
            if (blob3.Data != IntPtr.Zero)
            {
                Native.LocalFree(blob3.Data);
            }
            return destination;
        }

        private static class Native
        {
            public const uint CRYPTPROTECT_UI_FORBIDDEN = 1;

            [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
            public static extern bool CryptProtectData(ref DataBlob dataIn, string description, IntPtr optionalEntropy, IntPtr reserved, IntPtr promptStruct, uint flags, out DataBlob dataOut);
            [DllImport("crypt32.dll", CharSet = CharSet.Unicode)]
            public static extern bool CryptProtectData(ref DataBlob dataIn, string description, ref DataBlob optionalEntropy, IntPtr reserved, IntPtr promptStruct, uint flags, out DataBlob dataOut);
            [DllImport("kernel32.dll")]
            public static extern IntPtr LocalFree(IntPtr hMem);

            [StructLayout(LayoutKind.Sequential)]
            public struct CryptProtectPromptStruct
            {
                public int Size;
                public int Flags;
                public IntPtr Window;
                public string Message;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct DataBlob
            {
                public int Size;
                public IntPtr Data;
            }
        }
    }
}
