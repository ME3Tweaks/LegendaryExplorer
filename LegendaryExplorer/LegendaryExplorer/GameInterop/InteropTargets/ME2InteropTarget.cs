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
    public class ME2InteropTarget : InteropTarget
    {
        public override MEGame Game => MEGame.ME2;
        public override bool CanExecuteConsoleCommands => true;
        public override bool CanUpdateTOC => false;

        public string InteropASIName => "ZZZ_LEXInteropME2.asi";
        public override bool CanUseLLE => true;
        public override string OldInteropASIName => "ME3ExplorerInteropME2.asi";
        public override string InteropASIDownloadLink => "https://github.com/ME3Tweaks/ME2-ASI-Plugins/releases/tag/v2.0-LegendaryExplorerInterop";
        public override string InteropASIMD5 => "a65d9325dd3b0ec5ea4184cc10e5e692";

        public override string BinkBypassMD5 => "a5318e756893f6232284202c1196da13";
        public override string OriginalBinkMD5 => "56a99d682e752702604533b2d5055a5e";
        public override InteropModInfo ModInfo { get; } = new InteropModInfo("DLC_MOD_Interop2")
        {
            LiveEditorFilename = "ME2LiveEditor"
        };

        public override string ProcessName =>
            throw new NotImplementedException("ME2 has multiple process names, use TryGetProcess");

        public override uint GameMessageSignature => 0x02AC00C3;

        public override bool TryGetProcess(out Process process)
        {
            process = Process.GetProcessesByName("ME2Game").FirstOrDefault() ?? Process.GetProcessesByName("MassEffect2").FirstOrDefault();
            return process != null && process.MainModule?.FileVersionInfo.ProductMajorPart < 2;
        }

        public override void SelectGamePath()
        {
            OpenFileDialog ofd = new()
            {
                Title = "Select Mass Effect 2 executable",
                Filter = "MassEffect2.exe|MassEffect2.exe"
            };
            if (ofd.ShowDialog() == true)
            {
                string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                Settings.Global_ME2Directory = ME2Directory.DefaultGamePath = gamePath;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}