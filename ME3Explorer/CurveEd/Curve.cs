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
        private float inVal;
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

        public event EventHandler SharedValueChanged;

        protected void OnSharedValueChanged(EventArgs e)
        {
            SharedValueChanged?.Invoke(this, e);
        }
        public float InVal
        {
            get { return inVal; }
            set
            {
                if (value != inVal)
                {
                    inVal = value;
                    OnPropertyChanged();
                    OnSharedValueChanged(EventArgs.Empty);
                }
            }
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
                    OnSharedValueChanged(EventArgs.Empty);
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

        public event EventHandler SharedValueChanged;

        public Curve(string name, LinkedList<CurvePoint> points)
        {
            Name = name;
            CurvePoints = points;
            foreach (var point in points)
            {
                point.SharedValueChanged += Point_SharedValueChanged;
            }
        }

        private void Point_SharedValueChanged(object sender, EventArgs e)
        {
            SharedValueChanged?.Invoke(this, e);
        }

        public Curve()
        {
            Name = "";
            CurvePoints = new LinkedList<CurvePoint>();
        }
    }
}
