using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for ExceptionHandlerDialogWPF.xaml
    /// </summary>
    public partial class ExceptionHandlerDialogWPF : Window
    {
        Exception exception;
        public bool Handled = false;
        public ExceptionHandlerDialogWPF(Exception exception)
        {
            InitializeComponent();
            this.exception = exception;
            ExceptionStackTrace_TextBox.Text = FlattenException(exception);
            ExceptionMessage_TextBlock.Text = exception.Message;
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

        /// <summary>
        /// Flattens an exception into a printable string
        /// </summary>
        /// <param name="exception">Exception to flatten</param>
        /// <returns>Printable string</returns>
        public static string FlattenException(Exception exception)
        {
            var stringBuilder = new StringBuilder();

            while (exception != null)
            {
                stringBuilder.AppendLine(exception.GetType().Name + ": " + exception.Message);
                stringBuilder.AppendLine(exception.StackTrace);

                exception = exception.InnerException;
            }

            return stringBuilder.ToString();
        }
    }
}
