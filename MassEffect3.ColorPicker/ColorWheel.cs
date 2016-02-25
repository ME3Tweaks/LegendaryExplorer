using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace MassEffect3.ColorPicker
{
	public class ColorWheel
		: UserControl
	{
		/// <summary>
		///     This number controls what you might call the tesselation of
		///     the color wheel. Higher value = slower, lower value = looks worse.
		/// </summary>
		private const int ColorTesselation = 80;

		/// <summary>
		///     Required designer variable.
		/// </summary>
		private Container _Components;

		private PictureBox _PictureBox;

		private Bitmap _RenderBitmap;
		private bool _Tracking;

		#region public ColorHSV Color

		private ColorHsv _Color;

		public ColorHsv Color
		{
			get { return _Color; }

			set
			{
				if (_Color != value)
				{
					_Color = value;
					OnColorChanged();
					Refresh();
				}
			}
		}

		#endregion

		public ColorWheel()
		{
			InitializeComponent();
			_Color = new ColorHsv(0, 0, 0);
		}

		private static PointF SphericalToCartesian(float r, float theta)
		{
			var x = r * (float) Math.Cos(theta);
			var y = r * (float) Math.Sin(theta);

			return new PointF(x, y);
		}

		private static PointF[] GetCirclePoints(float r, PointF center)
		{
			var points = new PointF[ColorTesselation];

			for (var i = 0; i < ColorTesselation; i++)
			{
				var theta = (i / (float) ColorTesselation) * 2 * (float) Math.PI;
				points[i] = SphericalToCartesian(r, theta);
				points[i].X += center.X;
				points[i].Y += center.Y;
			}

			return points;
		}

		private Color[] GetColors()
		{
			var colors = new Color[ColorTesselation];

			for (var i = 0; i < ColorTesselation; i++)
			{
				var hue = (i * 360) / ColorTesselation;
				colors[i] = new ColorHsv(hue, 100, 100).ToColor();
			}

			return colors;
		}

		protected override void OnLoad(EventArgs e)
		{
			InitRendering();
			base.OnLoad(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			InitRendering();
			base.OnPaint(e);
		}

		private void InitRendering()
		{
			if (_RenderBitmap == null)
			{
				InitRenderSurface();
				_PictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
				var size = (int) Math.Ceiling(ComputeDiameter(Size));
				_PictureBox.Size = new Size(size, size);
				_PictureBox.Image = _RenderBitmap;
			}
		}

		private void OnWheelPaint(object sender, PaintEventArgs e)
		{
			var radius = ComputeRadius(Size);
			var theta = (Color.Hue / 360.0f) * 2.0f * (float) Math.PI;
			var alpha = (Color.Saturation / 100.0f);
			var x = (alpha * (radius - 1) * (float) Math.Cos(theta)) + radius;
			var y = (alpha * (radius - 1) * (float) Math.Sin(theta)) + radius;
			var ix = (int) x;
			var iy = (int) y;

			// Draw the 'target rectangle'
			var container = e.Graphics.BeginContainer();
			e.Graphics.PixelOffsetMode = PixelOffsetMode.None;
			e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
			e.Graphics.DrawRectangle(Pens.Black, ix - 1, iy - 1, 3, 3);
			e.Graphics.DrawRectangle(Pens.White, ix, iy, 1, 1);
			e.Graphics.EndContainer(container);
		}

		private void InitRenderSurface()
		{
			if (_RenderBitmap != null)
			{
				_RenderBitmap.Dispose();
			}

			var wheelDiameter = (int) ComputeDiameter(Size);

			_RenderBitmap = new Bitmap(
				Math.Max(1, (wheelDiameter * 4) / 3),
				Math.Max(1, (wheelDiameter * 4) / 3),
				PixelFormat.Format24bppRgb);

			using (var g = Graphics.FromImage(_RenderBitmap))
			{
				g.Clear(BackColor);
				DrawWheel(
					g,
					_RenderBitmap.Width,
					_RenderBitmap.Height);
			}
		}

		private void DrawWheel(Graphics g, int width, int height)
		{
			var radius = ComputeRadius(new Size(width, height));
			var points = GetCirclePoints(
				Math.Max(1.0f, radius - 1),
				new PointF(radius, radius));

			using (var pgb = new PathGradientBrush(points))
			{
				pgb.CenterColor = new ColorHsv(0, 0, 100).ToColor();
				pgb.CenterPoint = new PointF(radius, radius);
				pgb.SurroundColors = GetColors();

				g.FillEllipse(pgb, 0, 0, radius * 2, radius * 2);
			}
		}

		private static float ComputeRadius(Size size)
		{
			return Math.Min((float) size.Width / 2, (float) size.Height / 2);
		}

		private static float ComputeDiameter(Size size)
		{
			return Math.Min(size.Width, (float) size.Height);
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			if (_RenderBitmap != null &&
				Equals(ComputeRadius(Size), ComputeRadius(_RenderBitmap.Size)) == false)
			{
				_RenderBitmap.Dispose();
				_RenderBitmap = null;
			}

			Invalidate();
		}

		[Category("Action")]
		[Description("Occurs when the selected color changes.")]
		public event EventHandler ColorChanged;

		protected virtual void OnColorChanged()
		{
			if (ColorChanged != null)
			{
				ColorChanged(this, EventArgs.Empty);
			}
		}

		private void GrabColor(Point point)
		{
			// center our coordinate system so the middle is (0,0), and positive Y is facing up
			var cx = point.X - (Width / 2);
			var cy = point.Y - (Height / 2);

			var theta = Math.Atan2(cy, cx);

			if (theta < 0)
			{
				theta += 2 * Math.PI;
			}

			var alpha = Math.Sqrt((cx * cx) + (cy * cy));

			var h = (int) ((theta / (Math.PI * 2)) * 360.0);
			// ReSharper disable PossibleLossOfFraction
			var s = (int) Math.Min(100.0, (alpha / (Width / 2)) * 100);
			// ReSharper restore PossibleLossOfFraction
			const int v = 100;

			_Color = new ColorHsv(h, s, v);
			OnColorChanged();
			Invalidate(true);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			if (e.Button == MouseButtons.Left)
			{
				_Tracking = true;
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);

			if (_Tracking)
			{
				GrabColor(new Point(e.X, e.Y));
				_Tracking = false;
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			new Point(e.X, e.Y);
			if (_Tracking)
			{
				GrabColor(new Point(e.X, e.Y));
			}
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

		private void OnWheelMouseMove(object sender, MouseEventArgs e)
		{
			OnMouseMove(e);
		}

		private void OnWheelMouseUp(object sender, MouseEventArgs e)
		{
			OnMouseUp(e);
		}

		private void OnWheelMouseDown(object sender, MouseEventArgs e)
		{
			OnMouseDown(e);
		}

		private void OnWheelClick(object sender, EventArgs e)
		{
			OnClick(e);
		}

		#region Component Designer generated code

		/// <summary>
		///     Required method for Designer support - do not modify
		///     the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this._PictureBox = new System.Windows.Forms.PictureBox();
			((System.ComponentModel.ISupportInitialize) (this._PictureBox)).BeginInit();
			this.SuspendLayout();
			// 
			// pictureBox
			// 
			this._PictureBox.Dock = System.Windows.Forms.DockStyle.Fill;
			this._PictureBox.Location = new System.Drawing.Point(0, 0);
			this._PictureBox.Name = "_PictureBox";
			this._PictureBox.Size = new System.Drawing.Size(150, 150);
			this._PictureBox.TabIndex = 0;
			this._PictureBox.TabStop = false;
			this._PictureBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.OnWheelMouseMove);
			this._PictureBox.Click += new System.EventHandler(this.OnWheelClick);
			this._PictureBox.MouseDown += new System.Windows.Forms.MouseEventHandler(this.OnWheelMouseDown);
			this._PictureBox.Paint += new System.Windows.Forms.PaintEventHandler(this.OnWheelPaint);
			this._PictureBox.MouseUp += new System.Windows.Forms.MouseEventHandler(this.OnWheelMouseUp);
			// 
			// ColorWheel
			// 
			this.Controls.Add(this._PictureBox);
			this.Name = "ColorWheel";
			((System.ComponentModel.ISupportInitialize) (this._PictureBox)).EndInit();
			this.ResumeLayout(false);
		}

		#endregion
	}
}