using ME3Explorer.Packages;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ME3Explorer.Pathfinding_Editor
{
    public partial class ReachSpecRecalculator : Form
    {
        PathfindingEditor pe;
        private BackgroundWorker worker;
        public const int MAX_DISTANCE_TOLERANCE = 5;
        public const float MAX_DIRECTION_TOLERANCE = .001f;

        public ReachSpecRecalculator(PathfindingEditor pe)
        {
            this.pe = pe;
            InitializeComponent();
            CenterToParent();
        }

        private void recalculateButton_Click(object sender, EventArgs e)
        {
            worker = new System.ComponentModel.BackgroundWorker();
            recalculateButton.Enabled = false;
            reachSpecProgressBar.Style = ProgressBarStyle.Marquee;
            worker.WorkerReportsProgress = true;
            worker.DoWork += reachSpecCalculatorThread_DoWork;
            worker.ProgressChanged += reachSpecCalculatorThread_ProgressChanged;
            worker.RunWorkerCompleted += reachSpecCalculatorThread_Done;

            BGThreadOptions bgo = new BGThreadOptions();
            bgo.pcc = pe.pcc;
            bgo.readOnly = readOnlyCheckbox.Checked;

            worker.RunWorkerAsync(bgo);
        }

        private void reachSpecCalculatorThread_Done(object sender, RunWorkerCompletedEventArgs e)
        {
            //throw new NotImplementedException();
            recalculateButton.Enabled = true;
        }

        private void reachSpecCalculatorThread_DoWork(object sender, DoWorkEventArgs e)
        {
            worker.ReportProgress(-1, "Getting list of all used ReachSpecs in pcc");
            //Figure out which exports have PathList.
            BGThreadOptions bgo = (BGThreadOptions)e.Argument;
            IMEPackage pcc = bgo.pcc;
            HashSet<int> reachSpecExportIndexes = new HashSet<int>();
            foreach (IExportEntry exp in pcc.Exports)
            {
                ArrayProperty<ObjectProperty> pathList = exp.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                if (pathList != null)
                {
                    foreach (ObjectProperty reachSpecObj in pathList)
                    {
                        reachSpecExportIndexes.Add(reachSpecObj.Value - 1);
                    }
                }
            }
            worker.ReportProgress(-1, "Calculating " + reachSpecExportIndexes.Count + " reachspecs");

            int currentCalculationNum = 1; //Start at 1 cause humans.
            int numNeedingRecalc = 0;
            foreach (int specExportId in reachSpecExportIndexes)
            {
                //worker.ReportProgress(-1, "Calculating reachspecs [" + currentCalculationNum + "/" + reachSpecExportIndexes.Count + "]");
                IExportEntry exp = pcc.Exports[specExportId];
                bool needsRecalc = calculateReachSpec(exp, bgo.readOnly);
                if (needsRecalc)
                {
                    numNeedingRecalc++;
                }
                double percent = (currentCalculationNum / reachSpecExportIndexes.Count) * 100;
                int feedPercent = RoundDoubleToInt(percent);
                worker.ReportProgress(feedPercent);
                currentCalculationNum++;
            }


            if (bgo.readOnly)
            {
                if (numNeedingRecalc == 0)
                {
                    worker.ReportProgress(-1, "No reachspecs need updated.");
                }
                else
                {
                    worker.ReportProgress(-1, numNeedingRecalc + " reachspec" + ((numNeedingRecalc > 1) ? "s" : "") + " need updated.");
                }
            }
            else
            {
                if (numNeedingRecalc == 0)
                {
                    worker.ReportProgress(-1, "No reachspecs needed updating.");
                }
                else
                {
                    worker.ReportProgress(-1, numNeedingRecalc + " reachspec" + ((numNeedingRecalc > 1) ? "s" : "") + " have been updated.");
                }
            }

        }

        private void reachSpecCalculatorThread_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage >= 0)
            {
                reachSpecProgressBar.Style = ProgressBarStyle.Continuous;
                reachSpecProgressBar.Value = e.ProgressPercentage;
                return;
            }

            if (e.ProgressPercentage == -1)
            {
                progressLabel.Visible = true;
                progressLabel.Text = (string)e.UserState;
                return;
            }

            //Recalculate reachspec on UI thread, because the shared memory implementation doesn't support background thread updates...
            if (e.ProgressPercentage == -2)
            {
                ReachSpecUpdaterUIThreadOptions o = (ReachSpecUpdaterUIThreadOptions)e.UserState;
                recalculateReachSpec(o.reachSpecExport, o.calculatedProperDistance, o.dirX, o.dirY, o.dirZ);

            }
        }

        private bool calculateReachSpec(IExportEntry reachSpecExport, bool readOnly, IExportEntry startNodeExport = null)
        {

            //Get start and end exports.
            var properties = reachSpecExport.GetProperties();
            ObjectProperty start = properties.GetProp<ObjectProperty>("Start");
            StructProperty end = properties.GetProp<StructProperty>("End");

            ObjectProperty endActorObj = end.GetProp<ObjectProperty>("Actor");

            if (start.Value > 0 && endActorObj.Value > 0)
            {
                //We should capture GUID here

                IExportEntry startNode = reachSpecExport.FileRef.Exports[start.Value - 1];
                IExportEntry endNode = reachSpecExport.FileRef.Exports[endActorObj.Value - 1];

                if (startNodeExport != null && startNode.Index != startNodeExport.Index)
                {
                    //ERROR!
                    MessageBox.Show(reachSpecExport.Index + " " + reachSpecExport.ObjectName + " start does not match it's containing pathlist reference (" + startNodeExport.Index + " " + startNodeExport.ObjectName + ")");
                }

                float startX = 0, startY = 0, startZ = 0;
                float destX = 0, destY = 0, destZ = 0;

                StructProperty startLocationProp = startNode.GetProperty<StructProperty>("location");
                StructProperty endLocationProp = endNode.GetProperty<StructProperty>("location");

                if (startLocationProp != null && endLocationProp != null)
                {
                    startX = startLocationProp.GetProp<FloatProperty>("X");
                    startY = startLocationProp.GetProp<FloatProperty>("Y");
                    startZ = startLocationProp.GetProp<FloatProperty>("Z");
                    destX = endLocationProp.GetProp<FloatProperty>("X");
                    destY = endLocationProp.GetProp<FloatProperty>("Y");
                    destZ = endLocationProp.GetProp<FloatProperty>("Z");

                    Point3D startPoint = new Point3D(startX, startY, startZ);
                    Point3D destPoint = new Point3D(destX, destY, destZ);

                    double distance = startPoint.getDistanceToOtherPoint(destPoint);
                    if (distance != 0)
                    {
                        float dirX = (float)((destPoint.X - startPoint.X) / distance);
                        float dirY = (float)((destPoint.Y - startPoint.Y) / distance);
                        float dirZ = (float)((destPoint.Z - startPoint.Z) / distance);


                        //Get Original Values, for comparison.
                        StructProperty specDirection = properties.GetProp<StructProperty>("Direction");
                        float origX = 0, origY = 0, origZ = 0;
                        if (specDirection != null)
                        {
                            origX = specDirection.GetProp<FloatProperty>("X");
                            origY = specDirection.GetProp<FloatProperty>("Y");
                            origZ = specDirection.GetProp<FloatProperty>("Z");
                            IntProperty origDistanceProp = properties.GetProp<IntProperty>("Distance");
                            if (origDistanceProp != null)
                            {
                                int origDistance = origDistanceProp.Value;
                                int calculatedProperDistance = RoundDoubleToInt(distance);
                                int distanceDiff = Math.Abs(origDistance - calculatedProperDistance);
                                ReachSpecUpdaterUIThreadOptions recalcOption = new ReachSpecUpdaterUIThreadOptions(reachSpecExport, calculatedProperDistance, dirX, dirY, dirZ);

                                if (distanceDiff > MAX_DISTANCE_TOLERANCE)
                                {
                                    // Difference.
                                    Debug.WriteLine("Diff Distance is > tolerance: " + distanceDiff + ", should be " + calculatedProperDistance);
                                    if (!readOnly)
                                    {
                                        worker.ReportProgress(-2, recalcOption);
                                    }
                                    return true;
                                }

                                float diffX = origX - dirX;
                                float diffY = origY - dirY;
                                float diffZ = origZ - dirZ;
                                if (Math.Abs(diffX) > MAX_DIRECTION_TOLERANCE)
                                {
                                    // Difference.
                                    Debug.WriteLine("Diff Direction X is > tolerance: " + diffX + ", should be " + dirX);
                                    if (!readOnly)
                                    {
                                        worker.ReportProgress(-2, recalcOption);
                                    }
                                    return true;
                                }
                                if (Math.Abs(diffY) > MAX_DIRECTION_TOLERANCE)
                                {
                                    // Difference.
                                    Debug.WriteLine("Diff Direction Y is > tolerance: " + diffY + ", should be " + dirY);
                                    if (!readOnly)
                                    {
                                        worker.ReportProgress(-2, recalcOption);
                                    }
                                    return true;

                                }
                                if (Math.Abs(diffZ) > MAX_DIRECTION_TOLERANCE)
                                {
                                    // Difference.
                                    Debug.WriteLine("Diff Direction Z is > tolerance: " + diffZ + ", should be " + dirZ);
                                    if (!readOnly)
                                    {
                                        worker.ReportProgress(-2, recalcOption);
                                    }
                                    return true;
                                }

                                return false;
                            }
                        }

                    }



                }
            }
            //We really shouldn't reach here, hopefully.
            return false;

        }

        private void recalculateReachSpec(IExportEntry reachSpecExport, int calculatedProperDistance, float dirX, float dirY, float dirZ)
        {
            Unreal.PropertyCollection props = reachSpecExport.GetProperties();
            IntProperty prop = props.GetProp<IntProperty>("Distance");
            StructProperty directionProp = props.GetProp<StructProperty>("Direction");
            FloatProperty propX = directionProp.GetProp<FloatProperty>("X");
            FloatProperty propY = directionProp.GetProp<FloatProperty>("Y");
            FloatProperty propZ = directionProp.GetProp<FloatProperty>("Z");

            prop.Value = calculatedProperDistance;
            propX.Value = dirX;
            propY.Value = dirY;
            propZ.Value = dirZ;

            reachSpecExport.WriteProperties(props);
        }

        /// <summary>
        /// Rounds a double to an int. Because apparently Microsoft doesn't know how to round numbers.
        /// </summary>
        /// <param name="d">Double to round</param>
        /// <returns>Rounded int</returns>
        static int RoundDoubleToInt(double d)
        {
            if (d < 0)
            {
                return (int)(d - 0.5);
            }
            return (int)(d + 0.5);
        }


    }
    class BGThreadOptions
    {
        public IMEPackage pcc;
        public bool readOnly;

        public BGThreadOptions()
        {

        }
    }

    class ReachSpecUpdaterUIThreadOptions
    {
        public IExportEntry reachSpecExport;
        public int calculatedProperDistance;
        public float dirX;
        public float dirY;
        public float dirZ;

        public ReachSpecUpdaterUIThreadOptions(IExportEntry reachSpecExport, int calculatedProperDistance, float dirX, float dirY, float dirZ)
        {
            this.reachSpecExport = reachSpecExport;
            this.calculatedProperDistance = calculatedProperDistance;
            this.dirX = dirX;
            this.dirY = dirY;
            this.dirZ = dirZ;
        }
    }
}
