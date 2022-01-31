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

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    internal sealed class Anchor : Thumb, INotifyPropertyChanged
    {
        public readonly CurveGraph graph;
        public readonly LinkedListNode<CurvePoint> point;

        public Handle leftHandle;
        public Handle rightHandle;

        public BezierSegment leftBez;
        public BezierSegment rightBez;

        private double _y;
        public double Y
        {
            get => _y;
            set
            {
                if (SetProperty(ref _y, value) && graph != null)
                {
                    point.Value.OutVal = Convert.ToSingle(graph.toUnrealY(_y));
                    if (IsSelected)
                    {
                        string val = point.Value.OutVal.ToString("0.###");
                        if (!graph.yTextBox.Text.isNumericallyEqual(val))
                        {
                            graph.yTextBox.Text = val;
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
                if (SetProperty(ref _x, value) && graph != null)
                {
                    point.Value.InVal = Convert.ToSingle(graph.toUnrealX(_x));
                    if (IsSelected)
                    {
                        string val = point.Value.InVal.ToString("0.###");
                        if (!graph.xTextBox.Text.isNumericallyEqual(val))
                        {
                            graph.xTextBox.Text = val;
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
                    if (a.leftHandle is null)
                    {
                        a.leftHandle = new Handle(a, true);
                        a.graph.graph.Children.Add(a.leftHandle);
                    }

                    if (a.rightHandle is null)
                    {
                        a.rightHandle = new Handle(a, false);
                        a.graph.graph.Children.Add(a.rightHandle);
                    }

                    string val = a.point.Value.InVal.ToString("0.###");
                    if (!a.graph.xTextBox.Text.isNumericallyEqual(val))
                    {
                        a.graph.xTextBox.Text = val;
                    }
                    val = a.point.Value.OutVal.ToString("0.###");
                    if (!a.graph.yTextBox.Text.isNumericallyEqual(val))
                    {
                        a.graph.yTextBox.Text = val;
                    }

                    //left handle
                    if (a.point.Previous != null)
                    {
                        switch (a.point.Previous.Value.InterpMode)
                        {
                            case CurveMode.CIM_CurveAuto:
                            case CurveMode.CIM_CurveUser:
                            case CurveMode.CIM_CurveBreak:
                            case CurveMode.CIM_CurveAutoClamped:
                                a.leftHandle.Visibility = Visibility.Visible;
                                break;
                            case CurveMode.CIM_Linear:
                            case CurveMode.CIM_Constant:
                            default:
                                a.leftHandle.Visibility = Visibility.Hidden;
                                break;
                        }
                    }
                    else
                    {
                        a.leftHandle.Visibility = Visibility.Hidden;
                    }

                    //right handle
                    if (a.point.Next != null)
                    {
                        switch (a.point.Value.InterpMode)
                        {
                            case CurveMode.CIM_CurveAuto:
                            case CurveMode.CIM_CurveUser:
                            case CurveMode.CIM_CurveBreak:
                            case CurveMode.CIM_CurveAutoClamped:
                                a.rightHandle.Visibility = Visibility.Visible;
                                break;
                            case CurveMode.CIM_Linear:
                            case CurveMode.CIM_Constant:
                            default:
                                a.rightHandle.Visibility = Visibility.Hidden;
                                break;
                        }
                    }
                    else
                    {
                        a.rightHandle.Visibility = Visibility.Hidden;
                    }
                }
                //unselected
                else
                {
                    if (a.rightHandle is not null)
                    {
                        a.graph.graph.Children.Remove(a.rightHandle);
                        a.rightHandle.Dispose();
                        a.rightHandle = null;
                    }
                    if (a.leftHandle is not null)
                    {
                        a.graph.graph.Children.Remove(a.leftHandle);
                        a.leftHandle.Dispose();
                        a.leftHandle = null;
                    }
                }
            }
        }

        public Anchor()
        {
        }

        public Anchor(CurveGraph g, LinkedListNode<CurvePoint> p)
        {
            graph = g;
            point = p;
            X = graph.toLocalX(point.Value.InVal);
            Y = graph.toLocalY(point.Value.OutVal);
            this.DragDelta += OnDragDelta;
            this.DragStarted += OnDragStarted;
            this.MouseDown += Anchor_MouseDown;

            leftBez = null;
            rightBez = null;
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
                switch (point.Value.InterpMode)
                {
                    case CurveMode.CIM_CurveAuto:
                    case CurveMode.CIM_CurveUser:
                    case CurveMode.CIM_CurveAutoClamped:
                        var breakTangents = new MenuItem();
                        breakTangents.Header = "Break Tangents";
                        breakTangents.Click += BreakTangents_Click;
                        cm.Items.Add(breakTangents);
                        break;
                    case CurveMode.CIM_CurveBreak:
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
            if (point.Previous != null)
            {
                graph.SelectedPoint = point.Previous.Value;
            }
            else if (point.Next != null)
            {
                graph.SelectedPoint = point.Next.Value;
            }
            graph.SelectedCurve.RemovePoint(point);
            graph.Paint();
        }

        private void SetTime_Click(object sender, RoutedEventArgs e)
        {
            float prev = point.Previous?.Value.InVal ?? float.MinValue;
            float next = point.Next?.Value.InVal ?? float.MaxValue;
            string res = PromptDialog.Prompt(this, $"Enter time between {prev} and {next}", "Set Time", point.Value.InVal.ToString());
            if (float.TryParse(res, out float result) && result > prev && result < next)
            {
                X = graph.toLocalX(result);
                graph.Paint(true);
            }
        }

        private void SetValue_Click(object sender, RoutedEventArgs e)
        {
            string res = PromptDialog.Prompt(this, "Enter new value", "Set Value", point.Value.OutVal.ToString());
            if (float.TryParse(res, out float result))
            {
                Y = graph.toLocalY(result);
                graph.Paint(true);
            }
        }

        private void BreakTangents_Click(object sender, RoutedEventArgs e)
        {
            point.Value.InterpMode = CurveMode.CIM_CurveBreak;
            graph.invokeSelectedPointChanged();
        }

        private void FlattenTangents_Click(object sender, RoutedEventArgs e)
        {
            point.Value.LeaveTangent = point.Value.ArriveTangent = 0;
            point.Value.InterpMode = CurveMode.CIM_CurveUser;
            graph.invokeSelectedPointChanged();
            graph.Paint();
        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            IsSelected = true;
            graph.SelectedPoint = point.Value;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if(Keyboard.Modifiers is ModifierKeys.Shift or ModifierKeys.Control)
            {
                double prev = graph.toLocalX(point.Previous?.Value.InVal ?? float.MinValue);
                double next = graph.toLocalX(point.Next?.Value.InVal ?? float.MaxValue);
                double change = e.HorizontalChange;
                if ((X + change) <= prev || (X + change) >= next) change = 0f;

                X += change;
                if (leftHandle != null) leftHandle.X += change;
                if (rightHandle != null) rightHandle.X += change;
            }

            if (Keyboard.Modifiers != ModifierKeys.Shift)
            {
                Y -= e.VerticalChange;
                if (leftHandle != null) leftHandle.Y -= e.VerticalChange;
                if (rightHandle != null) rightHandle.Y -= e.VerticalChange;
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
