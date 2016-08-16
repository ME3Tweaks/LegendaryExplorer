using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsefulThings.WPF;

namespace ME3Explorer.Packages
{
    public abstract class MEPackage : INotifyPropertyChanged
    {
        protected const int appendFlag = 0x00100000;

        public string FileName { get; protected set; }

        protected byte[] header;
        protected uint magic { get { return BitConverter.ToUInt32(header, 0); } }
        protected ushort lowVers { get { return BitConverter.ToUInt16(header, 4); } }
        protected ushort highVers { get { return BitConverter.ToUInt16(header, 6); } }
        protected int expDataBegOffset { get { return BitConverter.ToInt32(header, 8); } set { Buffer.BlockCopy(BitConverter.GetBytes(value), 0, header, 8, sizeof(int)); } }
        protected int nameSize { get { int val = BitConverter.ToInt32(header, 12); return (val < 0) ? val * -2 : val; } }
        protected uint flags { get { return BitConverter.ToUInt32(header, 16 + nameSize); } }

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

        protected uint namesAdded;
        protected List<string> names;
        public IReadOnlyList<string> Names { get { return names; } }
        
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
            lastSaved = DateTime.Now;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastSaved)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
        }

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
            names.Add(name);
            namesAdded++;
            return names.Count - 1;
        }

        public void addName(string name)
        {
            if (!names.Contains(name))
            {
                names.Add(name);
                namesAdded++;
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

        public ObservableCollection<GenericWindow> Tools { get; private set; } = new ObservableCollection<GenericWindow>();

        public void RegisterTool(GenericWindow gen)
        {
            Tools.Add(gen);
            gen.RegisterClosed(() =>
            {
                Tools.Remove(gen);
                if (Tools.Count == 0)
                {
                    noLongerOpenInTools?.Invoke(this, EventArgs.Empty);
                }
            });
        }

        public void Release(System.Windows.Window wpfWindow = null, System.Windows.Forms.Form winForm = null)
        {
            if (wpfWindow != null)
            {
                GenericWindow gen = Tools.First(x => x == wpfWindow);
                Tools.Remove(gen);
                if (Tools.Count == 0)
                {
                    noLongerOpenInTools?.Invoke(this, EventArgs.Empty);
                }
                gen.Dispose();
            }
            else if (winForm != null)
            {
                GenericWindow gen = Tools.First(x => x == winForm);
                Tools.Remove(gen);
                if (Tools.Count == 0)
                {
                    noLongerOpenInTools?.Invoke(this, EventArgs.Empty);
                }
                gen.Dispose();
            }
        }

        public event EventHandler noLongerOpenInTools;
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void exportChanged(object sender, PropertyChangedEventArgs e)
        {
        }
    }
}
