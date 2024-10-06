using System;
using System.Collections.Generic;
using System.ComponentModel;
using LegendaryExplorer.Misc; 
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    public class CurvePoint : NotifyPropertyChangedBase
    {
        private float inVal;
        private float outVal;
        private float arriveTangent;
        private float leaveTangent;
        private EInterpCurveMode interpMode;

        public Curve Curve;

        public float InVal
        {
            get => inVal;
            set => SetProperty(ref inVal, value);
        }

        public EInterpCurveMode InterpMode
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

        public CurvePoint(float inVal, float outVal, float arriveTangent, float leaveTangent, EInterpCurveMode interpMode)
        {
            InVal = inVal;
            OutVal = outVal;
            ArriveTangent = arriveTangent;
            LeaveTangent = leaveTangent;
            InterpMode = interpMode;
        }

        public CurvePoint(float inVal, float outVal, float arriveTangent, float leaveTangent)
        {
            InVal = inVal;
            OutVal = outVal;
            ArriveTangent = arriveTangent;
            LeaveTangent = leaveTangent;
            //accurate float comparison ( == )
            if (Math.Abs(arriveTangent - leaveTangent) < float.Epsilon)
            {
                interpMode = EInterpCurveMode.CIM_CurveUser;
            }
            else
            {
                interpMode = EInterpCurveMode.CIM_CurveBreak;
            }
        }
    }

    public class Curve : NotifyPropertyChangedBase
    {
        private bool isSelected;
        public bool IsSelected { get => isSelected; set => SetProperty(ref isSelected, value); }
        public string Name { get; }

        public readonly LinkedList<CurvePoint> CurvePoints;

        public event EventHandler SharedValueChanged;

        public event EventHandler<(bool added, int index)> ListModified;

        public Action SaveChanges;

        public Curve(string name, LinkedList<CurvePoint> points)
        {
            Name = name;
            CurvePoints = points;
            foreach (var point in points)
            {
                point.Curve = this;
                point.PropertyChanged += Point_PropertyChanged;
            }
        }

        private bool updating;
        private void Point_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (updating)
            {
                return;
            }
            updating = true;
            if (e.PropertyName is nameof(CurvePoint.InVal) or nameof(CurvePoint.InterpMode))
            {
                SharedValueChanged?.Invoke(this, e);
            }

            SaveChanges?.Invoke();
            updating = false;
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
            newPoint.Curve = this;
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
            CurvePoints = [];
        }
    }
}
