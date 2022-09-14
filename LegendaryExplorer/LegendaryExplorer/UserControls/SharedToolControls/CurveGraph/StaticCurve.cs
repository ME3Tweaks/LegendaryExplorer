using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.SharedToolControls.Curves
{
    public class StaticCurve : Shape
    {
        protected override Geometry DefiningGeometry { get; }
     
        public StaticCurve(CurveGraph graph, IEnumerable<CurvePoint> curvePoints, bool extendLeft = false, bool extendRight = false)
        {
            using IEnumerator<CurvePoint> enumerator = curvePoints.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                DefiningGeometry = Geometry.Empty;
                return;
            }

            CurvePoint firstPoint = enumerator.Current;
            double x1 = graph.toLocalX(firstPoint.InVal);
            double y1 = graph.toLocalY(firstPoint.OutVal);
            double slope1 = firstPoint.LeaveTangent;
            var interpMode = firstPoint.InterpMode;
            var geom = new StreamGeometry();
            using StreamGeometryContext ctxt = geom.Open();
            if (extendLeft)
            {
                ctxt.BeginFigure(new Point(-10, graph.ActualHeight - y1), false, false);
                ctxt.LineTo(new Point(x1, graph.ActualHeight - y1), true, true);
            }
            else
            {
                ctxt.BeginFigure(new Point(x1, graph.ActualHeight - y1), false, false);
            }

            foreach (CurvePoint curvePoint in enumerator)
            {
                double x2 = graph.toLocalX(curvePoint.InVal);
                double y2 = graph.toLocalY(curvePoint.OutVal);
                double slope2 = curvePoint.ArriveTangent;
                switch (interpMode)
                {
                    case EInterpCurveMode.CIM_Linear:
                        ctxt.LineTo(new Point(x2, graph.ActualHeight - y2), true, true);
                        break;
                    case EInterpCurveMode.CIM_Constant:
                        ctxt.LineTo(new Point(x2, graph.ActualHeight - y1), true, true);
                        ctxt.LineTo(new Point(x2, graph.ActualHeight - y2), true, true);
                        break;
                    case EInterpCurveMode.CIM_CurveUser:
                    case EInterpCurveMode.CIM_CurveAuto:
                    case EInterpCurveMode.CIM_CurveBreak:
                    case EInterpCurveMode.CIM_CurveAutoClamped:
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

            if (extendRight)
            {
                ctxt.LineTo(new Point(graph.ActualWidth + 10, graph.ActualHeight - y1), true, true);
            }

            DefiningGeometry = geom;
        }
    }
}
