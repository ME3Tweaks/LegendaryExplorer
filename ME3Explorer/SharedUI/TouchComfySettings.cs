using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.SharedUI
{
    public class TouchComfySettings
    {
        /// <summary>
        /// A static property which you'd like to bind to
        /// </summary>
        public static int TreeViewMargin => Properties.Settings.Default.TouchComfyMode ? 5 : 2;
        public static int InterpreterWPFNodeMargin => Properties.Settings.Default.TouchComfyMode ? 3 : 1;

        private static readonly PropertyChangedEventArgs TreeViewMarginEventArgs = new PropertyChangedEventArgs(nameof(TreeViewMargin));
        private static readonly PropertyChangedEventArgs InterpreterWPFNodeMarginEventArgs = new PropertyChangedEventArgs(nameof(InterpreterWPFNodeMargin));
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        static TouchComfySettings()
        {
            // Set up an empty event handler
            StaticPropertyChanged += (sender, e) => { return; };
        }

        internal static void ModeSwitched()
        {
            StaticPropertyChanged?.Invoke(null, TreeViewMarginEventArgs);
            StaticPropertyChanged?.Invoke(null, InterpreterWPFNodeMarginEventArgs);
        }
    }
}
