using FontAwesome5;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Numerics;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI;
using LegendaryExplorer.SharedUI.Controls;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using InterpCurveVector = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<System.Numerics.Vector3>;
using InterpCurveFloat = LegendaryExplorerCore.Unreal.BinaryConverters.InterpCurve<float>;

namespace LegendaryExplorer.Tools.PathfindingEditor
{
    /// <summary>
    /// Interaction logic for ValidationPanel.xaml
    /// </summary>
    public partial class ValidationPanel : NotifyPropertyChangedControlBase
    {
        private const int MAX_DISTANCE_TOLERANCE = 5;
        private const float MAX_DIRECTION_TOLERANCE = .001f;

        private IMEPackage _pcc;

        public ExportEntry PersistentLevel { get; private set; }
        public IMEPackage Pcc { get => _pcc; private set => SetProperty(ref _pcc, value); }
        BackgroundWorker fixAndValidateWorker;
        public ObservableCollectionExtended<ListBoxTask> ValidationTasks { get; } = new();
        private readonly object _myCollectionLock = new();

        private string _lastRunOnText;
        public string LastRunOnText { get => _lastRunOnText; set => SetProperty(ref _lastRunOnText, value); }

        public ValidationPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            BindingOperations.EnableCollectionSynchronization(ValidationTasks, _myCollectionLock);
        }

        public void SetLevel(ExportEntry persistentLevel)
        {
            PersistentLevel = persistentLevel;
            Pcc = PersistentLevel.FileRef;
            LastRunOnText = "Not yet run";
        }

        public void UnloadPackage()
        {
            PersistentLevel = null;
            Pcc = null;
        }

        public ICommand FixAndValidateCommand { get; set; }

        private void LoadCommands()
        {
            FixAndValidateCommand = new GenericCommand(FixAndValidate, CanFixAndValidate);
        }

        private bool CanFixAndValidate()
        {
            return Pcc != null && (fixAndValidateWorker == null || fixAndValidateWorker.IsBusy == false);
        }

        private void FixAndValidate()
        {
            if (Pcc != null && (fixAndValidateWorker == null || fixAndValidateWorker.IsBusy == false))
            {
                ValidationTasks.ClearEx();
                fixAndValidateWorker = new BackgroundWorker();
                fixAndValidateWorker.DoWork += Background_FixAndValidate;
                fixAndValidateWorker.RunWorkerCompleted += FixAndValidate_Completed;

                fixAndValidateWorker.RunWorkerAsync();
                CommandManager.InvalidateRequerySuggested(); //Recalculate commands.
            }
        }

