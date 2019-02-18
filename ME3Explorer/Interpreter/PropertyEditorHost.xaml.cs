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

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for PropertyEditorHost.xaml
    /// </summary>
    public partial class PropertyEditorHost : NotifyPropertyChangedControlBase
    {
        public IExportEntry Export
        {
            get => (IExportEntry)GetValue(ExportProperty);
            set => SetValue(ExportProperty, value);
        }

        // Using a DependencyProperty as the backing store for Export.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ExportProperty =
            DependencyProperty.Register(nameof(Export), typeof(IExportEntry), typeof(PropertyEditorHost), new PropertyMetadata(OnExportChanged));

        private static void OnExportChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is IExportEntry export 
             && d is PropertyEditorHost propEdHost)
            {
                propEdHost.propEd.Props = export.GetProperties();
                propEdHost.propEd.Pcc = export.FileRef;
            }
        }
        public PropertyEditorHost()
        {
            InitializeComponent();
        }
    }
}
