using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    public class StaticCurve : Shape
    {
        protected override Geometry DefiningGeometry { get; }
     
        public StaticCurve(CurveGraph graph, LinkedList<CurvePoint> curvePoints)
        {
            if (curvePoints.Count == 0)
            {
                DefiningGeometry = Geometry.Empty;
                return;
            }

            var firstPoint = curvePoints.First.Value;
            double x1 = graph.toLocalX(firstPoint.InVal);
            double y1 = graph.toLocalY(firstPoint.OutVal);
            double slope1 = firstPoint.LeaveTangent;
            var interpMode = firstPoint.InterpMode;
            var geom = new StreamGeometry();
            using StreamGeometryContext ctxt = geom.Open();
            ctxt.BeginFigure(new Point(x1, graph.ActualHeight - y1), false, false);

            foreach (CurvePoint curvePoint in curvePoints.Skip(1))
            {
                double x2 = graph.toLocalX(curvePoint.InVal);
                double y2 = graph.toLocalY(curvePoint.OutVal);
                double slope2 = curvePoint.ArriveTangent;
                switch (interpMode)
                {
                    case CurveMode.CIM_Linear:
                        ctxt.LineTo(new Point(x2, graph.ActualHeight - y2), true, true);
                        break;
                    case CurveMode.CIM_Constant:
                        ctxt.LineTo(new Point(x2, graph.ActualHeight - y1), true, true);
                        ctxt.LineTo(new Point(x2, graph.ActualHeight - y2), true, true);
                        break;
                    case CurveMode.CIM_CurveUser:
                    case CurveMode.CIM_CurveAuto:
                    case CurveMode.CIM_CurveBreak:
                    case CurveMode.CIM_CurveAutoClamped:
                        BezierSegment.BezierTo(graph, ctxt, x1, y1, slope1, x2, y2, slope2);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                x1 = x2;
                y1 = y2;
                slope1 = curvePoint.LeaveTangent;
                interpMode = curvePoint.InterpMode;
            }

            DefiningGeometry = geom;
        }
    }
}
