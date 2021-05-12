using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace LegendaryExplorerCore.Dialogue
{
    public class StageDirection : INotifyPropertyChanged
    {
        public int StageStrRef { get; set; }
        public string StageLine { get; set; }
        public string Direction { get; set; }

        public StageDirection(int StageStrRef, string StageLine, string Direction)
        {

            this.StageStrRef = StageStrRef;
            this.StageLine = StageLine;
            this.Direction = Direction;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
