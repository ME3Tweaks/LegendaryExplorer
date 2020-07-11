using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            UDKConstructorDelegate = UDKPackage.RegisterLoader();
            MEConstructorDelegate = MEPackage.RegisterLoader();
        }

        public static IMEPackage OpenMEPackage(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            pathToFile = Path.GetFullPath(pathToFile); //STANDARDIZE INPUT

            IMEPackage package;
            if (forceLoadFromDisk)
            {
                package = LoadPackage(pathToFile);
            }
            else
            {
                package = openPackages.GetOrAdd(pathToFile, LoadPackage);
            }

            IMEPackage LoadPackage(string filePath)
            {
                ushort version = 0;
                ushort licenseVersion = 0;
                bool fullyCompressed = false;
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    EndianReader er = new EndianReader(fs);
                    if (fs.ReadUInt32() == UnrealPackageFile.packageTagBigEndian) er.Endian = Endian.Big;

                    // This is stored as integer by cooker as it is flipped by size word in big endian
                    uint versionLicenseePacked = er.ReadUInt32();
                    if (versionLicenseePacked == 0x00020000 && er.Endian == Endian.Little)
                    {
                        //block size - this is a fully compressed file. we must decompress it
                        //for some reason fully compressed files use a little endian package tag
                        var usfile = filePath + ".us";
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

                IMEPackage pkg;
                if (fullyCompressed ||
                    (version == MEPackage.ME3UnrealVersion && (licenseVersion == MEPackage.ME3LicenseeVersion || licenseVersion == MEPackage.ME3Xenon2011DemoLicenseeVersion)) ||
                    version == MEPackage.ME3WiiUUnrealVersion && licenseVersion == MEPackage.ME3LicenseeVersion ||
                    version == MEPackage.ME2UnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                    version == MEPackage.ME2PS3UnrealVersion && licenseVersion == MEPackage.ME2PS3LicenseeVersion ||
                    version == MEPackage.ME2DemoUnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                    version == MEPackage.ME1UnrealVersion && licenseVersion == MEPackage.ME1LicenseeVersion ||
                    version == MEPackage.ME1PS3UnrealVersion && licenseVersion == MEPackage.ME1PS3LicenseeVersion)
                {
                    pkg = MEConstructorDelegate(filePath, MEGame.Unknown);
                    ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"MEPackage {Path.GetFileName(filePath)}", new WeakReference(pkg));
                }
                else if (version == 868 && licenseVersion == 0)
                {
                    //UDK
                    pkg = UDKConstructorDelegate(filePath, false);
                    ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"UDKPackage {Path.GetFileName(filePath)}", new WeakReference(pkg));
                }
                else
                {
                    throw new FormatException("Not an ME1, ME2, ME3, or UDK package file.");
                }

                if (!forceLoadFromDisk)
                {
                    pkg.noLongerUsed += Package_noLongerUsed;
                }

                return pkg;
            }

            if (user != null)
            {
                package.RegisterTool(user);
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

        public static IMEPackage OpenUDKPackage(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.UDK)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not a UDK package file.");
        }

        public static IMEPackage OpenME3Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.ME3)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not an ME3 package file.");
        }

        public static IMEPackage OpenME2Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.ME2)
            {
                return pck;
            }

            pck.Release(user);
            throw new FormatException("Not an ME2 package file.");
        }

        public static IMEPackage OpenME1Package(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            IMEPackage pck = OpenMEPackage(pathToFile, user, forceLoadFromDisk);
            if (pck.Game == MEGame.ME1)
            {
                return pck;
            }

            pck.Release(user);
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