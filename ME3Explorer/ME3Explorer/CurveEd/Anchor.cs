using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Helpers;

namespace ME3Explorer.CurveEd
{
    class Anchor : Thumb
    {
        public CurveGraph graph;
        public LinkedListNode<CurvePoint> point;

        public Handle leftHandle;
        public Handle rightHandle;

        public BezierSegment leftBez;
        public BezierSegment rightBez;

        public double Y
        {
            get => (double)GetValue(YProperty);
            set => SetValue(YProperty, value);
        }

        // Using a DependencyProperty as the backing store for Y.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty YProperty =
            DependencyProperty.Register(nameof(Y), typeof(double), typeof(Anchor), new PropertyMetadata(0.0, OnYChanged));

        private static void OnYChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Anchor a && a.graph != null)
            {
                a.point.Value.OutVal = Convert.ToSingle(a.graph.toUnrealY((double)e.NewValue));
                if (a.IsSelected)
                {
                    string val = a.point.Value.OutVal.ToString("0.###");
                    if (!a.graph.yTextBox.Text.isNumericallyEqual(val))
                    {
                        a.graph.yTextBox.Text = val;
                    } 
                }
            }
        }

        public double X
        {
            get => (double)GetValue(XProperty);
            set => SetValue(XProperty, value);
        }

        // Using a DependencyProperty as the backing store for X.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty XProperty =
            DependencyProperty.Register(nameof(X), typeof(double), typeof(Anchor), new PropertyMetadata(0.0, OnXChanged));

        private static void OnXChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Anchor a && a.graph != null)
            {
                a.point.Value.InVal = Convert.ToSingle(a.graph.toUnrealX((double)e.NewValue));
                if (a.IsSelected)
                {
                    string val = a.point.Value.InVal.ToString("0.###");
                    if (!a.graph.xTextBox.Text.isNumericallyEqual(val))
                    {
                        a.graph.xTextBox.Text = val;
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
                    a.leftHandle.Visibility = a.rightHandle.Visibility = Visibility.Hidden;
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

            leftHandle = new Handle(this, true);
            graph.graph.Children.Add(leftHandle);
            rightHandle = new Handle(this, false);
            graph.graph.Children.Add(rightHandle);

            leftBez = null;
            rightBez = null;
        }

        private void Anchor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                ContextMenu cm = new ContextMenu();
                MenuItem setTime = new MenuItem {Header = "Set Time"};
                setTime.Click += SetTime_Click;
                cm.Items.Add(setTime);
                MenuItem setValue = new MenuItem {Header = "Set Value"};
                setValue.Click += SetValue_Click;
                cm.Items.Add(setValue);
                switch (point.Value.InterpMode)
                {
                    case CurveMode.CIM_CurveAuto:
                    case CurveMode.CIM_CurveUser:
                    case CurveMode.CIM_CurveAutoClamped:
                        MenuItem breakTangents = new MenuItem();
                        breakTangents.Header = "Break Tangents";
                        breakTangents.Click += BreakTangents_Click;
                        cm.Items.Add(breakTangents);
                        break;
                    case CurveMode.CIM_CurveBreak:
                        MenuItem flattenTangents = new MenuItem();
                        flattenTangents.Header = "Flatten Tangents";
                        flattenTangents.Click += FlattenTangents_Click;
                        cm.Items.Add(flattenTangents);
                        break;
                    default:
                        break;
                }
                MenuItem deleteKey = new MenuItem {Header = "Delete Key"};
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
            if (float.TryParse(res, out var result) && result > prev && result < next)
            {
                X = graph.toLocalX(result);
                graph.Paint(true);
            }
        }

        private void SetValue_Click(object sender, RoutedEventArgs e)
        {
            string res = PromptDialog.Prompt(this, "Enter new value", "Set Value", point.Value.OutVal.ToString());
            if (float.TryParse(res, out var result))
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
            point.Value.InterpMode = CurveMode.CIM_CurveAutoClamped;
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
            if(Keyboard.Modifiers == ModifierKeys.Shift || Keyboard.Modifiers == ModifierKeys.Control)
            {
                double prev = graph.toLocalX(point.Previous?.Value.InVal ?? float.MinValue);
                double next = graph.toLocalX(point.Next?.Value.InVal ?? float.MaxValue);
                double change = e.HorizontalChange;
                if ((X + change) <= prev || (X + change) >= next) change = 0f;

                X += change;
                leftHandle.X += change;
                rightHandle.X += change;
            }

            if (Keyboard.Modifiers != ModifierKeys.Shift)
            {
                Y -= e.VerticalChange;
                leftHandle.Y -= e.VerticalChange;
                rightHandle.Y -= e.VerticalChange;
            }
        }
    }
}
