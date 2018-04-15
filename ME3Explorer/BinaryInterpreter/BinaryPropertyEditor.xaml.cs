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
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for PropertyEditor.xaml
    /// </summary>
    public partial class BinaryPropertyEditor : UserControl, INotifyPropertyChanged
    {
        public PropertyCollection Props
        {
            get { return (PropertyCollection)GetValue(PropsProperty); }
            set { SetValue(PropsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Props.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PropsProperty =
            DependencyProperty.Register("Props", typeof(PropertyCollection), typeof(BinaryPropertyEditor), new PropertyMetadata());



        public IMEPackage Pcc
        {
            get { return (IMEPackage)GetValue(PccProperty); }
            set { SetValue(PccProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Pcc.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PccProperty =
            DependencyProperty.Register("Pcc", typeof(IMEPackage), typeof(BinaryPropertyEditor), new PropertyMetadata());



        public BinaryPropertyEditor()
        {
            InitializeComponent();
        }

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
    }
}
