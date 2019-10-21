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
using Microsoft.Win32;
using ClosedXML.Excel;
using Gammtek.Conduit.Extensions;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using Microsoft.WindowsAPICodePack.Dialogs;

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

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new CurveEditor(), CurrentLoadedExport)
                {
                    Title = $"Curve Editor - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.InstancedFullPath} - {CurrentLoadedExport.FileRef.FilePath}"
                };
                elhw.Show();
            }
        }

        public override void LoadExport(ExportEntry exportEntry)
        {
            if (CurrentLoadedExport != exportEntry || !IsKeyboardFocusWithin)
            {
                graph.Clear();
                CurrentLoadedExport = exportEntry;
                Load();
            }
        }

        public CurveEditor(ExportEntry exp)
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

        public void ExportCurvesToXLS()
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Curve");
            var trackKeys = new SortedDictionary<float, List<float>>();  // time is key, then all the floats at that time
            var curveList = new List<string>();  //List of column names
            //write data to list
            foreach (var track in InterpCurveTracks)
            {
                int n = 0;
                foreach(var curve in track.Curves)
                {
                    n++;
                    curveList.Add(curve.Name);
                    foreach(var point in curve.CurvePoints)
                    {
                        float time = point.InVal;
                        if (!trackKeys.ContainsKey(time))
                        {
                            trackKeys.Add(time, new List<float>());
                        }
                        else
                        {
                            while (trackKeys[time].Count < n - 1) //if previous curves didn't have this time add null [better way]?
                            {
                                trackKeys[time].Add((float)0.12345678);
                            }
                        }
                        trackKeys[time].Add(point.OutVal);
                    }
                }
            }
            
            //Write to XL
            int xlrow = 1;
            int xlcol = 1;
            worksheet.Cell(xlrow, xlcol).Value = "Time";
            foreach (var cn in curveList)
            {
                xlcol++;
                worksheet.Cell(xlrow, xlcol).Value = cn;
            }

            foreach (var tk in trackKeys)
            {
                xlrow++;
                xlcol = 1;
                worksheet.Cell(xlrow, xlcol).Value = tk.Key.ToString();
                foreach (var point in tk.Value)
                {
                    xlcol++;
                    if(point != (float)0.12345678) //skip null values
                        worksheet.Cell(xlrow, xlcol).Value = point.ToString();
                }
            }

            CommonSaveFileDialog m = new CommonSaveFileDialog
            {
                Title = "Select excel output",
                DefaultFileName = $"{CurrentLoadedExport.ObjectNameString}_{CurrentLoadedExport.UIndex}.xlsx",
                DefaultExtension = "xlsx",
            };
            m.Filters.Add(new CommonFileDialogFilter("Excel Files", "*.xlsx"));
            var owner = Window.GetWindow(this);
            if (m.ShowDialog(owner) == CommonFileDialogResult.Ok)
            {
                owner.RestoreAndBringToFront();
                try
                {
                    workbook.SaveAs(m.FileName);
                    MessageBox.Show($"Curves exported to {System.IO.Path.GetFileName(m.FileName)}.");
                }
                catch
                {
                    MessageBox.Show($"Save to {System.IO.Path.GetFileName(m.FileName)} failed.\nCheck the excel file is not open.");
                }
            }
        }

        public void ImportCurvesFromXLS()
        {
            var wdlg = MessageBox.Show("Do you want to import a new curve from Excel and overwrite the existing curve values?\n \nThe sheet must be in the correct format:\n- Headers must match the overwritten curves\n- All cells must contain a value\n- Time values must be ordered.", "Import Curves", MessageBoxButton.OKCancel);
            if (wdlg == MessageBoxResult.Cancel)
                return;

            var curveList = new List<string>(); //List of headers
            foreach (var otrack in InterpCurveTracks)
            {
                foreach (var ocurve in otrack.Curves)
                {
                    curveList.Add(ocurve.Name);
                }
            }

            OpenFileDialog oDlg = new OpenFileDialog //Load Excel
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };
            oDlg.Title = "Import Excel table";
            var result = oDlg.ShowDialog();

            if (!result.HasValue || !result.Value)
                return;

            var Workbook = new XLWorkbook(oDlg.FileName);
            IXLWorksheet iWorksheet;
            if (Workbook.Worksheets.Count() > 1)
            {
                try
                {
                    iWorksheet = Workbook.Worksheet("Curve");
                }
                catch
                {
                    MessageBox.Show("Curve Sheet not found");
                    return;
                }
            }
            else
            {
                iWorksheet = Workbook.Worksheet(1);
            }

            var xlrowCount = iWorksheet.RowsUsed().Count();
            //Check headers
            for (int hdr = 0; hdr < curveList.Count; hdr++) //skip time (first) column
            {
                var expected = curveList[hdr];
                var returned = (string)iWorksheet.Cell(1, hdr + 2).Value; //+2 as XL starts at 1, and skip time column
                if(expected != returned)
                {
                    MessageBox.Show("The imported column headers do not match.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                    return;
                }
            }
            //Check time is in order
            float previoustime = -9999;  
            for (int row = 2; row <= xlrowCount; row++)
            {
                var t = iWorksheet.Cell(row, 1).Value.ToString();
                if (!Single.TryParse(t, out float time)  || time < previoustime)
                {
                    MessageBox.Show("The imported timings are not in order.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                    return;
                }
                previoustime = time;
            }
            //CHECK Every cell has a numeric value
            foreach(var cell in iWorksheet.RangeUsed().Cells())
            {
                if(cell.IsNull() || cell.IsEmpty())
                {
                    MessageBox.Show("The sheet contains empty cells.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                    return;
                }
            }

            //Import data to curves
            foreach (var track in InterpCurveTracks)
            {
                //var curveType = track.curveType;
                foreach(var curve in track.Curves)
                {
                    curve.CurvePoints.Clear();
                    string cname = curve.Name;
                    int xlcolumn = curveList.IndexOf(cname) + 2;  //Find correct column offset as XL starts at 1, skip first column (time)

                    for (int xlrow = 2; xlrow < xlrowCount; xlrow++) //Get Excel points start at 2 because top contains headers
                    {
                        var time = iWorksheet.Cell(xlrow, 1).Value.ToString();
                        var outval = iWorksheet.Cell(xlrow, xlcolumn).Value.ToString();
                        if(outval != null && float.TryParse(time, out float t) && float.TryParse(outval, out float v))
                        {
                            CurvePoint point = new CurvePoint(t, v, 0, 0, CurveMode.CIM_CurveAuto);
                            curve.CurvePoints.AddLast(point);
                        }
                        else
                        {
                            MessageBox.Show("Data error. Aborted");
                            return;
                        }
                    }
                }
                Commit();
            }
            MessageBox.Show("Import complete.", "Import Curves");
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

        public override bool CanParse(ExportEntry exportEntry)
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
            return false;
        }

        public override void Dispose()
        {
            UnloadExport();
            if (TrackList.ItemsSource is List<InterpCurve> curvelist)
            {
                foreach (var interpCurve in curvelist)
                {
                    foreach (var curve in interpCurve.Curves)
                    {
                        curve.SaveChanges = null;
                    }
                }
            }
        }

        private void ImportFromExcel_Click(object sender, RoutedEventArgs e)
        {
            ImportCurvesFromXLS();
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportCurvesToXLS();
        }
    }
}
