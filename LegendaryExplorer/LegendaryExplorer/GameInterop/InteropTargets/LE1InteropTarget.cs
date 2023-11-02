using System.IO;
using System.Windows.Input;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public class LE1InteropTarget : InteropTarget
    {
        public override MEGame Game => MEGame.LE1;
        public override bool CanExecuteConsoleCommands => true;
        public override bool CanUpdateTOC => false;
        public override bool CanUseLLE => true;

        public override string InteropASIDownloadLink =>
            "https://github.com/ME3Tweaks/LE1-ASI-Plugins/releases/tag/LE1LEXInterop-v7";
        public override string InteropASIMD5 => "473f0076a3d8a45b4c5d3e4501fa1c2a";
        public override string BinkBypassMD5 { get; }
        public override string OriginalBinkMD5 => "1f00452ad61a944556399e2ad5292b35";



        public override InteropModInfo ModInfo { get; } = new("DLC_MOD_InteropLE1")
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