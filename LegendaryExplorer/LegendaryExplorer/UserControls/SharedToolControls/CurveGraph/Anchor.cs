using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    internal sealed class Anchor : Thumb, INotifyPropertyChanged
    {
        public readonly CurveGraph Graph;
        public readonly LinkedListNode<CurvePoint> Point;
        public int AnchorIndex;

        public Handle LeftHandle;
        public Handle RightHandle;

        public BezierSegment LeftBez;
        public BezierSegment RightBez;

        private double _y;
        public double Y
        {
            get => _y;
            set
            {
                if (SetProperty(ref _y, value) && Graph != null)
                {
                    Point.Value.OutVal = Convert.ToSingle(Graph.toUnrealY(_y));
                    if (IsSelected)
                    {
                        if (Point.Value.InterpMode is EInterpCurveMode.CIM_CurveAuto or EInterpCurveMode.CIM_CurveAutoClamped)
                        {
                            LeftHandle?.TangentUpdate();
                            RightHandle?.TangentUpdate();
                            if (LeftBez != null) LeftBez.Slope2 = Point.Value.ArriveTangent;
                            if (RightBez != null) RightBez.Slope1 = Point.Value.LeaveTangent;
                        }
                        int i = AnchorIndex;
                        if (i > 0 && Point.Previous is { } prevNode && prevNode.Value.InterpMode is EInterpCurveMode.CIM_CurveAuto or EInterpCurveMode.CIM_CurveAutoClamped)
                        {
                            if (Graph.Anchors[i - 1].LeftBez != null) Graph.Anchors[i - 1].LeftBez.Slope2 = prevNode.Value.ArriveTangent;
                            if (Graph.Anchors[i - 1].RightBez != null) Graph.Anchors[i - 1].RightBez.Slope1 = prevNode.Value.LeaveTangent;
                        }
                        if (i < Graph.Anchors.Count - 1 && Point.Next is { } nextNode && nextNode.Value.InterpMode is EInterpCurveMode.CIM_CurveAuto or EInterpCurveMode.CIM_CurveAutoClamped)
                        {
                            if (Graph.Anchors[i + 1].LeftBez != null) Graph.Anchors[i + 1].LeftBez.Slope2 = nextNode.Value.ArriveTangent;
                            if (Graph.Anchors[i + 1].RightBez != null) Graph.Anchors[i + 1].RightBez.Slope1 = nextNode.Value.LeaveTangent;
                        }
                        string val = Point.Value.OutVal.ToString("0.###");
                        if (!Graph.yTextBox.Text.IsNumericallyEqual(val))
                        {
                            Graph.yTextBox.Text = val;
                        }
                    }
                }
            }
        }

        private double _x;

        public double X
        {
            get => _x;
            set
            {
                if (SetProperty(ref _x, value) && Graph != null)
                {
                    Point.Value.InVal = Convert.ToSingle(Graph.toUnrealX(_x));
                    if (IsSelected)
                    {
                        string val = Point.Value.InVal.ToString("0.###");
                        if (!Graph.xTextBox.Text.IsNumericallyEqual(val))
                        {
                            Graph.xTextBox.Text = val;
                        }
                    }
                }
            }
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsSelected.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(Anchor), new PropertyMetadata(false, OnIsSelectedChange));

        private static void OnIsSelectedChange(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Anchor a)
            {
                //selected
                if ((bool)e.NewValue)
                {
                    if (a.LeftHandle is null)
                    {
                        a.LeftHandle = new Handle(a, true);
                        a.Graph.graph.Children.Add(a.LeftHandle);
                    }

                    if (a.RightHandle is null)
                    {
                        a.RightHandle = new Handle(a, false);
                        a.Graph.graph.Children.Add(a.RightHandle);
                    }

                    string val = a.Point.Value.InVal.ToString("0.###");
                    if (!a.Graph.xTextBox.Text.IsNumericallyEqual(val))
                    {
                        a.Graph.xTextBox.Text = val;
                    }
                    val = a.Point.Value.OutVal.ToString("0.###");
                    if (!a.Graph.yTextBox.Text.IsNumericallyEqual(val))
                    {
                        a.Graph.yTextBox.Text = val;
                    }

                    //left handle
                    if (a.Point.Previous != null)
                    {
                        switch (a.Point.Previous.Value.InterpMode)
                        {
                            case EInterpCurveMode.CIM_CurveAuto:
                            case EInterpCurveMode.CIM_CurveUser:
                            case EInterpCurveMode.CIM_CurveBreak:
                            case EInterpCurveMode.CIM_CurveAutoClamped:
                                a.LeftHandle.Visibility = Visibility.Visible;
                                break;
                            case EInterpCurveMode.CIM_Linear:
                            case EInterpCurveMode.CIM_Constant:
                            default:
                                a.LeftHandle.Visibility = Visibility.Hidden;
                                break;
                        }
                    }
                    else
                    {
                        a.LeftHandle.Visibility = Visibility.Hidden;
                    }

                    //right handle
                    if (a.Point.Next != null)
                    {
                        switch (a.Point.Value.InterpMode)
                        {
                            case EInterpCurveMode.CIM_CurveAuto:
                            case EInterpCurveMode.CIM_CurveUser:
                            case EInterpCurveMode.CIM_CurveBreak:
                            case EInterpCurveMode.CIM_CurveAutoClamped:
                                a.RightHandle.Visibility = Visibility.Visible;
                                break;
                            case EInterpCurveMode.CIM_Linear:
                            case EInterpCurveMode.CIM_Constant:
                            default:
                                a.RightHandle.Visibility = Visibility.Hidden;
                                break;
                        }
                    }
                    else
                    {
                        a.RightHandle.Visibility = Visibility.Hidden;
                    }
                }
                //unselected
                else
                {
                    if (a.RightHandle is not null)
                    {
                        a.Graph.graph.Children.Remove(a.RightHandle);
                        a.RightHandle.Dispose();
                        a.RightHandle = null;
                    }
                    if (a.LeftHandle is not null)
                    {
                        a.Graph.graph.Children.Remove(a.LeftHandle);
                        a.LeftHandle.Dispose();
                        a.LeftHandle = null;
                    }
                }
            }
        }

        public Anchor()
        {
        }

        public Anchor(CurveGraph g, LinkedListNode<CurvePoint> p)
        {
            Graph = g;
            Point = p;
            X = Graph.toLocalX(Point.Value.InVal);
            Y = Graph.toLocalY(Point.Value.OutVal);
            this.DragDelta += OnDragDelta;
            this.DragStarted += OnDragStarted;
            this.MouseDown += Anchor_MouseDown;

            LeftBez = null;
            RightBez = null;
        }

        private void Anchor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                var cm = new ContextMenu();
                var setTime = new MenuItem {Header = "Set Time"};
                setTime.Click += SetTime_Click;
                cm.Items.Add(setTime);
                var setValue = new MenuItem {Header = "Set Value"};
                setValue.Click += SetValue_Click;
                cm.Items.Add(setValue);
                switch (Point.Value.InterpMode)
                {
                    case EInterpCurveMode.CIM_CurveAuto:
                    case EInterpCurveMode.CIM_CurveUser:
                    case EInterpCurveMode.CIM_CurveAutoClamped:
                        var breakTangents = new MenuItem();
                        breakTangents.Header = "Break Tangents";
                        breakTangents.Click += BreakTangents_Click;
                        cm.Items.Add(breakTangents);
                        break;
                    case EInterpCurveMode.CIM_CurveBreak:
                        var flattenTangents = new MenuItem();
                        flattenTangents.Header = "Flatten Tangents";
                        flattenTangents.Click += FlattenTangents_Click;
                        cm.Items.Add(flattenTangents);
                        break;
                    default:
                        break;
                }
                var deleteKey = new MenuItem {Header = "Delete Key"};
                deleteKey.Click += DeleteKey_Click;
                cm.Items.Add(deleteKey);
                cm.PlacementTarget = sender as Anchor;
                cm.IsOpen = true;
            }
        }

        private void DeleteKey_Click(object sender, RoutedEventArgs e)
        {
            if (Point.Previous != null)
            {
                Graph.SelectedPoint = Point.Previous.Value;
            }
            else if (Point.Next != null)
            {
                Graph.SelectedPoint = Point.Next.Value;
            }
            Graph.SelectedCurve.RemovePoint(Point);
            Graph.Paint();
        }

        private void SetTime_Click(object sender, RoutedEventArgs e)
        {
            float prev = Point.Previous?.Value.InVal ?? float.MinValue;
            float next = Point.Next?.Value.InVal ?? float.MaxValue;
            string res = PromptDialog.Prompt(this, $"Enter time between {prev} and {next}", "Set Time", Point.Value.InVal.ToString());
            if (float.TryParse(res, out float result) && result > prev && result < next)
            {
                X = Graph.toLocalX(result);
                Graph.Paint(true);
            }
        }

        private void SetValue_Click(object sender, RoutedEventArgs e)
        {
            string res = PromptDialog.Prompt(this, "Enter new value", "Set Value", Point.Value.OutVal.ToString());
            if (float.TryParse(res, out float result))
            {
                Y = Graph.toLocalY(result);
                Graph.Paint(true);
            }
        }

        private void BreakTangents_Click(object sender, RoutedEventArgs e)
        {
            Point.Value.InterpMode = EInterpCurveMode.CIM_CurveBreak;
            Graph.invokeSelectedPointChanged();
        }

        private void FlattenTangents_Click(object sender, RoutedEventArgs e)
        {
            Point.Value.LeaveTangent = Point.Value.ArriveTangent = 0;
            Point.Value.InterpMode = EInterpCurveMode.CIM_CurveUser;
            Graph.invokeSelectedPointChanged();
            Graph.Paint();
        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            IsSelected = true;
            Graph.SelectedPoint = Point.Value;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if(Keyboard.Modifiers is ModifierKeys.Shift or ModifierKeys.Control)
            {
                double prev = Graph.toLocalX(Point.Previous?.Value.InVal ?? float.MinValue);
                double next = Graph.toLocalX(Point.Next?.Value.InVal ?? float.MaxValue);
                double change = e.HorizontalChange;
                if ((X + change) <= prev || (X + change) >= next) change = 0f;

                X += change;
                if (LeftHandle != null) LeftHandle.X += change;
                if (RightHandle != null) RightHandle.X += change;
            }

            if (Keyboard.Modifiers != ModifierKeys.Shift)
            {
                Y -= e.VerticalChange;
                if (LeftHandle != null) LeftHandle.Y -= e.VerticalChange;
                if (RightHandle != null) RightHandle.Y -= e.VerticalChange;

            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
