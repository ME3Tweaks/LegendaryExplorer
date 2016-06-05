using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

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
            get { return (double)GetValue(YProperty); }
            set { SetValue(YProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Y.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty YProperty =
            DependencyProperty.Register("Y", typeof(double), typeof(Anchor), new PropertyMetadata(0.0, OnYChanged));

        private static void OnYChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            Anchor a = sender as Anchor;
            if (a?.graph != null)
            {
                a.point.Value.OutVal = Convert.ToSingle(a.graph.globalY((double)e.NewValue));
            }
        }

        public double X
        {
            get { return (double)GetValue(XProperty); }
            set { SetValue(XProperty, value); }
        }

        // Using a DependencyProperty as the backing store for X.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty XProperty =
            DependencyProperty.Register("X", typeof(double), typeof(Anchor), new PropertyMetadata(0.0));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set { SetValue(IsSelectedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsSelected.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(Anchor), new PropertyMetadata(false, OnIsSelectedChange));

        private static void OnIsSelectedChange(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            Anchor a = sender as Anchor;
            if (a != null)
            {
                if ((bool)e.NewValue == false)
                {
                    a.leftHandle.Visibility = a.rightHandle.Visibility = Visibility.Hidden;
                }
                else
                {
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
            }
        }

        public Anchor()
        {
        }

        public Anchor(CurveGraph g, LinkedListNode<CurvePoint> p)
        {
            graph = g;
            point = p;
            X = graph.localX(point.Value.InVal);
            Y = graph.localY(point.Value.OutVal);
            this.DragDelta += OnDragDelta;
            this.DragStarted += OnDragStarted;

            leftHandle = new Handle(this, true);
            graph.graph.Children.Add(leftHandle);
            rightHandle = new Handle(this, false);
            graph.graph.Children.Add(rightHandle);

            leftBez = null;
            rightBez = null;
        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            IsSelected = true;
            graph.SelectedPoint = point.Value;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            Y = Y - e.VerticalChange;
            leftHandle.Y = leftHandle.Y - e.VerticalChange;
            rightHandle.Y = rightHandle.Y - e.VerticalChange;
        }
    }
}
