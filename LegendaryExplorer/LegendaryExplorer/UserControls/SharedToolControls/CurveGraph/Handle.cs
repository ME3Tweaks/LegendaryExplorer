using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shapes;
using LegendaryExplorer.Misc;
using LegendaryExplorer.SharedUI.Converters;

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    internal class Handle : Thumb, INotifyPropertyChanged
    {
        public const double HANDLE_LENGTH = 30f;
        private const double angleCutoff = 90 * (Math.PI / 180);
        public Anchor anchor;

        private readonly Line line;

        private readonly bool Left;

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        private double _slope;
        public double Slope
        {
            get => _slope;
            set
            {
                if (SetProperty(ref _slope, value))
                {
                    if (Left)
                    {
                        anchor.point.Value.ArriveTangent = Convert.ToSingle(value);
                        if (anchor.leftBez != null)
                        {
                            anchor.leftBez.Slope2 = value;
                        }
                    }
                    else
                    {
                        anchor.point.Value.LeaveTangent = Convert.ToSingle(value);
                        if (anchor.rightBez != null)
                        {
                            anchor.rightBez.Slope1 = value;
                        }
                    }
                }
            }
        }

        public Handle(Anchor a, bool left)
        {
            anchor = a;
            Left = left;
            _slope = Left ? a.point.Value.ArriveTangent : a.point.Value.LeaveTangent;
            line = new Line();
            line.bind(Line.X1Property, a, nameof(X));
            line.bind(Line.Y1Property, a, nameof(Y), CurveEdSubtractionConverter.Instance, a.graph.ActualHeight);
            line.bind(Line.X2Property, this, nameof(X));
            line.bind(Line.Y2Property, this, nameof(Y), CurveEdSubtractionConverter.Instance, a.graph.ActualHeight);
            line.bind(VisibilityProperty, this, nameof(Visibility));
            line.Style = a.graph.FindResource("HandleLine") as Style;
            a.graph.graph.Children.Add(line); 
            this.DragDelta += OnDragDelta;

            double hScale = a.graph.HorizontalScale;
            double vScale = a.graph.VerticalScale;
            double xLength = (HANDLE_LENGTH * (Left ? -1 : 1)) / Math.Sqrt(Math.Pow(hScale, 2) + Math.Pow(_slope, 2) * Math.Pow(vScale, 2));
            X = xLength * hScale + a.X;
            Y = _slope * xLength * vScale + a.Y;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            switch (anchor.point.Value.InterpMode)
            {
                case CurveMode.CIM_CurveAuto:
                case CurveMode.CIM_CurveAutoClamped:
                    anchor.point.Value.InterpMode = CurveMode.CIM_CurveUser;
                    anchor.graph.invokeSelectedPointChanged();
                    break;
                case CurveMode.CIM_CurveUser:
                case CurveMode.CIM_CurveBreak:
                case CurveMode.CIM_Linear:
                case CurveMode.CIM_Constant:
                default:
                    break;
            }
            Point pos = Mouse.GetPosition(anchor.graph);
            double angle = Math.Atan2(anchor.graph.ActualHeight - pos.Y - anchor.Y, pos.X - anchor.X);
            if (Left && Math.Abs(angle) < angleCutoff + 0.01)
            {
                angle = (angleCutoff + 0.01) * Math.Sign(angle);
            }
            else if (!Left && Math.Abs(angle) > angleCutoff - 0.01)
            {
                angle = (angleCutoff - 0.01) * Math.Sign(angle);
            }
            double rise = HANDLE_LENGTH * Math.Sin(angle);
            double run = HANDLE_LENGTH * Math.Cos(angle);
            Y = anchor.Y + rise;
            X = anchor.X + run;
            Slope = (rise / anchor.graph.VerticalScale) / (run / anchor.graph.HorizontalScale);

            if (anchor.point.Value.InterpMode == CurveMode.CIM_CurveUser)
            {
                Handle otherHandle = Left ? anchor.rightHandle : anchor.leftHandle;
                if (otherHandle is not null)
                {
                    otherHandle.X = anchor.X - run;
                    otherHandle.Y = anchor.Y - rise;
                    otherHandle.Slope = Slope;
                }
            }

        }

        public void Dispose()
        {
            anchor.graph.graph.Children.Remove(line);
            DragDelta -= OnDragDelta;
            anchor = null;
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
