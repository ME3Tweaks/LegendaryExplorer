using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("GalaxyMapSaveRecord")]
	public class GalaxyMap : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("Planets")]
		private List<Planet> _Planets = new List<Planet>();

		[OriginalName("Systems")]
		private List<System> _Systems = new List<System>();

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _Planets);
			stream.Serialize(ref _Systems, s => s.Version < 51, () => new List<System>());
		}

		#region Properties

		[LocalizedDisplayName("Planets", typeof (Localization.GalaxyMap))]
		public List<Planet> Planets
		{
			get { return _Planets; }
			set
			{
				if (value != _Planets)
				{
					_Planets = value;
					NotifyPropertyChanged("Planets");
				}
			}
		}

		[LocalizedDisplayName("Systems", typeof (Localization.GalaxyMap))]
		public List<System> Systems
		{
			get { return _Systems; }
			set
			{
				if (value != _Systems)
				{
					_Systems = value;
					NotifyPropertyChanged("Systems");
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

		#region Children

		[TypeConverter(typeof (ExpandableObjectConverter))]
		[OriginalName("PlanetSaveRecord")]
		public class Planet : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("PlanetID")]
			private int _Id;

			[OriginalName("Probes")]
			private List<Vector2D> _Probes = new List<Vector2D>();

			[OriginalName("bShowAsScanned")]
			private bool _ShowAsScanned;

			[OriginalName("bVisited")]
			private bool _Visited;

			#endregion

			// for CollectionEditor
			[Browsable(false)]
			public string Name
			{
				get { return _Id.ToString(CultureInfo.InvariantCulture); }
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Id);
				stream.Serialize(ref _Visited);
				stream.Serialize(ref _Probes);
				stream.Serialize(ref _ShowAsScanned, s => s.Version < 51, () => false);
			}

			public override string ToString()
			{
				return Name ?? "(null)";
			}

			private void NotifyPropertyChanged(string propertyName)
			{
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
				}
			}

			#region Properties

			[LocalizedDisplayName("Planet_Id", typeof (Localization.GalaxyMap))]
			public int Id
			{
				get { return _Id; }
				set
				{
					if (value != _Id)
					{
						_Id = value;
						NotifyPropertyChanged("Id");
					}
				}
			}

			[LocalizedDisplayName("Planet_Visited", typeof (Localization.GalaxyMap))]
			public bool Visited
			{
				get { return _Visited; }
				set
				{
					if (value != _Visited)
					{
						_Visited = value;
						NotifyPropertyChanged("Visited");
					}
				}
			}

			[LocalizedDisplayName("Planet_Probes", typeof (Localization.GalaxyMap))]
			public List<Vector2D> Probes
			{
				get { return _Probes; }
				set
				{
					if (value != _Probes)
					{
						_Probes = value;
						NotifyPropertyChanged("Probes");
					}
				}
			}

			[LocalizedDisplayName("Planet_ShowAsScanned", typeof (Localization.GalaxyMap))]
			public bool ShowAsScanned
			{
				get { return _ShowAsScanned; }
				set
				{
					if (value != _ShowAsScanned)
					{
						_ShowAsScanned = value;
						NotifyPropertyChanged("ShowAsScanned");
					}
				}
			}

			#endregion
		}

		[TypeConverter(typeof (ExpandableObjectConverter))]
		[OriginalName("SystemSaveRecord")]
		public class System : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("SystemID")]
			private int _Id;

			[OriginalName("fReaperAlertLevel")]
			private float _ReaperAlertLevel;

			[OriginalName("bReapersDetected")]
			private bool _ReapersDetected;

			#endregion

			// for CollectionEditor
			[Browsable(false)]
			public string Name
			{
				get { return _Id.ToString(CultureInfo.InvariantCulture); }
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Id);
				stream.Serialize(ref _ReaperAlertLevel);
				stream.Serialize(ref _ReapersDetected, s => s.Version < 58, () => false);
			}

			public override string ToString()
			{
				return Name ?? "(null)";
			}

			private void NotifyPropertyChanged(string propertyName)
			{
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
				}
			}

			#region Properties

			[LocalizedDisplayName("System_Id", typeof (Localization.GalaxyMap))]
			public int Id
			{
				get { return _Id; }
				set
				{
					if (value != _Id)
					{
						_Id = value;
						NotifyPropertyChanged("Id");
					}
				}
			}

			[LocalizedDisplayName("System_ReaperAlertLevel", typeof (Localization.GalaxyMap))]
			public float ReaperAlertLevel
			{
				get { return _ReaperAlertLevel; }
				set
				{
					if (Equals(value, _ReaperAlertLevel) == false)
					{
						_ReaperAlertLevel = value;
						NotifyPropertyChanged("ReaperAlertLevel");
					}
				}
			}

			[LocalizedDisplayName("System_ReapersDetected", typeof (Localization.GalaxyMap))]
			public bool ReapersDetected
			{
				get { return _ReapersDetected; }
				set
				{
					if (value != _ReapersDetected)
					{
						_ReapersDetected = value;
						NotifyPropertyChanged("ReapersDetected");
					}
				}
			}

			#endregion
		}

		#endregion
	}
}