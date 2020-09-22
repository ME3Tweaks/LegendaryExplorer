using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ME3ExplorerCore.Tests
{
    public static class GlobalTest
    {
        private static bool initialized;

        public static void Init()
        {
            if (initialized) return;
            var sc = new SynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(sc);
            CoreLib.InitLib(TaskScheduler.FromCurrentSynchronizationContext(), x => { });
            initialized = true;
        }
        /// <summary>
        /// Looks in parent folders for folder containing a folder named "testdata" as Azure DevOps seems to build project differently than on a VS installation
        /// </summary>
        /// <returns></returns>
        public static string GetTestDataDirectory()
        {
            var dir = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
            while (Directory.GetParent(dir.FullName) != null)
            {
                dir = Directory.GetParent(dir.FullName);
                var testDataPath = Path.Combine(dir.FullName, "testdata");
                if (Directory.Exists(testDataPath)) return testDataPath;
            }

            throw new Exception("Could not find testdata directory!");
        }


        public static string GetTestPackagesDirectory() => Path.Combine(GetTestDataDirectory(), "packages");

    }
}
