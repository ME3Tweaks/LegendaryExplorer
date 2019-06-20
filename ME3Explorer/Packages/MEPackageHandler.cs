using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using System.Diagnostics;
using StreamHelpers;

namespace ME3Explorer.Packages
{
    public static class MEPackageHandler
    {
        static readonly ConcurrentDictionary<string, IMEPackage> openPackages = new ConcurrentDictionary<string, IMEPackage>();
        public static ObservableCollection<IMEPackage> packagesInTools = new ObservableCollection<IMEPackage>();

        static Func<string, UDKPackage> UDKConstructorDelegate;
        static Func<string, ME1Package> ME1ConstructorDelegate;
        static Func<string, ME2Package> ME2ConstructorDelegate;
        static Func<string, ME3Package> ME3ConstructorDelegate;

        public static void Initialize()
        {
            UDKConstructorDelegate = UDKPackage.Initialize();
            ME1ConstructorDelegate = ME1Package.Initialize();
            ME2ConstructorDelegate = ME2Package.Initialize();
            ME3ConstructorDelegate = ME3Package.Initialize();
        }

        public static IMEPackage OpenMEPackage(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null)
        {
            IMEPackage package;
            pathToFile = Path.GetFullPath(pathToFile); //STANDARDIZE INPUT
            if (!openPackages.ContainsKey(pathToFile))
            {
                ushort version;
                ushort licenseVersion;

                using (FileStream fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(4, SeekOrigin.Begin);
                    version = fs.ReadUInt16();
                    licenseVersion = fs.ReadUInt16();
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
                else if (version == 868 && licenseVersion == 0)
                {
                    //UDK
                    package = UDKConstructorDelegate(pathToFile);
                }
                else
                {
                    throw new FormatException("Not an ME1, ME2, ME3, or UDK package file.");
                }
                package.noLongerUsed += Package_noLongerUsed;
                openPackages.TryAdd(pathToFile, package);
            }
            else
            {
                package = openPackages[pathToFile];
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
            else
            {
                package.RegisterUse();
            }
            return package;
        }

        private static void Package_noLongerUsed(MEPackage sender)
        {
            var package = sender.FileName;
            if (Path.GetFileNameWithoutExtension(package) != "Core") //Keep Core loaded as it is very often referenced
            {
                openPackages.TryRemove(package, out IMEPackage _);
            }
        }

        private static void addToPackagesInTools(IMEPackage package)
        {
            if (!packagesInTools.Contains(package))
            {
                packagesInTools.Add(package);
                package.noLongerOpenInTools += Package_noLongerOpenInTools;
            }
        }

        private static void Package_noLongerOpenInTools(MEPackage sender)
        {
            IMEPackage package = sender as IMEPackage;
            packagesInTools.Remove(package);
            package.noLongerOpenInTools -= Package_noLongerOpenInTools;

        }

        public static ME3Package OpenME3Package(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, wpfWindow, winForm);
            if (pck is ME3Package pcc)
            {
                return pcc;
            }

            pck.Release(wpfWindow, winForm);
            throw new FormatException("Not an ME3 package file.");
        }

        public static ME2Package OpenME2Package(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, wpfWindow, winForm);
            if (pck is ME2Package pcc)
            {
                return pcc;
            }

            pck.Release(wpfWindow, winForm);
            throw new FormatException("Not an ME2 package file.");
        }

        public static ME1Package OpenME1Package(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, wpfWindow, winForm);
            if (pck is ME1Package pcc)
            {
                return pcc;
            }

            pck.Release(wpfWindow, winForm);
            throw new FormatException("Not an ME1 package file.");
        }

        internal static void PrintOpenPackages()
        {
            Debug.WriteLine("Open Packages:");
            foreach (KeyValuePair<string, IMEPackage> package in openPackages)
            {
                Debug.WriteLine(package.Key);
            }
        }
    }
}