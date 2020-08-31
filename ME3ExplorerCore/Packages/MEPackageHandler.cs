using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;

namespace ME3ExplorerCore.Packages
{
    public static class MEPackageHandler
    {
        static readonly ConcurrentDictionary<string, IMEPackage> openPackages = new ConcurrentDictionary<string, IMEPackage>(StringComparer.OrdinalIgnoreCase);
        public static ObservableCollection<IMEPackage> packagesInTools = new ObservableCollection<IMEPackage>();

        static Func<string, bool, UDKPackage> UDKConstructorDelegate;
        static Func<string, MEGame, MEPackage> MEConstructorDelegate;
        static Func<Stream, string, MEPackage> MEStreamConstructorDelegate;

        public static void Initialize()
        {
            UDKConstructorDelegate = UDKPackage.RegisterLoader();
            MEConstructorDelegate = MEPackage.RegisterLoader();
            MEStreamConstructorDelegate = MEPackage.RegisterStreamLoader();
        }

        public static IMEPackage OpenMEPackageFromStream(Stream inStream, string associatedFilePath = null)
        {
            IMEPackage package;
            package = LoadPackage(inStream);
            IMEPackage LoadPackage(Stream stream, string filePath = null)
            {
                ushort version = 0;
                ushort licenseVersion = 0;
                bool fullyCompressed = false;

                EndianReader er = new EndianReader(stream);
                if (stream.ReadUInt32() == UnrealPackageFile.packageTagBigEndian) er.Endian = Endian.Big;

                // This is stored as integer by cooker as it is flipped by size word in big endian
                uint versionLicenseePacked = er.ReadUInt32();
                // don't check for fully compressed. We don't support it from stream

                if (!fullyCompressed)
                {
                    version = (ushort)(versionLicenseePacked & 0xFFFF);
                    licenseVersion = (ushort)(versionLicenseePacked >> 16);
                }



                IMEPackage pkg;
                if ((version == MEPackage.ME3UnrealVersion && (licenseVersion == MEPackage.ME3LicenseeVersion || licenseVersion == MEPackage.ME3Xenon2011DemoLicenseeVersion)) ||
                    version == MEPackage.ME3WiiUUnrealVersion && licenseVersion == MEPackage.ME3LicenseeVersion ||
                    version == MEPackage.ME2UnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                    version == MEPackage.ME2PS3UnrealVersion && licenseVersion == MEPackage.ME2PS3LicenseeVersion ||
                    version == MEPackage.ME2DemoUnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                    version == MEPackage.ME1UnrealVersion && licenseVersion == MEPackage.ME1LicenseeVersion ||
                    version == MEPackage.ME1PS3UnrealVersion && licenseVersion == MEPackage.ME1PS3LicenseeVersion)
                {
                    stream.Position -= 8; //reset to where we started for delegate
                    pkg = MEStreamConstructorDelegate(stream, filePath);
                    // Todo
                    //ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("MEPackage (Stream)", new WeakReference(pkg));
                }
                else if (version == 868 && licenseVersion == 0)
                {
                    //UDK
                    throw new Exception("Cannot load UDK packages from streams at this time.");
                    //pkg = UDKConstructorDelegate(filePath, false);
                    //ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("UDKPackage (Stream)", new WeakReference(pkg));
                }
                else
                {
                    throw new FormatException("Not an ME1, ME2, ME3, or UDK package stream.");
                }

                return pkg;
            }

            // is this useful for stream versions since they can't be shared?
            // package.RegisterUse();
            return package;
        }
        public static IMEPackage OpenMEPackage(string pathToFile, IPackageUser user = null, bool forceLoadFromDisk = false)
        {
            pathToFile = Path.GetFullPath(pathToFile); //STANDARDIZE INPUT

            IMEPackage package;
            if (forceLoadFromDisk)
            {
                using (FileStream fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
                {
                    package = LoadPackage(fs, pathToFile);
                }
            }
            else
            {
                package = openPackages.GetOrAdd(pathToFile, fpath =>
                {
                    using (FileStream fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read))
                    {
                        return LoadPackage(fs, fpath);
                    }
                });
            }

            IMEPackage LoadPackage(Stream stream, string filePath = null)
            {
                ushort version = 0;
                ushort licenseVersion = 0;
                bool fullyCompressed = false;

                EndianReader er = new EndianReader(stream);
                if (stream.ReadUInt32() == UnrealPackageFile.packageTagBigEndian) er.Endian = Endian.Big;

                // This is stored as integer by cooker as it is flipped by size word in big endian
                uint versionLicenseePacked = er.ReadUInt32();
                if ((versionLicenseePacked == 0x00020000 || versionLicenseePacked == 0x00010000) && er.Endian == Endian.Little && filePath != null) //can only load fully compressed packages from disk since we won't know what the .us files has
                {
                    //block size - this is a fully compressed file. we must decompress it
                    //for some reason fully compressed files use a little endian package tag
                    var usfile = filePath + ".us";
                    if (File.Exists(usfile))
                    {
                        fullyCompressed = true;
                    }
                    else if (File.Exists(filePath + ".UNCOMPRESSED_SIZE"))
                    {
                        fullyCompressed = true;
                    }
                }

                if (!fullyCompressed)
                {
                    version = (ushort)(versionLicenseePacked & 0xFFFF);
                    licenseVersion = (ushort)(versionLicenseePacked >> 16);
                }


                IMEPackage pkg;
                if (fullyCompressed ||
                    (version == MEPackage.ME3UnrealVersion && (licenseVersion == MEPackage.ME3LicenseeVersion || licenseVersion == MEPackage.ME3Xenon2011DemoLicenseeVersion)) ||
                    version == MEPackage.ME3WiiUUnrealVersion && licenseVersion == MEPackage.ME3LicenseeVersion ||
                    version == MEPackage.ME2UnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                    version == MEPackage.ME2PS3UnrealVersion && licenseVersion == MEPackage.ME2PS3LicenseeVersion ||
                    version == MEPackage.ME2DemoUnrealVersion && licenseVersion == MEPackage.ME2LicenseeVersion ||
                    version == MEPackage.ME1UnrealVersion && licenseVersion == MEPackage.ME1LicenseeVersion ||
                    version == MEPackage.ME1PS3UnrealVersion && licenseVersion == MEPackage.ME1PS3LicenseeVersion ||
                    version == MEPackage.ME1XboxUnrealVersion && licenseVersion == MEPackage.ME1XboxLicenseeVersion)
                {
                    pkg = MEConstructorDelegate(filePath, MEGame.Unknown);
                    // TODO
                    //ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"MEPackage {Path.GetFileName(filePath)}", new WeakReference(pkg));
                }
                else if (version == 868 && licenseVersion == 0)
                {
                    //UDK
                    pkg = UDKConstructorDelegate(filePath, false);
                    // TODO
                    //ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem($"UDKPackage {Path.GetFileName(filePath)}", new WeakReference(pkg));
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

        //useful for scanning operations, where a common set of packages are going to be referenced repeatedly
        public static DisposableCollection<IMEPackage> OpenMEPackages(IEnumerable<string> filePaths)
        {
            return new DisposableCollection<IMEPackage>(filePaths.Select(filePath => OpenMEPackage(filePath)));
        }
    }

    public class DisposableCollection<T> : List<T>, IDisposable where T : IDisposable
    {
        public DisposableCollection() : base() { }
        public DisposableCollection(IEnumerable<T> collection) : base(collection) { }
        public DisposableCollection(int capacity) : base(capacity) { }

        public void Dispose()
        {
            foreach (T disposable in this)
            {
                disposable?.Dispose();
            }
        }
    }
}