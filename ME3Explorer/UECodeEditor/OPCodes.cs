using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ME3Explorer.UECodeEditor
{
    public static class OPCodes
    {
        public struct OPCEntry
        {
            public int OPCode;
            public string Pattern;
        }

        public static List<OPCEntry> Table;
        public static bool isValid(byte[] buff, int pos)
        {
            if (Table == null)
                return false;
            byte b1 = buff[pos];
            byte b2 = buff[pos + 1];
            int index;
            if ((b1 & 0xF0) == 0x70)
                index = ((b1 - 0x70) << 8) + b2;
            else
                index = b1;
            foreach(OPCEntry e in Table)
                if (e.OPCode == index)
                    return true;
            return false;
        }

        public static string GetPattern(byte[] buff, int pos)
        {
            if (Table == null)
                return "";
            byte b1 = buff[pos];
            byte b2 = buff[pos + 1];
            int index;
            if ((b1 & 0xF0) == 0x70)
                index = ((b1 - 0x70) << 8) + b2;
            else
                index = b1;
            foreach (OPCEntry e in Table)
                if (e.OPCode == index)
                    return e.Pattern;
            return "";
        }
    }
}
