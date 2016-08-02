using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gibbed.IO;
using AmaroK86.MassEffect3.ZlibBlock;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Forms;
using System.Collections.ObjectModel;

namespace ME3Explorer.Packages
{
    /// <summary>
    ///     Class used to Compress/Decompress pcc files.
    /// </summary>
    public static class MEPackageHandler
    {
        static Dictionary<string, WeakReference<IMEPackage>> openPackages = new Dictionary<string, WeakReference<IMEPackage>>();
        public static ObservableCollection<IMEPackage> packagesInTools = new ObservableCollection<IMEPackage>();

        static Func<string, ME1Package> ME1ConstructorDelegate;
        static Func<string, ME2Package> ME2ConstructorDelegate;
        static Func<string, ME3Package> ME3ConstructorDelegate;

        public static void Initialize()
        {
            ME1ConstructorDelegate = ME1Package.Initialize();
            ME2ConstructorDelegate = ME2Package.Initialize();
            ME3ConstructorDelegate = ME3Package.Initialize();
        }

        public static IMEPackage OpenMEPackage(string pathToFile, Window wpfWindow = null, Form winForm = null)
        {
            IMEPackage package = null;
            if (openPackages.ContainsKey(pathToFile))
            {
                if (openPackages[pathToFile].TryGetTarget(out package))
                {
                    if (wpfWindow != null)
                    {
                        package.RegisterTool(new GenericWindow(wpfWindow, Path.GetFileName(pathToFile)));
                        addToPackagesInTools(package);
                    }
                    else if (winForm != null)
                    {
                        package.RegisterTool(new GenericWindow(winForm, Path.GetFileName(pathToFile)));
                        addToPackagesInTools(package);
                    }
                    return package;
                }
                else
                {
                    openPackages.Remove(pathToFile);
                }
            }
            ushort version;
            ushort licenseVersion;
            using (FileStream fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
            {
                fs.Seek(4, SeekOrigin.Begin);
                version = fs.ReadValueU16();
                licenseVersion = fs.ReadValueU16();
            }

            if (version == 684 && licenseVersion == 194)
            {
                package = ME3ConstructorDelegate(pathToFile);
            }
            else if (version == 512 && licenseVersion == 130)
            {
                package = ME2ConstructorDelegate(pathToFile);
            }
            else if (version == 491 && licenseVersion == 1008)
            {
                package = ME1ConstructorDelegate(pathToFile);
            }
            else
            {
                throw new FormatException("Not an ME1, ME2, or ME3 package file.");
            }
            if (wpfWindow != null)
            {
                package.RegisterTool(new GenericWindow(wpfWindow, Path.GetFileName(pathToFile)));
                addToPackagesInTools(package);
            }
            else if (winForm != null)
            {
                package.RegisterTool(new GenericWindow(winForm, Path.GetFileName(pathToFile)));
                addToPackagesInTools(package);
            }
            openPackages.Add(pathToFile, new WeakReference<IMEPackage>(package));
            return package;
        }

        private static void addToPackagesInTools(IMEPackage package)
        {
            if (!packagesInTools.Contains(package))
            {
                packagesInTools.Add(package);
                package.noLongerOpenInTools += Package_noLongerOpenInTools;
            }
        }

        private static void Package_noLongerOpenInTools(object sender, EventArgs e)
        {
            packagesInTools.Remove(sender as IMEPackage);
        }

        public static ME3Package OpenME3Package(string pathToFile, Window wpfWindow = null, Form winForm = null)
        {
            ME3Package pcc = OpenMEPackage(pathToFile, wpfWindow, winForm) as ME3Package;
            if (pcc == null)
            {
                throw new FormatException("Not an ME3 package file.");
            }
            return pcc;
        }

        public static ME2Package OpenME2Package(string pathToFile, Window wpfWindow = null, Form winForm = null)
        {
            ME2Package pcc = OpenMEPackage(pathToFile, wpfWindow, winForm) as ME2Package;
            if (pcc == null)
            {
                throw new FormatException("Not an ME2 package file.");
            }
            return pcc;
        }

        public static ME1Package OpenME1Package(string pathToFile, Window wpfWindow = null, Form winForm = null)
        {
            ME1Package pcc = OpenMEPackage(pathToFile, wpfWindow, winForm) as ME1Package;
            if (pcc == null)
            {
                throw new FormatException("Not an ME1 package file.");
            }
            return pcc;
        }
    }
}
