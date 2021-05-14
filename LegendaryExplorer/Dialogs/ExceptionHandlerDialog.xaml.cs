using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore.Helpers;
using Microsoft.AppCenter.Crashes;

namespace LegendaryExplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for ExceptionHandlerDialogWPF.xaml
    /// </summary>
    public partial class ExceptionHandlerDialog : Window
    {
        public bool Handled;

        public ExceptionHandlerDialog(Exception exception)
        {
            InitializeComponent();
            string flattened = exception.FlattenException();
            ExceptionStackTrace_TextBox.Text = flattened;
            ExceptionMessage_TextBlock.Text = exception.Message;
            var errorSize = MeasureString(flattened);

            Height = Math.Min(900, errorSize.Height + 250);
            if (Settings.Analytics_Enabled)
            {
                Crashes.TrackError(exception);
            }
        }

        private Size MeasureString(string candidate)
        {
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(this.ExceptionStackTrace_TextBox.FontFamily, this.ExceptionStackTrace_TextBox.FontStyle, this.ExceptionStackTrace_TextBox.FontWeight, this.ExceptionStackTrace_TextBox.FontStretch),
                this.ExceptionStackTrace_TextBox.FontSize,
                Brushes.Black,
                new NumberSubstitution(),
                1);

            return new Size(formattedText.Width, formattedText.Height);
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(1);
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            Handled = true;
            Close();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ExceptionStackTrace_TextBox.Text);
            }
            catch (Exception)
            {
                //what are we going to do. Crash on the error dialog?
            }
        }
    }
}
