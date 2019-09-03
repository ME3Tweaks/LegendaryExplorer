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
        static Func<string, MEGame, MEPackage> MEConstructorDelegate;

        public static void Initialize()
        {
            UDKConstructorDelegate = UDKPackage.Initialize();
            MEConstructorDelegate = MEPackage.Initialize();
        }

        public static IMEPackage OpenMEPackage(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null, bool forceLoadFromDisk = false)
        {
            IMEPackage package;
            pathToFile = Path.GetFullPath(pathToFile); //STANDARDIZE INPUT
            if (forceLoadFromDisk || !openPackages.ContainsKey(pathToFile))
            {
                ushort version;
                ushort licenseVersion;

                using (FileStream fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
                {
                    fs.Seek(4, SeekOrigin.Begin);
                    version = fs.ReadUInt16();
                    licenseVersion = fs.ReadUInt16();
                }


                if (version == 684 && licenseVersion == 194 ||
                    version == 512 && licenseVersion == 130 ||
                    version == 491 && licenseVersion == 1008)
                {
                    package = MEConstructorDelegate(pathToFile, MEGame.Unknown);
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

                if (!forceLoadFromDisk)
                {
                    package.noLongerUsed += Package_noLongerUsed;
                    openPackages.TryAdd(pathToFile, package);
                }
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

        public static void CreateAndSaveMePackage(string path, MEGame game)
        {
            MEConstructorDelegate(path, game).save();
        }

        private static void Package_noLongerUsed(UnrealPackageFile sender)
        {
            var packagePath = sender.FilePath;
            if (Path.GetFileNameWithoutExtension(packagePath) != "Core") //Keep Core loaded as it is very often referenced
            {
                openPackages.TryRemove(packagePath, out IMEPackage _);
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

        private static void Package_noLongerOpenInTools(UnrealPackageFile sender)
        {
            IMEPackage package = sender as IMEPackage;
            packagesInTools.Remove(package);
            sender.noLongerOpenInTools -= Package_noLongerOpenInTools;

        }

        public static IMEPackage OpenME3Package(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, wpfWindow, winForm, forceLoadFromDisk);
            if (pck.Game == MEGame.ME3)
            {
                return pck;
            }

            pck.Release(wpfWindow, winForm);
            throw new FormatException("Not an ME3 package file.");
        }

        public static IMEPackage OpenME2Package(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, wpfWindow, winForm, forceLoadFromDisk);
            if (pck.Game == MEGame.ME2)
            {
                return pck;
            }

            pck.Release(wpfWindow, winForm);
            throw new FormatException("Not an ME2 package file.");
        }

        public static IMEPackage OpenME1Package(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, wpfWindow, winForm, forceLoadFromDisk);
            if (pck.Game == MEGame.ME1)
            {
                return pck;
            }

            pck.Release(wpfWindow, winForm);
            throw new FormatException("Not an ME1 package file.");
        }

        public static bool IsPackageInUse(string pathToFile) => openPackages.ContainsKey(Path.GetFullPath(pathToFile));

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