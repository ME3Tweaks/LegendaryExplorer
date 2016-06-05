using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ME3Explorer.CurveEd
{
    public enum CurveMode : byte
    {
        CIM_Linear,
        CIM_CurveAuto,
        CIM_Constant,
        CIM_CurveUser,
        CIM_CurveBreak,
        CIM_CurveAutoClamped,
    }

    public class CurvePoint : INotifyPropertyChanged
    {
        public float InVal;
        public float OutVal;
        public float ArriveTangent;
        public float LeaveTangent;
        private CurveMode interpMode;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        public event EventHandler InterpModeChanged;

        protected void OnInterpModeChanged(EventArgs e)
        {
            InterpModeChanged?.Invoke(this, e);
        }

        public CurveMode InterpMode
        {
            get { return interpMode; }
            set
            {
                if (value != interpMode)
                {
                    interpMode = value;
                    OnPropertyChanged();
                    OnInterpModeChanged(EventArgs.Empty);
                }
            }
        }

        public CurvePoint(float inVal, float outVal, float arriveTangent, float leaveTangent, CurveMode interpMode)
        {
            InVal = inVal;
            OutVal = outVal;
            ArriveTangent = arriveTangent;
            LeaveTangent = leaveTangent;
            InterpMode = interpMode;
        }
    }

    public class Curve
    {
        public string Name { get; }

        public LinkedList<CurvePoint> CurvePoints;

        public event EventHandler InterpModeChanged;

        public Curve(string name, LinkedList<CurvePoint> points)
        {
            Name = name;
            CurvePoints = points;
            foreach (var point in points)
            {
                point.InterpModeChanged += Point_InterpModeChanged;
            }
        }

        private void Point_InterpModeChanged(object sender, EventArgs e)
        {
            InterpModeChanged?.Invoke(this, e);
        }

        public Curve()
        {
            Name = "";
            CurvePoints = new LinkedList<CurvePoint>();
        }
    }
}
