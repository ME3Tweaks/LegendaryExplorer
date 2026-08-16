using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Controls;

/// <summary>
/// A performant, interactive graph editor control for visualizing pathfinding networks.
/// <para>
/// Features:
/// <list type="bullet">
///   <item>Zoom (scroll wheel, centered on cursor) and pan (middle/right mouse drag).</item>
///   <item>Background image mapped to graph-space coordinates.</item>
///   <item>Nodes with configurable shape, colors, label, and border — draggable with the left mouse button.</item>
///   <item>Connections rendered as lines with configurable color, width, and dash style.</item>
///   <item>Temporary per-node color overrides for highlighting.</item>
/// </list>
/// </para>
/// </summary>
public class NetworkGraphEditor : Border
{
    protected readonly Canvas _canvas;
    private readonly MatrixTransform _canvasTransform;

    /// <summary>Number of initial children in the canvas (background image, tile layer, connections) to preserve during <see cref="ClearAll"/>.</summary>
    protected virtual int CanvasChildrenToPreserve => 3;

    /// <summary>Exposes the shared canvas transform so subclasses can apply it to additional graph-space layers.</summary>
    protected MatrixTransform CanvasTransform => _canvasTransform;
    private readonly ConnectionRenderer _connectionRenderer;
    private readonly Image _backgroundImage;
    private readonly Canvas _tileLayer;
    private readonly List<TileEntry> _tileEntries = [];
    private bool _tileUpdatePending;
    private readonly Canvas _overlayCanvas;
    private readonly Border _hoverPanel;
    private readonly StackPanel _hoverPanelContent;
    private readonly Border _scaleBarContainer;
    private readonly Rectangle _scaleBarLine;
    private readonly TextBlock _scaleBarText;

    private readonly List<GraphNode> _nodes = [];
    private readonly Dictionary<GraphNode, NodeControl> _nodeControls = [];
    private readonly Dictionary<GraphNode, PropertyChangedEventHandler> _nodePropertyHandlers = [];

    // Interaction state
    private bool _isPanning;
    private Point _lastPanPosition;
    private NodeControl? _draggedNode;
    private Point _dragStartScreenPosition;
    private Point _dragStartGraphPosition;
    private double _dragStartNodeX;
    private double _dragStartNodeY;
    private bool _isDraggingNode;
    private readonly HashSet<GraphNode> _selectedNodes = [];

    // Zoom configuration
    private const double MinZoom = 0.01;
    private const double MaxZoom = 50.0;
    private const double ZoomFactor = 1.15;

    /// <summary>Minimum zoom level required to draw labels on nodes. Helps with performance when zoomed out.</summary>
    public static double NodeLabelZoomThreshold = 0.5;

    // Selection
    private static readonly Color SelectionHighlightColor = Colors.White;

    // Batched connection refresh
    private bool _connectionRefreshPending;

    // View animation
    private DispatcherTimer? _animationTimer;

    private bool _lastShowingLabels = true;

