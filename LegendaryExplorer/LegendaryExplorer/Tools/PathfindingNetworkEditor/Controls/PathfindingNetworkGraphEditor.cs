using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Controls;

/// <summary>
/// Extends <see cref="NetworkGraphEditor"/> with pathfinding-specific features,
/// such as drawing rotation arrows on navigation-point nodes when zoomed in
/// beyond a configurable threshold.
/// </summary>
public class PathfindingNetworkGraphEditor : NetworkGraphEditor
{
    // -----------------------------------------------------------------------
    //  Rotation-arrow overlay
    // -----------------------------------------------------------------------

    private readonly Canvas _rotationCanvas;
    private readonly Dictionary<NavigationPoint, RotationArrowVisual> _rotationArrows = new();

    private bool _showRotation;

    /// <summary>
    /// When <see langword="true"/>, a small direction arrow is drawn on every
    /// <see cref="NavigationPoint"/> node once the zoom level exceeds
    /// <see cref="Settings.PathfindingNetworkEditor_RotationArrowMinZoom"/>.
    /// </summary>
    public bool ShowRotation
    {
        get => _showRotation;
        set
        {
            if (_showRotation == value) return;
            _showRotation = value;
            UpdateAllArrowVisibility();
        }
    }

    /// <summary>Number of initial children in the canvas to preserve (background image, tile layer, connections, rotation layer).</summary>
    protected override int CanvasChildrenToPreserve => 4;

    // -----------------------------------------------------------------------
    //  Z-range filter
    // -----------------------------------------------------------------------

    private double _zFilterMin = double.MinValue;
    private double _zFilterMax = double.MaxValue;

