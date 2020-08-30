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
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for PropertyEditor.xaml
    /// </summary>
    public partial class PropertyEditor
    {
        public PropertyCollection Props
        {
            get => (PropertyCollection)GetValue(PropsProperty);
            set => SetValue(PropsProperty, value);
        }

        // Using a DependencyProperty as the backing store for Props.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PropsProperty =
            DependencyProperty.Register(nameof(Props), typeof(PropertyCollection), typeof(PropertyEditor), new PropertyMetadata());



        public IMEPackage Pcc
        {
            get => (IMEPackage)GetValue(PccProperty);
            set => SetValue(PccProperty, value);
        }

        // Using a DependencyProperty as the backing store for Pcc.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PccProperty =
            DependencyProperty.Register(nameof(Pcc), typeof(IMEPackage), typeof(PropertyEditor), new PropertyMetadata());



        public PropertyEditor()
        {
            InitializeComponent();
        }
    }

    [ValueConversion(typeof(int), typeof(string))]
    public class UIndexToObjectNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] is int uIndex && values[1] is IMEPackage pcc)
            {
                return $"({pcc.getObjectName(uIndex)})";
            }
            return "()";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }

    [ValueConversion(typeof(int), typeof(string))]
    public class StringRefToStringConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] is int strRef && values[1] is IMEPackage pcc)
            {
                switch (pcc.Game)
                {
                    case MEGame.ME1:
                        return "ME1 StrRef not Supporte";
                    case MEGame.ME2:
                        return $"\"{ME2Explorer.ME2TalkFiles.findDataById(strRef)}\"";
                    case MEGame.ME3:
                        return $"\"{ME3TalkFiles.findDataById(strRef)}\"";
                    case MEGame.UDK:
                        return "UDK StrRef not supported";
                }
            }
            return "No Data";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }
}
