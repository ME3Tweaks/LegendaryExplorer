using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Controls;

/// <summary>
/// A vertical dual-thumb range slider for filtering graph nodes by Z value.
/// <para>
/// The track spans <see cref="MinValue"/> (bottom) to <see cref="MaxValue"/> (top).
/// The upper thumb controls <see cref="UpperValue"/> and the lower thumb controls <see cref="LowerValue"/>.
/// A yellow line is drawn at <see cref="CameraValue"/> to indicate the camera's current Z.
/// </para>
/// </summary>
public class ZRangeSlider : FrameworkElement
{
    // -----------------------------------------------------------------------
    //  Layout constants
    // -----------------------------------------------------------------------

    private const double TopPad     = 20; // space above track for the MaxValue label
    private const double BotPad     = 20; // space below track for the MinValue label
    private const double TrackHalfW = 3;  // half-width of the track bar
    private const double ThumbHalfW = 18; // half-width of each thumb rectangle
    private const double ThumbHalfH = 8;  // half-height of each thumb rectangle
    private const double CamHalfW   = 22; // half-width of the camera indicator line

    // -----------------------------------------------------------------------
    //  Dependency Properties
    // -----------------------------------------------------------------------

    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(ZRangeSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(ZRangeSlider),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LowerValueProperty =
        DependencyProperty.Register(nameof(LowerValue), typeof(double), typeof(ZRangeSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty UpperValueProperty =
        DependencyProperty.Register(nameof(UpperValue), typeof(double), typeof(ZRangeSlider),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CameraValueProperty =
        DependencyProperty.Register(nameof(CameraValue), typeof(double), typeof(ZRangeSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double MinValue    { get => (double)GetValue(MinValueProperty);    set => SetValue(MinValueProperty,    value); }
    public double MaxValue    { get => (double)GetValue(MaxValueProperty);    set => SetValue(MaxValueProperty,    value); }
    public double LowerValue  { get => (double)GetValue(LowerValueProperty);  set => SetValue(LowerValueProperty,  value); }
    public double UpperValue  { get => (double)GetValue(UpperValueProperty);  set => SetValue(UpperValueProperty,  value); }
    public double CameraValue { get => (double)GetValue(CameraValueProperty); set => SetValue(CameraValueProperty, value); }

    // -----------------------------------------------------------------------
    //  Frozen rendering resources
    // -----------------------------------------------------------------------

    private static T Freeze<T>(T obj) where T : Freezable { obj.Freeze(); return obj; }

    private static readonly Brush TrackBg          = Freeze(new SolidColorBrush(Color.FromRgb(45, 45, 45)));
    private static readonly Brush ActiveBandBrush  = Freeze(new SolidColorBrush(Color.FromArgb(110, 80, 200, 120)));
    private static readonly Brush ThumbBrush       = Freeze(new SolidColorBrush(Color.FromRgb(155, 210, 165)));
    private static readonly Pen   ThumbPen         = Freeze(new Pen(Brushes.White, 1.0));
    private static readonly Pen   CamPen           = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(255, 210, 0)), 1.5));
    private static readonly Pen   CamOutlinePen    = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), 3.0));
    private static readonly SolidColorBrush ThumbLabelBrush    = Freeze(new SolidColorBrush(Colors.Black));
    private static readonly SolidColorBrush ExtremeLabelBrush  = Freeze(new SolidColorBrush(Color.FromRgb(160, 160, 160)));
    private static readonly Typeface LabelFace = new("Segoe UI");

    // -----------------------------------------------------------------------
    //  Drag state
    // -----------------------------------------------------------------------

    private enum DragTarget { None, Lower, Upper }
    private DragTarget _drag = DragTarget.None;

    // -----------------------------------------------------------------------
    //  Constructor
    // -----------------------------------------------------------------------

    public ZRangeSlider()
    {
        Focusable = false;
        Cursor = Cursors.SizeNS;
        MinWidth = 50;
    }

    // -----------------------------------------------------------------------
    //  Coordinate helpers
    // -----------------------------------------------------------------------

    private double TrackTop    => TopPad;
    private double TrackBottom => Math.Max(TopPad + 1, ActualHeight - BotPad);
    private double TrackHeight => TrackBottom - TrackTop;

    /// <summary>Converts a Z value to a screen Y position (max at top, min at bottom).</summary>
    private double ToY(double value)
    {
        double range = MaxValue - MinValue;
        if (Math.Abs(range) < 1e-6) return (TrackTop + TrackBottom) / 2.0;
        return TrackBottom - Math.Clamp((value - MinValue) / range, 0.0, 1.0) * TrackHeight;
    }