    /// <summary>
    /// Shows or hides node controls based on whether each node's Z value falls within
    /// [<paramref name="minZ"/>, <paramref name="maxZ"/>]. Nodes outside the range are
    /// collapsed; nodes inside are made visible.
    /// </summary>
    public void SetZFilter(double minZ, double maxZ)
    {
        _zFilterMin = minZ;
        _zFilterMax = maxZ;
        foreach (var node in Nodes)
        {
            var ctrl = GetNodeControl(node);
            if (ctrl is not null)
                ctrl.Visibility = PassesZFilter(node) ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private bool PassesZFilter(GraphNode node) => node.Z >= _zFilterMin && node.Z <= _zFilterMax;

    public PathfindingNetworkGraphEditor()
    {
        // Create a graph-space layer for rotation arrows.
        // It is added to the main graph canvas at index 3 (between connections and nodes).
        _rotationCanvas = new Canvas
        {
            IsHitTestVisible = false
        };
        _canvas.Children.Insert(3, _rotationCanvas);

        // React to zoom/pan changes so we can update arrow visibility.
        CanvasTransform.Changed += OnCanvasTransformChanged;
    }

    // -----------------------------------------------------------------------
    //  Override node lifecycle methods
    // -----------------------------------------------------------------------

    public override void AddNode(GraphNode node)
    {
        base.AddNode(node);
        if (node is NavigationPoint nav)
            AddRotationArrow(nav);
        var ctrl = GetNodeControl(node);
        if (ctrl is not null && !PassesZFilter(node))
            ctrl.Visibility = Visibility.Collapsed;
    }

    public override bool RemoveNode(GraphNode node)
    {
        var removed = base.RemoveNode(node);
        if (removed && node is NavigationPoint nav)
            RemoveRotationArrow(nav);
        return removed;
    }

    public override void ClearAll()
    {
        base.ClearAll();
        // Since _rotationCanvas is inside _canvas at index 2, base.ClearAll()
        // will preserve it now that we've updated CanvasChildrenToPreserve.
        // We just need to clear its children (the actual arrow visuals).
        DetachAllRotationArrows();
        _rotationCanvas.Children.Clear();
        _zFilterMin = double.MinValue;
        _zFilterMax = double.MaxValue;
    }

    // -----------------------------------------------------------------------
    //  Rotation-arrow management
    // -----------------------------------------------------------------------

    private void AddRotationArrow(NavigationPoint nav)
    {
        var arrow = new RotationArrowVisual(nav);
        _rotationArrows[nav] = arrow;
        _rotationCanvas.Children.Add(arrow);
        arrow.Visibility = ArrowShouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RemoveRotationArrow(NavigationPoint nav)
    {
        if (_rotationArrows.Remove(nav, out var arrow))
        {
            arrow.Detach();
            _rotationCanvas.Children.Remove(arrow);
        }
    }

    private void DetachAllRotationArrows()
    {
        foreach (var arrow in _rotationArrows.Values)
            arrow.Detach();
        _rotationArrows.Clear();
    }

    private void OnCanvasTransformChanged(object? sender, EventArgs e)
    {
        if (_showRotation)
            UpdateAllArrowVisibility();
    }

    private void UpdateAllArrowVisibility()
    {
        var visibility = ArrowShouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
        foreach (var arrow in _rotationArrows.Values)
            arrow.Visibility = visibility;
    }

    private bool ArrowShouldBeVisible =>
        _showRotation && CurrentZoom >= Settings.PathfindingNetworkEditor_RotationArrowMinZoom;

    // -----------------------------------------------------------------------
    //  Inner visual — one per NavigationPoint
    // -----------------------------------------------------------------------

    private sealed class RotationArrowVisual : FrameworkElement
    {
        private readonly NavigationPoint _node;

        public RotationArrowVisual(NavigationPoint node)
        {
            _node = node;
            IsHitTestVisible = false;
            Width  = node.Width;
            Height = node.Height;
            UpdateCanvasPosition();
            node.PropertyChanged += OnNodePropertyChanged;
        }

        /// <summary>Unsubscribes from the node's property-changed event.</summary>
        public void Detach() => _node.PropertyChanged -= OnNodePropertyChanged;

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(GraphNode.X):
                case nameof(GraphNode.Y):
                    UpdateCanvasPosition();
                    break;
                case nameof(GraphNode.Width):
                    Width = _node.Width;
                    UpdateCanvasPosition();
                    InvalidateVisual();
                    break;
                case nameof(GraphNode.Height):
                    Height = _node.Height;
                    UpdateCanvasPosition();
                    InvalidateVisual();
                    break;
                case nameof(NavigationPoint.RotationArrowColor):
                    InvalidateVisual();
                    break;
            }
        }

        private void UpdateCanvasPosition()
        {
            Canvas.SetLeft(this, _node.X - _node.Width  / 2);
            Canvas.SetTop (this, _node.Y - _node.Height / 2);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var w = _node.Width;
            var h = _node.Height;

            // Convert Unreal rotation units → radians.
            // In Unreal (top-down): 0 = East (+X). WPF canvas has +Y downward, so
            // we negate the sine component to keep East = right, North = up on screen.
            double angleRad = _node.RotationYaw * (2.0 * Math.PI / 65536.0);
            double dx =  Math.Cos(angleRad);
            double dy = Math.Sin(angleRad); // negate Y for WPF's y-down coordinate space

            double cx = w / 2.0;
            double cy = h / 2.0;

            // Arrow length — at least 64 units to stick out from behind the shape.
            double radius   = Math.Min(w, h) / 2.0;
            double arrowLen = Math.Max(64.0, radius * 1.5);
            double headLen  = arrowLen * 0.25; // Smaller head proportion for long arrows
            double headHalf = headLen  * 0.40;

            // Perpendicular direction (for arrowhead base)
            double px = -dy;
            double py =  dx;

            var tip    = new Point(cx + dx * arrowLen, cy + dy * arrowLen);
            var shaftEnd = new Point(tip.X - dx * headLen, tip.Y - dy * headLen);
            var tailStart = new Point(cx - dx * radius * 0.25, cy - dy * radius * 0.25);

            var headL  = new Point(shaftEnd.X + px * headHalf, shaftEnd.Y + py * headHalf);
            var headR  = new Point(shaftEnd.X - px * headHalf, shaftEnd.Y - py * headHalf);

            var brush = new SolidColorBrush(_node.RotationArrowColor);
            brush.Freeze();
            var pen = new Pen(brush, 1.5);
            pen.Freeze();

            // Shaft
            dc.DrawLine(pen, tailStart, shaftEnd);

            // Filled arrowhead triangle
            var head = new StreamGeometry();
            using (var ctx = head.Open())
            {
                ctx.BeginFigure(tip,   isFilled: true, isClosed: true);
                ctx.LineTo(headL, isStroked: false, isSmoothJoin: false);
                ctx.LineTo(headR, isStroked: false, isSmoothJoin: false);
            }
            head.Freeze();
            dc.DrawGeometry(brush, null, head);
        }
    }
}
