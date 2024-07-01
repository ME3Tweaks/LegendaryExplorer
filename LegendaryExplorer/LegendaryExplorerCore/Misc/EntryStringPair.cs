using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Misc
{
    /// <summary>
    /// Information about how to open an export in a LEX tool; can be used by external tools
    /// (such as M3's deployment checks). Used to store an entry reference without holding
    /// onto the package
    /// </summary>
    public class LEXOpenable
    {
        public string FilePath { get; init; }
        public string EntryPath { get; init; }
        public int EntryUIndex { get; init; }
        public string EntryClass { get; init; }

        // Todo: Add support for tool IDs?
        public LEXOpenable()
        {
        }

        public LEXOpenable(IEntry entry)
        {
            FilePath = entry.FileRef.FilePath; // CAN BE NULL IF THIS IS MEMORY FILE !!
            EntryUIndex = entry.UIndex;
            EntryClass = entry.ClassName;
            EntryPath = entry.InstancedFullPath;
        }

        public LEXOpenable(IMEPackage pcc, int uIndex)
        {
            IEntry entry = pcc.GetEntry(uIndex);
            FilePath = pcc.FilePath;
            EntryPath = entry.InstancedFullPath;
            EntryUIndex = uIndex;
            EntryClass = entry.ClassName;
        }

        public bool IsImport()
        {
            return EntryUIndex < 0;
        }
    }

    public class EntryStringPair : INotifyPropertyChanged
    {
        public EntryStringPair(string message)
        {
            Message = message;
        }

        public EntryStringPair(IEntry entry, string message)
        {
            Entry = entry;
            Message = message;
        }

        public EntryStringPair(LEXOpenable entry, string message)
        {
            Openable = entry;
            Message = message;
        }

        public EntryStringPair(IEntry entry) : this(entry, $"{entry.UIndex,-9}\t{entry.InstancedFullPath} in {entry.FileRef.FilePath}")
        {
        }

        /// <summary>
        /// Entry reference without package reference
        /// </summary>
        public LEXOpenable Openable { get; set; }
        public IEntry Entry { get; set; }
        public string Message { get; set; }

        public override string ToString() => Message;

        public static implicit operator EntryStringPair(ExportEntry entry)
        {
            return new EntryStringPair(entry);
        }

        public static implicit operator EntryStringPair(ImportEntry entry)
        {
            return new EntryStringPair(entry);
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
