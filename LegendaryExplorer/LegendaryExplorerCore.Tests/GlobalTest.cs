using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Tests
{
    public static class GlobalTest
    {
        private static bool initialized;
        /// <summary>
        /// Initializes the game paths for use in a testing environment
        /// </summary>
        public static void Init()
        {
            if (initialized) return;
            var sc = new SynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sc);
            LegendaryExplorerCoreLib.InitLib(TaskScheduler.FromCurrentSynchronizationContext(), x => { });

            //Tests should always use the testing data, not the users local installs of the games
            ME1Directory.DefaultGamePath = GetTestMiniGamePath(MEGame.ME1);
            ME2Directory.DefaultGamePath = GetTestMiniGamePath(MEGame.ME2);
            ME3Directory.DefaultGamePath = GetTestMiniGamePath(MEGame.ME3);
            LE1Directory.DefaultGamePath = GetTestMiniGamePath(MEGame.LE1);
            LE2Directory.DefaultGamePath = GetTestMiniGamePath(MEGame.LE2);
            LE3Directory.DefaultGamePath = GetTestMiniGamePath(MEGame.LE3);

            initialized = true;
        }

        private static string testDataDirectory;
        /// <summary>
        /// Looks in parent folders for folder containing a folder named "testdata" as Azure DevOps seems to build project differently than on a VS installation
        /// </summary>
        /// <returns></returns>
        public static string GetTestDataDirectory()
        {
            if (testDataDirectory is not null)
            {
                return testDataDirectory;
            }
            var dir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            while (Directory.GetParent(dir.FullName) != null)
            {
                dir = Directory.GetParent(dir.FullName);
                var testDataPath = Path.Combine(dir.FullName, "Testing");
                if (Directory.Exists(testDataPath))
                {
                    return testDataDirectory = Path.Combine(testDataPath, "testdata");
                }
            }

            throw new Exception("Could not find testdata directory!");
        }

        public static string GetTestMiniGamePath(MEGame game) => Path.Combine(GetTestDataDirectory(), "packages", "PC", game.ToString());

        public static string GetTestPackagesDirectory() => Path.Combine(GetTestDataDirectory(), "packages");
        public static string GetTestSFARsDirectory() => Path.Combine(GetTestDataDirectory(), "sfars");
        public static string GetTestMountsDirectory() => Path.Combine(GetTestDataDirectory(), "mounts");
        public static string GetTestTLKDirectory() => Path.Combine(GetTestDataDirectory(), "tlk");
        public static string GetTestDataMiscDirectory() => Path.Combine(GetTestDataDirectory(), "misc");
        public static string GetTestTexturesDirectory() => Path.Combine(GetTestDataDirectory(), "textures");
        public static string GetTestCoalescedDirectory() => Path.Combine(GetTestDataDirectory(), "coalesced");
        public static string GetTestISBDirectory() => Path.Combine(GetTestDataDirectory(), "isb");
        public static string GetLocalProfileDirectory() => Path.Combine(GetTestDataDirectory(), "localprofile");

        /// <summary>
        /// Gets the expected game for a file based on the name of the containing directory. It be an MEGame Enum.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static MEGame GetExpectedGame(string filePath)
        {
            var name = Directory.GetParent(filePath).Name;
            if (Enum.TryParse<MEGame>(name, out var val))
            {
                return val;
            }

            return MEGame.Unknown;
        }

        public static (MEGame expectedGame, MEPackage.GamePlatform expectedPlatform) GetExpectedTypes(string p)
        {
            MEPackage.GamePlatform expectedPlatform = MEPackage.GamePlatform.Unknown;
            MEGame expectedGame = MEGame.Unknown;

            string parentname = Directory.GetParent(p).FullName;
            int level = 0;
            while (parentname != null)
            {
                var dirname = Path.GetFileName(parentname);
                if (dirname == "demo" || dirname.CaseInsensitiveEquals("BioGame") || dirname.Contains("CookedPC", StringComparison.OrdinalIgnoreCase))
                {
                    parentname = Directory.GetParent(parentname).FullName;
                    continue;
                }

                if (level == 0)
                {
                    expectedGame = Enum.Parse<MEGame>(dirname);
                }
                else if (level == 1)
                {
                    expectedPlatform = Enum.Parse<MEPackage.GamePlatform>(dirname);
                }
                else
                {
                    break;
                }

                parentname = Directory.GetParent(parentname).FullName;
                level++;
            }

            return (expectedGame, expectedPlatform);
        }
    }
}
