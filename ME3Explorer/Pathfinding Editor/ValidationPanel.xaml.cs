using FontAwesome5;
using FontAwesome5.WPF;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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

namespace ME3Explorer.Pathfinding_Editor
{
    /// <summary>
    /// Interaction logic for ValidationPanel.xaml
    /// </summary>
    public partial class ValidationPanel : NotifyPropertyChangedControlBase
    {

        private const int MAX_DISTANCE_TOLERANCE = 5;
        private const float MAX_DIRECTION_TOLERANCE = .001f;

        private IMEPackage _pcc;

        public IExportEntry PersistentLevel { get; private set; }
        public IMEPackage Pcc { get => _pcc; private set => SetProperty(ref _pcc, value); }
        BackgroundWorker fixAndValidateWorker;
        public ObservableCollectionExtended<ListBoxTask> ValidationTasks { get; } = new ObservableCollectionExtended<ListBoxTask>();
        private readonly object _myCollectionLock = new object();

        private string _lastRunOnText;
        public string LastRunOnText { get => _lastRunOnText; set => SetProperty(ref _lastRunOnText, value); }

        public ValidationPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            BindingOperations.EnableCollectionSynchronization(ValidationTasks, _myCollectionLock);
        }

        public void SetLevel(IExportEntry persistentLevel)
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
            recalculateReachspecs(task);

            task = new ListBoxTask("Fixing stack headers");
            ValidationTasks.Add(task);
            fixStackHeaders(task);

            task = new ListBoxTask("Finding duplicate GUIDs");
            ValidationTasks.Add(task);
            findDuplicateGUIDs(task);

            task = new ListBoxTask("Relinking pathfinding chain");
            ValidationTasks.Add(task);
            relinkPathfindingChain(task);

