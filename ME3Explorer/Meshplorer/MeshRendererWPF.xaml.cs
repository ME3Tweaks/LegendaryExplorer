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
        FrostyRenderImage imageSource;
        public SwapChain SwapChain { get; private set; } = null;
        public Texture2D BackBuffer { get; private set; } = null;
        public SharpDX.Direct3D11.Device Device { get; private set; } = null;


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


        public void LoadDirect3D()
        {
            Window window = Window.GetWindow(this);
            var wih = new WindowInteropHelper(window);
            imageSource = new FrostyRenderImage(wih.Handle);
            // Set up description of swap chain
            SwapChainDescription scd = new SwapChainDescription();
            scd.BufferCount = 1;
            scd.ModeDescription = new ModeDescription(1024,1024, new Rational(60, 1), Format.B8G8R8A8_UNorm);
            scd.Usage = Usage.RenderTargetOutput;

            
            scd.OutputHandle = wih.Handle;
            scd.SampleDescription.Count = 1;
            scd.SampleDescription.Quality = 0;
            scd.IsWindowed = true;

            // Create device and swap chain according to the description above
            SharpDX.Direct3D11.Device d;
            SwapChain sc;
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.SingleThreaded;
#if DEBUG
            flags |= DeviceCreationFlags.Debug;
#endif
            SharpDX.Direct3D11.Device.CreateWithSwapChain(DriverType.Hardware, flags, scd, out d, out sc);
            this.SwapChain = sc; // we have to use these temp variables
            this.Device = d; // because properties can't be passed as out parameters. =(
            BackBuffer = Texture2D.FromSwapChain<Texture2D>(SwapChain, 0);

            var textureD3D11 = new Texture2D(Device, new Texture2DDescription
            {
                Width = 1024,
                Height = 1024,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.SharedKeyedmutex
            });

            imageSource.SetBackBuffer(textureD3D11);
            FrostyImageContainer.Source = imageSource;
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
            ((PresentationSource)sender).ContentRendered -= TestUserControl_ContentRendered;
            LoadDirect3D();
        }
    }
}
