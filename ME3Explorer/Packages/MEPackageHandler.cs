using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Collections.Concurrent;

namespace ME3Explorer.Packages
{
    /// <summary>
    ///     Class used to Compress/Decompress pcc files.
    /// </summary>
    public static class MEPackageHandler
    {
        static Dictionary<string, IMEPackage> openPackages = new Dictionary<string, IMEPackage>();

        public static IMEPackage OpenMEPackage(string pathToFile)
        {
            if (openPackages.ContainsKey(pathToFile))
            {
                return openPackages[pathToFile];
            }
            IMEPackage package = null;
            ushort version;
            ushort licenseVersion;
            using (FileStream fs = new FileStream(pathToFile, FileMode.Open))
            {
                fs.Seek(4, SeekOrigin.Begin);
                version = fs.ReadValueU16();
                licenseVersion = fs.ReadValueU16();
            }

            if (version == 684 && licenseVersion == 194)
            {
                package = new ME3Package(pathToFile);
            }
            else if (version == 512 && licenseVersion == 130)
            {
                package = new ME2Package(pathToFile);
            }
            else if (version == 491 && licenseVersion == 1008)
            {
                package = new ME1Package(pathToFile);
            }
            else
            {
                throw new FormatException("Not an ME1, ME2, or ME3 package file.");
            }
            openPackages.Add(pathToFile, package);
            return package;
        }
    }
}
