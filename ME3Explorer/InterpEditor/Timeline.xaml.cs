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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;

namespace ME3Explorer.Matinee
{
    /// <summary>
    /// Interaction logic for Timeline.xaml
    /// </summary>
    public partial class Timeline : NotifyPropertyChangedControlBase
    {
        public IMEPackage Pcc => InterpDataExport?.FileRef;

        private IExportEntry _interpDataExport;
        public IExportEntry InterpDataExport
        {
            get => _interpDataExport;
            set
            {
                if (SetProperty(ref _interpDataExport, value))
                {
                    LoadGroups();
                }
            }
        }

        public ObservableCollectionExtended<InterpGroup> InterpGroups { get; } = new ObservableCollectionExtended<InterpGroup>();

        public Timeline()
        {
            InitializeComponent();
        }

        private void LoadGroups()
        {
            InterpGroups.ClearEx();
            var groupsProp = InterpDataExport.GetProperty<ArrayProperty<ObjectProperty>>("InterpGroups");
            if (groupsProp != null)
            {
                var groupExports = groupsProp.Where(prop => Pcc.isUExport(prop.Value)).Select(prop => Pcc.getUExport(prop.Value));
                InterpGroups.AddRange(groupExports.Select(exp => new InterpGroup(exp)));
            }
        }
    }
}
