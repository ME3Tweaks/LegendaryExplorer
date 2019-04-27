using FontAwesome.WPF;
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
    public partial class ValidationPanel : UserControl, INotifyPropertyChanged
    {

        private const int MAX_DISTANCE_TOLERANCE = 5;
        private const float MAX_DIRECTION_TOLERANCE = .001f;

        private IMEPackage _pcc;

        public IExportEntry PersistentLevel { get; private set; }
        public IMEPackage Pcc { get => _pcc; private set => SetProperty(ref _pcc, value); }
        BackgroundWorker fixAndValidateWorker;
        public ObservableCollectionExtended<ListBoxTask> ValidationTasks { get; } = new ObservableCollectionExtended<ListBoxTask>();
        private object _myCollectionLock = new object();

        private string _lastRunOnText;
        public string LastRunOnText { get => _lastRunOnText; set => SetProperty(ref _lastRunOnText, value); }

        public ValidationPanel()
        {
            DataContext = this;
            LoadCommands();
            InitializeComponent();
            BindingOperations.EnableCollectionSynchronization(ValidationTasks, _myCollectionLock);
        }

        public void SetLevel(IExportEntry PersistentLevel)
        {
            this.PersistentLevel = PersistentLevel;
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
            FixAndValidateCommand = new RelayCommand(FixAndValidate, CanFixAndValidate);
        }

        private bool CanFixAndValidate(object obj)
        {
            return Pcc != null && (fixAndValidateWorker == null || fixAndValidateWorker.IsBusy == false);
        }

        private void FixAndValidate(object obj)
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

        private void FixAndValidate_Completed(object sender, RunWorkerCompletedEventArgs e)
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
            HashSet<int> reachSpecExportIndexes = new HashSet<int>();
            List<string> badSpecs = new List<string>();
            //HashSet<string> names = new HashSet<string>();

            int numNeedingRecalc = 0;
            for (int i = 0; i < Pcc.ExportCount; i++)
            {
                IExportEntry exp = Pcc.Exports[i];
                ArrayProperty<ObjectProperty> pathList = exp.GetProperty<ArrayProperty<ObjectProperty>>("PathList");
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
                            badSpecs.Add(reachSpecObj.Value.ToString() + " " + spec.ObjectName + " start value does not match the node that references it (" + exp.UIndex + ")");
                        }

                        //get end
                        StructProperty end = specProps.GetProp<StructProperty>("End");
                        ObjectProperty endActorObj = end.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(spec));
                        if (endActorObj.Value == start.Value)
                        {
                            isBad = true;
                            badSpecs.Add(reachSpecObj.Value.ToString() + " " + spec.ObjectName + " start and end property is the same. This will crash the game.");
                        }

                        var guid = new UnrealGUID(end.GetProp<StructProperty>("Guid"));
                        if ((guid.A | guid.B | guid.C | guid.D) == 0 && endActorObj.Value == 0)
                        {
                            isBad = true;
                            badSpecs.Add(reachSpecObj.Value.ToString() + " " + spec.ObjectName + " has no external guid and has no endactor.");
                        }
                        if (endActorObj.Value > Pcc.ExportCount || endActorObj.Value < 0)
                        {
                            isBad = true;
                            badSpecs.Add(reachSpecObj.Value.ToString() + " " + spec.ObjectName + " has invalid end property (past end of bounds or less than 0).");
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
                                numNeedingRecalc++;
                            }
                        }
                    }

                }
            }
            task?.Complete(numNeedingRecalc + " ReachSpec" + (numNeedingRecalc == 1 ? "" : "s") + " were recalculated");
        }

        private bool calculateReachSpec(IExportEntry reachSpecExport, IExportEntry startNodeExport = null)
        {

            //Get start and end exports.
            var properties = reachSpecExport.GetProperties();
            ObjectProperty start = properties.GetProp<ObjectProperty>("Start");
            StructProperty end = properties.GetProp<StructProperty>("End");

            ObjectProperty endActorObj = end.GetProp<ObjectProperty>(SharedPathfinding.GetReachSpecEndName(reachSpecExport));

            if (start.Value > 0 && endActorObj.Value > 0)
            {
                //We should capture GUID here

                IExportEntry startNode = reachSpecExport.FileRef.getUExport(start.Value);
                IExportEntry endNode = reachSpecExport.FileRef.getUExport(endActorObj.Value);

                if (startNodeExport != null && startNode.UIndex != startNodeExport.UIndex)
                {
                    //ERROR!
                    ValidationTasks.Add(new ListBoxTask
                    {
                        Header = reachSpecExport.UIndex + " " + reachSpecExport.ObjectName + " start does not match it's containing pathlist reference (" + startNodeExport.UIndex + " " + startNodeExport.ObjectName + ")",
                        Icon = FontAwesomeIcon.Times,
                        Foreground = Brushes.Red,
                        Spinning = false
                    });

                    //MessageBox.Show(
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
                                int calculatedProperDistance = SharedPathfinding.RoundDoubleToInt(distance);
                                int distanceDiff = Math.Abs(origDistance - calculatedProperDistance);

                                if (distanceDiff > MAX_DISTANCE_TOLERANCE)
                                {
                                    // Difference.
                                    Debug.WriteLine("Diff Distance is > tolerance: " + distanceDiff + ", should be " + calculatedProperDistance);
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
                            }
                        }

                    }



                }
            }
            //We really shouldn't reach here, hopefully.
            return false;
        }

        public void fixStackHeaders(ListBoxTask task = null)
        {
            int itemcount = 2;
            int numUpdated = 0;

            Dictionary<int, List<int>> mpIDs = new Dictionary<int, List<int>>();
            Debug.WriteLine("Start of header fix scan===================");

            //start full scan.
            itemcount = 2;

            //This will update netindex'es for all items that start with TheWorld.PersistentLevel and are 3 items long.
            foreach (IExportEntry exportEntry in Pcc.Exports)
            {
                string path = exportEntry.GetFullPath;
                string[] pieces = path.Split('.');

                if (pieces.Length < 3 || pieces[0] != "TheWorld" || pieces[1] != "PersistentLevel")
                {
                    continue;
                }

                int idOffset = 0;
                if ((PersistentLevel.ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
                {
                    byte[] exportData = exportEntry.Data;
                    int classId1 = BitConverter.ToInt32(exportData, 0);
                    int classId2 = BitConverter.ToInt32(exportData, 4);
                    //Debug.WriteLine(maybe_MPID);

                    int metadataClass = exportEntry.idxClass;
                    if ((classId1 != metadataClass) || (classId2 != metadataClass))
                    {
                        Debug.WriteLine("Updating class type at start of export data " + exportEntry.UIndex + " " + exportEntry.ClassName);
                        //Update unreal header
                        SharedPathfinding.WriteMem(exportData, 0, BitConverter.GetBytes(metadataClass));
                        SharedPathfinding.WriteMem(exportData, 4, BitConverter.GetBytes(metadataClass));
                        numUpdated++;
                        exportEntry.Data = exportData;

                    }
                    idOffset = 0x1A;
                }

                //Maybe MP IDs??
                //int maybe_MPID = BitConverter.ToInt32(exportEntry.Data, idOffset);
                //List<int> idList;
                //if (mpIDs.TryGetValue(maybe_MPID, out idList))
                //{
                //    //Debug.WriteLine(itemcount);
                //    idList.Add(exportEntry.Index);
                //}
                //else
                //{
                //    mpIDs[maybe_MPID] = new List<int>();
                //    mpIDs[maybe_MPID].Add(exportEntry.Index);
                //}
                itemcount++;
            }
/*
            //Update IDs
            for (int index = 0; index < mpIDs.Count; index++)
            {
                var item = mpIDs.ElementAt(index);
                List<int> valueList = item.Value;
                if (valueList.Count > 1 && item.Key != 0 && item.Key != -1)
                {
                    string itemlist = Pcc.Exports[valueList[0]].ObjectName;
                    for (int i = 1; i < valueList.Count; i++)
                    // for (int i = valueList.Count - 1; i > 1; i--)
                    {
                        int max = mpIDs.Keys.Max();
                        //Debug.WriteLine("New max key size: " + max);
                        IExportEntry export = Pcc.Exports[valueList[i]];
                        itemlist += " " + export.ObjectName;
                        string exportname = export.ObjectName;
                        if (exportname.Contains("SFXOperation") || exportname.Contains("ReachSpec"))
                        {

                            int idOffset = 0;
                            if ((export.ObjectFlags & (ulong)UnrealFlags.EObjectFlags.HasStack) != 0)
                            {
                                idOffset = 0x1A;
                            }
                            byte[] exportData = export.Data;

                            int nameId = BitConverter.ToInt32(exportData, 4);

                            if (Pcc.isName(nameId) && Pcc.getNameEntry(nameId) == export.ObjectName)
                            {
                                //It's a primitive component header
                                idOffset += 8;
                                continue;
                            }
                            int maybe_MPID = BitConverter.ToInt32(exportData, idOffset);

                            max++;
                            int origId = maybe_MPID;
                            SharedPathfinding.WriteMem(exportData, idOffset, BitConverter.GetBytes(max));
                            numUpdated++;


                            maybe_MPID = BitConverter.ToInt32(exportData, idOffset); //read new fixed id
                            export.Data = exportData;

                            if (export.EntryHasPendingChanges)
                            {
                                Debug.WriteLine("Updated MPID " + origId + " -> " + maybe_MPID + " " + export.ObjectName + " in exp " + export.Index);
                            }
                            //add to new list to prevent rewrite of dupes.
                            mpIDs[maybe_MPID] = new List<int>();
                            mpIDs[maybe_MPID].Add(export.Index);
                        }

                    }
                    //Debug.WriteLine(itemlist);
                }
            }*/
            //if (showUI)
            //{
            task?.Complete(numUpdated + " export" + (numUpdated != 1 ? "s" : "") + " stack headers updated");
            //}
        }

        public void relinkPathfindingChain(ListBoxTask task = null)
        {
            List<IExportEntry> pathfindingChain = new List<IExportEntry>();

            int start = PersistentLevel.propsEnd();
            byte[] data = PersistentLevel.Data;
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
            List<IExportEntry> nextNavigationPointChain = new List<IExportEntry>();
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
                SharedPathfinding.WriteMem(expData, (int)nextNav.ValueOffset, BitConverter.GetBytes(nextchainItem.UIndex));
                chainItem.Data = expData;
                //Debug.WriteLine(chainItem.UIndex + " Chain link -> " + nextchainItem.UIndex);
            }
            task?.Complete("NavigationPoint chain has been updated");
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

                        List<UnrealGUID> list;
                        if (navGuidLists.TryGetValue(nav.ToString(), out list))
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
                    Debug.WriteLine("Number of duplicates: " + guidList.Count);
                    //contains duplicates
                    foreach (UnrealGUID guid in guidList)
                    {
                        //Debug.WriteLine(guid.levelListIndex + " Duplicate: " + guid.export.ObjectName);
                        duplicateGuids.Add(guid);
                        ListBoxTask v = new ListBoxTask
                        {
                            Header = "Dupliate GUID found on export " + guid.export.UIndex + " " + guid.export.ObjectName + "_" + guid.export.indexValue,
                            Icon = FontAwesomeIcon.TimesRectangle,
                            Spinning = false,
                            Foreground = Brushes.Red
                        };
                        ValidationTasks.Add(v);
                    }
                }
            }
            task?.Complete(numduplicates + " duplicate GUID" + (numduplicates != 1 ? "s" : "") + " were found");
        }
        #region PropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }


        #endregion
    }
}
