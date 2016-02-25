using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MassEffect3.ColorPicker
{
	public partial class ColorBox : UserControl
	{
		private ColorBgra _FillColor;

		public ColorBox()
		{
			InitializeComponent();
			FillColor = ColorBgra.Transparent;
		}

		public ColorBgra FillColor
		{
			get { return _FillColor; }

			set
			{
				if (_FillColor != value)
				{
					_FillColor = value;
					OnFillColorChanged();
					Invalidate();
				}
			}
		}

		public event EventHandler FillColorChanged;

		private void OnFillColorChanged()
		{
			if (FillColorChanged != null)
			{
				FillColorChanged(this, new EventArgs());
			}
		}

		private void DrawFill(Graphics g)
		{
			g.FillRectangle(
				new HatchBrush(
					HatchStyle.LargeCheckerBoard,
					Color.White,
					Color.Gray),
				ClientRectangle);
			g.FillRectangle(
				new SolidBrush(
					_FillColor.ToColor()),
				ClientRectangle);
		}

		protected void OnPaint(object sender, PaintEventArgs e)
		{
			DrawFill(e.Graphics);
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			DrawFill(e.Graphics);
		}
	}
}