    /// <summary>Converts a screen Y position to a Z value.</summary>
    private double ToValue(double y)
    {
        double range = MaxValue - MinValue;
        if (Math.Abs(range) < 1e-6) return MinValue;
        return MinValue + Math.Clamp((TrackBottom - y) / TrackHeight, 0.0, 1.0) * range;
    }

    // -----------------------------------------------------------------------
    //  Hit-test override so the whole element is clickable
    // -----------------------------------------------------------------------

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => new PointHitTestResult(this, hitTestParameters.HitPoint);

    // -----------------------------------------------------------------------
    //  Rendering
    // -----------------------------------------------------------------------

    protected override void OnRender(DrawingContext dc)
    {
        double cx  = ActualWidth / 2.0;
        double top = TrackTop;
        double bot = TrackBottom;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Track background
        dc.DrawRectangle(TrackBg, null,
            new Rect(cx - TrackHalfW, top, TrackHalfW * 2, bot - top));

        double upperY = ToY(Math.Clamp(UpperValue, MinValue, MaxValue));
        double lowerY = ToY(Math.Clamp(LowerValue, MinValue, MaxValue));

        // Active band between the two thumbs
        if (upperY < lowerY)
            dc.DrawRectangle(ActiveBandBrush, null,
                new Rect(cx - TrackHalfW, upperY, TrackHalfW * 2, lowerY - upperY));

        // Camera Z indicator line (outlined then filled so it reads on any background)
        double camY = ToY(CameraValue);
        dc.DrawLine(CamOutlinePen,
            new Point(cx - CamHalfW, camY),
            new Point(cx + CamHalfW, camY));
        dc.DrawLine(CamPen,
            new Point(cx - CamHalfW, camY),
            new Point(cx + CamHalfW, camY));

        // Thumbs — draw upper first so the lower is on top when they overlap
        DrawThumb(dc, cx, upperY, $"{UpperValue:F0}", dpi);
        DrawThumb(dc, cx, lowerY, $"{LowerValue:F0}", dpi);

        // Extreme value labels
        var maxFt = MakeText($"{MaxValue:F0}", 9, ExtremeLabelBrush, dpi);
        dc.DrawText(maxFt, new Point(cx - maxFt.Width / 2, top - maxFt.Height - 1));

        var minFt = MakeText($"{MinValue:F0}", 9, ExtremeLabelBrush, dpi);
        dc.DrawText(minFt, new Point(cx - minFt.Width / 2, bot + 1));
    }

    private void DrawThumb(DrawingContext dc, double cx, double y, string label, double dpi)
    {
        dc.DrawRoundedRectangle(ThumbBrush, ThumbPen,
            new Rect(cx - ThumbHalfW, y - ThumbHalfH, ThumbHalfW * 2, ThumbHalfH * 2), 3, 3);

        var ft = MakeText(label, 8, ThumbLabelBrush, dpi);
        ft.MaxTextWidth = ThumbHalfW * 2;
        ft.TextAlignment = TextAlignment.Center;
        dc.DrawText(ft, new Point(cx - ThumbHalfW, y - ft.Height / 2));
    }

    private static FormattedText MakeText(string text, double size, SolidColorBrush brush, double dpi) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelFace, size, brush, dpi);

    // -----------------------------------------------------------------------
    //  Mouse input
    // -----------------------------------------------------------------------

    private bool HitsThumb(Point pos, double cx, double thumbY) =>
        Math.Abs(pos.X - cx) <= ThumbHalfW + 2 &&
        Math.Abs(pos.Y - thumbY) <= ThumbHalfH + 4;

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Left) return;

        var pos    = e.GetPosition(this);
        double cx  = ActualWidth / 2.0;
        double uy  = ToY(Math.Clamp(UpperValue, MinValue, MaxValue));
        double ly  = ToY(Math.Clamp(LowerValue, MinValue, MaxValue));

        // Prefer upper thumb when both overlap
        if (HitsThumb(pos, cx, uy))
            _drag = DragTarget.Upper;
        else if (HitsThumb(pos, cx, ly))
            _drag = DragTarget.Lower;
        else
            return;

        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drag == DragTarget.None) return;

        double v = ToValue(e.GetPosition(this).Y);
        if (_drag == DragTarget.Upper)
            // SetCurrentValue preserves the two-way binding while still notifying it
            SetCurrentValue(UpperValueProperty, Math.Max(LowerValue, Math.Clamp(v, MinValue, MaxValue)));
        else
            SetCurrentValue(LowerValueProperty, Math.Min(UpperValue, Math.Clamp(v, MinValue, MaxValue)));

        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton != MouseButton.Left || _drag == DragTarget.None) return;
        _drag = DragTarget.None;
        ReleaseMouseCapture();
        e.Handled = true;
    }
}
