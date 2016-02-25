using System;
using System.Drawing;
using System.Windows.Forms;

namespace MassEffect3.ColorPicker
{
	public partial class ColorDialog : Form
	{
		private int _IgnoreChangedEventCounter;

		public ColorDialog()
		{
			InitializeComponent();
			colorWheel1.Color = ColorHsv.FromColor(Color.White);
		}

		public ColorBgra WheelColor
		{
			get
			{
				var rgb = colorWheel1.Color.ToRgb();
				return ColorBgra.FromBgra(rgb.B.ClampToByte(),
					rgb.G.ClampToByte(),
					rgb.R.ClampToByte(),
					alphaColorSlider.Value.ClampToByte());
			}

			set
			{
				colorWheel1.Color = ColorHsv.FromColor(value.ToColor());
				alphaColorSlider.Value = value.A;
			}
		}

		private bool IgnoreChangedEvents
		{
			get { return _IgnoreChangedEventCounter != 0; }
		}

		private void PushIgnoreChangedEvents()
		{
			++_IgnoreChangedEventCounter;
		}

		private void PopIgnoreChangedEvents()
		{
			--_IgnoreChangedEventCounter;
		}

		private void SetColorGradientMinMaxColorsRgb(int r, int g, int b)
		{
			redColorSlider.MaxColor = Color.FromArgb(255, g, b);
			redColorSlider.MinColor = Color.FromArgb(0, g, b);
			greenColorSlider.MaxColor = Color.FromArgb(r, 255, b);
			greenColorSlider.MinColor = Color.FromArgb(r, 0, b);
			blueColorSlider.MaxColor = Color.FromArgb(r, g, 255);
			blueColorSlider.MinColor = Color.FromArgb(r, g, 0);
		}

		// ReSharper disable UnusedParameter.Local
		private void SetColorGradientMinMaxColorsAlpha(int a)
			// ReSharper restore UnusedParameter.Local
		{
			/*
            Color[] colors = new Color[256];

            for (int newA = 0; newA <= 255; ++newA)
            {
                colors[newA] = Color.FromArgb(
                    newA,
                    this.redColorSlider.Value,
                    this.greenColorSlider.Value,
                    this.blueColorSlider.Value);
            }

            this.alphaColorSlider.CustomGradient = colors;
            */

			alphaColorSlider.MaxColor = Color.FromArgb(
				redColorSlider.Value,
				greenColorSlider.Value,
				blueColorSlider.Value);
		}

		private void SetColorGradientValuesRgb(int r, int g, int b)
		{
			// ReSharper disable RedundantCheckBeforeAssignment
			if (redColorSlider.Value != r) // ReSharper restore RedundantCheckBeforeAssignment
			{
				redColorSlider.Value = r;
			}

			// ReSharper disable RedundantCheckBeforeAssignment
			if (greenColorSlider.Value != g) // ReSharper restore RedundantCheckBeforeAssignment
			{
				greenColorSlider.Value = g;
			}

			// ReSharper disable RedundantCheckBeforeAssignment
			if (blueColorSlider.Value != b) // ReSharper restore RedundantCheckBeforeAssignment
			{
				blueColorSlider.Value = b;
			}
		}

		private void SetColorGradientValuesHsv(int h, int s, int v)
		{
			// ReSharper disable RedundantCheckBeforeAssignment
			if (hueColorSlider.Value != h) // ReSharper restore RedundantCheckBeforeAssignment
			{
				hueColorSlider.Value = h;
			}

			// ReSharper disable RedundantCheckBeforeAssignment
			if (saturationColorSlider.Value != s) // ReSharper restore RedundantCheckBeforeAssignment
			{
				saturationColorSlider.Value = s;
			}

			// ReSharper disable RedundantCheckBeforeAssignment
			if (valueColorSlider.Value != v) // ReSharper restore RedundantCheckBeforeAssignment
			{
				valueColorSlider.Value = v;
			}
		}

		private void SetColorGradientMinMaxColorsHsv(int h, int s, int v)
		{
			var hueColors = new Color[361];

			for (var newH = 0; newH <= 360; ++newH)
			{
				var hsv = new ColorHsv(newH, 100, 100);
				hueColors[newH] = hsv.ToColor();
			}

			hueColorSlider.CustomGradient = hueColors;

			var satColors = new Color[101];

			for (var newS = 0; newS <= 100; ++newS)
			{
				var hsv = new ColorHsv(h, newS, v);
				satColors[newS] = hsv.ToColor();
			}

			saturationColorSlider.CustomGradient = satColors;

			valueColorSlider.MaxColor = new ColorHsv(h, s, 100).ToColor();
			valueColorSlider.MinColor = new ColorHsv(h, s, 0).ToColor();
		}

