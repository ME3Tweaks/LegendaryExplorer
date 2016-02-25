using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using MassEffect3.FileFormats;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("ME1PlotTableRecord")]
	// ReSharper disable InconsistentNaming
	public class ME1PlotTable : ISerializable, INotifyPropertyChanged
		// ReSharper restore InconsistentNaming
	{
		public ME1PlotTable()
		{
			_BoolVariablesWrapper = new BitArrayWrapper(_BoolVariables);
		}

		#region Fields

		private readonly BitArrayWrapper _BoolVariablesWrapper;

		[OriginalName("BoolVariables")]
		private BitArray _BoolVariables = new BitArray(0);

		[OriginalName("FloatVariables")]
		private List<float> _FloatVariables = new List<float>();

		[OriginalName("IntVariables")]
		private List<int> _IntVariables = new List<int>();

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _BoolVariables);
			stream.Serialize(ref _IntVariables);
			stream.Serialize(ref _FloatVariables);
		}

		#region Properties

		[Browsable(false)]
		public BitArray BoolVariables
		{
			get { return _BoolVariables; }
		}

		[DisplayName("Bool Variables")]
		public BitArrayWrapper BoolVariablesWrapper
		{
			get { return _BoolVariablesWrapper; }
			/*set
            {
                if (value != this._BoolVariables)
                {
                    this._BoolVariables = value;
                    this.NotifyPropertyChanged("BoolVariables");
                }
            }*/
		}

		[DisplayName("Int Variables")]
		public List<int> IntVariables
		{
			get { return _IntVariables; }
			set
			{
				if (value != _IntVariables)
				{
					_IntVariables = value;
					NotifyPropertyChanged("IntVariables");
				}
			}
		}

		[DisplayName("Float Variables")]
		public List<float> FloatVariables
		{
			get { return _FloatVariables; }
			set
			{
				if (value != _FloatVariables)
				{
					_FloatVariables = value;
					NotifyPropertyChanged("FloatVariables");
				}
			}
		}

		#endregion

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}