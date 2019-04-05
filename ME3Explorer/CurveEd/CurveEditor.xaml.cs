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
using Gibbed.IO;
using ME3Explorer.Packages;
using ME3Explorer.Unreal;

namespace ME3Explorer.CurveEd
{
    /// <summary>
    /// Interaction logic for CurveEditor.xaml
    /// </summary>
    public sealed partial class CurveEditor : ExportLoaderControl
    {
        public List<InterpCurve> InterpCurveTracks;

        public CurveEditor()
        {
            InitializeComponent();
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            CurrentLoadedExport = exportEntry;
            Load();
        }

        public CurveEditor(IExportEntry exp)
        {
            InitializeComponent();
            LoadExport(exp);
        }

        private void Load()
        {
            InterpCurveTracks = new List<InterpCurve>();

            var props = CurrentLoadedExport.GetProperties();
            foreach (var prop in props)
            {
                if (prop is StructProperty structProp)
                {
                    if (Enum.TryParse(structProp.StructType, out CurveType _))
                    {
                        InterpCurveTracks.Add(new InterpCurve(CurrentLoadedExport.FileRef, structProp));
                    }
                }
            }

            foreach (var interpCurve in InterpCurveTracks)
            {
                foreach (var curve in interpCurve.Curves)
                {
                    curve.SaveChanges = Commit;
                }
            }

            TrackList.ItemsSource = InterpCurveTracks;
            graph.Paint();
        }

        private void TrackList_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            CurveGraph.TrackLoading = true;
            if (e.NewValue is Curve curve)
            {
                graph.SelectedCurve = curve;
            }
            graph.Paint(true);
            CurveGraph.TrackLoading = false;
        }

        private void graph_SelectedPointChanged(object sender, RoutedPropertyChangedEventArgs<CurvePoint> e)
        {
            if (e.NewValue == null)
            {
                return;
            }
            switch (e.NewValue.InterpMode)
            {
                case CurveMode.CIM_Linear:
                    btnLinear.IsChecked = true;
                    break;
                case CurveMode.CIM_CurveAuto:
                    btnAuto.IsChecked = true;
                    break;
                case CurveMode.CIM_Constant:
                    btnConstant.IsChecked = true;
                    break;
                case CurveMode.CIM_CurveUser:
                    btnUser.IsChecked = true;
                    break;
                case CurveMode.CIM_CurveBreak:
                    btnBreak.IsChecked = true;
                    break;
                case CurveMode.CIM_CurveAutoClamped:
                    btnClamped.IsChecked = true;
                    break;
            }
        }

        private void btnInterpMode_Click(object sender, RoutedEventArgs e)
        {
            CurvePoint selectedPoint = graph.SelectedPoint;
            switch ((sender as RadioButton)?.Name)
            {
                case "btnLinear":
                    selectedPoint.InterpMode = CurveMode.CIM_Linear;
                    break;
                case "btnAuto":
                    selectedPoint.InterpMode = CurveMode.CIM_CurveAuto;
                    break;
                case "btnConstant":
                    selectedPoint.InterpMode = CurveMode.CIM_Constant;
                    break;
                case "btnUser":
                    selectedPoint.InterpMode = CurveMode.CIM_CurveUser;
                    break;
                case "btnBreak":
                    selectedPoint.InterpMode = CurveMode.CIM_CurveBreak;
                    break;
                case "btnClamped":
                    selectedPoint.InterpMode = CurveMode.CIM_CurveAutoClamped;
                    break;
            }
            graph.Paint();
            graph.SelectedPoint = selectedPoint;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Commit();
        }

        private void Commit()
        {
            if (!CurveGraph.TrackLoading)
            {
                var props = CurrentLoadedExport.GetProperties();
                foreach (InterpCurve item in InterpCurveTracks)
                {
                    props.AddOrReplaceProp(item.WriteProperties());
                }
                CurrentLoadedExport.WriteProperties(props);
            }
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = InterpCurveTracks.Count > 0;
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Commit();
            //pcc.save();
            //MessageBox.Show("Done");
        }

        public override void UnloadExport()
        {
            graph.Clear();
            InterpCurveTracks = null;
            CurrentLoadedExport = null;
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            if (exportEntry.FileRef.Game == MEGame.ME3)
            {
                var props = exportEntry.GetProperties();
                foreach (var prop in props)
                {
                    if (prop is StructProperty structProp 
                        && Enum.TryParse(structProp.StructType, out CurveType _))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public override void Dispose()
        {
            //nothing to dispose in this control for now
        }
    }
}
