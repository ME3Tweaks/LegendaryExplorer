using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace MassEffect3.ColorPicker
{
	public sealed class ColorGradient : UserControl
	{
		private const int TriangleSize = 7;
		private const int TriangleHalfLength = (TriangleSize - 1) / 2;

		#region public bool DrawNearNub

		private bool _DrawNearNub = true;

		[Category("Gradient")]
		[DisplayName("Draw Near Nub")]
		public bool DrawNearNub
		{
			get { return _DrawNearNub; }

			set
			{
				_DrawNearNub = value;
				Invalidate();
			}
		}

		#endregion

		#region public bool DrawFarNub

		private bool _DrawFarNub = true;

		[Category("Gradient")]
		[DisplayName("Draw Far Nub")]
		public bool DrawFarNub
		{
			get { return _DrawFarNub; }

			set
			{
				_DrawFarNub = value;
				Invalidate();
			}
		}

		#endregion

		/// <summary>
		///     Required designer variable.
		/// </summary>
		private Container _Components;

		private int _Highlight = -1;
		private int _Tracking = -1;

		private int[] _Values;

		#region public int Value

		[Category("Gradient")]
		public int Value
		{
			get { return GetValue(0); }

			set { SetValue(0, value); }
		}

		#endregion

		#region public Color[] CustomGradient

		private Color[] _CustomGradient;

		[Category("Gradient")]
		[DisplayName("Custom Gradient")]
		public Color[] CustomGradient
		{
			get
			{
				if (_CustomGradient == null)
				{
					return null;
				}

				return (Color[]) _CustomGradient.Clone();
			}

			set
			{
				if (value != _CustomGradient)
				{
					if (value == null)
					{
						_CustomGradient = null;
					}
					else
					{
						_CustomGradient = (Color[]) value.Clone();
					}

					Invalidate();
				}
			}
		}

		#endregion

		#region public Orientation Orientation

		private Orientation _Orientation = Orientation.Vertical;

		[Category("Gradient")]
		public Orientation Orientation
		{
			get { return _Orientation; }

			set
			{
				if (value != _Orientation)
				{
					_Orientation = value;
					Invalidate();
				}
			}
		}

		#endregion

		#region public int Count

		[Browsable(false)]
		public int Count
		{
			get { return _Values.Length; }

			set
			{
				if (value < 0 || value > 16)
				{
					throw new ArgumentOutOfRangeException("value", value, "Count must be between 0 and 16");
				}

				_Values = new int[value];

				if (value > 1)
				{
					for (var i = 0; i < value; i++)
					{
						_Values[i] = i * 255 / (value - 1);
					}
				}
				else if (value == 1)
				{
					_Values[0] = 128;
				}

				OnValueChanged(0);
				Invalidate();
			}
		}

		#endregion

		public ColorGradient()
		{
			InitializeComponent();
			DoubleBuffered = true;
			ResizeRedraw = true;
			Count = 1;
		}

		// value from [0,255] that specifies the hsv "value" component
		// where we should draw little triangles that show the value

		public int GetValue(int index)
		{
			if (index < 0 || index >= _Values.Length)
			{
				throw new ArgumentOutOfRangeException("index", index, "Index must be within the bounds of the array");
			}

			return _Values[index];
		}

		public void SetValue(int index, int val)
		{
			var min = -1;
			var max = 256;

			if (index < 0 || index >= _Values.Length)
			{
				throw new ArgumentOutOfRangeException("index", index, "Index must be within the bounds of the array");
			}

			if (index - 1 >= 0)
			{
				min = _Values[index - 1];
			}

			if (index + 1 < _Values.Length)
			{
				max = _Values[index + 1];
			}

			if (_Values[index] != val)
			{
				_Values[index] = val.Clamp(min + 1, max - 1);
				OnValueChanged(index);
				Invalidate();
			}

			Update();
		}

		public event EventHandler ValueChanged;
		// ReSharper disable UnusedParameter.Local
		private void OnValueChanged(int index)
			// ReSharper restore UnusedParameter.Local
		{
			if (ValueChanged != null)
			{
				ValueChanged(this, new EventArgs());
			}
		}

		private void DrawGradient(Graphics g)
		{
			g.PixelOffsetMode = PixelOffsetMode.Half;

			float gradientAngle;

			switch (_Orientation)
			{
				case Orientation.Horizontal:
				{
					gradientAngle = 180.0f;
					break;
				}

				case Orientation.Vertical:
				{
					gradientAngle = 90.0f;
					break;
				}

				default:
				{
					throw new InvalidEnumArgumentException();
				}
			}

			// draw gradient
			var gradientRect = ClientRectangle;

			switch (_Orientation)
			{
				case Orientation.Horizontal:
				{
					gradientRect.Inflate(-TriangleHalfLength, -TriangleSize + 3);
					break;
				}

				case Orientation.Vertical:
				{
					gradientRect.Inflate(-TriangleSize + 3, -TriangleHalfLength);
					break;
				}

				default:
				{
					throw new InvalidEnumArgumentException();
				}
			}

			g.FillRectangle(new HatchBrush(
				HatchStyle.LargeCheckerBoard,
				Color.White,
				Color.Gray),
				gradientRect);

			if (_CustomGradient != null &&
				_CustomGradient.Length > 2 &&
				gradientRect.Width > 1 &&
				gradientRect.Height > 1)
			{
				var gradientSurface = new Bitmap(
					gradientRect.Size.Width,
					gradientRect.Size.Height,
					PixelFormat.Format32bppArgb);

				if (Orientation == Orientation.Horizontal)
				{
					for (var x = 0; x < gradientSurface.Width; ++x)
					{
						// TODO: refactor, double buffer, save this computation in a bitmap somewhere
						var index =
							x * (_CustomGradient.Length - 1) /
							(double) (gradientSurface.Width - 1);

						var indexL = (int) Math.Floor(index);
						var t = 1.0 - (index - indexL);

						var indexR = (int) Math.Min(
							_CustomGradient.Length - 1,
							Math.Ceiling(index));

						var colorL = _CustomGradient[indexL];
						var colorR = _CustomGradient[indexR];

						var a1 = colorL.A / 255.0;
						var r1 = colorL.R / 255.0;
						var g1 = colorL.G / 255.0;
						var b1 = colorL.B / 255.0;

						var a2 = colorR.A / 255.0;
						var r2 = colorR.R / 255.0;
						var g2 = colorR.G / 255.0;
						var b2 = colorR.B / 255.0;

						var at = (t * a1) + ((1.0 - t) * a2);

						double rt;
						double gt;
						double bt;

						if (Equals(at, 0.0))
						{
							rt = 0;
							gt = 0;
							bt = 0;
						}
						else
						{
							rt = ((t * a1 * r1) + ((1.0 - t) * a2 * r2)) / at;
							gt = ((t * a1 * g1) + ((1.0 - t) * a2 * g2)) / at;
							bt = ((t * a1 * b1) + ((1.0 - t) * a2 * b2)) / at;
						}

						var ap = (int) (Math.Round(at * 255.0)).Clamp(0, 255);
						var rp = (int) (Math.Round(rt * 255.0)).Clamp(0, 255);
						var gp = (int) (Math.Round(gt * 255.0)).Clamp(0, 255);
						var bp = (int) (Math.Round(bt * 255.0)).Clamp(0, 255);

						for (var y = 0; y < gradientSurface.Height; ++y)
						{
							const int srcA = 0;
							const int srcR = 0;
							const int srcG = 0;
							const int srcB = 0;

							// we are assuming that src.A = 255
							var ad = ((ap * ap) + (srcA * (255 - ap))) / 255;
							var rd = ((rp * ap) + (srcR * (255 - ap))) / 255;
							var gd = ((gp * ap) + (srcG * (255 - ap))) / 255;
							var bd = ((bp * ap) + (srcB * (255 - ap))) / 255;

							// TODO: proper alpha blending!
							gradientSurface.SetPixel(x, y, Color.FromArgb(ad, rd, gd, bd));
						}
					}

					g.DrawImage(
						gradientSurface,
						gradientRect,
						new Rectangle(new Point(0, 0), gradientSurface.Size),
						GraphicsUnit.Pixel);
				}
				else if (Orientation == Orientation.Vertical)
				{
					g.FillRectangle(Brushes.Red, gradientRect);
					/*                    
                    g.DrawImage(
                        gradientSurface,
                        gradientRect,
                        new Rectangle(new Point(0, 0), gradientSurface.Size),
                        GraphicsUnit.Pixel);
                    */
				}
				else
				{
					throw new InvalidEnumArgumentException();
				}
			}
			else if (_CustomGradient != null &&
					_CustomGradient.Length == 2)
			{
				using (var lgb =
					new LinearGradientBrush(
						ClientRectangle,
						_CustomGradient[1],
						_CustomGradient[0],
						gradientAngle,
						false))
				{
					g.FillRectangle(lgb, gradientRect);
				}
			}
			else
			{
				using (var lgb =
					new LinearGradientBrush(
						ClientRectangle,
						_MaxColor,
						_MinColor,
						gradientAngle,
						false))
				{
					g.FillRectangle(lgb, gradientRect);
				}
			}

			// fill background
			using (var nonGradientRegion = new Region())
			{
				nonGradientRegion.MakeInfinite();
				nonGradientRegion.Exclude(gradientRect);

				using (var sb = new SolidBrush(BackColor))
				{
					//g.FillRegion(sb, nonGradientRegion.GetRegionReadOnly());
					g.FillRegion(sb, nonGradientRegion);
				}
			}

			// draw value triangles
			for (var i = 0; i < _Values.Length; i++)
			{
				var pos = ValueToPosition(_Values[i]);
				Brush brush;
				Pen pen;

				if (i == _Highlight)
				{
					brush = Brushes.Blue;
					pen = (Pen) Pens.White.Clone();
				}
				else
				{
					brush = Brushes.Black;
					pen = (Pen) Pens.Gray.Clone();
				}

				g.SmoothingMode = SmoothingMode.AntiAlias;

				Point a1;
				Point b1;
				Point c1;

				Point a2;
				Point b2;
				Point c2;

				switch (_Orientation)
				{
					case Orientation.Horizontal:
					{
						a1 = new Point(pos - TriangleHalfLength, 0);
						b1 = new Point(pos, TriangleSize - 1);
						c1 = new Point(pos + TriangleHalfLength, 0);

						a2 = new Point(a1.X, Height - 1 - a1.Y);
						b2 = new Point(b1.X, Height - 1 - b1.Y);
						c2 = new Point(c1.X, Height - 1 - c1.Y);
						break;
					}

					case Orientation.Vertical:
					{
						a1 = new Point(0, pos - TriangleHalfLength);
						b1 = new Point(TriangleSize - 1, pos);
						c1 = new Point(0, pos + TriangleHalfLength);

						a2 = new Point(Width - 1 - a1.X, a1.Y);
						b2 = new Point(Width - 1 - b1.X, b1.Y);
						c2 = new Point(Width - 1 - c1.X, c1.Y);
						break;
					}

					default:
					{
						throw new InvalidEnumArgumentException();
					}
				}

				if (_DrawNearNub)
				{
					g.FillPolygon(brush, new[]
					{
						a1, b1, c1, a1
					});
				}

				if (_DrawFarNub)
				{
					g.FillPolygon(brush, new[]
					{
						a2, b2, c2, a2
					});
				}

				// ReSharper disable ConditionIsAlwaysTrueOrFalse
				if (pen != null)
					// ReSharper restore ConditionIsAlwaysTrueOrFalse
				{
					if (_DrawNearNub)
					{
						g.DrawPolygon(pen, new[]
						{
							a1, b1, c1, a1
						});
					}

					if (_DrawFarNub)
					{
						g.DrawPolygon(pen, new[]
						{
							a2, b2, c2, a2
						});
					}

					pen.Dispose();
				}
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			DrawGradient(e.Graphics);
		}

		protected override void OnPaintBackground(PaintEventArgs pevent)
		{
			DrawGradient(pevent.Graphics);
		}

		/// <summary>
		///     Clean up any resources being used.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (_Components != null)
				{
					_Components.Dispose();
					_Components = null;
				}
			}

			base.Dispose(disposing);
		}

		private int PositionToValue(int pos)
		{
			int max;

			switch (_Orientation)
			{
				case Orientation.Horizontal:
				{
					max = Width;
					break;
				}

				case Orientation.Vertical:
				{
					max = Height;
					break;
				}

				default:
				{
					throw new InvalidEnumArgumentException();
				}
			}

			var val = (((max - TriangleSize) - (pos - TriangleHalfLength)) * 255) / (max - TriangleSize);

			if (_Orientation == Orientation.Horizontal)
			{
				val = 255 - val;
			}

			return val;
		}

		private int ValueToPosition(int val)
		{
			int max;

			if (_Orientation == Orientation.Horizontal)
			{
				val = 255 - val;
			}

			switch (_Orientation)
			{
				case Orientation.Horizontal:
				{
					max = Width;
					break;
				}

				case Orientation.Vertical:
				{
					max = Height;
					break;
				}

				default:
				{
					throw new InvalidEnumArgumentException();
				}
			}

			var pos = TriangleHalfLength + ((max - TriangleSize) - (((val * (max - TriangleSize)) / 255)));
			return pos;
		}

		private int WhichTriangle(int val)
		{
			var bestIndex = -1;
			var bestDistance = int.MaxValue;
			var v = PositionToValue(val);

			for (var i = 0; i < _Values.Length; i++)
			{
				var distance = Math.Abs(_Values[i] - v);

				if (distance < bestDistance)
				{
					bestDistance = distance;
					bestIndex = i;
				}
			}

			return bestIndex;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			if (e.Button == MouseButtons.Left)
			{
				var val = GetOrientedValue(e);
				_Tracking = WhichTriangle(val);
				Invalidate();
				OnMouseMove(e);
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);

			if (e.Button == MouseButtons.Left)
			{
				OnMouseMove(e);
				_Tracking = -1;
				Invalidate();
			}
		}

		private int GetOrientedValue(MouseEventArgs me)
		{
			return GetOrientedValue(new Point(me.X, me.Y));
		}

		private int GetOrientedValue(Point pt)
		{
			int pos;

			switch (_Orientation)
			{
				case Orientation.Horizontal:
				{
					pos = pt.X;
					break;
				}

				case Orientation.Vertical:
				{
					pos = pt.Y;
					break;
				}

				default:
				{
					throw new InvalidEnumArgumentException();
				}
			}

			return pos;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			var pos = GetOrientedValue(e);

			if (_Tracking >= 0)
			{
				var val = PositionToValue(pos);
				SetValue(_Tracking, val);
			}
			else
			{
				var oldHighlight = _Highlight;
				_Highlight = WhichTriangle(pos);

				if (_Highlight != oldHighlight)
				{
					InvalidateTriangle(oldHighlight);
					InvalidateTriangle(_Highlight);
				}
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			var oldhighlight = _Highlight;
			_Highlight = -1;
			InvalidateTriangle(oldhighlight);
		}

		private void InvalidateTriangle(int index)
		{
			if (index < 0 || index >= _Values.Length)
			{
				return;
			}

			var value = ValueToPosition(_Values[index]);
			Rectangle rect;

			switch (_Orientation)
			{
				case Orientation.Horizontal:
					rect = new Rectangle(value - TriangleHalfLength, 0, TriangleSize, Height);
					break;

				case Orientation.Vertical:
					rect = new Rectangle(0, value - TriangleHalfLength, Width, TriangleSize);
					break;

				default:
					throw new InvalidEnumArgumentException();
			}

			Invalidate(rect, true);
		}

		#region Component Designer generated code

		/// <summary>
		///     Required method for Designer support - do not modify
		///     the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this._Components = new System.ComponentModel.Container();
		}

		#endregion

		#region public Color MinColor

		private Color _MinColor = Color.Red;

		[Category("Gradient")]
		[DisplayName("Min Color")]
		public Color MinColor
		{
			get { return _MinColor; }

			set
			{
				if (_MinColor != value)
				{
					_MinColor = value;
					Invalidate();
				}
			}
		}

		#endregion

		#region public Color MaxColor

		private Color _MaxColor = Color.Blue;

		[Category("Gradient")]
		[DisplayName("Max Color")]
		public Color MaxColor
		{
			get { return _MaxColor; }

			set
			{
				if (_MaxColor != value)
				{
					_MaxColor = value;
					Invalidate();
				}
			}
		}

		#endregion
	}
}