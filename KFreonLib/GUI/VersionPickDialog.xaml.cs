using KFreonLib.MEDirectories;
using KFreonLib.Misc;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;


namespace KFreonLib.GUI
{
    public partial class VersionPickDialog : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        CustomMessageBoxResult _result;

        private VersionPickDialog()
        {
            InitializeComponent();
        }

        public static CustomMessageBoxResult ShowDialog(Form owner, string caption, string message)
        {
            VersionPickDialog messageBox = new VersionPickDialog();
            var helper = new WindowInteropHelper(messageBox);
            helper.Owner = owner.Handle;
            return messageBox.ShowDialogInternal(caption, message);
        }

        public static int AskForGameVersion(Form owner, string title = "Game Version", string message = "Choose which Mass Effect game to work with:")
        {
            return (int)VersionPickDialog.ShowDialog(owner, title, message);
        }

        public CustomMessageBoxResult ShowDialogInternal(string caption, string message)
        {
            Title = caption;
            messageBlock.Text = message;

            me1button.Content = "ME1";
            if (ME1Directory.gamePath == null)
                me1button.IsEnabled = false;
            me2button.Content = "ME2";
            if (ME2Directory.gamePath == null)
                me2button.IsEnabled = false;
            me3button.Content = "ME3";
            if (ME3Directory.gamePath == null)
                me3button.IsEnabled = false;

            ShowDialog();
            return _result;
        }

        private void me1button_Click(object sender, RoutedEventArgs e)
        {
            _result = CustomMessageBoxResult.ME1;
            Close();
        }

        private void me2button_Click(object sender, RoutedEventArgs e)
        {
            _result = CustomMessageBoxResult.ME2;
            Close();
        }

        private void me3button_Click(object sender, RoutedEventArgs e)
        {
            _result = CustomMessageBoxResult.ME3;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }
    }

    public enum CustomMessageBoxResult
    {
        ME1 = 1,
        ME2 = 2,
        ME3 = 3,
    }
}