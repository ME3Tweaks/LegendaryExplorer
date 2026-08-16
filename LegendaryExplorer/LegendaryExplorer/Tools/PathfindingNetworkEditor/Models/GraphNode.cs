using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;

/// <summary>
/// Base class representing a node in the pathfinding network graph.
/// Subclass to define different node types with different default visual properties.
/// </summary>
public class GraphNode : INotifyPropertyChanged
{
    private double _x;
    private double _y;
    private double _z;
    private string _label = string.Empty;
    private Color _backgroundColor = Colors.LightBlue;
    private Color _borderColor = Colors.DarkBlue;
    private NodeShape _shape = NodeShape.Circle;
    private double _width = 40;
    private double _height = 40;
    private double _borderThickness = 2;
    private Color? _temporaryBackgroundColor;
    private Color? _temporaryBorderColor;

    /// <summary>
    /// Center X coordinate in graph space.
    /// </summary>
    public double X
    {
        get => _x;
        set { if (_x != value) { _x = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Center Y coordinate in graph space.
    /// </summary>
    public double Y
    {
        get => _y;
        set { if (_y != value) { _y = value; OnPropertyChanged(); } }
    }

    public double Z
    {
        get => _z;
        set { if (_z != value) { _z = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Text label displayed on the node.
    /// </summary>
    public string Label
    {
        get => _label;
        set { if (_label != value) { _label = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Default background fill color.
    /// </summary>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor != value)
            {
                _backgroundColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveBackgroundColor));
            }
        }
    }

    /// <summary>
    /// Default border color.
    /// </summary>
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor != value)
            {
                _borderColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveBorderColor));
            }
        }
    }

    /// <summary>
    /// Visual shape of the node.
    /// </summary>
    public NodeShape Shape
    {
        get => _shape;
        set { if (_shape != value) { _shape = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Width of the node in graph units.
    /// </summary>
    public double Width
    {
        get => _width;
        set { if (_width != value) { _width = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Height of the node in graph units.
    /// </summary>
    public double Height
    {
        get => _height;
        set { if (_height != value) { _height = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Border line thickness in device-independent pixels.
    /// </summary>
    public double BorderThickness
    {
        get => _borderThickness;
        set { if (_borderThickness != value) { _borderThickness = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Temporary background color override. Set to <c>null</c> to revert to <see cref="BackgroundColor"/>.
    /// </summary>
    public Color? TemporaryBackgroundColor
    {
        get => _temporaryBackgroundColor;
        set
        {
            if (_temporaryBackgroundColor != value)
            {
                _temporaryBackgroundColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveBackgroundColor));
            }
        }
    }

    /// <summary>
    /// Temporary border color override. Set to <c>null</c> to revert to <see cref="BorderColor"/>.
    /// </summary>
    public Color? TemporaryBorderColor
    {
        get => _temporaryBorderColor;
        set
        {
            if (_temporaryBorderColor != value)
            {
                _temporaryBorderColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveBorderColor));
            }
        }
    }

    /// <summary>
    /// The background color currently in effect (temporary override or default).
    /// </summary>
    public Color EffectiveBackgroundColor => TemporaryBackgroundColor ?? BackgroundColor;

    /// <summary>
    /// The border color currently in effect (temporary override or default).
    /// </summary>
    public Color EffectiveBorderColor => TemporaryBorderColor ?? BorderColor;

    /// <summary>
    /// Clears any temporary color overrides, reverting to default colors.
    /// </summary>
    public void ClearTemporaryColors()
    {
        TemporaryBackgroundColor = null;
        TemporaryBorderColor = null;
    }

    /// <summary>
    /// Custom data associated with this node.
    /// </summary>
    public object? Tag { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