            LastRunOnText = "Last ran at " + DateTime.Now;
        }

        public void recalculateReachspecs(ListBoxTask task = null)
        {
            //Figure out which exports have PathList.
            var reachSpecExportIndexes = new HashSet<int>();
            var badSpecs = new List<string>();
            //HashSet<string> names = new HashSet<string>();

            int numRecalculated = 0;
            for (int i = 0; i < Pcc.ExportCount; i++)
            {
                IExportEntry exp = Pcc.Exports[i];
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
                        IExportEntry spec = Pcc.getUExport(reachSpecObj.Value);
                        var specProps = spec.GetProperties();
                        ObjectProperty start = specProps.GetProp<ObjectProperty>("Start");
                        if (start.Value != exp.UIndex)
                        {
                            isBad = true;
                            badSpecs.Add($"{reachSpecObj.Value} {spec.ObjectName} start value does not match the node that references it ({exp.UIndex})");
                        }

                        //get end
                        StructProperty end = specProps.GetProp<StructProperty>("End");
                        ObjectProperty endActorObj = end.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(spec));
                        if (endActorObj.Value == start.Value)
                        {
                            isBad = true;
                            badSpecs.Add($"{reachSpecObj.Value} {spec.ObjectName} start and end property is the same. This will crash the game.");
                        }

                        var guid = new UnrealGUID(end.GetProp<StructProperty>("Guid"));
                        if ((guid.A | guid.B | guid.C | guid.D) == 0 && endActorObj.Value == 0)
                        {
                            isBad = true;
                            badSpecs.Add($"{reachSpecObj.Value} {spec.ObjectName} has no external guid and has no endactor.");
                        }
                        if (endActorObj.Value > Pcc.ExportCount || endActorObj.Value < 0)
                        {
                            isBad = true;
                            badSpecs.Add($"{reachSpecObj.Value} {spec.ObjectName} has invalid end property (past end of bounds or less than 0).");
                        }
                        /*if (endActorObj.Value > 0)
                        {
                            IExportEntry expo = cc.Exports[endActorObj.Value - 1];
                            names.Add(expo.ClassName);
                        }*/
                        //
                        if (!isBad)
                        {
                            if (calculateReachSpec(spec))
                            {
                                numRecalculated++;
                            }
                        }
                    }

                }
            }
            task?.Complete($"{numRecalculated} ReachSpec{(numRecalculated == 1 ? "" : "s")} were recalculated");
        }

        private bool calculateReachSpec(IExportEntry reachSpecExport, IExportEntry startNodeExport = null)
        {

            //Get start and end exports.
            var properties = reachSpecExport.GetProperties();
            ObjectProperty start = properties.GetProp<ObjectProperty>("Start");
            StructProperty end = properties.GetProp<StructProperty>("End");

            ObjectProperty endActorObj = end.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(reachSpecExport));

            if (start.Value <= 0 || endActorObj.Value <= 0) return false;

            //We should capture GUID here

            IExportEntry startNode = reachSpecExport.FileRef.getUExport(start.Value);
            IExportEntry endNode = reachSpecExport.FileRef.getUExport(endActorObj.Value);

            if (startNodeExport != null && startNode.UIndex != startNodeExport.UIndex)
            {
                //ERROR!
                ValidationTasks.Add(new ListBoxTask
                {
                    Header = $"{reachSpecExport.UIndex} {reachSpecExport.ObjectName} start does not match it's containing pathlist reference ({startNodeExport.UIndex} {startNodeExport.ObjectName})",
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

            Point3D startPoint = new Point3D(startX, startY, startZ);
            Point3D destPoint = new Point3D(destX, destY, destZ);

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
            int calculatedProperDistance = SharedPathfinding.RoundDoubleToInt(distance);
            int distanceDiff = Math.Abs(origDistance - calculatedProperDistance);

            if (distanceDiff > MAX_DISTANCE_TOLERANCE)
            {
                // Difference.
                Debug.WriteLine("Diff Distance is > tolerance: " + distanceDiff + ", proper value should be " + calculatedProperDistance);
                reachSpecExport.WriteProperties(applyReachSpecCalculation(properties, calculatedProperDistance, dirX, dirY, dirZ));
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

        private PropertyCollection applyReachSpecCalculation(PropertyCollection props, int calculatedProperDistance, float dirX, float dirY, float dirZ)
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

        public void fixStackHeaders(ListBoxTask task = null)
        {
            int numUpdated = 0;

            var mpIDs = new Dictionary<int, List<int>>();
            Debug.WriteLine("Start of header fix scan===================");

            //start full scan.
            int itemcount = 2;

            //This will update netindex'es for all items that start with TheWorld.PersistentLevel and are 3 items long.

            List<int> NetIndexesInUse = new List<int>();
            foreach (IExportEntry exportEntry in Pcc.Exports)
            {
                string path = exportEntry.GetFullPath;
                string[] pieces = path.Split('.');

                if (pieces.Length < 3 || pieces[0] != "TheWorld" || pieces[1] != "PersistentLevel")
                {
                    continue;
                }

            }

            int nextNetIndex = 1;
            foreach (IExportEntry exportEntry in Pcc.Exports)
            {
                string path = exportEntry.GetFullPath;
                string[] pieces = path.Split('.');

                if (pieces.Length < 3 || pieces[0] != "TheWorld" || pieces[1] != "PersistentLevel")
                {
                    if (exportEntry.HasStack)
                    {
                        if (exportEntry.NetIndex >= 0)
                        {
                            Debug.WriteLine("Updating netindex on " + exportEntry.GetIndexedFullPath+" from "+exportEntry.NetIndex);
                            exportEntry.NetIndex = nextNetIndex++;
                        }
                    }

                    continue;
                }

                int idOffset = 0;
                if (exportEntry.HasStack)
                {
                    byte[] exportData = exportEntry.Data;
                    int classId1 = BitConverter.ToInt32(exportData, 0);
                    int classId2 = BitConverter.ToInt32(exportData, 4);
                    //Debug.WriteLine(maybe_MPID);

                    int metadataClass = exportEntry.idxClass;
                    if (classId1 != metadataClass || classId2 != metadataClass)
                    {
                        Debug.WriteLine($"Updating class type at start of export data {exportEntry.UIndex} {exportEntry.ClassName}");
                        //Update unreal header
                        exportData.OverwriteRange(0, BitConverter.GetBytes(metadataClass));
                        exportData.OverwriteRange(4, BitConverter.GetBytes(metadataClass));
                        exportEntry.Data = exportData;
                    }

                    if (exportEntry.NetIndex >= 0)
                    {
                        Debug.WriteLine("Updating netindex on " + exportEntry.GetIndexedFullPath + " from " + exportEntry.NetIndex);
                        exportEntry.NetIndex = nextNetIndex++;
                    }
                }

                itemcount++;
            }
            task?.Complete($"{itemcount} stack headers updated");
        }

        public void relinkPathfindingChain(ListBoxTask task = null)
        {
            var pathfindingChain = new List<IExportEntry>();

            byte[] data = PersistentLevel.getBinaryData();
            int start = 4;
            uint numberofitems = BitConverter.ToUInt32(data, start);
            int countoffset = start;

            start += 8;
            int itemcount = 2; //Skip bioworldinfo and Class

            //Get all nav items.
            while (itemcount <= numberofitems)
            {
                //get header.
                int itemexportid = BitConverter.ToInt32(data, start);
                if (Pcc.isUExport(itemexportid))
                {
                    IExportEntry exportEntry = Pcc.getUExport(itemexportid);
                    StructProperty navGuid = exportEntry.GetProperty<StructProperty>("NavGuid");
                    if (navGuid != null)
                    {
                        pathfindingChain.Add(exportEntry);
                    }

                    start += 4;
                    itemcount++;
                }
                else
                {
                    start += 4;
                    itemcount++;
                }
            }

            //Filter so it only has nextNavigationPoint. This will drop the end node
            var nextNavigationPointChain = new List<IExportEntry>();
            foreach (IExportEntry exportEntry in pathfindingChain)
            {
                ObjectProperty nextNavigationPointProp = exportEntry.GetProperty<ObjectProperty>("nextNavigationPoint");

                if (nextNavigationPointProp == null)
                {
                    //don't add this as its not part of this chain
                    continue;
                }
                nextNavigationPointChain.Add(exportEntry);
            }

            if (nextNavigationPointChain.Count > 0)
            {
                //Follow chain to end to find end node
                IExportEntry nodeEntry = nextNavigationPointChain[0];
                ObjectProperty nextNavPoint = nodeEntry.GetProperty<ObjectProperty>("nextNavigationPoint");

                while (nextNavPoint != null)
                {
                    nodeEntry = Pcc.getUExport(nextNavPoint.Value);
                    nextNavPoint = nodeEntry.GetProperty<ObjectProperty>("nextNavigationPoint");
                }

                //rebuild chain
                for (int i = 0; i < nextNavigationPointChain.Count; i++)
                {
                    IExportEntry chainItem = nextNavigationPointChain[i];
                    IExportEntry nextchainItem;
                    if (i < nextNavigationPointChain.Count - 1)
                    {
                        nextchainItem = nextNavigationPointChain[i + 1];
                    }
                    else
                    {
                        nextchainItem = nodeEntry;
                    }

                    ObjectProperty nextNav = chainItem.GetProperty<ObjectProperty>("nextNavigationPoint");

                    byte[] expData = chainItem.Data;
                    expData.OverwriteRange((int) nextNav.ValueOffset, BitConverter.GetBytes(nextchainItem.UIndex));
                    chainItem.Data = expData;
                    //Debug.WriteLine(chainItem.UIndex + " Chain link -> " + nextchainItem.UIndex);
                }

                task?.Complete("NavigationPoint chain has been updated");
            }
            else
            {
                task?.Complete("NavigationPoint chain not updated, no chain found");
            }
        }


        private void findDuplicateGUIDs(ListBoxTask task = null)
        {
            var navGuidLists = new Dictionary<string, List<UnrealGUID>>();
            var duplicateGuids = new List<UnrealGUID>();

            int start = PersistentLevel.propsEnd();
            //Read persistent level binary
            byte[] data = PersistentLevel.Data;

            uint exportid = BitConverter.ToUInt32(data, start);
            start += 4;
            uint numberofitems = BitConverter.ToUInt32(data, start);
            int countoffset = start;
            int itemcount = 2;
            start += 8;
            while (itemcount < numberofitems)
            {
                //get header.
                int itemexportid = BitConverter.ToInt32(data, start);
                if (Pcc.isUExport(itemexportid) && itemexportid > 0)
                {
                    IExportEntry exportEntry = Pcc.getUExport(itemexportid);
                    StructProperty navguid = exportEntry.GetProperty<StructProperty>("NavGuid");
                    if (navguid != null)
                    {
                        UnrealGUID nav = new UnrealGUID(navguid);

                        if (navGuidLists.TryGetValue(nav.ToString(), out List<UnrealGUID> list))
                        {
                            list.Add(nav);
                        }
                        else
                        {
                            list = new List<UnrealGUID>();
                            navGuidLists[nav.ToString()] = list;
                            list.Add(nav);
                        }
                    }
                    start += 4;
                    itemcount++;
                }
                else
                {
                    //INVALID or empty item encountered. We don't care right now though.
                    start += 4;
                    itemcount++;
                }
            }

            int numduplicates = 0;
            foreach (List<UnrealGUID> guidList in navGuidLists.Values)
            {
                if (guidList.Count > 1)
                {
                    numduplicates++;
                    Debug.WriteLine($"Number of duplicates: {guidList.Count}");
                    //contains duplicates
                    foreach (UnrealGUID guid in guidList)
                    {
                        //Debug.WriteLine(guid.levelListIndex + " Duplicate: " + guid.export.ObjectName);
                        duplicateGuids.Add(guid);
                        ListBoxTask v = new ListBoxTask
                        {
                            Header = $"Dupliate GUID found on export {guid.export.UIndex} {guid.export.ObjectName}_{guid.export.indexValue}",
                            Icon = EFontAwesomeIcon.Solid_Times,
                            Spinning = false,
                            Foreground = Brushes.Red
                        };
                        ValidationTasks.Add(v);
                    }
                }
            }
            task?.Complete(numduplicates + " duplicate GUID" + (numduplicates != 1 ? "s" : "") + " were found");
        }
    }
}
