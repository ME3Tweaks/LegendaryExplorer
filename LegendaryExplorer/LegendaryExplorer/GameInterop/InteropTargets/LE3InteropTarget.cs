using System.IO;
using System.Windows.Input;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public class LE3InteropTarget : InteropTarget
    {
        public override MEGame Game => MEGame.LE1;
        public override bool CanExecuteConsoleCommands => true;
        public override bool CanUpdateTOC => false;
        public override bool CanUseLLE => true;

        // THIS ALL NEEDS UPDATED
        public override string InteropASIDownloadLink => "https://github.com/ME3Tweaks/LE3-ASI-Plugins/releases/tag/LE3LEXInterop-v7";
        public override string InteropASIMD5 => "1be200877c78f129ee7ac6ce7c4f521d";
        public override string BinkBypassMD5 { get; }
        public override string OriginalBinkMD5 => "1f00452ad61a944556399e2ad5292b35";

        public override InteropModInfo ModInfo { get; } = new("DLC_MOD_InteropLE3")
        {
            LiveEditorFilename = "LE3LiveEditor"
        };
        public override string ProcessName => "MassEffect3";
        public override uint GameMessageSignature => 0x02AC00C5;
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