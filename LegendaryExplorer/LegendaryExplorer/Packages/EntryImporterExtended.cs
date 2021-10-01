using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Packages
{
    public static class EntryImporterExtended
    {
        public static void ShowRelinkResults(List<EntryStringPair> results)
        {
            Application.Current.Dispatcher.Invoke(() => new ListDialog(results, "Relink report", "The following items failed to relink.", null).Show());
        }

        public static void ShowRelinkResultsIfAny(List<EntryStringPair> results)
        {
            if (results.Any())
                ShowRelinkResults(results);
        }

        public static void ShowRelinkResultsIfAny(RelinkerOptionsPackage rop)
        {
            ShowRelinkResultsIfAny(rop.RelinkReport);
        }
    }
}
