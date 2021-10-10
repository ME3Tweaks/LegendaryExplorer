using System;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public class LE1InteropTarget : InteropTarget
    {
        public override MEGame Game => MEGame.LE1;
        public override bool CanExecuteConsoleCommands => true;
        public override bool CanUpdateTOC => false;
        public override string InteropASIName => "ZZZ_LEXInteropLE1.asi";
        public override string InteropASIDownloadLink { get; }
        public override string InteropASIMD5 => "8a021214ec99870e689a51dfa69ba8f6";
        public override string BinkBypassMD5 { get; }
        public override string OriginalBinkMD5 => "1f00452ad61a944556399e2ad5292b35";

        public override InteropModInfo ModInfo { get; } = new InteropModInfo("DLC_MOD_InteropLE1", true)
        {
            LiveEditorFilename = "LE1LiveEditor"
        };
        public override string ProcessName => "MassEffect1";
        public override uint GameMessageSignature => 0x02AC00C7;
        public override void SelectGamePath()
        {
            throw new NotImplementedException();
        }
    }
}