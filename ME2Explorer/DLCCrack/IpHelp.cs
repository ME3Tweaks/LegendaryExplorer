using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace ME2Explorer.DLC_Crack
{
    class IpHelp
    {
        public static List<Native.IP_ADAPTER_INFO> GetAdaptersInfo()
        {
            List<Native.IP_ADAPTER_INFO> list = new List<Native.IP_ADAPTER_INFO>();
            Native.IP_ADAPTER_INFO item = new Native.IP_ADAPTER_INFO();
            byte[] buffer = new byte[0x10 * Marshal.SizeOf(typeof(Native.IP_ADAPTER_INFO))];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            int length = buffer.Length;
            if (Native.GetAdaptersInfo(handle.AddrOfPinnedObject(), ref length) != 0)
            {
                return null;
            }
            item = (Native.IP_ADAPTER_INFO)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(Native.IP_ADAPTER_INFO));
            while (true)
            {
                list.Add(item);
                if (item.Next == IntPtr.Zero)
                {
                    break;
                }
                item = (Native.IP_ADAPTER_INFO)Marshal.PtrToStructure(item.Next, typeof(Native.IP_ADAPTER_INFO));
            }
            handle.Free();
            return list;
        }

        public static class Native
        {
            public const int MAX_ADAPTER_ADDRESS_LENGTH = 8;
            public const int MAX_ADAPTER_DESCRIPTION_LENGTH = 0x80;
            public const int MAX_ADAPTER_NAME_LENGTH = 0x100;

            [DllImport("iphlpapi.dll", CharSet = CharSet.Ansi)]
            public static extern uint GetAdaptersInfo(IntPtr adapterInfos, ref int bufOutLen);

            [StructLayout(LayoutKind.Sequential)]
            public struct IP_ADAPTER_INFO
            {
                public IntPtr Next;
                public int ComboIndex;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
                public string AdapterName;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x84)]
                public string AdapterDescription;
                public uint AddressLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
                public byte[] Address;
                public int Index;
                public uint Type;
                public uint DhcpEnabled;
                public IntPtr CurrentIpAddress;
                public IpHelp.Native.IP_ADDR_STRING IpAddressList;
                public IpHelp.Native.IP_ADDR_STRING GatewayList;
                public IpHelp.Native.IP_ADDR_STRING DhcpServer;
                public bool HaveWins;
                public IpHelp.Native.IP_ADDR_STRING PrimaryWinsServer;
                public IpHelp.Native.IP_ADDR_STRING SecondaryWinsServer;
                public int LeaseObtained;
                public int LeaseExpires;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct IP_ADDR_STRING
            {
                public IntPtr Next;
                public IpHelp.Native.IP_ADDRESS_STRING IpAddress;
                public IpHelp.Native.IP_ADDRESS_STRING IpMask;
                public int Context;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct IP_ADDRESS_STRING
            {
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x10)]
                public string Address;
            }
        }
    }
}
