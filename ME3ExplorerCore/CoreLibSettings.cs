using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ME3ExplorerCore
{
    public class CoreLibSettings : INotifyPropertyChanged
    {
        public static CoreLibSettings Instance { get; set; }

        public CoreLibSettings()
        {
            Instance = this;
        }
        public bool TLKGenderIsMale { get; set; }
        public string TLKDefaultLanguage { get; set; } // maybe should be enum?
        public bool ParseUnknownArrayTypesAsObject { get; internal set; }
        public string ME1Directory { get; internal set; }
        public string ME2Directory { get; internal set; }
        public string ME3Directory { get; internal set; }


        public event PropertyChangedEventHandler PropertyChanged;

        internal void Save()
        {

        }
    }
}