		private void SyncHsvFromRgb(ColorBgra bgra)
		{
			var hsv = ColorHsv.FromColor(bgra.ToColor());

			SetColorGradientValuesHsv(hsv.Hue, hsv.Saturation, hsv.Value);
			SetColorGradientMinMaxColorsHsv(hsv.Hue, hsv.Saturation, hsv.Value);

			colorWheel1.Color = hsv;
		}

		private void SyncRgbFromHsv(ColorHsv hsv)
		{
			var rgb = hsv.ToRgb();

			redColorSlider.Value = rgb.R;
			greenColorSlider.Value = rgb.G;
			blueColorSlider.Value = rgb.B;

			SetColorGradientValuesRgb(rgb.R, rgb.G, rgb.B);
			SetColorGradientMinMaxColorsRgb(rgb.R, rgb.G, rgb.B);
			SetColorGradientMinMaxColorsAlpha(alphaColorSlider.Value);
		}

		private void OnColorSliderValueChanged(object sender, EventArgs e)
		{
			if (IgnoreChangedEvents)
			{
				return;
			}

			PushIgnoreChangedEvents();

			if (sender == redColorSlider ||
				sender == greenColorSlider ||
				sender == blueColorSlider)
			{
				var color = ColorBgra.FromBgra(
					blueColorSlider.Value.ClampToByte(),
					greenColorSlider.Value.ClampToByte(),
					redColorSlider.Value.ClampToByte(),
					alphaColorSlider.Value.ClampToByte());

				SetColorGradientMinMaxColorsRgb(color.R, color.G, color.B);
				SetColorGradientMinMaxColorsAlpha(color.A);
				SetColorGradientValuesRgb(color.R, color.G, color.B);
				SetColorGradientMinMaxColorsAlpha(color.A);

				SyncHsvFromRgb(color);
				//OnUserColorChanged(rgbColor);
			}
			else if (
				sender == hueColorSlider ||
				sender == saturationColorSlider ||
				sender == valueColorSlider)
			{
				var oldHsv = colorWheel1.Color;
				var hsv = new ColorHsv(
					hueColorSlider.Value,
					saturationColorSlider.Value,
					valueColorSlider.Value);

				if (oldHsv != hsv)
				{
					colorWheel1.Color = hsv;

					SetColorGradientValuesHsv(
						hsv.Hue,
						hsv.Saturation,
						hsv.Value);

					SetColorGradientMinMaxColorsHsv(
						hsv.Hue,
						hsv.Saturation,
						hsv.Value);

					SyncRgbFromHsv(hsv);
					//ColorRGB rgbColor = hsv.ToRgb();
					//OnUserColorChanged(ColorBgra.FromBgra((byte)rgbColor.Blue, (byte)rgbColor.Green, (byte)rgbColor.Red, (byte)alphaUpDown.Value));
				}
			}

			PopIgnoreChangedEvents();
		}

		private void OnWheelColorChanged(object sender, EventArgs e)
		{
			if (IgnoreChangedEvents)
			{
				return;
			}

			PushIgnoreChangedEvents();

			var hsvColor = colorWheel1.Color;
			var rgbColor = hsvColor.ToRgb();
			var color = ColorBgra.FromBgra(
				(byte) rgbColor.B,
				(byte) rgbColor.G,
				(byte) rgbColor.R,
				(byte) alphaColorSlider.Value);

			hueColorSlider.Value = hsvColor.Hue;
			saturationColorSlider.Value = hsvColor.Saturation;
			valueColorSlider.Value = hsvColor.Value;

			redColorSlider.Value = color.R;
			greenColorSlider.Value = color.G;
			blueColorSlider.Value = color.B;

			alphaColorSlider.Value = color.A;

			SetColorGradientValuesHsv(
				hsvColor.Hue,
				hsvColor.Saturation,
				hsvColor.Value);

			SetColorGradientMinMaxColorsHsv(
				hsvColor.Hue,
				hsvColor.Saturation,
				hsvColor.Value);

			SetColorGradientValuesRgb(
				color.R,
				color.G,
				color.B);

			SetColorGradientMinMaxColorsRgb(
				color.R,
				color.G,
				color.B);

			SetColorGradientMinMaxColorsAlpha(color.A);

			PopIgnoreChangedEvents();
			Update();
		}
	}
}