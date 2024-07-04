using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ClosedXML.Excel;
using LegendaryExplorer.Misc;
using LegendaryExplorer.UserControls.SharedToolControls;
using LegendaryExplorer.UserControls.SharedToolControls.Curves;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    /// <summary>
    /// Interaction logic for CurveEditor.xaml
    /// </summary>
    public sealed partial class CurveEditor : ExportLoaderControl
    {
        public List<CurveEdInterpCurve> InterpCurveTracks;
        
        /// <summary>
        /// Indicates the status of this export loader
        /// </summary>
        public new bool IsLoaded;
        /// <summary>
        /// Indicates if the export loader was ever in the loaded state while the current export was active. If it wasn't, we should not write out changes.
        /// </summary>
        public bool WasLoadedThisExport;

        public float Time
        {
            get
            {
                float time = 0;
                if (InterpCurveTracks != null)
                {
                    foreach (Curve curve in InterpCurveTracks.SelectMany(interpCurve => interpCurve.Curves))
                    {
                        if (curve.CurvePoints.Last?.Value.InVal is float inVal && inVal > time)
                        {
                            time = inVal;
                        }
                    }
                }

                return time;
            }
        }

        public CurveEditor() : base("Curve Editor")
        {
            InitializeComponent();
        }

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                var elhw = new ExportLoaderHostedWindow(new CurveEditor(), CurrentLoadedExport)
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
                string fullPath = exportEntry.InstancedFullPath;
                int mainSeqIdx = fullPath.IndexOf("Main_Sequence.");
                if (mainSeqIdx >= 0)
                {
                    fullPath = fullPath.Substring("Main_Sequence.".Length + mainSeqIdx);
                }
                CurrentExportName_TextBlock.Text = fullPath;
                btnClamped.Visibility = CurrentLoadedExport.Game switch
                {
                    MEGame.ME1 => Visibility.Collapsed,
                    MEGame.ME2 => Visibility.Collapsed,
                    _ => Visibility.Visible
                };

                // If the export loader is 'loaded' (e.g. tab was selected in a tab control or is visible)
                // we should mark that the curve editor was loaded
                WasLoadedThisExport = IsLoaded;
            }
        }

        public CurveEditor(ExportEntry exp) : base("Curve Editor")
        {
            InitializeComponent();
            LoadExport(exp);
        }

        private void Load()
        {
            InterpCurveTracks = new List<CurveEdInterpCurve>();

            var props = CurrentLoadedExport.GetProperties();
            foreach (var prop in props)
            {
                if (prop is StructProperty structProp)
                {
                    if (Enum.TryParse(structProp.StructType, out CurveType _))
                    {
                        InterpCurveTracks.Add(new CurveEdInterpCurve(CurrentLoadedExport.FileRef, structProp));
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
                case EInterpCurveMode.CIM_Linear:
                    btnLinear.IsChecked = true;
                    break;
                case EInterpCurveMode.CIM_CurveAuto:
                    btnAuto.IsChecked = true;
                    break;
                case EInterpCurveMode.CIM_Constant:
                    btnConstant.IsChecked = true;
                    break;
                case EInterpCurveMode.CIM_CurveUser:
                    btnUser.IsChecked = true;
                    break;
                case EInterpCurveMode.CIM_CurveBreak:
                    btnBreak.IsChecked = true;
                    break;
                case EInterpCurveMode.CIM_CurveAutoClamped:
                    btnClamped.IsChecked = true;
                    break;
            }
        }

        private void btnInterpMode_Click(object sender, RoutedEventArgs e)
        {
            CurvePoint selectedPoint = graph.SelectedPoint;
            if (selectedPoint != null)
            {
                selectedPoint.InterpMode = (sender as RadioButton)?.Name switch
                {
                    nameof(btnLinear) => EInterpCurveMode.CIM_Linear,
                    nameof(btnAuto) => EInterpCurveMode.CIM_CurveAuto,
                    nameof(btnConstant) => EInterpCurveMode.CIM_Constant,
                    nameof(btnUser) => EInterpCurveMode.CIM_CurveUser,
                    nameof(btnBreak) => EInterpCurveMode.CIM_CurveBreak,
                    nameof(btnClamped) when Pcc.Game is not (MEGame.ME1 or MEGame.ME2) => EInterpCurveMode.CIM_CurveAutoClamped,
                    _ => selectedPoint.InterpMode
                };
                graph.Paint();
                graph.SelectedPoint = selectedPoint;
            }
        }

        private void Commit()
        {
            if (!CurveGraph.TrackLoading && CurrentLoadedExport is not null)
            {
                var props = CurrentLoadedExport.GetProperties();
                foreach (CurveEdInterpCurve item in InterpCurveTracks)
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
                foreach (var curve in track.Curves)
                {
                    n++;
                    curveList.Add(curve.Name);
                    foreach (var point in curve.CurvePoints)
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
                    if (point != (float)0.12345678) //skip null values
                        worksheet.Cell(xlrow, xlcol).Value = point.ToString();
                }
            }

            var m = new CommonSaveFileDialog
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
            var wdlg = MessageBox.Show("Do you want to import a new curve from Excel and overwrite the existing curve values?\n \nThe sheet must be in the correct format:\n- Headers must match the overwritten curves\n- All cells must contain a value\n- Time values must be ordered.\n- Values only, no links or formulas", "Import Curves", MessageBoxButton.OKCancel);
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

            var oDlg = new OpenFileDialog //Load Excel
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Import Excel table",
                CustomPlaces = AppDirectories.GameCustomPlaces
            };

            if (oDlg.ShowDialog() != true)
                return;

            var Workbook = new XLWorkbook(oDlg.FileName);
            IXLWorksheet iWorksheet;
            if (Workbook.Worksheets.Count() > 1)
            {
                try
                {
                    iWorksheet = Workbook.Worksheet(1);
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

            try
            {
                var xlrowCount = iWorksheet.RowsUsed().Count();
                //Check headers
                for (int hdr = 0; hdr < curveList.Count; hdr++) //skip time (first) column
                {
                    var expected = curveList[hdr];
                    var returned = (string)iWorksheet.Cell(1, hdr + 2).Value; //+2 as XL starts at 1, and skip time column
                    if (expected != returned)
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
                    if (!float.TryParse(t, out float time) || time < previoustime)
                    {
                        MessageBox.Show("The imported timings are not in order.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                        return;
                    }
                    previoustime = time;
                }
                //CHECK Every cell has a numeric value
                foreach (var cell in iWorksheet.RangeUsed().Cells())
                {
                    if (cell.IsNull() || cell.IsEmpty())
                    {
                        MessageBox.Show("The sheet contains empty cells.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                        return;
                    }
                    if (cell.Address.RowNumber > 1 && !float.TryParse(cell.Value.ToString(), out float f))
                    {
                        MessageBox.Show("The values contain text.\nPlease check import sheet.  Aborting.", "Import Curves", MessageBoxButton.OK);
                        return;
                    }
                }

                //Import data to curves
                foreach (var track in InterpCurveTracks)
                {
                    foreach (var curve in track.Curves)
                    {
                        curve.CurvePoints.Clear();
                        string cname = curve.Name;
                        int xlcolumn = curveList.IndexOf(cname) + 2;  //Find correct column offset as XL starts at 1, skip first column (time)

                        for (int xlrow = 2; xlrow <= xlrowCount; xlrow++) //Get Excel points start at 2 because top contains headers
                        {
                            var time = iWorksheet.Cell(xlrow, 1).Value.ToString();
                            var outval = iWorksheet.Cell(xlrow, xlcolumn).Value.ToString();
                            if (outval != null && float.TryParse(time, out float t) && float.TryParse(outval, out float v))
                            {
                                var point = new CurvePoint(t, v, 0, 0, EInterpCurveMode.CIM_CurveUser);
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
            catch (Exception e)
            {
                MessageBox.Show("Import failed. Check Import data.\n", "Error");
#if DEBUG
                MessageBox.Show($"{e.FlattenException()}", "Error");
#endif
            }
        }

        public override void UnloadExport()
        {
            // Do not commit changes if we were never even visible while the export was visible.
            if (WasLoadedThisExport)
            {
                Commit();
            }

            graph.Clear();
            InterpCurveTracks = null;
            CurrentLoadedExport = null;
            CurrentExportName_TextBlock.Text = null;
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
            if (TrackList.ItemsSource is List<CurveEdInterpCurve> curvelist)
            {
                foreach (var interpCurve in curvelist)
                {
                    foreach (var curve in interpCurve.Curves)
                    {
                        curve.SaveChanges = null;
                    }
                }
            }
            graph.Clear();
            graph.Dispose();
        }

        private void ImportFromExcel_Click(object sender, RoutedEventArgs e)
        {
            ImportCurvesFromXLS();
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportCurvesToXLS();
        }

        private void ExportSingleCurveToExcel_Click(object sender, RoutedEventArgs e)
        {
            var curve = graph.SelectedCurve;
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Curve");
            //Setup XL
            worksheet.Cell(1, 1).Value = "Time";
            worksheet.Cell(1, 2).Value = curve.Name;
            int xlrow = 1;
            //write data to list
            foreach (var point in curve.CurvePoints)
            {
                xlrow++;
                float time = point.InVal;
                float value = point.OutVal;
                worksheet.Cell(xlrow, 1).Value = point.InVal;
                worksheet.Cell(xlrow, 2).Value = point.OutVal;
            }

            CommonSaveFileDialog m = new CommonSaveFileDialog
            {
                Title = "Select excel output",
                DefaultFileName = $"{CurrentLoadedExport.ObjectNameString}_{CurrentLoadedExport.UIndex}_{curve.Name}.xlsx",
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
                    MessageBox.Show($"Curve exported to {System.IO.Path.GetFileName(m.FileName)}.");
                }
                catch
                {
                    MessageBox.Show($"Save to {System.IO.Path.GetFileName(m.FileName)} failed.\nCheck the excel file is not open.");
                }
            }
        }

        private void SetReferenceCurve(object sender, RoutedEventArgs e)
        {
            graph.ComparisonCurve = graph.SelectedCurve;
            graph.Paint();
        }

        private void CurveEditor_GotFocus(object sender, RoutedEventArgs e)
        {
            IsLoaded = true;
            WasLoadedThisExport = true;
        }

        private void CurveEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            IsLoaded = false;
        }
    }
}
