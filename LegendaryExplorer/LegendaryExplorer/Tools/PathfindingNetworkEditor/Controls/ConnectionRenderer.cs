using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Controls;

/// <summary>
/// Renders all graph connections in a single <see cref="OnRender"/> pass for maximum performance.
/// Pens are cached and frozen for efficient reuse.
/// </summary>
public class ConnectionRenderer : FrameworkElement
{
    private readonly List<GraphConnection> _connections = [];
    private readonly Dictionary<(Color color, double width, ConnectionLineStyle style), Pen> _penCache = [];

    /// <summary>
    /// The current set of connections being rendered.
    /// </summary>
    public IReadOnlyList<GraphConnection> Connections => _connections;

    /// <summary>
    /// Replaces all connections and triggers a re-render.
    /// </summary>
    public void SetConnections(IEnumerable<GraphConnection> connections)
    {
        _connections.Clear();
        _penCache.Clear();
        _connections.AddRange(connections);
        InvalidateVisual();
    }

    /// <summary>
    /// Adds a single connection.
    /// </summary>
    public void AddConnection(GraphConnection connection)
    {
        _connections.Add(connection);
        InvalidateVisual();
    }

    /// <summary>
    /// Adds multiple connections in one batch (single re-render).
    /// </summary>
    public void AddConnections(IEnumerable<GraphConnection> connections)
    {
        _connections.AddRange(connections);
        InvalidateVisual();
    }

    /// <summary>
    /// Removes a single connection.
    /// </summary>
    public bool RemoveConnection(GraphConnection connection)
    {
        var removed = _connections.Remove(connection);
        if (removed) InvalidateVisual();
        return removed;
    }

    /// <summary>
    /// Removes all connections.
    /// </summary>
    public void Clear()
    {
        _connections.Clear();
        _penCache.Clear();
        InvalidateVisual();
    }

    /// <summary>
    /// Forces a re-render (e.g., after node positions change).
    /// </summary>
    public void Refresh() => InvalidateVisual();

    private Pen GetOrCreatePen(Color color, double width, ConnectionLineStyle style)
    {
        var key = (color, width, style);
        if (_penCache.TryGetValue(key, out var pen))
            return pen;

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        pen = new Pen(brush, width)
        {
            DashStyle = style switch
            {
                ConnectionLineStyle.Dashed => DashStyles.Dash,
                ConnectionLineStyle.Dotted => DashStyles.Dot,
                ConnectionLineStyle.DashDot => DashStyles.DashDot,
                _ => DashStyles.Solid
            }
        };
        pen.Freeze();
        _penCache[key] = pen;
        return pen;
    }

    protected override void OnRender(DrawingContext dc)
    {
        foreach (var conn in _connections)
        {
            var pen = GetOrCreatePen(conn.LineColor, conn.LineWidth, conn.LineStyle);
            dc.DrawLine(pen,
                new Point(conn.Source.X, conn.Source.Y),
                new Point(conn.Target.X, conn.Target.Y));
        }
    }
}
