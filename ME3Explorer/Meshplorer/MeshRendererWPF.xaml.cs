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
        FrostyRenderImage imageSource = new FrostyRenderImage();
        public SwapChain SwapChain { get; private set; } = null;
        public Texture2D BackBuffer { get; private set; } = null;
        public SharpDX.Direct3D11.Device Device { get; private set; } = null;


        public MeshRendererWPF()
        {
            InitializeComponent();
            LoadDirect3D();
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
            // Set up description of swap chain
            SwapChainDescription scd = new SwapChainDescription();
            scd.BufferCount = 1;
            scd.ModeDescription = new ModeDescription(1024,1024, new Rational(60, 1), Format.B8G8R8A8_UNorm);
            scd.Usage = Usage.RenderTargetOutput;
            scd.OutputHandle = FrostyRenderImage.GetDesktopWindow();
            scd.SampleDescription.Count = 1;
            scd.SampleDescription.Quality = 0;
            scd.IsWindowed = true;
            scd.ModeDescription.Width = 1024;
            scd.ModeDescription.Height = 1024;

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


            imageSource.SetBackBuffer(BackBuffer);
            FrostyImageContainer.Source = imageSource;
        }
    }
}
