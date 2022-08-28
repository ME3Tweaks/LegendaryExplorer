using System;
using System.IO;
using System.Windows.Input;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public class LE1InteropTarget : InteropTarget
    {
        public override MEGame Game => MEGame.LE1;
        public override bool CanExecuteConsoleCommands => true;
        public override bool CanUpdateTOC => false;
        public override string InteropASIName => "LEXInteropLE1.asi";

        public override string InteropASIDownloadLink =>
            "https://github.com/ME3Tweaks/LE1-ASI-Plugins/releases/tag/LE1LEXInterop-v2.0";
        public override string InteropASIMD5 => "bda0e9d2ed2ca0b5be0fc962cdda5eb1";
        public override string BinkBypassMD5 { get; }
        public override string OriginalBinkMD5 => "1f00452ad61a944556399e2ad5292b35";

        public override InteropModInfo ModInfo { get; } = new InteropModInfo("DLC_MOD_InteropLE1", true)
        {
            LiveEditorFilename = "LE1LiveEditor",
            CanUseAnimViewer = true
        };
        public override string ProcessName => "MassEffect1";
        public override uint GameMessageSignature => 0x02AC00C7;
        public override void SelectGamePath()
        {
            OpenFileDialog ofd = new()
            {
                Title = "Select Mass Effect LE Launcher executable",
                Filter = "MassEffectLauncher.exe|MassEffectLauncher.exe"
            };
            if (ofd.ShowDialog() == true)
            {
                string gamePath = Path.GetDirectoryName(Path.GetDirectoryName(ofd.FileName));

                Settings.Global_LEDirectory = gamePath;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}