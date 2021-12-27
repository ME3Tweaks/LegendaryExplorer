using System.ComponentModel;

namespace LegendaryExplorerCore.Dialogue
{
    /// <summary>
    /// Represents a parsed BioStageDirection struct in a BioConversation
    /// </summary>
    /// <remarks>This struct only exists in Game 3</remarks>
    public class StageDirection : INotifyPropertyChanged
    {
        /// <summary>
        /// The TLK string ref of the node this Stage Direction applies to. The srStrRef property
        /// </summary>
        public int StageStrRef { get; set; }
        /// <summary>
        /// The parsed string value of the <see cref="StageStrRef"/> line that this direction applies to.
        /// </summary>
        public string StageLine { get; set; }
        /// <summary>
        /// The actual text of this stage direction. The sText property.
        /// </summary>
        public string Direction { get; set; }

        /// <summary>
        /// Creates a StateDirection
        /// </summary>
        /// <param name="stageStrRef">String ref of dialogue node</param>
        /// <param name="stageLine">Parsed string of dialogue node line</param>
        /// <param name="direction">Text of stage direction</param>
        public StageDirection(int stageStrRef, string stageLine, string direction)
        {
            StageStrRef = stageStrRef;
            StageLine = stageLine;
            Direction = direction;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
