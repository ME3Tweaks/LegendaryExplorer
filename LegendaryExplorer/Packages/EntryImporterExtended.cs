using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using LegendaryExplorer.Dialogs;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Packages
{
    public static class EntryImporterExtended
    {
        public static void ShowRelinkResults(List<EntryStringPair> results)
        {
            Application.Current.Dispatcher.Invoke(() => new ListDialog(results, "Relink report", "The following items failed to relink.", null).Show());
        }
    }
}
