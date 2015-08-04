using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFreonLib.Misc.Extensions
{
    public static class Extensions
    {
        public static bool? isDirectory(this string str)
        {
            // KFreon: Check if things exist first
            if (str == null || !File.Exists(str) && !Directory.Exists(str))
                return false;


            FileAttributes attr = File.GetAttributes(str);
            if (attr.HasFlag(FileAttributes.Directory))
                return true;
            else
                return false;
        }

        public static bool? isFile(this string str)
        {
            return !str.isDirectory();
        }
    }
}
