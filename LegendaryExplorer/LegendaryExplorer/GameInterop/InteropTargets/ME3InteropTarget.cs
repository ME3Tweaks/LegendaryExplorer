using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public class ME3InteropTarget : InteropTarget
    {
        public override MEGame Game => MEGame.ME3;
        public override bool CanExecuteConsoleCommands => true;
        public override bool CanUpdateTOC => true;
        public string InteropASIName => "LEXInteropME3.asi";
        public override string OldInteropASIName => "ME3ExplorerInterop.asi";
        public override bool CanUseLLE => true;

        public override string InteropASIDownloadLink => "https://github.com/ME3Tweaks/ME3-ASI-Plugins/releases/tag/v2.0-LegendaryExplorerInterop";
        public override string InteropASIMD5 => "7ac354e16e62434de656f7eea3259316";
        public override string BinkBypassMD5 => "1acccbdae34e29ca7a50951999ed80d5";
        public override string OriginalBinkMD5 => "128b560ef70e8085c507368da6f26fe6";

        public override InteropModInfo ModInfo { get; } = new InteropModInfo("DLC_MOD_Interop")
        {
            LiveEditorFilename = "ME3LiveEditor",
            CanUseCamPath = true,
            CanUseAnimViewer = true
        };
        public override string ProcessName => "MassEffect3";
        public override uint GameMessageSignature => 0x02AC00C2;
        public override bool TryGetProcess(out Process process)
        {
            process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            return process != null && process.MainModule?.FileVersionInfo.ProductMajorPart < 2;
        }
        public override void SelectGamePath()
        {
            OpenFileDialog ofd = new()
            {
                Title = "Select Mass Effect 3 executable",
                Filter = "MassEffect3.exe|MassEffect3.exe"
            };
            if (ofd.ShowDialog() == true)
            {
                string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName)));

                Settings.Global_ME3Directory = ME3Directory.DefaultGamePath = gamePath;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool SendTOCUpdateMessage()
        {
            if (TryGetProcess(out Process me3Process))
            {
                const uint ME3_TOCUPDATE = 0x8000 + 'T' + 'O' + 'C';
                return GameController.SendTOCMessage(me3Process.MainWindowHandle, ME3_TOCUPDATE);
            }
            return false;
        }
    }
}