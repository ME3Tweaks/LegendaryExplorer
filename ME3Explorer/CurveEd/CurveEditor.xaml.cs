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
using ME3Explorer.Unreal;

namespace ME3Explorer.CurveEd
{
    /// <summary>
    /// Interaction logic for CurveEditor.xaml
    /// </summary>
    public partial class CurveEditor : Window
    {
        private PCCObject pcc;
        private int index;

        public List<InterpCurve> InterpCurveTracks;

        public CurveEditor(PCCObject _pcc, int Index)
        {
            InitializeComponent();
            pcc = _pcc;
            index = Index;
            InterpCurveTracks = new List<InterpCurve>();

            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            CurveType throwaway = CurveType.InterpCurveVector;
            foreach (var p in props)
            {
                if (p.TypeVal == PropertyReader.Type.StructProperty)
                {
                    if (Enum.TryParse(pcc.getNameEntry(p.Value.IntValue), out throwaway))
                    {
                        InterpCurveTracks.Add(new InterpCurve(pcc, p));
                    }
                }
            }

            TrackList.ItemsSource = InterpCurveTracks;
            graph.Paint();
        }

        private void TrackList_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is Curve)
            {
                graph.SelectedCurve = e.NewValue as Curve;
            }
            graph.Paint(true);
        }

        private void graph_SelectedPointChanged(object sender, RoutedPropertyChangedEventArgs<CurvePoint> e)
        {
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
                default:
                    break;
            }
        }

        private void btnInterpMode_Click(object sender, RoutedEventArgs e)
        {
            CurvePoint selectedPoint = graph.SelectedPoint;
            switch ((sender as RadioButton).Name)
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
                default:
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
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, pcc.Exports[index]);
            foreach (var p in props)
            {
                if (p.TypeVal == PropertyReader.Type.StructProperty)
                {
                    foreach (InterpCurve item in InterpCurveTracks)
                    {
                        if (pcc.getNameEntry(p.Name) == item.Name)
                        {
                            pcc.Exports[index].Data.OverwriteRange(p.offsetval - 24, item.Serialize());
                        }
                    }
                }
            }
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = InterpCurveTracks.Count > 0;
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Commit();
            pcc.save();
            MessageBox.Show("Done");
        }
    }
}
