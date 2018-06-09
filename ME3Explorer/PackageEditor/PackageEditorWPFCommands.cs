using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ME3Explorer.Commands
{
    public static class PackageEditorWPFCommands
    {
        public static RoutedUICommand FindCommand { get; } = new RoutedUICommand("Find", "FindCommand", typeof(PackageEditorWPF));
        public static RoutedUICommand GotoCommand { get; } = new RoutedUICommand("Goto", "GotoEntryCommand", typeof(PackageEditorWPF));
    }
}
