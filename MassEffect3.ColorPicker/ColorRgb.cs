using System;
using System.Drawing;

namespace MassEffect3.ColorPicker
{
	[Serializable]
	public struct ColorRgb
	{
		public int B;
		public int G;
		public int R;

		public ColorRgb(int r, int g, int b)
		{
			if (r < 0 || r > 255)
			{
				throw new ArgumentOutOfRangeException("r", r, "r must corrospond to a byte value");
			}

			if (g < 0 || g > 255)
			{
				throw new ArgumentOutOfRangeException("g", g, "g must corrospond to a byte value");
			}

			if (b < 0 || b > 255)
			{
				throw new ArgumentOutOfRangeException("b", b, "b must corrospond to a byte value");
			}

			R = r;
			G = g;
			B = b;
		}

		public static ColorRgb FromHsv(ColorHsv hsv)
		{
			return hsv.ToRgb();
		}

		public Color ToColor()
		{
			return Color.FromArgb(R, G, B);
		}

		public ColorHsv ToHsv()
		{
			// In this function, R, G, and B values must be scaled 
			// to be between 0 and 1.
			// HsvColor.Hue will be a value between 0 and 360, and 
			// HsvColor.Saturation and value are between 0 and 1.

			var r = (double) R / 255;
			var g = (double) G / 255;
			var b = (double) B / 255;

			double h;
			double s;

			var min = Math.Min(Math.Min(r, g), b);
			var max = Math.Max(Math.Max(r, g), b);
			var v = max;
			var delta = max - min;

			if (Equals(max, 0.0) || Equals(delta, 0.0))
			{
				// R, G, and B must be 0, or all the same.
				// In this case, S is 0, and H is undefined.
				// Using H = 0 is as good as any...
				s = 0;
				h = 0;
			}
			else
			{
				s = delta / max;
				if (Equals(r, max))
				{
					// Between Yellow and Magenta
					h = (g - b) / delta;
				}
				else if (Equals(g, max))
				{
					// Between Cyan and Yellow
					h = 2 + (b - r) / delta;
				}
				else
				{
					// Between Magenta and Cyan
					h = 4 + (r - g) / delta;
				}
			}

			// Scale h to be between 0 and 360. 
			// This may require adding 360, if the value
			// is negative.
			h *= 60;

			if (h < 0)
			{
				h += 360;
			}

			// Scale to the requirements of this 
			// application. All values are between 0 and 255.
			return new ColorHsv((int) h, (int) (s * 100), (int) (v * 100));
		}

		public override string ToString()
		{
			return String.Format("({0}, {1}, {2})", R, G, B);
		}
	}
}