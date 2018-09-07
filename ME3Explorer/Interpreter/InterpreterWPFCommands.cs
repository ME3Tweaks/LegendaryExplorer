using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ME3Explorer.Commands
{
    public static class InterpreterWPFCommands
    {
        public static RoutedUICommand RemovePropertyCommand { get; } = new RoutedUICommand("RemoveProperty", "RemovePropertyCommand", typeof(InterpreterWPF));
        public static RoutedUICommand ArrayOrderByValueCommand { get; } = new RoutedUICommand("ArrayOrderByValue", "ArrayOrderByValueCommand", typeof(InterpreterWPF));

        
        // public static RoutedUICommand GotoCommand { get; } = new RoutedUICommand("Goto", "GotoEntryCommand", typeof(PackageEditorWPF));
        //public static RoutedUICommand ComparePackageCommand { get; } = new RoutedUICommand("ComparePackage", "ComparePackageCommand", typeof(PackageEditorWPF));

        //public static RoutedUICommand CustCommand1 { get; } = new RoutedUICommand("CMD", "CMDCMD", typeof(PackageEditorWPF));

    }
}
