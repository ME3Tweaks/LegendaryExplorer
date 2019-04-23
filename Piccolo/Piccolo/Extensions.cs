using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UMD.HCIL.Piccolo
{
    public static class Extensions
    {
        public static RectangleF BoundingRect(this IEnumerable<PNode> nodes)
        {
            return nodes.Select(node => node.GlobalFullBounds).BoundingRect();
        }

        /// <summary>
        /// Creates the smallest possible rectangle that contains all the provided rectangles
        /// </summary>
        /// <param name="rectangles"></param>
        /// <returns></returns>
        public static RectangleF BoundingRect(this IEnumerable<RectangleF> rectangles)
        {
            RectangleF result = RectangleF.Empty;
            foreach (var rect in rectangles)
            {
                if (result != RectangleF.Empty)
                {
                    if (rect != RectangleF.Empty)
                    {
                        result = RectangleF.Union(result, rect);
                    }
                }
                else
                {
                    result = rect;
                }
            }
            return result;
        }

        public static float Difference(this float a, float b)
        {
            return Math.Abs(a - b);
        }

        public static void Deconstruct(this PointF point, out float x, out float y)
        {
            x = point.X;
            y = point.Y;
        }
        public static void Deconstruct(this RectangleF rect, out float x, out float y)
        {
            x = rect.X;
            y = rect.Y;
        }
    }
}
