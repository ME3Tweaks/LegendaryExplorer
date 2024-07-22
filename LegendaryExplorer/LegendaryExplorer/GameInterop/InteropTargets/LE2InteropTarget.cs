﻿using System;
using System.IO;
using System.Windows.Input;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.Packages;
using Microsoft.Win32;

namespace LegendaryExplorer.GameInterop.InteropTargets
{
    public class LE2InteropTarget : InteropTarget
    {
        public override MEGame Game => MEGame.LE2;
        public override bool CanExecuteConsoleCommands => true;
        public override bool CanUpdateTOC => false;
        public override bool CanUseLLE => true;

        public override string InteropASIDownloadLink => "https://github.com/ME3Tweaks/LE2-ASI-Plugins/releases/tag/LE2LEXInterop-v7";
        public override string InteropASIMD5 => "7ae5b617655dd75517804b43cd3c8019";
        public override string BinkBypassMD5 { get; }
        public override string OriginalBinkMD5 => "1f00452ad61a944556399e2ad5292b35";

        public override InteropModInfo ModInfo => throw new NotImplementedException();
        public override string ProcessName => "MassEffect2";
        public override uint GameMessageSignature => 0x02AC00C6;
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