using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ME3Explorer.Commands
{
    public static class SoundplorerWPFCommands
    {
        public static RoutedUICommand OpenInWwiseBankEditor { get; } = new RoutedUICommand("OpenInWwiseBankEditor", "OpenInWwiseBankEditorCommand", typeof(WwiseBankEditor.WwiseEditor));
    }
}
