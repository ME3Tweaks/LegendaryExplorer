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
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    internal class Handle : Thumb, INotifyPropertyChanged
    {
        private const double HANDLE_LENGTH = 30f;
        private const double ANGLE_CUTOFF = 90 * (Math.PI / 180);
        private Anchor anchor;

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

        private float _slope;

        private float Slope
        {
            get => _slope;
            set
            {
                if (value != _slope)
                {
                    _slope = value;
                    if (Left)
                    {
                        anchor.Point.Value.ArriveTangent = value;
                        if (anchor.LeftBez != null)
                        {
                            anchor.LeftBez.Slope2 = value;
                        }
                    }
                    else
                    {
                        anchor.Point.Value.LeaveTangent = value;
                        if (anchor.RightBez != null)
                        {
                            anchor.RightBez.Slope1 = value;
                        }
                    }
                }
            }
        }

        public Handle(Anchor a, bool left)
        {
            anchor = a;
            Left = left;
            _slope = Left ? a.Point.Value.ArriveTangent : a.Point.Value.LeaveTangent;
            line = new Line();
            line.bind(Line.X1Property, a, nameof(X));
            line.bind(Line.Y1Property, a, nameof(Y), CurveEdSubtractionConverter.Instance, a.Graph.ActualHeight);
            line.bind(Line.X2Property, this, nameof(X));
            line.bind(Line.Y2Property, this, nameof(Y), CurveEdSubtractionConverter.Instance, a.Graph.ActualHeight);
            line.bind(VisibilityProperty, this, nameof(Visibility));
            line.Style = a.Graph.FindResource("HandleLine") as Style;
            a.Graph.graph.Children.Add(line); 
            this.DragDelta += OnDragDelta;

            double hScale = a.Graph.HorizontalScale;
            double vScale = a.Graph.VerticalScale;
            double xLength = (HANDLE_LENGTH * (Left ? -1 : 1)) / Math.Sqrt(Math.Pow(hScale, 2) + Math.Pow(_slope, 2) * Math.Pow(vScale, 2));
            X = xLength * hScale + a.X;
            Y = _slope * xLength * vScale + a.Y;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            switch (anchor.Point.Value.InterpMode)
            {
                case EInterpCurveMode.CIM_CurveAuto:
                case EInterpCurveMode.CIM_CurveAutoClamped:
                    anchor.Point.Value.InterpMode = EInterpCurveMode.CIM_CurveUser;
                    anchor.Graph.invokeSelectedPointChanged();
                    break;
                case EInterpCurveMode.CIM_CurveUser:
                case EInterpCurveMode.CIM_CurveBreak:
                case EInterpCurveMode.CIM_Linear:
                case EInterpCurveMode.CIM_Constant:
                default:
                    break;
            }
            Point pos = Mouse.GetPosition(anchor.Graph);
            double angle = Math.Atan2(anchor.Graph.ActualHeight - pos.Y - anchor.Y, pos.X - anchor.X);
            if (Left && Math.Abs(angle) < ANGLE_CUTOFF + 0.01)
            {
                angle = (ANGLE_CUTOFF + 0.01) * Math.Sign(angle);
            }
            else if (!Left && Math.Abs(angle) > ANGLE_CUTOFF - 0.01)
            {
                angle = (ANGLE_CUTOFF - 0.01) * Math.Sign(angle);
            }
            double rise = HANDLE_LENGTH * Math.Sin(angle);
            double run = HANDLE_LENGTH * Math.Cos(angle);
            Y = anchor.Y + rise;
            X = anchor.X + run;
            Slope = (float)((rise / anchor.Graph.VerticalScale) / (run / anchor.Graph.HorizontalScale));

            if (anchor.Point.Value.InterpMode == EInterpCurveMode.CIM_CurveUser)
            {
                Handle otherHandle = Left ? anchor.RightHandle : anchor.LeftHandle;
                if (otherHandle is not null)
                {
                    otherHandle.X = anchor.X - run;
                    otherHandle.Y = anchor.Y - rise;
                    otherHandle.Slope = Slope;
                }
            }
        }

        public void TangentUpdate()
        {
            _slope = Left ? anchor.Point.Value.ArriveTangent : anchor.Point.Value.LeaveTangent;
            double hScale = anchor.Graph.HorizontalScale;
            double vScale = anchor.Graph.VerticalScale;
            double xLength = (HANDLE_LENGTH * (Left ? -1 : 1)) / Math.Sqrt(Math.Pow(hScale, 2) + Math.Pow(_slope, 2) * Math.Pow(vScale, 2));
            X = xLength * hScale + anchor.X;
            Y = _slope * xLength * vScale + anchor.Y;
        }

        public void Dispose()
        {
            anchor.Graph.graph.Children.Remove(line);
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
