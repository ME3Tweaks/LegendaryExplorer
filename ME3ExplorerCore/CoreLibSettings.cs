using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ME3ExplorerCore
{
    public class CoreLibSettings : INotifyPropertyChanged
    {
        // In ME3Explorer (not this lib) the property changed event is listened to for changes and maps them into the .net framework defaults system
        public static CoreLibSettings Instance { get; set; }

        public CoreLibSettings()
        {
            Instance = this;
        }
        public bool TLKGenderIsMale { get; set; }
        public string TLKDefaultLanguage { get; set; } = "INT"; // maybe should be enum?
        public bool ParseUnknownArrayTypesAsObject { get; set; }
        public string ME1Directory { get; set; }
        public string ME2Directory { get; set; }
        public string ME3Directory { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;
    }
}
