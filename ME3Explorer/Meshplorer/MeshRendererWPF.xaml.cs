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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.DXGI.Device;

namespace ME3Explorer.Meshplorer
{
    /// <summary>
    /// Interaction logic for MeshRendererWPF.xaml
    /// </summary>
    public partial class MeshRendererWPF : ExportLoaderControl
    {
        private static readonly string[] parsableClasses = { "SkeletalMesh", "StaticMesh" };


        public MeshRendererWPF()
        {
            InitializeComponent();
        }

        public override bool CanParse(ExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith(("Default__"));
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
        }

        public override void PopOut()
        {
            //throw new NotImplementedException();
        }

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Get PresentationSource
            PresentationSource presentationSource = PresentationSource.FromVisual((Visual)sender);

            // Subscribe to PresentationSource's ContentRendered event
            presentationSource.ContentRendered += TestUserControl_ContentRendered;
        }

        void TestUserControl_ContentRendered(object sender, EventArgs e)
        {
            // Don't forget to unsubscribe from the event
            
        }
    }
}
