using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Controls;

/// <summary>
/// Lightweight visual element that renders a single graph node using <see cref="OnRender"/>.
/// Shape, color, and label are driven by the bound <see cref="GraphNode"/>.
/// </summary>
public class NodeControl : FrameworkElement
{
    private readonly GraphNode _node;
    private static readonly Typeface DefaultTypeface = new("Segoe UI");

    /// <summary>
    /// When true, node labels are drawn. Managed by <see cref="NetworkGraphEditor"/>.
    /// </summary>
    public bool ShouldDrawLabels { get; set; } = true;

    // Cached rendering resources — recreated only when the source color/thickness changes.
    private Pen? _borderPen;
    private SolidColorBrush? _fillBrush;
    private Color _cachedBorderColor;
    private Color _cachedFillColor;
    private double _cachedBorderThickness;

    public NodeControl(GraphNode node)
    {
        _node = node;
        Width = node.Width;
        Height = node.Height;
        Cursor = Cursors.Hand;
        node.PropertyChanged += OnNodePropertyChanged;
    }

    /// <summary>
    /// The graph node this control represents.
    /// </summary>
    public GraphNode Node => _node;

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GraphNode.EffectiveBackgroundColor):
            case nameof(GraphNode.EffectiveBorderColor):
            case nameof(GraphNode.Label):
            case nameof(GraphNode.Shape):
            case nameof(GraphNode.BorderThickness):
            case nameof(PlayerNode.RotationYaw):
            case nameof(CameraNode.FOV):
                InvalidateVisual();
                break;
            case nameof(GraphNode.Width):
                Width = _node.Width;
                InvalidateVisual();
                break;
            case nameof(GraphNode.Height):
                Height = _node.Height;
                InvalidateVisual();
                break;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var fillColor = _node.EffectiveBackgroundColor;
        var borderColor = _node.EffectiveBorderColor;
        var borderThickness = _node.BorderThickness;

        if (_fillBrush is null || _cachedFillColor != fillColor)
        {
            _fillBrush = new SolidColorBrush(fillColor);
            _fillBrush.Freeze();
            _cachedFillColor = fillColor;
        }

        if (_borderPen is null || _cachedBorderColor != borderColor || _cachedBorderThickness != borderThickness)
        {
            var borderBrush = new SolidColorBrush(borderColor);
            borderBrush.Freeze();
            _borderPen = new Pen(borderBrush, borderThickness);
            _borderPen.Freeze();
            _cachedBorderColor = borderColor;
            _cachedBorderThickness = borderThickness;
        }

        var w = _node.Width;
        var h = _node.Height;

        switch (_node.Shape)
        {
            case NodeShape.Circle:
                dc.DrawEllipse(_fillBrush, _borderPen, new Point(w / 2, h / 2), w / 2, h / 2);
                break;

            case NodeShape.Rectangle:
                dc.DrawRoundedRectangle(_fillBrush, _borderPen, new Rect(0, 0, w, h), 4, 4);
                break;

            case NodeShape.Diamond:
            {
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(w / 2, 0), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(w, h / 2), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w / 2, h), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(0, h / 2), isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.Hexagon:
            {
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    var qw = w / 4;
                    ctx.BeginFigure(new Point(qw, 0), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(w - qw, 0), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w, h / 2), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w - qw, h), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(qw, h), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(0, h / 2), isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.Mantle:
            {
                // An up-then-right (L-shaped) arrow.
                // The shaft runs up the left side then turns right, with arrowheads at the
                // top of the vertical run and at the right end of the horizontal run.
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    var sx = w * 0.35;  // shaft half-width / notch x
                    var sy = h * 0.35;  // shaft half-height / notch y
                    var ah = w * 0.30;  // arrowhead half-span

                    // Right-pointing arrowhead at (w, h/2), shaft going left then down
                    // then up-shaft going to top, arrowhead pointing up at (w/2, 0).
                    // Outline (single filled polygon, wound clockwise):
                    //   Start at tip of right arrow
                    ctx.BeginFigure(new Point(w, h / 2), isFilled: true, isClosed: true);
                    // right arrowhead lower edge → shaft bottom-right
                    ctx.LineTo(new Point(w - ah, h / 2 + ah), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w - ah, h / 2 + sx), isStroked: true, isSmoothJoin: false);
                    // shaft goes left to the elbow
                    ctx.LineTo(new Point(w / 2 + sx, h / 2 + sx), isStroked: true, isSmoothJoin: false);
                    // elbow — shaft goes up
                    ctx.LineTo(new Point(w / 2 + sx, ah), isStroked: true, isSmoothJoin: false);
                    // up arrowhead right edge → tip → left edge
                    ctx.LineTo(new Point(w / 2 + ah, ah), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w / 2, 0), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w / 2 - ah, ah), isStroked: true, isSmoothJoin: false);
                    // up arrowhead left edge back down shaft
                    ctx.LineTo(new Point(w / 2 - sx, ah), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w / 2 - sx, h / 2 - sx), isStroked: true, isSmoothJoin: false);
                    // elbow inner corner
                    ctx.LineTo(new Point(w - ah, h / 2 - sx), isStroked: true, isSmoothJoin: false);
                    // right arrowhead upper edge
                    ctx.LineTo(new Point(w - ah, h / 2 - ah), isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.Jump:
            {
                // A chevron / lightning-bolt: diagonal right-up then diagonal right-down.
                // Two arrowhead tips: upper-right and lower-right; shaft enters from the left.
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    var mx = w / 2;   // x midpoint (peak of the V)
                    var sh = h * 0.20; // half shaft thickness

                    // Upper arrowhead tip
                    ctx.BeginFigure(new Point(w, h * 0.25), isFilled: true, isClosed: true);
                    // Lower arrowhead tip
                    ctx.LineTo(new Point(w, h * 0.75), isStroked: true, isSmoothJoin: false);
                    // Inner lower diagonal back to centre
                    ctx.LineTo(new Point(mx, h / 2 + sh), isStroked: true, isSmoothJoin: false);
                    // Continue inner lower diagonal to left edge
                    ctx.LineTo(new Point(0, h - sh), isStroked: true, isSmoothJoin: false);
                    // Left edge bottom to top
                    ctx.LineTo(new Point(0, h * sh / h), isStroked: true, isSmoothJoin: false);
                    // Outer upper diagonal to centre
                    ctx.LineTo(new Point(mx, h / 2 - sh), isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.BoostUp:
            case NodeShape.BoostDown:
            {
                // A stylised boot with a rocket nozzle on the sole and an arrow indicating direction.
                bool up = _node.Shape == NodeShape.BoostUp;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    // --- boot outline (fills bottom 60 % of the bounding box) ---
                    var bootTop  = h * 0.40;
                    var bootMid  = h * 0.60;
                    var bootBot  = h * 0.85;
                    var nozzleH  = h * 0.15; // nozzle protrudes below bootBot
                    var legLeft  = w * 0.25;
                    var legRight = w * 0.55;
                    var toeRight = w * 0.85;

                    // Boot body
                    ctx.BeginFigure(new Point(legLeft, bootTop), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(legRight, bootTop), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(legRight, bootMid), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(toeRight, bootMid), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(toeRight, bootBot), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(legLeft, bootBot), isStroked: true, isSmoothJoin: false);

                    // Nozzle (flared exhaust bell under the heel)
                    var nozzleLeft  = legLeft + w * 0.04;
                    var nozzleRight = legRight - w * 0.04;
                    var bellLeft    = legLeft - w * 0.04;
                    var bellRight   = legRight + w * 0.04;
                    ctx.LineTo(new Point(nozzleLeft, bootBot), isStroked: false, isSmoothJoin: false);
                    ctx.LineTo(new Point(bellLeft,   bootBot + nozzleH * 0.5), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(bellLeft,   bootBot + nozzleH), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(bellRight,  bootBot + nozzleH), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(bellRight,  bootBot + nozzleH * 0.5), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(nozzleRight, bootBot), isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);

                // --- direction arrow above (BoostUp) or below (BoostDown) the boot ---
                var arrowGeo = new StreamGeometry();
                using (var ctx = arrowGeo.Open())
                {
                    var arrowX    = w / 2;
                    var shaftW    = w * 0.12;
                    var arrowSpan = w * 0.22;

                    if (up)
                    {
                        // Arrow points upward in the top 35 % of the bounding box
                        var tipY   = 0.0;
                        var baseY  = h * 0.35;
                        var shaftY = h * 0.38;
                        ctx.BeginFigure(new Point(arrowX, tipY), isFilled: true, isClosed: true);
                        ctx.LineTo(new Point(arrowX + arrowSpan, baseY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX + shaftW,    baseY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX + shaftW,    shaftY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX - shaftW,    shaftY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX - shaftW,    baseY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX - arrowSpan, baseY), isStroked: true, isSmoothJoin: false);
                    }
                    else
                    {
                        // Arrow points downward below the nozzle
                        var tipY   = h;
                        var baseY  = h * 0.88;
                        var shaftY = h * 0.85;
                        ctx.BeginFigure(new Point(arrowX, tipY), isFilled: true, isClosed: true);
                        ctx.LineTo(new Point(arrowX + arrowSpan, baseY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX + shaftW,    baseY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX + shaftW,    shaftY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX - shaftW,    shaftY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX - shaftW,    baseY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(arrowX - arrowSpan, baseY), isStroked: true, isSmoothJoin: false);
                    }
                }
                arrowGeo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, arrowGeo);
                break;
            }

            case NodeShape.CoverLink:
            {
                // T-shaped tetromino: stem pointing up, horizontal bar at the bottom.
                // Stem occupies the middle third horizontally; bar spans the full width.
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    var barH     = h * 0.40;  // height of the horizontal bar
                    var stemL    = w / 3;      // stem left edge
                    var stemR    = w * 2 / 3;  // stem right edge

                    ctx.BeginFigure(new Point(stemL, 0), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(stemR, 0),    isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(stemR, h - barH), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w, h - barH), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w, h),        isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(0, h),        isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(0, h - barH), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(stemL, h - barH), isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.CoverSlotMarker:
            {
                // 4-point shield: like a diamond but the left/right vertices sit at 3/4 up
                // the shape (y = h/4 from the top) rather than at the midpoint (y = h/2).
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    var sideY = h * 0.25; // left/right vertices are 1/4 from the top → 3/4 up
                    ctx.BeginFigure(new Point(w / 2, 0), isFilled: true, isClosed: true); // top
                    ctx.LineTo(new Point(w, sideY),    isStroked: true, isSmoothJoin: false); // right
                    ctx.LineTo(new Point(w / 2, h),    isStroked: true, isSmoothJoin: false); // bottom
                    ctx.LineTo(new Point(0, sideY),    isStroked: true, isSmoothJoin: false); // left
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.LadderUp:
            case NodeShape.LadderDown:
            {
                // Two vertical rails with 3 horizontal rungs and a directional triangle.
                // FillRule.Nonzero ensures rung/rail overlaps render as solid fill.
                bool isUp = _node.Shape == NodeShape.LadderUp;
                var arrowBaseY = isUp ? h * 0.28 : h * 0.72;
                var railTop    = isUp ? h * 0.24 : 0;
                var railBot    = isUp ? h        : h * 0.76;
                var rungH      = h * 0.06;
                var rung1Y     = isUp ? h * 0.40 : h * 0.14;
                var rung2Y     = isUp ? h * 0.58 : h * 0.32;
                var rung3Y     = isUp ? h * 0.76 : h * 0.50;

                var geo = new StreamGeometry { FillRule = FillRule.Nonzero };
                using (var ctx = geo.Open())
                {
                    // Arrowhead triangle — wound clockwise in screen space
                    if (isUp)
                    {
                        ctx.BeginFigure(new Point(w / 2, 0), isFilled: true, isClosed: true);
                        ctx.LineTo(new Point(w * 0.80, arrowBaseY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(w * 0.20, arrowBaseY), isStroked: true, isSmoothJoin: false);
                    }
                    else
                    {
                        ctx.BeginFigure(new Point(w / 2, h), isFilled: true, isClosed: true);
                        ctx.LineTo(new Point(w * 0.20, arrowBaseY), isStroked: true, isSmoothJoin: false);
                        ctx.LineTo(new Point(w * 0.80, arrowBaseY), isStroked: true, isSmoothJoin: false);
                    }

                    // Left rail
                    ctx.BeginFigure(new Point(w * 0.20, railTop), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(w * 0.36, railTop), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.36, railBot), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.20, railBot), isStroked: true, isSmoothJoin: false);

                    // Right rail
                    ctx.BeginFigure(new Point(w * 0.64, railTop), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(w * 0.80, railTop), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.80, railBot), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.64, railBot), isStroked: true, isSmoothJoin: false);

                    // Rung 1
                    ctx.BeginFigure(new Point(w * 0.20, rung1Y), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(w * 0.80, rung1Y),         isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.80, rung1Y + rungH), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.20, rung1Y + rungH), isStroked: true, isSmoothJoin: false);

                    // Rung 2
                    ctx.BeginFigure(new Point(w * 0.20, rung2Y), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(w * 0.80, rung2Y),         isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.80, rung2Y + rungH), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.20, rung2Y + rungH), isStroked: true, isSmoothJoin: false);

                    // Rung 3
                    ctx.BeginFigure(new Point(w * 0.20, rung3Y), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(w * 0.80, rung3Y),         isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.80, rung3Y + rungH), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(w * 0.20, rung3Y + rungH), isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.SFXDoor:
            {
                // Door rectangle with an inward rectangular notch on the left side at
                // mid-height representing the door handle recess.
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(w, 0),              isStroked: true, isSmoothJoin: false); // top
                    ctx.LineTo(new Point(w, h),              isStroked: true, isSmoothJoin: false); // right side
                    ctx.LineTo(new Point(0, h),              isStroked: true, isSmoothJoin: false); // bottom
                    ctx.LineTo(new Point(0, h * 0.65),       isStroked: true, isSmoothJoin: false); // left below notch
                    ctx.LineTo(new Point(w * 0.18, h * 0.65), isStroked: true, isSmoothJoin: false); // notch bottom
                    ctx.LineTo(new Point(w * 0.18, h * 0.35), isStroked: true, isSmoothJoin: false); // notch inner side
                    ctx.LineTo(new Point(0, h * 0.35),       isStroked: true, isSmoothJoin: false); // notch top
                    // closes back to (0, 0)
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.Unknown:
            {
                // Build the vector geometry of a "?" glyph from the font outline and
                // render it with the node's own fill/border so it scales with the node.
                var dpi = VisualTreeHelper.GetDpi(this);
                var ft = new FormattedText(
                    "?",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    DefaultTypeface,
                    Math.Min(w, h) * 0.80,
                    Brushes.Black, // foreground is unused — only BuildGeometry is called
                    dpi.PixelsPerDip);
                var geo = ft.BuildGeometry(new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
                if (geo is not null)
                    dc.DrawGeometry(_fillBrush, _borderPen, geo);
                break;
            }

            case NodeShape.Player:
            {
                var rotate = 0.0;
                if (_node is PlayerNode playerNode)
                {
                    rotate = playerNode.RotationYaw * (360.0 / 65536.0); // Negate for WPF y-down
                }

                dc.PushTransform(new RotateTransform(rotate, w / 2, h / 2));
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    // Triangle pointing right (+X in Unreal is forward, which corresponds to angle 0)
                    ctx.BeginFigure(new Point(w, h / 2), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(0, h), isStroked: true, isSmoothJoin: false);
                    ctx.LineTo(new Point(0, 0), isStroked: true, isSmoothJoin: false);
                }
                geo.Freeze();
                dc.DrawGeometry(_fillBrush, _borderPen, geo);
                dc.Pop();
                break;
            }

            case NodeShape.Camera:
            {
                var rotate = 0.0;
                var fov = 90.0;
                if (_node is CameraNode cameraNode)
                {
                    rotate = cameraNode.RotationYaw * (360.0 / 65536.0); // Negate for WPF y-down
                    fov = cameraNode.FOV;
                }

                dc.PushTransform(new RotateTransform(rotate, w / 2, h / 2));

                // Draw standard camera shape (e.g. circle)
                dc.DrawEllipse(_fillBrush, _borderPen, new Point(w / 2, h / 2), w / 4, h / 4);

                // Draw FOV lines
                var fovRad = fov * Math.PI / 180.0;
                var radiusX = w * 2;
                var end1X = w / 2 + Math.Cos(-fovRad / 2) * radiusX;
                var end1Y = h / 2 + Math.Sin(-fovRad / 2) * radiusX;
                var end2X = w / 2 + Math.Cos(fovRad / 2) * radiusX;
                var end2Y = h / 2 + Math.Sin(fovRad / 2) * radiusX;

                dc.DrawLine(_borderPen, new Point(w / 2, h / 2), new Point(end1X, end1Y));
                dc.DrawLine(_borderPen, new Point(w / 2, h / 2), new Point(end2X, end2Y));

                dc.Pop();
                break;
            }
        }

        // Draw label text
        if (ShouldDrawLabels && !string.IsNullOrEmpty(_node.Label))
        {
            // Choose contrasting text color
            var bg = _node.EffectiveBackgroundColor;
            var brightness = 0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B;
            var textBrush = brightness > 128 ? Brushes.Black : Brushes.White;

            var dpi = VisualTreeHelper.GetDpi(this);
            var text = new FormattedText(
                _node.Label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                DefaultTypeface,
                11,
                textBrush,
                dpi.PixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, w - 4),
                MaxTextHeight = Math.Max(1, h - 4),
                TextAlignment = TextAlignment.Center,
                Trimming = TextTrimming.CharacterEllipsis
            };

            // Position the layout rectangle so that the formatted text (which
            // uses TextAlignment.Center) is centered within the node. Using
            // MaxTextWidth for the layout width ensures the center alignment
            // behaves as expected rather than relying on the measured text
            // width.
            var xOrigin = (w - text.MaxTextWidth) / 2;
            var yOrigin = (h - text.Height) / 2;
            dc.DrawText(text, new Point(xOrigin, yOrigin));
        }
    }

    /// <summary>
    /// Unsubscribes from node property-change events. Call when removing the control.
    /// </summary>
    public void Detach()
    {
        _node.PropertyChanged -= OnNodePropertyChanged;
    }
}
