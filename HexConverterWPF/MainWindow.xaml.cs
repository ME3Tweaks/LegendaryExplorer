using HexConverterWPF.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HexConverterWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region properties
        private bool _alwaysOnTop;
        public bool AlwaysOnTop
        {
            get { return _alwaysOnTop; }
            set
            {
                _alwaysOnTop = value;
                OnPropertyChanged();
                Topmost = value;
            }
        }

        private string _littleEndianText;
        public string LittleEndianText
        {
            get { return _littleEndianText; }
            set
            {
                _littleEndianText = value;
                OnPropertyChanged();
            }
        }

        private string _bigEndianText;
        public string BigEndianText
        {
            get { return _bigEndianText; }
            set
            {
                _bigEndianText = value;
                OnPropertyChanged();
            }
        }

        private string _signedIntegerText;
        public string SignedIntegerText
        {
            get { return _signedIntegerText; }
            set
            {
                _signedIntegerText = value;
                OnPropertyChanged();
            }
        }

        private string _unsignedIntegerText;
        public string UnsignedIntegerText
        {
            get { return _unsignedIntegerText; }
            set
            {
                _unsignedIntegerText = value;
                OnPropertyChanged();
            }
        }

        private string _floatText;
        public string FloatText
        {
            get { return _floatText; }
            set
            {
                _floatText = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Property Changed Notification
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion

        List<TextBox> HexInputTextBoxes;
        List<TextBox> DecInputTextBoxes;
        List<TextBox> AllInputTextBoxes;
        public MainWindow()
        {
            DataContext = this;
            FloatText = BigEndianText = LittleEndianText = SignedIntegerText = UnsignedIntegerText = 0.ToString();
            InitializeComponent();
            AllInputTextBoxes = new List<TextBox>(new TextBox[] { Float_TextBox, BigEndian_TextBox, LittleEndian_TextBox, Signed_TextBox, Unsigned_TextBox });
            HexInputTextBoxes = new List<TextBox>(new TextBox[] { BigEndian_TextBox, LittleEndian_TextBox });
            DecInputTextBoxes = new List<TextBox>(new TextBox[] { Float_TextBox, Signed_TextBox, Unsigned_TextBox });
        }

        private void RunConversions(TextBox sourceTextBox)
        {
            string sourceStr = sourceTextBox.Text.Trim().ToUpper();
            sourceTextBox.Text = sourceStr; //force uppercase

            if (sourceTextBox == BigEndian_TextBox || sourceTextBox == LittleEndian_TextBox)
            {
                sourceStr = sourceStr.PadLeft(8, '0');
                sourceTextBox.Text = sourceStr;

                byte[] asCurrentEndian = new byte[4];
                byte[] asReversedEndian = new byte[4];
                string reversedEndianStr = "";
                for (int i = 0; i < 4; i++)
                {
                    asCurrentEndian[i] = Convert.ToByte(sourceStr.Substring(i * 2, 2), 16);
                    reversedEndianStr = asCurrentEndian[i].ToString("X2") + reversedEndianStr;
                    asReversedEndian[3 - i] = asCurrentEndian[i];
                }
                if (sourceTextBox == BigEndian_TextBox)
                {
                    FloatText = BitConverter.ToSingle(asReversedEndian, 0).ToString();
                    UnsignedIntegerText = BitConverter.ToUInt32(asReversedEndian, 0).ToString();
                    SignedIntegerText = BitConverter.ToInt32(asReversedEndian, 0).ToString();
                    LittleEndianText = reversedEndianStr;
                }
                else
                {
                    FloatText = BitConverter.ToSingle(asCurrentEndian, 0).ToString();
                    UnsignedIntegerText = BitConverter.ToUInt32(asCurrentEndian, 0).ToString();
                    SignedIntegerText = BitConverter.ToInt32(asCurrentEndian, 0).ToString();
                    BigEndianText = reversedEndianStr;
                }
            }
            else if (sourceTextBox == Float_TextBox)
            {
                SignedIntegerText = "N/A";
                UnsignedIntegerText = "N/A";

                float n = 0;
                string seperator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                string wrongsep;
                if (seperator == ".")
                    wrongsep = ",";
                else
                    wrongsep = ".";
                sourceStr = sourceStr.Replace(wrongsep, seperator);

                if (float.TryParse(sourceStr, out n))
                {
                    byte[] buff = BitConverter.GetBytes(n);
                    string s = "", s2 = "";
                    for (int i = 0; i < 4; i++)
                    {
                        s += buff[i].ToString("x2");
                        s2 = buff[i].ToString("x2") + s2;
                    }
                    LittleEndianText = s;
                    BigEndianText = s2;
                    //textBox3.Text = BitConverter.ToInt32(buff, 0).ToString();
                    //textBox4.Text = BitConverter.ToUInt32(buff, 0).ToString();
                }
            }
            else if (sourceTextBox == Unsigned_TextBox)
            {
                uint n;
                if (uint.TryParse(sourceStr, out n))
                {
                    byte[] buff = BitConverter.GetBytes(n);
                    string s = "", s2 = "";
                    for (int i = 0; i < 4; i++)
                    {
                        s += buff[i].ToString("X2");
                        s2 = buff[i].ToString("X2") + s2;
                    }
                    LittleEndianText = s;
                    BigEndianText = s2;
                    SignedIntegerText = BitConverter.ToInt32(buff, 0).ToString();
                    FloatText = BitConverter.ToSingle(buff, 0).ToString(); //Is this useful??
                }
            }
            else if (sourceTextBox == Signed_TextBox)
            {
                int n;
                if (int.TryParse(sourceStr, out n))
                {
                    byte[] buff = BitConverter.GetBytes(n);
                    string s = "", s2 = "";
                    for (int i = 0; i < 4; i++)
                    {
                        s += buff[i].ToString("X2");
                        s2 = buff[i].ToString("X2") + s2;
                    }
                    LittleEndianText = s;
                    BigEndianText = s2;
                    UnsignedIntegerText = BitConverter.ToUInt32(buff, 0).ToString();
                    FloatText = BitConverter.ToSingle(buff, 0).ToString(); //Is this useful??
                }
            }

            /*
                byte[] buff = new byte[4];
                byte[] buff2 = new byte[4];
                string s = "";
                //build reverse endian
                for (int i = 0; i < 4; i++)
                {
                    buff[i] = Convert.ToByte(sourceStr.Substring(i * 2, 2), 16);
                    s = buff[i].ToString("x2") + s;
                    buff2[3 - i] = buff[i];
                }
                textBox2.Text = s;
                textBox3.Text = BitConverter.ToInt32(buff, 0).ToString();
                textBox4.Text = BitConverter.ToUInt32(buff, 0).ToString();
                textBox5.Text = BitConverter.ToSingle(buff, 0).ToString();*/
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                RunConversions((TextBox)sender);
            }
        }

        /*
        private void ConvertFromBigEndian(object sender, EventArgs e)
        {
            string input = textBox1.Text.ToLower().Replace(" ", "");
            input = padwith0s(input);
            textBox1.Text = input;
            if (!isHexString(input) || input.Length != 8)
            {
                MessageBox.Show("Invalid input!");
                return;
            }

        }

        private string padwith0s(string input)
        {
            if (input.Length < 8)
            {
                for (int i = input.Length; i < 8; i++)
                {
                    input += '0';
                }
            }
            return input;
        }

        public static string hexchars = "0123456789abcdefABCDEF";

        public static bool isHexString(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                int f = -1;
                for (int j = 0; j < hexchars.Length; j++)
                    if (s[i] == hexchars[j])
                    {
                        f = j;
                        break;
                    }
                if (f == -1)
                    return false;
            }
            return true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string input = textBox2.Text.ToLower().Replace(" ", "");
            input = padwith0s(input);
            textBox2.Text = input;
            if (!isHexString(input) || input.Length != 8)
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = new byte[4];
            byte[] buff2 = new byte[4];
            string s = "";
            for (int i = 0; i < 4; i++)
            {
                buff[i] = Convert.ToByte(input.Substring(i * 2, 2), 16);
                s = buff[i].ToString("x2") + s;
                buff2[3 - i] = buff[i];
            }
            textBox1.Text = s;
            textBox3.Text = BitConverter.ToInt32(buff2, 0).ToString();
            textBox4.Text = BitConverter.ToUInt32(buff2, 0).ToString();
            textBox5.Text = BitConverter.ToSingle(buff2, 0).ToString();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            int n = 0;
            if (!Int32.TryParse(textBox3.Text, out n))
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = BitConverter.GetBytes(n);
            string s = "", s2 = "";
            for (int i = 0; i < 4; i++)
            {
                s += buff[i].ToString("x2");
                s2 = buff[i].ToString("x2") + s2;
            }
            textBox1.Text = s;
            textBox2.Text = s2;
            textBox4.Text = BitConverter.ToUInt32(buff, 0).ToString();
            textBox5.Text = BitConverter.ToSingle(buff, 0).ToString();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            uint n = 0;
            if (!UInt32.TryParse(textBox4.Text, out n))
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = BitConverter.GetBytes(n);
            string s = "", s2 = "";
            for (int i = 0; i < 4; i++)
            {
                s += buff[i].ToString("x2");
                s2 = buff[i].ToString("x2") + s2;
            }
            textBox1.Text = s;
            textBox2.Text = s2;
            textBox3.Text = BitConverter.ToInt32(buff, 0).ToString();
            textBox5.Text = BitConverter.ToSingle(buff, 0).ToString();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            float n = 0;
            string seperator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string wrongsep;
            if (seperator == ".")
                wrongsep = ",";
            else
                wrongsep = ".";
            textBox5.Text = textBox5.Text.Replace(wrongsep, seperator);
            if (!float.TryParse(textBox5.Text, out n))
            {
                MessageBox.Show("Invalid input!");
                return;
            }
            byte[] buff = BitConverter.GetBytes(n);
            string s = "", s2 = "";
            for (int i = 0; i < 4; i++)
            {
                s += buff[i].ToString("x2");
                s2 = buff[i].ToString("x2") + s2;
            }
            textBox1.Text = s;
            textBox2.Text = s2;
            textBox3.Text = BitConverter.ToInt32(buff, 0).ToString();
            textBox4.Text = BitConverter.ToUInt32(buff, 0).ToString();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button1_Click(null, null);
            }
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button2_Click(null, null);
            }
        }

        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button3_Click(null, null);
            }
        }

        private void textBox4_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button4_Click(null, null);
            }
        }

        private void textBox5_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button5_Click(null, null);
            }
        }*/

        private delegate void TextBoxSelectAllDelegate(object sender);

        private void TextBoxSelectAll(object sender)
        {
            (sender as TextBox).SelectAll();
        }

        private void MyTextBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            TextBoxSelectAllDelegate d = TextBoxSelectAll;

            this.Dispatcher.BeginInvoke(d,
                System.Windows.Threading.DispatcherPriority.ApplicationIdle, sender);
        }
    }
}