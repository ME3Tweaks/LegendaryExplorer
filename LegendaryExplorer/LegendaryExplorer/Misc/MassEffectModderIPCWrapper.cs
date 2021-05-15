/**
 * This file is taken from ALOT Installer
 * and has been modified for use with
 * ME3Explorer - ME3Tweaks Fork
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LegendaryExplorer.Misc;
using SlavaGu.ConsoleAppLauncher;

namespace LegendaryExplorer.Misc
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
        public static ConsoleApp RunMEM(string args, Dictionary<string, Action<string>> IPCTriggers = null, Dictionary<string, Action<string>> NonIPCTriggers = null, bool cSharpVersion = false)
        {
            string exe = Path.Combine(AppDirectories.AppDataFolder, "staticexecutables", cSharpVersion ? "MassEffectModderNoGuiCS.exe" : "MassEffectModderNoGuiQT.exe");
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
                if (IPCTriggers != null && str.StartsWith("[IPC]", StringComparison.Ordinal)) //needs culture ordinal check??
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

                    return; //Do not allow nonIPC to try to handle an IPC command.
                }

                if (NonIPCTriggers != null && !string.IsNullOrWhiteSpace(str))
                {
                    string command = str.Substring(0, str.IndexOf(' '));
                    if (NonIPCTriggers.ContainsKey(command))
                    {
                        string param = str.Substring(command.Length + 1).Trim();
                        NonIPCTriggers[command].Invoke(param);
                    }
                }
            };
            BACKGROUND_MEM_PROCESS.Run();
            return BACKGROUND_MEM_PROCESS;
        }
    }
}
