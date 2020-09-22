using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ME3ExplorerCore.Tests.helpers
{
    public static class Extensions
    {
        /// <summary>
        /// Checks if the extension of a string matches a package
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool RepresentsPackageFilePath(this string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(@".pcc", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".sfm", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".u", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".upk", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (extension.Equals(@".xxx", StringComparison.InvariantCultureIgnoreCase)) return true;
            return false;
        }
    }
}
