using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Gammtek.Conduit.IO;
using StreamHelpers;

namespace ME3Explorer.Packages
{
    public static class MEPackageHandler
    {
        static readonly ConcurrentDictionary<string, IMEPackage> openPackages = new ConcurrentDictionary<string, IMEPackage>();
        public static ObservableCollection<IMEPackage> packagesInTools = new ObservableCollection<IMEPackage>();

        static Func<string, bool, UDKPackage> UDKConstructorDelegate;
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
                ushort version = 0;
                ushort licenseVersion = 0;
                uint versionLicenseePacked;
                bool fullyCompressed = false;
                using (FileStream fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
                {
                    EndianReader er = new EndianReader(fs);
                    if (fs.ReadUInt32() == MEPackage.packageTagBigEndian) er.Endian = Endian.Big;

                    // This is stored as integer by cooker as it is flipped by size word in big endian
                    versionLicenseePacked = er.ReadUInt32();
                    if (versionLicenseePacked == 0x00020000 && er.Endian == Endian.Little)
                    {
                        //block size - this is a fully compressed file. we must decompress it
                        //for some reason fully compressed files use a little endian package tag
                        var usfile = pathToFile + ".us";
                        if (File.Exists(usfile))
                        {
                            fullyCompressed = true;
                        }
                    }

                    if (!fullyCompressed)
                    {
                        version = (ushort)(versionLicenseePacked & 0xFFFF);
                        licenseVersion = (ushort)(versionLicenseePacked >> 16);
                    }
                }


                if (fullyCompressed ||
                    (version == MEPackage.ME3UnrealVersion && (licenseVersion == MEPackage.ME3LicenseeVersion || licenseVersion == MEPackage.ME3Xenon2011DemoLicenseeVersion)) ||
                    version == MEPackage.ME3WiiUUnrealVersion && licenseVersion == MEPackage.ME3LicenseeVersion ||
                    version == MEPackage.ME2UnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                    version == MEPackage.ME2DemoUnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                    version == MEPackage.ME1UnrealVersion && licenseVersion == MEPackage.ME1LicenseeVersion ||
                    version == MEPackage.ME1PS3UnrealVersion && licenseVersion == MEPackage.ME1PS3LicenseeVersion)
                {
                    package = MEConstructorDelegate(pathToFile, MEGame.Unknown);
                }
                else if (version == 868 && licenseVersion == 0)
                {
                    //UDK
                    package = UDKConstructorDelegate(pathToFile, false);
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

        public static void CreateAndSavePackage(string path, MEGame game)
        {
            switch (game)
            {
                case MEGame.UDK:
                    UDKConstructorDelegate(path, true).Save();
                    break;
                default:
                    MEConstructorDelegate(path, game).Save();
                    break;
            }
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

        public static IMEPackage OpenUDKPackage(string pathToFile, WPFBase wpfWindow = null, WinFormsBase winForm = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, wpfWindow, winForm, forceLoadFromDisk);
            if (pck.Game == MEGame.UDK)
            {
                return pck;
            }

            pck.Release(wpfWindow, winForm);
            throw new FormatException("Not a UDK package file.");
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

        public static bool TryOpenInExisting<T>(string filePath, out T tool) where T : WPFBase
        {
            foreach (IMEPackage pcc in packagesInTools)
            {
                if (pcc.FilePath == filePath)
                {
                    foreach (GenericWindow window in pcc.Tools)
                    {
                        if (window.tool.type == typeof(T))
                        {
                            tool = (T)window.WPF;
                            tool.RestoreAndBringToFront();
                            return true;
                        }
                    }
                }
            }
            tool = null;
            return false;
        }
    }
}