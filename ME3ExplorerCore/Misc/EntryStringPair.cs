using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using ME3ExplorerCore.Packages;

namespace ME3ExplorerCore.Misc
{
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

        public IEntry Entry { get; set; }
        public string Message { get; set; }

        public override string ToString() => Message;

        public static implicit operator EntryStringPair(ExportEntry entry)
        {
            return new EntryStringPair(entry, $"{$"#{entry.UIndex}",-9} {entry.FileRef.FilePath}");
        }

        public static implicit operator EntryStringPair(ImportEntry entry)
        {
            return new EntryStringPair(entry, $"{$"#{entry.UIndex}",-9} {entry.FileRef.FilePath}");
        }
#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