    public NetworkGraphEditor()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));

        _canvasTransform = new MatrixTransform(Matrix.Identity);

        _canvas = new Canvas
        {
            RenderTransform = _canvasTransform,
            Background = Brushes.Transparent // Ensures hit-testing on empty areas
        };

        // Background image (rendered behind everything)
        _backgroundImage = new Image
        {
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        _canvas.Children.Add(_backgroundImage); // index 0

        // Tile layer — individual positioned images rendered above the background image.
        _tileLayer = new Canvas { IsHitTestVisible = false };
        _canvas.Children.Add(_tileLayer); // index 1

        // Connection layer (rendered behind nodes, single-pass)
        _connectionRenderer = new ConnectionRenderer
        {
            IsHitTestVisible = false
        };
        _canvas.Children.Add(_connectionRenderer); // index 2

        // Hover info panel — rendered in screen space above the transformed canvas.
        _hoverPanelContent = new StackPanel { Margin = new Thickness(6, 4, 6, 4) };
        _hoverPanel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            MaxWidth = 300,
            Child = _hoverPanelContent,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        _overlayCanvas = new Canvas { IsHitTestVisible = false };
        _overlayCanvas.Children.Add(_hoverPanel);

        // Scale bar visual (bottom-left)
        _scaleBarText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 10,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _scaleBarLine = new Rectangle
        {
            Height = 2,
            Fill = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        var scaleBarStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _scaleBarLine, _scaleBarText }
        };
        _scaleBarContainer = new Border
        {
            Child = scaleBarStack,
            IsHitTestVisible = false
        };
        _overlayCanvas.Children.Add(_scaleBarContainer);

        // Host both the graph canvas and the overlay in a Grid so they share the same space.
        var hostGrid = new Grid();
        hostGrid.Children.Add(_canvas);
        hostGrid.Children.Add(_overlayCanvas);
        Child = hostGrid;

        Focusable = true;
        FocusVisualStyle = null; // Remove the default focus rectangle if desired, or keep it. Let's keep it simple.

        MouseWheel += OnMouseWheel;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseMove += OnMouseMove;
        MouseLeave += OnMouseLeave;

        _canvasTransform.Changed += (_, _) =>
        {
            UpdateScaleBar();
            UpdateLabelVisibility();
            RequestTileUpdate();
        };
        SizeChanged += (_, _) => { UpdateScaleBar(); RequestTileUpdate(); };
    }

    private void UpdateLabelVisibility()
    {
        bool showing = CurrentZoom >= NodeLabelZoomThreshold;
        if (showing != _lastShowingLabels)
        {
            _lastShowingLabels = showing;
            foreach (var control in _nodeControls.Values)
            {
                control.ShouldDrawLabels = showing;
                control.InvalidateVisual();
            }
        }
    }

    // ------------------------------------------------------------------
    //  Events
    // ------------------------------------------------------------------

    /// <summary>Raised when a node is clicked (left mouse button, no drag).</summary>
    public event EventHandler<GraphNode>? NodeClicked;

    /// <summary>Raised when a node finishes being dragged to a new position.</summary>
    public event EventHandler<GraphNode>? NodeMoved;

    /// <summary>Raised when the selection changes. The argument is a snapshot of the currently selected nodes.</summary>
    public event EventHandler<IReadOnlyList<GraphNode>>? SelectionChanged;

    /// <summary>Raised when the mouse moves over the control, providing graph-space coordinates.</summary>
    public event EventHandler<Point>? GraphMouseMoved;

    // ------------------------------------------------------------------
    //  Public read-only state
    // ------------------------------------------------------------------

    /// <summary>All nodes currently in the editor.</summary>
    public IReadOnlyList<GraphNode> Nodes => _nodes;

    /// <summary>Currently selected nodes (snapshot; changes do not affect the editor).</summary>
    public IReadOnlyList<GraphNode> SelectedNodes => [.. _selectedNodes];

    /// <summary>Direct access to the connection renderer.</summary>
    public ConnectionRenderer ConnectionRenderer => _connectionRenderer;

    /// <summary>Current zoom scale factor.</summary>
    public double CurrentZoom => _canvasTransform.Matrix.M11;

    /// <summary>
    /// When set, provides contextual key/value rows displayed in a floating info panel
    /// while the pointer hovers over a node with no mouse button held.
    /// Return <see langword="null"/> or an empty list to suppress the panel for a given node.
    /// </summary>
    public Func<GraphNode, IReadOnlyList<(string Key, string Value)>?>? NodeHoverInfoProvider { get; set; }

    public static readonly DependencyProperty DisableArrowKeyPanningProperty =
        DependencyProperty.Register(nameof(DisableArrowKeyPanning), typeof(bool), typeof(NetworkGraphEditor), new PropertyMetadata(false));

    /// <summary>
    /// When <see langword="true"/>, the keyboard arrow keys do not pan the graph.
    /// </summary>
    public bool DisableArrowKeyPanning
    {
        get => (bool)GetValue(DisableArrowKeyPanningProperty);
        set => SetValue(DisableArrowKeyPanningProperty, value);
    }

    // ------------------------------------------------------------------
    //  Background image
    // ------------------------------------------------------------------

    /// <summary>
    /// Sets a background image mapped to four corner coordinates in graph space.
    /// The image is stretched to the axis-aligned bounding box of the corners.
    /// </summary>
    public void SetBackgroundImage(ImageSource imageSource, Point topLeft, Point topRight, Point bottomLeft, Point bottomRight)
    {
        var minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
        var minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
        var maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
        var maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

        SetBackgroundImage(imageSource, minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Sets a background image at a position and size in graph space.
    /// </summary>
    public void SetBackgroundImage(ImageSource imageSource, double x, double y, double width, double height)
    {
        _backgroundImage.Source = imageSource;
        Canvas.SetLeft(_backgroundImage, x);
        Canvas.SetTop(_backgroundImage, y);
        _backgroundImage.Width = width;
        _backgroundImage.Height = height;
        _backgroundImage.Visibility = Visibility.Visible;
    }

    /// <summary>Removes the background image.</summary>
    public void ClearBackgroundImage()
    {
        _backgroundImage.Source = null;
        _backgroundImage.Visibility = Visibility.Collapsed;
    }

    // ------------------------------------------------------------------
    //  Tiled background image
    // ------------------------------------------------------------------

    /// <summary>
    /// Replaces the tile layer with the supplied set of tiles. Each tile is mapped to a
    /// rectangular region in graph space. Only tiles whose bounds intersect the current
    /// viewport are loaded and displayed; tiles that scroll out of view are unloaded to
    /// conserve memory. The <see cref="BackgroundTile.SourceFactory"/> of each tile is
    /// called on the UI thread the first time the tile scrolls into view (and again if
    /// the tile is subsequently unloaded and re-enters the viewport).
    /// </summary>
    public void SetBackgroundTiles(IEnumerable<BackgroundTile> tiles)
    {
        ClearBackgroundTiles();
        foreach (var tile in tiles)
            _tileEntries.Add(new TileEntry(tile, _tileLayer));
        RequestTileUpdate();
    }

    /// <summary>Removes all background tiles and releases their image sources.</summary>
    public void ClearBackgroundTiles()
    {
        foreach (var entry in _tileEntries)
            entry.Image.Source = null;
        _tileEntries.Clear();
        _tileLayer.Children.Clear();
    }

    private void RequestTileUpdate()
    {
        if (_tileEntries.Count == 0 || _tileUpdatePending) return;
        _tileUpdatePending = true;
        Dispatcher.InvokeAsync(UpdateVisibleTiles, DispatcherPriority.Render);
    }

    private void UpdateVisibleTiles()
    {
        _tileUpdatePending = false;
        if (_tileEntries.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0) return;

        var topLeft     = ScreenToGraph(new Point(0, 0));
        var bottomRight = ScreenToGraph(new Point(ActualWidth, ActualHeight));
        var viewport    = new Rect(topLeft, bottomRight);

        foreach (var entry in _tileEntries)
        {
            var tileBounds = new Rect(entry.Tile.X, entry.Tile.Y, entry.Tile.Width, entry.Tile.Height);
            if (viewport.IntersectsWith(tileBounds))
            {
                if (entry.Image.Source is null)
                    entry.Image.Source = entry.Tile.SourceFactory();
                entry.Image.Visibility = Visibility.Visible;
            }
            else
            {
                entry.Image.Visibility = Visibility.Collapsed;
                entry.Image.Source = null; // Release the decoded bitmap so GC can reclaim memory
            }
        }
    }

    private sealed class TileEntry
    {
        public readonly BackgroundTile Tile;
        public readonly Image Image;

        public TileEntry(BackgroundTile tile, Canvas tileLayer)
        {
            Tile = tile;
            Image = new Image
            {
                Width = tile.Width,
                Height = tile.Height,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Canvas.SetLeft(Image, tile.X);
            Canvas.SetTop(Image, tile.Y);
            tileLayer.Children.Add(Image);
        }
    }

    // ------------------------------------------------------------------
    //  Node management
    // ------------------------------------------------------------------

    /// <summary>Adds a node to the editor.</summary>
    public virtual void AddNode(GraphNode node)
    {
        _nodes.Add(node);
        var control = CreateNodeControl(node);
        _canvas.Children.Add(control);
    }

    /// <summary>Adds multiple nodes.</summary>
    public void AddNodes(IEnumerable<GraphNode> nodes)
    {
        foreach (var node in nodes)
            AddNode(node);
    }

    /// <summary>Removes a node and its visual control.</summary>
    public virtual bool RemoveNode(GraphNode node)
    {
        if (!_nodeControls.TryGetValue(node, out var control))
            return false;

        if (_selectedNodes.Remove(node))
        {
            node.TemporaryBorderColor = null;
            FireSelectionChanged();
        }

        control.Detach();
        _canvas.Children.Remove(control);
        _nodeControls.Remove(node);

        if (_nodePropertyHandlers.TryGetValue(node, out var handler))
        {
            node.PropertyChanged -= handler;
            _nodePropertyHandlers.Remove(node);
        }

        return _nodes.Remove(node);
    }

    /// <summary>Gets the visual control for a node, if present.</summary>
    public NodeControl? GetNodeControl(GraphNode node) => _nodeControls.GetValueOrDefault(node);

    // ------------------------------------------------------------------
    //  Connection management
    // ------------------------------------------------------------------

    /// <summary>Adds a single connection.</summary>
    public void AddConnection(GraphConnection connection) => _connectionRenderer.AddConnection(connection);

    /// <summary>Adds multiple connections in one batch.</summary>
    public void AddConnections(IEnumerable<GraphConnection> connections) => _connectionRenderer.AddConnections(connections);

    /// <summary>Replaces all connections.</summary>
    public void SetConnections(IEnumerable<GraphConnection> connections) => _connectionRenderer.SetConnections(connections);

    /// <summary>Removes a connection.</summary>
    public bool RemoveConnection(GraphConnection connection) => _connectionRenderer.RemoveConnection(connection);

    // ------------------------------------------------------------------
    //  Clear / Reset
    // ------------------------------------------------------------------

    /// <summary>Removes all nodes, connections, the background image, and all background tiles.</summary>
    public virtual void ClearAll()
    {
        _animationTimer?.Stop();
        ClearSelection();
        foreach (var (node, handler) in _nodePropertyHandlers)
            node.PropertyChanged -= handler;

        foreach (var (_, control) in _nodeControls)
            control.Detach();

        _nodeControls.Clear();
        _nodePropertyHandlers.Clear();
        _nodes.Clear();
        _connectionRenderer.Clear();

        // Remove all Canvas children except standard layers (background image, tile layer, connection renderer)
        while (_canvas.Children.Count > CanvasChildrenToPreserve)
            _canvas.Children.RemoveAt(_canvas.Children.Count - 1);

        ClearBackgroundImage();
        ClearBackgroundTiles();
    }

    /// <summary>
    /// Clears the current selection, removing all highlights and raising <see cref="SelectionChanged"/>.
    /// Has no effect when nothing is selected.
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedNodes.Count == 0)
            return;
        foreach (var node in _selectedNodes)
            node.TemporaryBorderColor = null;
        _selectedNodes.Clear();
        FireSelectionChanged();
    }

    /// <summary>Resets zoom and pan to the identity transform.</summary>
    public void ResetView()
    {
        _canvasTransform.Matrix = Matrix.Identity;
    }

    // ------------------------------------------------------------------
    //  View navigation
    // ------------------------------------------------------------------

    /// <summary>Pans the view so that the specified graph-space point is centered in the control.</summary>
    public void CenterOn(Point graphPoint)
    {
        var screenTarget = new Point(ActualWidth / 2, ActualHeight / 2);
        var currentScreenPos = GraphToScreen(graphPoint);
        var dx = screenTarget.X - currentScreenPos.X;
        var dy = screenTarget.Y - currentScreenPos.Y;

        var matrix = _canvasTransform.Matrix;
        matrix.Translate(dx, dy);
        _canvasTransform.Matrix = matrix;
    }

    /// <summary>
    /// Animates the pan to center the specified graph-space point over 500ms.
    /// Does nothing if an animation is already in progress.
    /// </summary>
    public void AnimatedCenterOn(Point graphPoint)
    {
        if (_animationTimer?.IsEnabled == true)
            return;

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            Dispatcher.InvokeAsync(() => AnimatedCenterOn(graphPoint), DispatcherPriority.Background);
            return;
        }

        var startMatrix = _canvasTransform.Matrix;
        var zoom = startMatrix.M11;
        var startTx = startMatrix.OffsetX;
        var startTy = startMatrix.OffsetY;

        var targetTx = ActualWidth / 2 - graphPoint.X * zoom;
        var targetTy = ActualHeight / 2 - graphPoint.Y * zoom;

        var startTime = DateTime.UtcNow;
        const double totalMs = 500.0;

        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animationTimer.Tick += (_, _) =>
        {
            var t = Math.Clamp((DateTime.UtcNow - startTime).TotalMilliseconds / totalMs, 0.0, 1.0);
            var eased = EaseInOutCubic(t);

            var tx = startTx + (targetTx - startTx) * eased;
            var ty = startTy + (targetTy - startTy) * eased;
            _canvasTransform.Matrix = new Matrix(zoom, 0, 0, zoom, tx, ty);

            if (t >= 1.0)
                _animationTimer?.Stop();
        };
        _animationTimer.Start();
    }

    /// <summary>Adjusts zoom and pan so all content is visible with a small margin.</summary>
    public void ZoomToFit()
    {
        if (TryComputeFitMatrix(out var matrix))
            _canvasTransform.Matrix = matrix;
    }

    /// <summary>
    /// Animates the zoom and pan to fit all content within the viewport over the given
    /// <paramref name="duration"/>. If <paramref name="duration"/> is zero or negative the
    /// view is updated immediately. If the control has not yet been laid out the call is
    /// deferred until layout is complete.
    /// </summary>
    public void AnimatedZoomToFit(TimeSpan duration)
    {
        if (_animationTimer?.IsEnabled == true)
            return;

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            // Layout hasn't happened yet — retry after the next render pass.
            Dispatcher.InvokeAsync(() => AnimatedZoomToFit(duration), DispatcherPriority.Background);
            return;
        }

        if (!TryComputeFitMatrix(out var target))
            return;

        _animationTimer?.Stop();

        if (duration <= TimeSpan.Zero)
        {
            _canvasTransform.Matrix = target;
            return;
        }

        var startMatrix = _canvasTransform.Matrix;
        var startScale = startMatrix.M11;
        var startTx    = startMatrix.OffsetX;
        var startTy    = startMatrix.OffsetY;
        var targetScale = target.M11;
        var targetTx    = target.OffsetX;
        var targetTy    = target.OffsetY;

        var startTime = DateTime.UtcNow;
        var totalMs   = duration.TotalMilliseconds;

        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 fps
        };
        _animationTimer.Tick += (_, _) =>
        {
            var t     = Math.Clamp((DateTime.UtcNow - startTime).TotalMilliseconds / totalMs, 0.0, 1.0);
            var eased = EaseInOutCubic(t);

            var scale = startScale + (targetScale - startScale) * eased;
            var tx    = startTx    + (targetTx    - startTx)    * eased;
            var ty    = startTy    + (targetTy    - startTy)    * eased;
            _canvasTransform.Matrix = new Matrix(scale, 0, 0, scale, tx, ty);

            if (t >= 1.0)
                _animationTimer?.Stop();
        };
        _animationTimer.Start();
    }

    /// <summary>
    /// Computes the matrix that would make all current content fill the viewport with a
    /// small margin. Returns <see langword="false"/> when there is nothing to fit or when
    /// the control has not yet been measured.
    /// </summary>
    private bool TryComputeFitMatrix(out Matrix matrix)
    {
        matrix = Matrix.Identity;

        if (_nodes.Count == 0 && _backgroundImage.Visibility != Visibility.Visible && _tileEntries.Count == 0)
            return false;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var node in _nodes)
        {
            minX = Math.Min(minX, node.X - node.Width  / 2);
            minY = Math.Min(minY, node.Y - node.Height / 2);
            maxX = Math.Max(maxX, node.X + node.Width  / 2);
            maxY = Math.Max(maxY, node.Y + node.Height / 2);
        }

        if (_backgroundImage.Visibility == Visibility.Visible && _backgroundImage.Source is not null)
        {
            var imgLeft = Canvas.GetLeft(_backgroundImage);
            var imgTop  = Canvas.GetTop(_backgroundImage);
            minX = Math.Min(minX, imgLeft);
            minY = Math.Min(minY, imgTop);
            maxX = Math.Max(maxX, imgLeft + _backgroundImage.Width);
            maxY = Math.Max(maxY, imgTop  + _backgroundImage.Height);
        }

        foreach (var entry in _tileEntries)
        {
            minX = Math.Min(minX, entry.Tile.X);
            minY = Math.Min(minY, entry.Tile.Y);
            maxX = Math.Max(maxX, entry.Tile.X + entry.Tile.Width);
            maxY = Math.Max(maxY, entry.Tile.Y + entry.Tile.Height);
        }

        if (minX >= maxX || minY >= maxY)
            return false;

        var viewW = ActualWidth;
        var viewH = ActualHeight;
        if (viewW <= 0 || viewH <= 0)
            return false;

        var scale   = Math.Clamp(Math.Min(viewW / (maxX - minX), viewH / (maxY - minY)) * 0.9, MinZoom, MaxZoom);
        var centerX = (minX + maxX) / 2;
        var centerY = (minY + maxY) / 2;

        matrix = Matrix.Identity;
        matrix.Scale(scale, scale);
        matrix.Translate(viewW / 2 - centerX * scale, viewH / 2 - centerY * scale);
        return true;
    }

    private static double EaseInOutCubic(double t) =>
        t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    /// <summary>Sets the zoom level, optionally centered on a screen point.</summary>
    public void SetZoom(double scale, Point? screenCenter = null)
    {
        scale = Math.Clamp(scale, MinZoom, MaxZoom);
        var matrix = _canvasTransform.Matrix;
        var factor = scale / matrix.M11;

        var center = screenCenter ?? new Point(ActualWidth / 2, ActualHeight / 2);

        matrix.ScaleAt(factor, factor, center.X, center.Y);
        _canvasTransform.Matrix = matrix;
    }

    /// <summary>Converts a screen-space point to graph-space coordinates.</summary>
    public Point ScreenToGraph(Point screenPoint)
    {
        var inverse = _canvasTransform.Matrix;
        inverse.Invert();
        return inverse.Transform(screenPoint);
    }

    /// <summary>Converts a graph-space point to screen-space coordinates.</summary>
    public Point GraphToScreen(Point graphPoint) => _canvasTransform.Matrix.Transform(graphPoint);

    /// <summary>Pans the view by the specified screen-space deltas.</summary>
    public void Pan(double dx, double dy)
    {
        var matrix = _canvasTransform.Matrix;
        matrix.Translate(dx, dy);
        _canvasTransform.Matrix = matrix;
    }

    // ------------------------------------------------------------------
    //  Input handling
    // ------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || DisableArrowKeyPanning) return;

        const double panAmount = 40.0;
        switch (e.Key)
        {
            case Key.Left:
                Pan(panAmount, 0);
                e.Handled = true;
                break;
            case Key.Right:
                Pan(-panAmount, 0);
                e.Handled = true;
                break;
            case Key.Up:
                Pan(0, panAmount);
                e.Handled = true;
                break;
            case Key.Down:
                Pan(0, -panAmount);
                e.Handled = true;
                break;
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var screenPos = e.GetPosition(this);
        var matrix = _canvasTransform.Matrix;
        var scaleDelta = e.Delta > 0 ? ZoomFactor : 1.0 / ZoomFactor;

        var newScale = matrix.M11 * scaleDelta;
        if (newScale < MinZoom || newScale > MaxZoom)
            return;

        // ScaleAt post-multiplies (M' = M × S), so the center must be in screen
        // (output) space — passing it directly keeps the cursor position fixed.
        matrix.ScaleAt(scaleDelta, scaleDelta, screenPos.X, screenPos.Y);
        _canvasTransform.Matrix = matrix;
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        _hoverPanel.Visibility = Visibility.Collapsed;

        // Middle or right button → pan
        if (e.MiddleButton == MouseButtonState.Pressed ||
            (e.RightButton == MouseButtonState.Pressed && Keyboard.Modifiers == ModifierKeys.None))
        {
            _isPanning = true;
            _lastPanPosition = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
            return;
        }

        // Left button → node interaction
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var nodeControl = FindNodeControlAt(e.GetPosition(this));
            if (nodeControl is not null)
            {
                _draggedNode = nodeControl;
                _dragStartScreenPosition = e.GetPosition(this);
                _dragStartGraphPosition = ScreenToGraph(_dragStartScreenPosition);
                _dragStartNodeX = nodeControl.Node.X;
                _dragStartNodeY = nodeControl.Node.Y;
                _isDraggingNode = false;
                CaptureMouse();
                e.Handled = true;
            }
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_draggedNode is not null)
        {
            if (_isDraggingNode)
            {
                NodeMoved?.Invoke(this, _draggedNode.Node);
            }
            else
            {
                var clickedNode = _draggedNode.Node;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    // Ctrl+click: toggle this node in the selection
                    if (_selectedNodes.Remove(clickedNode))
                        clickedNode.TemporaryBorderColor = null;
                    else
                    {
                        _selectedNodes.Add(clickedNode);
                        clickedNode.TemporaryBorderColor = SelectionHighlightColor;
                    }
                }
                else
                {
                    // Plain click: replace selection with just this node
                    foreach (var n in _selectedNodes)
                        n.TemporaryBorderColor = null;
                    _selectedNodes.Clear();
                    _selectedNodes.Add(clickedNode);
                    clickedNode.TemporaryBorderColor = SelectionHighlightColor;
                }
                FireSelectionChanged();
                NodeClicked?.Invoke(this, clickedNode);
            }

            _draggedNode = null;
            _isDraggingNode = false;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        // Left-click on empty canvas space — clear selection unless Ctrl is held
        if (e.ChangedButton == MouseButton.Left && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ClearSelection();
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var currentScreenPos = e.GetPosition(this);
        GraphMouseMoved?.Invoke(this, ScreenToGraph(currentScreenPos));

        if (_isPanning)
        {
            _hoverPanel.Visibility = Visibility.Collapsed;
            var currentPos = currentScreenPos;
            var delta = currentPos - _lastPanPosition;
            _lastPanPosition = currentPos;

            var matrix = _canvasTransform.Matrix;
            matrix.Translate(delta.X, delta.Y);
            _canvasTransform.Matrix = matrix;
            e.Handled = true;
            return;
        }

        if (_draggedNode is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            if (!_isDraggingNode)
            {
                if (Math.Abs(currentScreenPos.X - _dragStartScreenPosition.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(currentScreenPos.Y - _dragStartScreenPosition.Y) < SystemParameters.MinimumVerticalDragDistance)
                    return;
                _isDraggingNode = true;
            }
            _hoverPanel.Visibility = Visibility.Collapsed;
            var currentGraphPos = ScreenToGraph(currentScreenPos);
            var dx = currentGraphPos.X - _dragStartGraphPosition.X;
            var dy = currentGraphPos.Y - _dragStartGraphPosition.Y;
            _draggedNode.Node.X = _dragStartNodeX + dx;
            _draggedNode.Node.Y = _dragStartNodeY + dy;
            e.Handled = true;
            return;
        }

        if (e.LeftButton  == MouseButtonState.Released &&
            e.MiddleButton == MouseButtonState.Released &&
            e.RightButton  == MouseButtonState.Released)
        {
            UpdateHoverPanel(currentScreenPos);
        }
    }

    /// <summary>
    /// Walks the visual tree from the hit-test result upward to find a <see cref="NodeControl"/>.
    /// </summary>
    private NodeControl? FindNodeControlAt(Point screenPos)
    {
        var result = InputHitTest(screenPos);
        var dep = result as DependencyObject;
        while (dep is not null)
        {
            if (dep is NodeControl nc)
                return nc;
            dep = VisualTreeHelper.GetParent(dep);
        }
        return null;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e) =>
        _hoverPanel.Visibility = Visibility.Collapsed;

    private void UpdateScaleBar()
    {
        double zoom = CurrentZoom;
        if (zoom <= 0 || ActualHeight <= 0) return;

        // How long the bar can be in pixels
        const double MaxBarScreenWidth = 150;

        // How many graph units that screen width represents
        double maxUnits = MaxBarScreenWidth / zoom;

        // Find a nice 'round' number logic (1, 2, 5, 10, 20, 50, ...) 
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(maxUnits)));
        double relativeValue = maxUnits / magnitude;

        double units;
        if (relativeValue >= 5) units = 5 * magnitude;
        else if (relativeValue >= 2) units = 2 * magnitude;
        else units = 1 * magnitude;

        // Position it explicitly. Since it lives in the overlay canvas, 
        // we set its distance from the bottom-left of the control.
        _scaleBarLine.Width = units * zoom;
        _scaleBarText.Text = $"{units:G} units";

        Canvas.SetLeft(_scaleBarContainer, 12);
        Canvas.SetTop(_scaleBarContainer, ActualHeight - 25);
    }

    private void UpdateHoverPanel(Point screenPos)
    {
        if (NodeHoverInfoProvider is null)
        {
            _hoverPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var nodeControl = FindNodeControlAt(screenPos);
        if (nodeControl is null)
        {
            _hoverPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var rows = NodeHoverInfoProvider(nodeControl.Node);
        if (rows is null || rows.Count == 0)
        {
            _hoverPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _hoverPanelContent.Children.Clear();
        foreach (var (key, value) in rows)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyBlock = new TextBlock
            {
                Text = key + ":",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(keyBlock, 0);

            var valueBlock = new TextBlock
            {
                Text = value,
                Foreground = Brushes.White,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(valueBlock, 1);

            row.Children.Add(keyBlock);
            row.Children.Add(valueBlock);
            _hoverPanelContent.Children.Add(row);
        }

        // Position upper-right of cursor, clamped so the panel stays inside the control.
        const double offsetX = 16;
        const double offsetY = 8;
        var panelW = _hoverPanel.ActualWidth  > 0 ? _hoverPanel.ActualWidth  : _hoverPanel.MaxWidth;
        var panelH = _hoverPanel.ActualHeight > 0 ? _hoverPanel.ActualHeight : 60;
        var x = Math.Min(screenPos.X + offsetX, ActualWidth  - panelW);
        var y = Math.Min(screenPos.Y + offsetY, ActualHeight - panelH);
        Canvas.SetLeft(_hoverPanel, Math.Max(0, x));
        Canvas.SetTop(_hoverPanel,  Math.Max(0, y));
        _hoverPanel.Visibility = Visibility.Visible;
    }

    // ------------------------------------------------------------------
    //  Internal helpers
    // ------------------------------------------------------------------

    private void FireSelectionChanged() =>
        SelectionChanged?.Invoke(this, [.. _selectedNodes]);

    private NodeControl CreateNodeControl(GraphNode node)
    {
        var control = new NodeControl(node);
        control.ShouldDrawLabels = CurrentZoom >= NodeLabelZoomThreshold;
        Canvas.SetLeft(control, node.X - node.Width / 2);
        Canvas.SetTop(control, node.Y - node.Height / 2);

        PropertyChangedEventHandler handler = (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(GraphNode.X):
                case nameof(GraphNode.Width):
                    Canvas.SetLeft(control, node.X - node.Width / 2);
                    RequestConnectionRefresh();
                    break;
                case nameof(GraphNode.Y):
                case nameof(GraphNode.Height):
                    Canvas.SetTop(control, node.Y - node.Height / 2);
                    RequestConnectionRefresh();
                    break;
            }
        };

        node.PropertyChanged += handler;
        _nodeControls[node] = control;
        _nodePropertyHandlers[node] = handler;
        return control;
    }

    /// <summary>
    /// Coalesces multiple per-frame connection refresh requests into a single
    /// <see cref="ConnectionRenderer.Refresh"/> call at <see cref="DispatcherPriority.Render"/>.
    /// </summary>
    private void RequestConnectionRefresh()
    {
        if (_connectionRefreshPending)
            return;
        _connectionRefreshPending = true;
        Dispatcher.InvokeAsync(() =>
        {
            _connectionRefreshPending = false;
            _connectionRenderer.Refresh();
        }, DispatcherPriority.Render);
    }
}
