using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    public class PlayerNode : GraphNode
    {
        private int _rotationYaw;

        public PlayerNode()
        {
            Shape = NodeShape.Player;
            Width = 50;
            Height = 50;
            BackgroundColor = Colors.BlueViolet;
            BorderColor = Colors.White;
            Label = "PLAYER";
        }

        public int RotationYaw
        {
            get => _rotationYaw;
            set
            {
                if (_rotationYaw != value)
                {
                    _rotationYaw = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
