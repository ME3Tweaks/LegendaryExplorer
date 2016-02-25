using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MassEffect3.ColorPicker
{
	public partial class ColorSlider : UserControl
	{
		public enum SliderMode
		{
			Channel,
			Degrees,
			Total,
		}

		private int _IgnoreChangedEventCounter;
		private SliderMode _Mode;

		public ColorSlider()
		{
			InitializeComponent();
			Mode = SliderMode.Channel;
		}

		private bool IgnoreChangedEvents
		{
			get { return _IgnoreChangedEventCounter != 0; }
		}

		[Bindable(true)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[Browsable(true)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
		public override string Text
		{
			get { return label.Text; }
			set { label.Text = value; }
		}

		public SliderMode Mode
		{
			get { return _Mode; }
			set
			{
				switch (value)
				{
					case SliderMode.Channel:
					{
						Minimum = 0;
						Maximum = 255;
						break;
					}

					case SliderMode.Degrees:
					{
						Minimum = 0;
						Maximum = 360;
						break;
					}

					case SliderMode.Total:
					{
						Minimum = 0;
						Maximum = 100;
						break;
					}
				}

				Increment = 1;
				_Mode = value;
			}
		}

		public int Value
		{
			get { return (int) numericUpDown.Value; }
			set
			{
				// ReSharper disable RedundantCheckBeforeAssignment
				if (numericUpDown.Value != value) // ReSharper restore RedundantCheckBeforeAssignment
				{
					numericUpDown.Value = value;
				}
			}
		}

		private int Increment
		{
			// ReSharper disable UnusedMember.Local
			get { return (int) numericUpDown.Increment; }
			// ReSharper restore UnusedMember.Local
			set { numericUpDown.Increment = value; }
		}

		private int Minimum
		{
			// ReSharper disable UnusedMember.Local
			get { return (int) numericUpDown.Minimum; }
			// ReSharper restore UnusedMember.Local
			set { numericUpDown.Minimum = value; }
		}

		private int Maximum
		{
			// ReSharper disable UnusedMember.Local
			get { return (int) numericUpDown.Maximum; }
			// ReSharper restore UnusedMember.Local
			set { numericUpDown.Maximum = value; }
		}

		[DisplayName("Min Color")]
		public Color MinColor
		{
			get { return colorGradient.MinColor; }
			set { colorGradient.MinColor = value; }
		}

		[DisplayName("Max Color")]
		public Color MaxColor
		{
			get { return colorGradient.MaxColor; }
			set { colorGradient.MaxColor = value; }
		}

		[DisplayName("Custom Gradient")]
		public Color[] CustomGradient
		{
			get { return colorGradient.CustomGradient; }
			set { colorGradient.CustomGradient = value; }
		}

		private void PushIgnoreChangedEvents()
		{
			++_IgnoreChangedEventCounter;
		}

		private void PopIgnoreChangedEvents()
		{
			--_IgnoreChangedEventCounter;
		}

		private void OnUpDownEnter(object sender, EventArgs e)
		{
			numericUpDown.Select(0, numericUpDown.Text.Length);
		}

		private void OnUpDownLeave(object sender, EventArgs e)
		{
			OnValueChanged(sender, e);
		}

		public event EventHandler ValueChanged;

		private void OnValueChanged(object sender, EventArgs e)
		{
			if (IgnoreChangedEvents)
			{
				return;
			}

			PushIgnoreChangedEvents();

			if (sender == colorGradient)
			{
				int value;

				switch (Mode)
				{
					case SliderMode.Channel:
					{
						value = colorGradient.Value;
						break;
					}

					case SliderMode.Degrees:
					{
						value = (colorGradient.Value * 360) / 255;
						break;
					}

					case SliderMode.Total:
					{
						value = (colorGradient.Value * 100) / 255;
						break;
					}

					default:
					{
						throw new Exception();
					}
				}

				// ReSharper disable RedundantCheckBeforeAssignment
				if (numericUpDown.Value != value) // ReSharper restore RedundantCheckBeforeAssignment
				{
					numericUpDown.Value = value;
				}
			}
			else if (sender == numericUpDown)
			{
				int value;

				switch (Mode)
				{
					case SliderMode.Channel:
					{
						value = (int) numericUpDown.Value;
						break;
					}

					case SliderMode.Degrees:
					{
						value = ((int) numericUpDown.Value * 255) / 360;
						break;
					}

					case SliderMode.Total:
					{
						value = ((int) numericUpDown.Value * 255) / 100;
						break;
					}

					default:
					{
						throw new Exception();
					}
				}

				// ReSharper disable RedundantCheckBeforeAssignment
				if (colorGradient.Value != value) // ReSharper restore RedundantCheckBeforeAssignment
				{
					colorGradient.Value = value;
				}
			}

			if (ValueChanged != null)
			{
				ValueChanged(this, new EventArgs());
			}

			PopIgnoreChangedEvents();
		}

		private void OnUpDownKeyUp(object sender, KeyEventArgs e)
		{
			OnValueChanged(sender, new EventArgs());
		}
	}
}