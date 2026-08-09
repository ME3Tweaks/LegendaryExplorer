using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;

/// <summary>
/// Represents a directed connection between two graph nodes.
/// </summary>
public class GraphConnection : INotifyPropertyChanged
{
    private GraphNode _source;
    private GraphNode _target;
    private Color _lineColor = Colors.Gray;
    private double _lineWidth = 1.5;
    private ConnectionLineStyle _lineStyle = ConnectionLineStyle.Solid;

    public GraphConnection(GraphNode source, GraphNode target)
    {
        _source = source;
        _target = target;
    }

    /// <summary>
    /// Source node of the connection.
    /// </summary>
    public GraphNode Source
    {
        get => _source;
        set { if (_source != value) { _source = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Target node of the connection.
    /// </summary>
    public GraphNode Target
    {
        get => _target;
        set { if (_target != value) { _target = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Color of the connection line.
    /// </summary>
    public Color LineColor
    {
        get => _lineColor;
        set { if (_lineColor != value) { _lineColor = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Width of the connection line in device-independent pixels.
    /// </summary>
    public double LineWidth
    {
        get => _lineWidth;
        set { if (_lineWidth != value) { _lineWidth = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Visual dash style of the connection line.
    /// </summary>
    public ConnectionLineStyle LineStyle
    {
        get => _lineStyle;
        set { if (_lineStyle != value) { _lineStyle = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Custom data associated with this connection.
    /// </summary>
    public object? Tag { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
