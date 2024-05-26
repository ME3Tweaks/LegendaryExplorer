using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HexConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region properties
        public bool AlwaysOnTop
        {
            get => Topmost;
            set
            {
                Topmost = value;
                OnPropertyChanged();
            }
        }

        private string _littleEndianText;
        public string LittleEndianText
        {
            get => _littleEndianText;
            set
            {
                _littleEndianText = value;
                OnPropertyChanged();
            }
        }

        private string _bigEndianText;
        public string BigEndianText
        {
            get => _bigEndianText;
            set
            {
                _bigEndianText = value;
                OnPropertyChanged();
            }
        }

        private string _signedIntegerText;
        public string SignedIntegerText
        {
            get => _signedIntegerText;
            set
            {
                _signedIntegerText = value;
                OnPropertyChanged();
            }
        }

        private string _unsignedIntegerText;
        public string UnsignedIntegerText
        {
            get => _unsignedIntegerText;
            set
            {
                _unsignedIntegerText = value;
                OnPropertyChanged();
            }
        }

        private string _floatText;
        public string FloatText
        {
            get => _floatText;
            set
            {
                _floatText = value;
                OnPropertyChanged();
            }
        }

        private string _unrealRotationText = "0";
        public string UnrealRotationText
        {
            get => _unrealRotationText;
            set
            {
                _unrealRotationText = value;
                OnPropertyChanged();
            }
        }

        private string _degreesRotationText = "0";
        public string DegreesRotationText
        {
            get => _degreesRotationText;
            set
            {
                _degreesRotationText = value;
                OnPropertyChanged();
            }
        }

        private string _radiansRotationText = "0";

        public string RadiansRotationText
        {
            get => _radiansRotationText;
            set
            {
                _radiansRotationText = value;
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
            AllInputTextBoxes = new List<TextBox>(new[] { Float_TextBox, BigEndian_TextBox, LittleEndian_TextBox, Signed_TextBox, Unsigned_TextBox, UnrealRot_TextBox });
            HexInputTextBoxes = new List<TextBox>(new[] { BigEndian_TextBox, LittleEndian_TextBox });
            DecInputTextBoxes = new List<TextBox>(new[] { Float_TextBox, Signed_TextBox, Unsigned_TextBox });
        }

        /// <summary>
        /// Converts Degrees to Unreal rotation units
        /// </summary>
        public static int DegreesToUnrealRotationUnits(float degrees) => Convert.ToInt32(degrees * 65536f / 360f);

        /// <summary>
        /// Converts Radians to Unreal rotation units
        /// </summary>
        public static int RadiansToUnrealRotationUnits(float radians) => Convert.ToInt32(radians * 180 / Math.PI * 65536f / 360f);

        /// <summary>
        /// Converts Unreal rotation units to Degrees
        /// </summary>
        public static float UnrealRotationUnitsToDegrees(int unrealRotationUnits) => unrealRotationUnits * 360f / 65536f;

        /// <summary>
        /// Converts Unreal rotation units to Radians
        /// </summary>
        public static double UnrealRotationUnitsToRadians(int unrealRotationUnits) => unrealRotationUnits * 360.0 / 65536.0 * Math.PI / 180.0;

        private void RunConversionsNumbers(TextBox sourceTextBox)
        {
            string sourceStr = sourceTextBox.Text.Trim().ToUpper().Replace(" ", "");
            sourceTextBox.Text = sourceStr; //force uppercase

            if (sourceTextBox == BigEndian_TextBox || sourceTextBox == LittleEndian_TextBox)
            {
                sourceStr = new string(sourceTextBox.Text.Where(c => hexChars.Contains(c)).ToArray()); //remove non-hex characters
                sourceStr = sourceStr.PadLeft(8, '0').Substring(0, 8); //only 8 long supported
                sourceTextBox.Text = sourceStr;

                var asCurrentEndian = new byte[4];
                var asReversedEndian = new byte[4];
                string reversedEndianStr = "";
                for (int i = 0; i < 4; i++)
                {
                    asCurrentEndian[i] = Convert.ToByte(sourceStr.Substring(i * 2, 2), 16);
                    reversedEndianStr = $"{asCurrentEndian[i]:X2}{reversedEndianStr}";
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

                string seperator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                string wrongsep;
                if (seperator == ".")
                    wrongsep = ",";
                else
                    wrongsep = ".";
                sourceStr = sourceStr.Replace(wrongsep, seperator);

                if (float.TryParse(sourceStr, out float n))
                {
                    byte[] buff = BitConverter.GetBytes(n);
                    string s = "", s2 = "";
                    for (int i = 0; i < 4; i++)
                    {
                        s = $"{s}{buff[i]:x2}";
                        s2 = $"{buff[i]:x2}{s2}";
                    }
                    LittleEndianText = s;
                    BigEndianText = s2;
                    //textBox3.Text = BitConverter.ToInt32(buff, 0).ToString();
                    //textBox4.Text = BitConverter.ToUInt32(buff, 0).ToString();
                }
            }
            else if (sourceTextBox == Unsigned_TextBox)
            {
                if (uint.TryParse(sourceStr, out uint n))
                {
                    byte[] buff = BitConverter.GetBytes(n);
                    string s = "", s2 = "";
                    for (int i = 0; i < 4; i++)
                    {
                        s = $"{s}{buff[i]:X2}";
                        s2 = $"{buff[i]:X2}{s2}";
                    }
                    LittleEndianText = s;
                    BigEndianText = s2;
                    SignedIntegerText = BitConverter.ToInt32(buff, 0).ToString();
                    FloatText = BitConverter.ToSingle(buff, 0).ToString(); //Is this useful??
                }
            }
            else if (sourceTextBox == Signed_TextBox)
            {
                if (int.TryParse(sourceStr, out int n))
                {
                    byte[] buff = BitConverter.GetBytes(n);
                    string s = "", s2 = "";
                    for (int i = 0; i < 4; i++)
                    {
                        s = $"{s}{buff[i]:X2}";
                        s2 = $"{buff[i]:X2}{s2}";
                    }
                    LittleEndianText = s;
                    BigEndianText = s2;
                    UnsignedIntegerText = BitConverter.ToUInt32(buff, 0).ToString();
                    FloatText = BitConverter.ToSingle(buff, 0).ToString(); //Is this useful??
                }
            }
        }

        private void OnKeyUpNumbers(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                RunConversionsNumbers((TextBox)sender);
            }
        }

        private void OnKeyUpRotation(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                RunConversionsRotation((TextBox)sender);
            }
        }

        private void RunConversionsRotation(TextBox sourceTextBox)
        {
            string sourceStr = sourceTextBox.Text.Trim().ToUpper().Replace(" ", "");
            sourceTextBox.Text = sourceStr; //force uppercase

            if (sourceTextBox == UnrealRot_TextBox)
            {
                if (int.TryParse(sourceStr, out var unrealUnits))
                {
                    DegreesRotationText = UnrealRotationUnitsToDegrees(unrealUnits).ToString();
                    RadiansRotationText = UnrealRotationUnitsToRadians(unrealUnits).ToString();
                }
            }
            else if (sourceTextBox == DegreesRot_TextBox)
            {
                if (float.TryParse(sourceStr, out var degrees))
                {
                    var radians = (float)(degrees * (Math.PI / 180f));
                    UnrealRotationText = RadiansToUnrealRotationUnits(radians).ToString();
                    RadiansRotationText = radians.ToString(CultureInfo.InvariantCulture);
                }
            }
            else if (sourceTextBox == RadiansRot_TextBox)
            {
                if (float.TryParse(sourceStr, out var radians))
                {
                    var degrees = (float)((radians * 180f) / Math.PI);
                    DegreesRotationText = degrees.ToString(CultureInfo.InvariantCulture);
                    UnrealRotationText = DegreesToUnrealRotationUnits(degrees).ToString();
                }
            }
        }

        private delegate void TextBoxSelectAllDelegate(TextBox sender);

        private void TextBoxSelectAll(TextBox sender)
        {
            sender.SelectAll();
        }

        private void MyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBoxSelectAllDelegate d = TextBoxSelectAll;

            Dispatcher.BeginInvoke(d, System.Windows.Threading.DispatcherPriority.ApplicationIdle, sender);
        }

        private const string hexChars = "0123456789abcdefABCDEF";

        private void Hex_TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            //is user attempts to enter a non-hex character, set handled to true, preventing it from being entered
            if (e.Text.All(c => !hexChars.Contains(c)))
            {
                e.Handled = true;
            }
        }
    }
}