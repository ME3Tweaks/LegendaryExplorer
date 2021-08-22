using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlavaGu.ConsoleAppLauncher;

namespace LegendaryExplorer.Misc
{
    public static class UModelHelper
    {
        public const int SupportedUModelBuildNum = 1555;
        public static int GetLocalUModelVersion()
        {
            int version = 0;
            var umodel = Path.Combine(AppDirectories.StaticExecutablesDirectory, @"umodel", @"umodel.exe");
            if (File.Exists(umodel))
            {
                try
                {
                    var umodelProc = new ConsoleApp(umodel, @"-version");
                    umodelProc.ConsoleOutput += (o, args2) =>
                    {
                        if (version != 0)
                            return; // don't care

                        string str = args2.Line;
                        if (str != null)
                        {
                            if (str.StartsWith("Compiled "))
                            {
                                var buildNum = str.Substring(str.LastIndexOf(" ", StringComparison.InvariantCultureIgnoreCase) + 1);
                                buildNum = buildNum.Substring(0, buildNum.IndexOf(")", StringComparison.InvariantCultureIgnoreCase)); // This is just in case build num changes drastically for some reason

                                if (int.TryParse(buildNum, out var parsedBuildNum))
                                {
                                    version = parsedBuildNum;
                                }
                            }

                        }
                    };
                    umodelProc.Run();
                    umodelProc.WaitForExit();
                }
                catch
                {
                    // ?
                    // This can happen if image has issues like incomplete exe 
                }

            }

            return version; // not found
        }
    }
}
