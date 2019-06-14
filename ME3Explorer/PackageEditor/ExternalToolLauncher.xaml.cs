using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Path = System.IO.Path;

namespace ME3Explorer.PackageEditor
{
    /// <summary>
    /// Interaction logic for ExternalToolLauncher.xaml
    /// </summary>
    public partial class ExternalToolLauncher : ExportLoaderControl
    {
        private static string[] parsableClasses = { "BioSWF", "GFxMovieInfo" };
        public ExternalToolLauncher()
        {
            InitializeComponent();
        }

        private void OpenWithJPEX_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var props = CurrentLoadedExport.GetProperties();
                string dataPropName = CurrentLoadedExport.FileRef.Game != MEGame.ME1 ? "RawData" : "Data";

                //This may be more efficient if it is copied with blockcopy instead.
                byte[] data = props.GetProp<ArrayProperty<ByteProperty>>(dataPropName).Select(x => x.Value).ToArray();
                string writeoutPath = Path.Combine(Path.GetTempPath(), CurrentLoadedExport.ObjectName + ".gfx");

                File.WriteAllBytes(writeoutPath, data);

                Process process = new Process();
                // Configure the process using the StartInfo properties.
                process.StartInfo.FileName = @"C:\Program Files (x86)\FFDec\ffdec.exe";
                process.StartInfo.Arguments = writeoutPath;
                process.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error launching external tool: "+ExceptionHandlerDialogWPF.FlattenException(ex));
                //MessageBox.Show("Error reading/saving SWF data:\n\n" + ExceptionHandlerDialogWPF.FlattenException(ex));
            }
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            return parsableClasses.Contains(exportEntry.ClassName) && !exportEntry.ObjectName.StartsWith(("Default__"));
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            //throw new NotImplementedException();
        }

        public override void UnloadExport()
        {
            CurrentLoadedExport = null;
            //throw new NotImplementedException();
        }

        public override void PopOut()
        {
            //throw new NotImplementedException();
        }

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}
