using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace ME3Explorer.Packages
{
    public enum PackageChange
    {
        ExportData,
        ExportHeader,
        Import,
        Names,
        ExportAdd,
        ImportAdd,
    }

    public struct PackageUpdate
    {
        public PackageChange change;
        public int index;
    }

    public abstract class MEPackage : ViewModelBase, INotifyPropertyChanged, IDisposable
    {
        protected const int appendFlag = 0x00100000;

        public string FileName { get; protected set; }

        public bool IsModified
        {
            get
            {
                return exports.Any(entry => entry.DataChanged == true) || imports.Any(entry => entry.HeaderChanged == true) || namesAdded > 0;
            }
        }
        public bool CanReconstruct { get { return !exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"); } }

        protected byte[] header;
        protected uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        protected ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        protected ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        protected int expDataBegOffset { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        protected int nameSize { get { int val = BitConverter.ToInt32(header, 12); return (val < 0) ? val * -2 : val; } }
        protected uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }


        public abstract int NameCount { get; protected set; }
        public abstract int ImportCount { get; protected set; }
        public abstract int ExportCount { get; protected set; }

        public bool IsCompressed
        {
            get { return (flags & 0x02000000) != 0; }
            protected set
            {
                if (value) // sets the compressed flag if bCompressed set equal to true
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | 0x02000000), 0, header, 16 + nameSize, sizeof(int));
                else // else set to false
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~0x02000000), 0, header, 16 + nameSize, sizeof(int));
            }
        }
        //has been saved with the revised Append method
        public bool IsAppend
        {
            get { return (flags & appendFlag) != 0; }
            protected set
            {
                if (value) // sets the append flag if IsAppend set equal to true
                    Buffer.BlockCopy(BitConverter.GetBytes(flags | appendFlag), 0, header, 16 + nameSize, sizeof(int));
                else // else set to false
                    Buffer.BlockCopy(BitConverter.GetBytes(flags & ~appendFlag), 0, header, 16 + nameSize, sizeof(int));
            }
        }

        #region Names
        protected uint namesAdded;
        protected List<string> names;
        public IReadOnlyList<string> Names { get { return names; } }

        public bool isName(int index)
        {
            return (index >= 0 && index < names.Count);
        }

        public string getNameEntry(int index)
        {
            if (!isName(index))
                return "";
            return names[index];
        }

        public int FindNameOrAdd(string name)
        {
            for (int i = 0; i < names.Count; i++)
                if (names[i] == name)
                    return i;
            addName(name);
            return names.Count - 1;
        }

        public void addName(string name)
        {
            if (!names.Contains(name))
            {
                names.Add(name);
                namesAdded++;
                NameCount = names.Count;

                updateTools(PackageChange.Names, NameCount);
                OnPropertyChanged(nameof(NameCount));
            }
        }

        public void replaceName(int idx, string newName)
        {
            if (idx >= 0 && idx <= names.Count - 1)
            {
                names[idx] = newName;
                updateTools(PackageChange.Names, idx);
            }
        }

        /// <summary>
        /// Checks whether a name exists in the PCC and returns its index
        /// If it doesn't exist returns -1
        /// </summary>
        /// <param name="nameToFind">The name of the string to find</param>
        /// <returns></returns>
        public int findName(string nameToFind)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Compare(nameToFind, getNameEntry(i)) == 0)
                    return i;
            }
            return -1;
        }

        public void setNames(List<string> list)
        {
            names = list;
        }
        #endregion

        #region Exports
        protected List<IExportEntry> exports;
        public IReadOnlyList<IExportEntry> Exports
        {
            get
            {
                return exports;
            }
        }

        public bool isExport(int index)
        {
            return (index >= 0 && index < exports.Count);
        }

        public void addExport(IExportEntry exportEntry)
        {
            if (exportEntry.FileRef != this)
                throw new Exception("you cannot add a new export entry from another pcc file, it has invalid references!");

            exportEntry.DataChanged = true;
            exportEntry.Index = exports.Count;
            exportEntry.PropertyChanged += exportChanged;
            exports.Add(exportEntry);
            ExportCount = exports.Count;

            updateTools(PackageChange.ExportAdd, ExportCount - 1);
            OnPropertyChanged(nameof(ExportCount));
        }

        public IExportEntry getExport(int index)
        {
            return exports[index];
        }
        #endregion

        #region Imports
        protected List<ImportEntry> imports;
        public IReadOnlyList<ImportEntry> Imports
        {
            get
            {
                return imports;
            }
        }

        public bool isImport(int index)
        {
            return (index >= 0 && index < imports.Count);
        }

        public void addImport(ImportEntry importEntry)
        {
            if (importEntry.FileRef != this)
                throw new Exception("you cannot add a new import entry from another pcc file, it has invalid references!");

            importEntry.Index = imports.Count;
            importEntry.PropertyChanged += importChanged;
            imports.Add(importEntry);
            ImportCount = imports.Count;

            updateTools(PackageChange.ImportAdd, ImportCount - 1);
            OnPropertyChanged(nameof(ImportCount));
        }

        public ImportEntry getImport(int index)
        {
            return imports[index];
        }

        #endregion

        #region IEntry
        /// <summary>
        ///     gets Export or Import name
        /// </summary>
        /// <param name="index">unreal index</param>
        public string getObjectName(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1].ObjectName;
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1].ObjectName;
            if (index == 0)
                return "Class";
            return "";
        }

        /// <summary>
        ///     gets Export or Import class
        /// </summary>
        /// <param name="index">unreal index</param>
        public string getObjectClass(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1].ClassName;
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1].ClassName;
            return "";
        }

        /// <summary>
        ///     gets Export or Import entry
        /// </summary>
        /// <param name="index">unreal index</param>
        public IEntry getEntry(int index)
        {
            if (index > 0 && index <= ExportCount)
                return exports[index - 1];
            if (-index > 0 && -index <= ImportCount)
                return imports[-index - 1];
            return null;
        }
        #endregion

        private DateTime? lastSaved;
        public DateTime LastSaved
        {
            get
            {
                if (lastSaved.HasValue)
                {
                    return lastSaved.Value;
                }
                else if (File.Exists(FileName))
                {
                    return (new FileInfo(FileName)).LastWriteTime;
                }
                else
                {
                    return DateTime.MinValue;
                }
            }
        }

        public long FileSize
        {
            get
            {
                if (File.Exists(FileName))
                {
                    return (new FileInfo(FileName)).Length;
                }
                return 0;
            }
        }

        protected virtual void AfterSave()
        {
            foreach (var export in exports)
            {
                export.DataChanged = false;
            }
            foreach (var import in imports)
            {
                import.HeaderChanged = false;
            }
            namesAdded = 0;

            lastSaved = DateTime.Now;
            OnPropertyChanged(nameof(LastSaved));
            OnPropertyChanged(nameof(FileSize));
            OnPropertyChanged(nameof(IsModified));
        }

        #region packageHandler stuff
        public ObservableCollection<GenericWindow> Tools { get; private set; } = new ObservableCollection<GenericWindow>();

        public void RegisterTool(GenericWindow gen)
        {
            RefCount++;
            Tools.Add(gen);
            gen.RegisterClosed(() =>
            {
                ReleaseGenericWindow(gen);
                Dispose();
            });
        }

        public void Release(System.Windows.Window wpfWindow = null, System.Windows.Forms.Form winForm = null)
        {
            if (wpfWindow != null)
            {
                GenericWindow gen = Tools.First(x => x == wpfWindow);
                ReleaseGenericWindow(gen);
            }
            else if (winForm != null)
            {
                GenericWindow gen = Tools.First(x => x == winForm);
                ReleaseGenericWindow(gen);
            }
            Dispose();
        }

        private void ReleaseGenericWindow(GenericWindow gen)
        {
            Tools.Remove(gen);
            if (Tools.Count == 0)
            {
                noLongerOpenInTools?.Invoke(this, EventArgs.Empty);
            }
            gen.Dispose();
        }

        public event EventHandler noLongerOpenInTools;

        protected void exportChanged(object sender, PropertyChangedEventArgs e)
        {
            IExportEntry exp = sender as IExportEntry;
            if (exp != null)
            {
                if (e.PropertyName == nameof(ExportEntry.DataChanged))
                {
                    updateTools(PackageChange.ExportData, exp.Index);
                }
                else if (e.PropertyName == nameof(ExportEntry.HeaderChanged))
                {
                    updateTools(PackageChange.ExportHeader, exp.Index);
                }
            }
        }

        protected void importChanged(object sender, PropertyChangedEventArgs e)
        {
            ImportEntry imp = sender as ImportEntry;
            if (imp != null)
            {
                if (e.PropertyName == nameof(ImportEntry.HeaderChanged))
                {
                    updateTools(PackageChange.Import, imp.Index);
                }
            }
        }

        HashSet<PackageUpdate> pendingUpdates = new HashSet<PackageUpdate>();
        List<Task> tasks = new List<Task>();
        Dictionary<int, bool> taskCompletion = new Dictionary<int, bool>();
        const int queuingDelay = 50;
        protected void updateTools(PackageChange change, int index)
        {
            PackageUpdate update = new PackageUpdate { change = change, index = index };
            if (!pendingUpdates.Contains(update))
            {
                pendingUpdates.Add(update);
                Task task = Task.Delay(queuingDelay);
                taskCompletion[task.Id] = false;
                tasks.Add(task);
                task.ContinueWith(x =>
                {
                    taskCompletion[x.Id] = true;
                    if (tasks.TrueForAll(t => taskCompletion[t.Id]))
                    {
                        tasks.Clear();
                        taskCompletion.Clear();
                        foreach (var item in Tools)
                        {
                            item.handleUpdate(pendingUpdates.ToList());
                        }
                        pendingUpdates.Clear();
                        OnPropertyChanged(nameof(IsModified));
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }


        public event EventHandler noLongerUsed;
        private int RefCount;

        public void RegisterUse()
        {
            RefCount++;
        }

        /// <summary>
        /// Doesn't neccesarily dispose the object.
        /// Will only do so once this has been called by every place that uses it.
        /// Recommend using the using block.
        /// </summary>
        public void Dispose()
        {
            RefCount--;
            if (RefCount == 0)
            {
                noLongerUsed?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion
    }
}