        private static void FixAndValidate_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested(); //Recalculate commands.
        }

        private void Background_FixAndValidate(object sender, DoWorkEventArgs e)
        {
            var task = new ListBoxTask("Recalculating reachspecs");
            ValidationTasks.Add(task);
            RecalculateReachspecs(task);

            task = new ListBoxTask("Fixing stack headers");
            ValidationTasks.Add(task);
            FixStackHeaders(task);

            task = new ListBoxTask("Finding duplicate GUIDs");
            ValidationTasks.Add(task);
            FindDuplicateGUIDs(task);

            task = new ListBoxTask("Relinking pathfinding chain");
            ValidationTasks.Add(task);
            RelinkPathfindingChain(task);

            task = new ListBoxTask("Recalculating SplineComponents");
            ValidationTasks.Add(task);
            RecalculateSplineComponents(Pcc, task);

            LastRunOnText = "Last ran at " + DateTime.Now;
        }

        public static void RecalculateSplineComponents(IMEPackage mePackage, ListBoxTask task = null)
        {
            int numRecalculated = 0;
            foreach (ExportEntry splineActor in mePackage.Exports.Where(exp => exp.ClassName == "SplineActor"))
            {
                if (splineActor.GetProperty<ArrayProperty<StructProperty>>("Connections") is { } connections && connections.Any())
                {
                    Vector3 location = CommonStructs.GetVector3(splineActor, "location", Vector3.Zero);
                    Vector3 splineActorTangent = CommonStructs.GetVector3(splineActor, "SplineActorTangent", new Vector3(300f, 0f, 0f));

                    foreach (StructProperty connection in connections)
                    {
                        int splineComponentUIndex = connection.GetProp<ObjectProperty>("SplineComponent")?.Value ?? 0;
                        int connectedActorUIndex = connection.GetProp<ObjectProperty>("ConnectTo")?.Value ?? 0;
                        if (mePackage.TryGetUExport(splineComponentUIndex, out ExportEntry splineComponent) && mePackage.TryGetUExport(connectedActorUIndex, out ExportEntry connectedActor))
                        {
                            if (task != null)
                            {
                                task.Header = $"Recalculating SplineComponents {((double)splineComponent.UIndex / mePackage.ExportCount):P}";
                            }

                            var splineInfo = new InterpCurveVector();
                            Vector3 tangent = ActorUtils.GetLocalToWorld(splineActor).TransformNormal(splineActorTangent);

                            splineInfo.AddPoint(0f, location, tangent, tangent, EInterpCurveMode.CIM_CurveUser);

                            Vector3 connectedActorLocation = CommonStructs.GetVector3(connectedActor, "location", Vector3.Zero);
                            Vector3 connectedActorSplineActorTangent = CommonStructs.GetVector3(connectedActor, "SplineActorTangent", new Vector3(300f, 0f, 0f));

                            tangent = ActorUtils.GetLocalToWorld(connectedActor).TransformNormal(connectedActorSplineActorTangent);

                            splineInfo.AddPoint(1f, connectedActorLocation, tangent, tangent, EInterpCurveMode.CIM_CurveUser);

                            splineComponent.WriteProperty(splineInfo.ToStructProperty(mePackage.Game, "SplineInfo"));

                            var splineReparamTable = new InterpCurveFloat();
                            if (splineInfo.Points.Count > 1)
                            {
                                const int steps = 10;
                                float totalDist = 0;

                                float input = splineInfo.Points[0].InVal;
                                float end = splineInfo.Points.Last().InVal;
                                float interval = (end - input) / (steps - 1);
                                Vector3 oldPos = splineInfo.Eval(input, Vector3.Zero);
                                Vector3 startPos = oldPos;
                                Vector3 newPos = startPos;
                                splineReparamTable.AddPoint(totalDist, input);
                                input += interval;
                                for (int i = 1; i < steps; i++)
                                {
                                    newPos = splineInfo.Eval(input, Vector3.Zero);
                                    totalDist += (newPos - oldPos).Length();
                                    oldPos = newPos;
                                    splineReparamTable.AddPoint(totalDist, input);
                                    input += interval;
                                }

                                if (totalDist != 0f)
                                {
                                    splineComponent.WriteProperty(new FloatProperty((startPos - newPos).Length() / totalDist, "SplineCurviness"));
                                }
                            }

                            splineComponent.WriteProperty(splineReparamTable.ToStructProperty(mePackage.Game, "SplineReparamTable"));
                            numRecalculated++;
                        }
                    }
                }
            }
            task?.Complete($"{numRecalculated} SplineComponent{(numRecalculated == 1 ? " was" : "s were")} recalculated");
        }

        public void RecalculateReachspecs(ListBoxTask task = null)
        {
            //Figure out which exports have PathList.
            var reachSpecExportIndexes = new HashSet<int>();
            var badSpecs = new List<string>();
            //HashSet<string> names = new HashSet<string>();

            int numRecalculated = 0;
            for (int i = 0; i < Pcc.Exports.Count; i++)
            {
                ExportEntry exp = Pcc.Exports[i];
                var pathList = exp.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
                if (pathList != null)
                {
                    if (task != null)
                    {
                        task.Header = $"Recalculating reachspecs {(int)(i * 100.0 / Pcc.ExportCount)}%";
                    }
                    foreach (ObjectProperty reachSpecObj in pathList)
                    {
                        //reachSpecExportIndexes.Add(reachSpecObj.Value - 1);
                        bool isBad = false;
                        if (Pcc.TryGetUExport(reachSpecObj.Value, out ExportEntry spec))
                        {
                            var specProps = spec.GetProperties();
                            ObjectProperty start = specProps.GetProp<ObjectProperty>("Start");
                            if (start.Value != exp.UIndex)
                            {
                                isBad = true;
                                badSpecs.Add($"{reachSpecObj.Value} {spec.ObjectName.Instanced} start value does not match the node that references it ({exp.UIndex})");
                            }

                            //get end
                            StructProperty end = specProps.GetProp<StructProperty>("End");
                            ObjectProperty endActorObj = end.GetProp<ObjectProperty>(PathEdUtils.GetReachSpecEndName(spec));
                            if (endActorObj.Value == start.Value)
                            {
                                isBad = true;
                                badSpecs.Add($"{reachSpecObj.Value} {spec.ObjectName.Instanced} start and end property is the same. This will crash the game.");
                            }

                            var guid = new FGuid(end.GetProp<StructProperty>("Guid"));
                            if ((guid.A | guid.B | guid.C | guid.D) == 0 && endActorObj.Value == 0)
                            {
                                isBad = true;
                                badSpecs.Add($"{reachSpecObj.Value} {spec.ObjectName.Instanced} has no external guid and has no endactor.");
                            }
                            if (endActorObj.Value > Pcc.ExportCount || endActorObj.Value < 0)
                            {
                                isBad = true;
                                badSpecs.Add($"{reachSpecObj.Value} {spec.ObjectName.Instanced} has invalid end property (past end of bounds or less than 0).");
                            }
                            /*if (endActorObj.Value > 0)
                            {
                                ExportEntry expo = cc.Exports[endActorObj.Value - 1];
                                names.Add(expo.ClassName);
                            }*/
                            //
                            if (!isBad)
                            {
                                Debug.WriteLine($"Calculating {spec.UIndex.ToString()} {spec.ClassName}");
                                if (calculateReachSpec(spec))
                                {
                                    numRecalculated++;
                                }
                            }
                        }
                        else
                        {
                            badSpecs.Add($"{reachSpecObj.Value} is incorrectly included in the path list.");
                        }
                    }
                }
            }
            task?.Complete($"{numRecalculated} ReachSpec{(numRecalculated == 1 ? " was" : "s were")} recalculated");
        }

        private bool calculateReachSpec(ExportEntry reachSpecExport, ExportEntry startNodeExport = null)
        {
            //Get start and end exports.
            var properties = reachSpecExport.GetProperties();
            ObjectProperty start = properties.GetProp<ObjectProperty>("Start");
            StructProperty end = properties.GetProp<StructProperty>("End");

            ObjectProperty endActorObj = end.GetProp<ObjectProperty>(PathEdUtils.GetReachSpecEndName(reachSpecExport));

            if (start.Value <= 0 || endActorObj.Value <= 0) return false;

            //We should capture GUID here

            ExportEntry startNode = reachSpecExport.FileRef.GetUExport(start.Value);
            ExportEntry endNode = reachSpecExport.FileRef.GetUExport(endActorObj.Value);

            if (startNodeExport != null && startNode.UIndex != startNodeExport.UIndex)
            {
                //ERROR!
                ValidationTasks.Add(new ListBoxTask
                {
                    Header = $"{reachSpecExport.UIndex} {reachSpecExport.ObjectName.Instanced} start does not match it's containing pathlist reference ({startNodeExport.UIndex} {startNodeExport.ObjectName.Instanced})",
                    Icon = EFontAwesomeIcon.Solid_Times,
                    Foreground = Brushes.Red,
                    Spinning = false
                });

                //MessageBox.Show(
            }

            StructProperty startLocationProp = startNode.GetProperty<StructProperty>("location");
            StructProperty endLocationProp = endNode.GetProperty<StructProperty>("location");

            if (startLocationProp == null || endLocationProp == null) return false;

            float startX = startLocationProp.GetProp<FloatProperty>("X");
            float startY = startLocationProp.GetProp<FloatProperty>("Y");
            float startZ = startLocationProp.GetProp<FloatProperty>("Z");
            float destX = endLocationProp.GetProp<FloatProperty>("X");
            float destY = endLocationProp.GetProp<FloatProperty>("Y");
            float destZ = endLocationProp.GetProp<FloatProperty>("Z");

            var startPoint = new Point3D(startX, startY, startZ);
            var destPoint = new Point3D(destX, destY, destZ);

            double distance = startPoint.getDistanceToOtherPoint(destPoint);
            if (distance == 0) return false;

            float dirX = (float)((destPoint.X - startPoint.X) / distance);
            float dirY = (float)((destPoint.Y - startPoint.Y) / distance);
            float dirZ = (float)((destPoint.Z - startPoint.Z) / distance);

            //Get Original Values, for comparison.
            StructProperty specDirection = properties.GetProp<StructProperty>("Direction");
            if (specDirection == null) return false;

            float origX = specDirection.GetProp<FloatProperty>("X");
            float origY = specDirection.GetProp<FloatProperty>("Y");
            float origZ = specDirection.GetProp<FloatProperty>("Z");
            IntProperty origDistanceProp = properties.GetProp<IntProperty>("Distance");
            if (origDistanceProp == null) return false;

            int origDistance = origDistanceProp.Value;
            int calculatedProperDistance = PathEdUtils.RoundDoubleToInt(distance);
            int distanceDiff = Math.Abs(origDistance - calculatedProperDistance);

            if (distanceDiff > MAX_DISTANCE_TOLERANCE)
            {
                // Difference.
                Debug.WriteLine("Diff Distance is > tolerance: " + distanceDiff + ", proper value should be " + calculatedProperDistance);
                reachSpecExport.WriteProperties(ApplyReachSpecCalculation(properties, calculatedProperDistance, dirX, dirY, dirZ));
                return true;
            }

            float diffX = origX - dirX;
            float diffY = origY - dirY;
            float diffZ = origZ - dirZ;
            if (Math.Abs(diffX) > MAX_DIRECTION_TOLERANCE)
            {
                // Difference.
                Debug.WriteLine("Diff Direction X is > tolerance: " + diffX + ", should be " + dirX);
                return true;
            }
            if (Math.Abs(diffY) > MAX_DIRECTION_TOLERANCE)
            {
                // Difference.
                Debug.WriteLine("Diff Direction Y is > tolerance: " + diffY + ", should be " + dirY);
                return true;
            }
            if (Math.Abs(diffZ) > MAX_DIRECTION_TOLERANCE)
            {
                // Difference.
                Debug.WriteLine("Diff Direction Z is > tolerance: " + diffZ + ", should be " + dirZ);
                return true;
            }

            return false;
            //We really shouldn't reach here, hopefully.
        }

        private static PropertyCollection ApplyReachSpecCalculation(PropertyCollection props, int calculatedProperDistance, float dirX, float dirY, float dirZ)
        {
            IntProperty prop = props.GetProp<IntProperty>("Distance");
            StructProperty directionProp = props.GetProp<StructProperty>("Direction");
            FloatProperty propX = directionProp.GetProp<FloatProperty>("X");
            FloatProperty propY = directionProp.GetProp<FloatProperty>("Y");
            FloatProperty propZ = directionProp.GetProp<FloatProperty>("Z");

            prop.Value = calculatedProperDistance;
            propX.Value = dirX;
            propY.Value = dirY;
            propZ.Value = dirZ;

            return props;
        }

        public void FixStackHeaders(ListBoxTask task = null)
        {
            var mpIDs = new Dictionary<int, List<int>>();
            Debug.WriteLine("Start of header fix scan===================");

            //start full scan.
            int itemcount = 2;

            //This will update netindex'es for all items that start with TheWorld.PersistentLevel and are 3 items long.

            var NetIndexesInUse = new List<int>();
            foreach (ExportEntry exportEntry in Pcc.Exports)
            {
                string path = exportEntry.FullPath;
                string[] pieces = path.Split('.');

                if (pieces.Length < 3 || pieces[0] != "TheWorld" || pieces[1] != "PersistentLevel")
                {
                    continue;
                }
            }

            int nextNetIndex = 1;
            foreach (ExportEntry exportEntry in Pcc.Exports)
            {
                string path = exportEntry.FullPath;
                string[] pieces = path.Split('.');

                if (pieces.Length < 3 || pieces[0] != "TheWorld" || pieces[1] != "PersistentLevel")
                {
                    if (exportEntry.HasStack)
                    {
                        if (exportEntry.NetIndex >= 0)
                        {
                            Debug.WriteLine("Updating netindex on " + exportEntry.InstancedFullPath + " from " + exportEntry.NetIndex);
                            exportEntry.NetIndex = nextNetIndex++;
                        }
                    }

                    continue;
                }

                if (exportEntry.HasStack)
                {
                    byte[] exportData = exportEntry.Data;
                    int classId1 = BitConverter.ToInt32(exportData, 0);
                    int classId2 = BitConverter.ToInt32(exportData, 4);
                    //Debug.WriteLine(maybe_MPID);

                    int metadataClass = exportEntry.IsClass ? 0 : exportEntry.Class.UIndex;
                    if (classId1 != metadataClass || classId2 != metadataClass)
                    {
                        Debug.WriteLine($"Updating class type at start of export data {exportEntry.UIndex} {exportEntry.ClassName}");
                        //Update unreal header
                        exportData.OverwriteRange(0, BitConverter.GetBytes(metadataClass));
                        exportData.OverwriteRange(4, BitConverter.GetBytes(metadataClass));
                        exportEntry.Data = exportData;
                    }

                    // probably shouldn't do this...
                    if (exportEntry.NetIndex >= 0)
                    {
                        Debug.WriteLine("Updating netindex on " + exportEntry.InstancedFullPath + " from " + exportEntry.NetIndex);
                        exportEntry.NetIndex = nextNetIndex++;
                    }
                }

                itemcount++;
            }
            task?.Complete($"{itemcount} stack headers updated");
        }

        public void RelinkPathfindingChain(ListBoxTask task = null)
        {
            if (Pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is ExportEntry levelExport)
            {
                Level level = ObjectBinary.From<Level>(levelExport);
                var validNodesInLevel = new List<ExportEntry>();
                var validCoverlinksInLevel = new List<ExportEntry>();
                foreach (int actorUIndex in level.Actors)
                {
                    if (Pcc.IsUExport(actorUIndex))
                    {
                        ExportEntry exportEntry = Pcc.GetUExport(actorUIndex);
                        StructProperty navGuid = exportEntry.GetProperty<StructProperty>("NavGuid");
                        if (navGuid != null)
                        {
                            if (exportEntry.ClassName == "CoverLink")
                            {
                                validCoverlinksInLevel.Add(exportEntry);
                            }
                            else
                            {
                                validNodesInLevel.Add(exportEntry);
                            }
                        }
                    }
                }

                // NavChain
                if (validNodesInLevel.Any())
                {
                    // has nav chain

                    // not sure this should be done...
                    level.NavListStart = validNodesInLevel.First().UIndex;
                    level.NavListEnd = validNodesInLevel.Last().UIndex;

                    for (int i = 0; i < validNodesInLevel.Count; i++)
                    {
                        ExportEntry parsingExp = validNodesInLevel[i];
                        if (i != validNodesInLevel.Count - 1)
                        {
                            var nextNavigationPointProp = new ObjectProperty(validNodesInLevel[i + 1].UIndex, "nextNavigationPoint");
                            parsingExp.WriteProperty(nextNavigationPointProp);
                        }
                        else
                        {
                            parsingExp.RemoveProperty("nextNavigationPoint");
                        }
                    }
                }

                //Coverlink Chain
                // Disable until we can figure out why this breaks MP cover
                //if (validCoverlinksInLevel.Any())
                //{
                //    // not sure how this is handled with 1
                //    level.CoverListStart = validCoverlinksInLevel.First().UIndex;
                //    level.CoverListEnd = validCoverlinksInLevel.Last().UIndex;

                //    for (int i = 0; i < validCoverlinksInLevel.Count ; i++)
                //    {
                //        ExportEntry parsingExp = validCoverlinksInLevel[i];
                //        if (i != validCoverlinksInLevel.Count - 1)
                //        {
                //            ObjectProperty nextCoverlink = new ObjectProperty(validCoverlinksInLevel[i + 1].UIndex, "NextCoverLink");
                //            parsingExp.WriteProperty(nextCoverlink);
                //        }
                //        else
                //        {
                //            parsingExp.RemoveProperty("NextCoverLink");
                //        }
                //    }
                //}

                levelExport.WriteBinary(level);
                task?.Complete("NavigationPoint chain has been updated");
            }
            else
            {
                task?.Complete("NavigationPoint chain not updated, no chain found");
            }
        }

        private void FindDuplicateGUIDs(ListBoxTask task = null)
        {
            var navGuidLists = new Dictionary<string, List<FGuid>>();
            var duplicateGuids = new List<FGuid>();

            foreach (int actorUIndex in PersistentLevel.GetBinaryData<Level>().Actors)
            {
                if (Pcc.TryGetUExport(actorUIndex, out ExportEntry exportEntry) 
                 && exportEntry.GetProperty<StructProperty>("NavGuid") is StructProperty navGuid)
                {
                    var nav = new FGuid(navGuid)
                    {
                        export = exportEntry
                    };
                    navGuidLists.AddToListAt(nav.ToString(), nav);
                }
            }

            int numduplicates = 0;
            foreach (List<FGuid> guidList in navGuidLists.Values)
            {
                if (guidList.Count > 1)
                {
                    numduplicates++;
                    Debug.WriteLine($"Number of duplicates: {guidList.Count}");
                    //contains duplicates
                    foreach (FGuid guid in guidList)
                    {
                        //Debug.WriteLine(guid.levelListIndex + " Duplicate: " + guid.export.ObjectName);
                        duplicateGuids.Add(guid);
                        ValidationTasks.Add(new ListBoxTask
                        {
                            Header = $"Duplicate GUID found on export {guid.export?.UIndex} {guid.export?.ObjectName.Instanced}",
                            Icon = EFontAwesomeIcon.Solid_Times,
                            Spinning = false,
                            Foreground = Brushes.Red
                        });
                    }
                }
            }
            task?.Complete($"{numduplicates} duplicate GUID{(numduplicates != 1 ? "s were" : " was")} found");
        }
    }
}
