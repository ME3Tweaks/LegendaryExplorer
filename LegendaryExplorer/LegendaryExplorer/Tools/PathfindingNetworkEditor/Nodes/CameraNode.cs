using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes
{
    public class CameraNode : GraphNode
    {
        private int _rotationYaw;
        private float _fov = 90f;

        public CameraNode()
        {
            Shape = NodeShape.Camera;
            Width = 50;
            Height = 50;
            BackgroundColor = Colors.DarkSlateGray;
            BorderColor = Colors.White;
            Label = "CAMERA";
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

        public float FOV
        {
            get => _fov;
            set
            {
                if (_fov != value)
                {
                    _fov = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
