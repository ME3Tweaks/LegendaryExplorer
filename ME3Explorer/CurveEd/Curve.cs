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

    public class CurvePoint : NotifyPropertyChangedBase
    {
        private float inVal;
        private float outVal;
        private float arriveTangent;
        private float leaveTangent;
        private CurveMode interpMode;

        public float InVal
        {
            get => inVal;
            set => SetProperty(ref inVal, value);
        }

        public CurveMode InterpMode
        {
            get => interpMode;
            set => SetProperty(ref interpMode, value);
        }

        public float OutVal
        {
            get => outVal;
            set => SetProperty(ref outVal, value);
        }

        public float ArriveTangent
        {
            get => arriveTangent;
            set => SetProperty(ref arriveTangent, value);
        }

        public float LeaveTangent
        {
            get => leaveTangent;
            set => SetProperty(ref leaveTangent, value);
        }

        public CurvePoint(float inVal, float outVal, float arriveTangent, float leaveTangent, CurveMode interpMode)
        {
            InVal = inVal;
            OutVal = outVal;
            ArriveTangent = arriveTangent;
            LeaveTangent = leaveTangent;
            InterpMode = interpMode;
        }

        public CurvePoint(float _inVal, float _outVal, float _arriveTangent, float _leaveTangent)
        {
            InVal = _inVal;
            OutVal = _outVal;
            ArriveTangent = _arriveTangent;
            LeaveTangent = _leaveTangent;
            //accurate float comparison ( == )
            if (Math.Abs(_arriveTangent - _leaveTangent) < float.Epsilon)
            {
                interpMode = CurveMode.CIM_CurveUser;
            }
            else
            {
                interpMode = CurveMode.CIM_CurveBreak;
            }
        }
    }

    public class Curve : NotifyPropertyChangedBase
    {
        private bool isSelected;
        public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }
        public string Name { get; }

        public LinkedList<CurvePoint> CurvePoints;

        public event EventHandler SharedValueChanged;

        public event EventHandler<(bool added, int index)> ListModified;

        public Action SaveChanges;

        public Curve(string name, LinkedList<CurvePoint> points)
        {
            Name = name;
            CurvePoints = points;
            foreach (var point in points)
            {
                point.PropertyChanged += Point_PropertyChanged;
            }
        }

        private void Point_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CurvePoint.InVal) || e.PropertyName == nameof(CurvePoint.InterpMode))
            {
                SharedValueChanged?.Invoke(this, e);
            }

            SaveChanges?.Invoke();

        }

        public void RemovePoint(LinkedListNode<CurvePoint> p)
        {
            int index = CurvePoints.IndexOf(p);
            CurvePoints.Remove(p);
            ListModified?.Invoke(this, (added: false, index));
            SaveChanges?.Invoke();
        }

        public void AddPoint(CurvePoint newPoint, LinkedListNode<CurvePoint> relTo, bool before = true)
        {
            newPoint.PropertyChanged += Point_PropertyChanged;
            LinkedListNode<CurvePoint> addedNode;
            if (relTo == null)
            {
                addedNode = CurvePoints.AddFirst(newPoint);
            }
            else if (before)
            {
                addedNode = CurvePoints.AddBefore(relTo, newPoint);
            }
            else
            {
                addedNode = CurvePoints.AddAfter(relTo, newPoint);
            }
            ListModified?.Invoke(this, (added: true, index: CurvePoints.IndexOf(addedNode)));
            SaveChanges?.Invoke();
        }

        public Curve()
        {
            Name = "";
            CurvePoints = new LinkedList<CurvePoint>();
        }
    }
}
