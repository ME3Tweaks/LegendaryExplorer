/**
 * This file is taken from ALOT Installer
 * and has been modified for use with
 * ME3Explorer - ME3Tweaks Fork
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SlavaGu.ConsoleAppLauncher;

namespace MassEffectModder
{
    public class MassEffectModderIPCWrapper
    {

        /// <summary>
        /// Process handler for MEM in install mode. This should not be called directly except by RunAndTimeMEM.
        /// </summary>
        /// <param name="exe"></param>
        /// <param name="args"></param>
        /// <param name="worker"></param>
        /// <param name="acceptedIPC"></param>
        public static ConsoleApp RunMEM(string args, Dictionary<string, Action<string>> IPCTriggers)
        {
            string exe = @"C:\Users\Mgame\Desktop\MassEffectModderNoGui.exe";
            Debug.WriteLine("Running process: " + exe + " " + args);
            //Log.Information("Running process: " + exe + " " + args);


            var BACKGROUND_MEM_PROCESS = new ConsoleApp(exe, args);
            //BACKGROUND_MEM_PROCESS_ERRORS = new List<string>();
            //BACKGROUND_MEM_PROCESS_PARSED_ERRORS = new List<string>();
            BACKGROUND_MEM_PROCESS.ConsoleOutput += (o, args2) =>
            {
                string str = args2.Line;
                //if (DEBUG_LOGGING)
                //{
                //    Utilities.WriteDebugLog(str);
                //}
                if (str.StartsWith("[IPC]", StringComparison.Ordinal)) //needs culture ordinal check??
                {
                    string command = str.Substring(5);
                    int endOfCommand = command.IndexOf(' ');
                    if (endOfCommand > 0)
                    {
                        command = command.Substring(0, endOfCommand);
                    }

                    if (IPCTriggers.ContainsKey(command))
                    {
                        string param = str.Substring(endOfCommand + 5).Trim();
                        IPCTriggers[command].Invoke(param);
                    }
                }
                else
                {
                    if (str.Trim() != "")
                    {
                        if (str.StartsWith("Exception occured") ||
                            str.StartsWith("Program crashed"))
                        {
                            //Log.Error("MEM process output: " + str);
                        }
                        else
                        {
                            //Log.Information("MEM process output: " + str);
                        }
                    }
                }
            };
            BACKGROUND_MEM_PROCESS.Run();
            return BACKGROUND_MEM_PROCESS;
        }
    }
}
