using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("MorphHeadSaveRecord")]
	public class MorphHead : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("AccessoryMeshes")]
		private List<string> _AccessoryMeshes = new List<string>();

		[OriginalName("HairMesh")]
		private string _HairMesh;

		[OriginalName("LOD0Vertices")]
		private List<Vector> _Lod0Vertices = new List<Vector>();

		[OriginalName("LOD1Vertices")]
		private List<Vector> _Lod1Vertices = new List<Vector>();

		[OriginalName("LOD2Vertices")]
		private List<Vector> _Lod2Vertices = new List<Vector>();

		[OriginalName("LOD3Vertices")]
		private List<Vector> _Lod3Vertices = new List<Vector>();

		[OriginalName("MorphFeatures")]
		private List<MorphFeature> _MorphFeatures = new List<MorphFeature>();

		[OriginalName("OffsetBones")]
		private List<OffsetBone> _OffsetBones = new List<OffsetBone>();

		[OriginalName("ScalarParameters")]
		private List<ScalarParameter> _ScalarParameters = new List<ScalarParameter>();

		[OriginalName("TextureParameters")]
		private List<TextureParameter> _TextureParameters = new List<TextureParameter>();

		[OriginalName("VectorParameters")]
		private BindingList<VectorParameter> _VectorParameters = new BindingList<VectorParameter>();

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _HairMesh);
			stream.Serialize(ref _AccessoryMeshes);
			stream.Serialize(ref _MorphFeatures);
			stream.Serialize(ref _OffsetBones);
			stream.Serialize(ref _Lod0Vertices);
			stream.Serialize(ref _Lod1Vertices);
			stream.Serialize(ref _Lod2Vertices);
			stream.Serialize(ref _Lod3Vertices);
			stream.Serialize(ref _ScalarParameters);
			stream.Serialize(ref _VectorParameters);
			stream.Serialize(ref _TextureParameters);
		}

		#region Properties

		[LocalizedDisplayName("HairMesh", typeof (Localization.MorphHead))]
		public string HairMesh
		{
			get { return _HairMesh; }
			set
			{
				if (value != _HairMesh)
				{
					_HairMesh = value;
					NotifyPropertyChanged("HairMesh");
				}
			}
		}

		[Editor(
			"System.Windows.Forms.Design.StringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
			, typeof (UITypeEditor))]
		[LocalizedDisplayName("AccessoryMeshes", typeof (Localization.MorphHead))]
		public List<string> AccessoryMeshes
		{
			get { return _AccessoryMeshes; }
			set
			{
				if (value != _AccessoryMeshes)
				{
					_AccessoryMeshes = value;
					NotifyPropertyChanged("AccessoryMeshes");
				}
			}
		}

		[LocalizedDisplayName("MorphFeatures", typeof (Localization.MorphHead))]
		public List<MorphFeature> MorphFeatures
		{
			get { return _MorphFeatures; }
			set
			{
				if (value != _MorphFeatures)
				{
					_MorphFeatures = value;
					NotifyPropertyChanged("MorphFeatures");
				}
			}
		}

		[LocalizedDisplayName("OffsetBones", typeof (Localization.MorphHead))]
		public List<OffsetBone> OffsetBones
		{
			get { return _OffsetBones; }
			set
			{
				if (value != _OffsetBones)
				{
					_OffsetBones = value;
					NotifyPropertyChanged("OffsetBones");
				}
			}
		}

		[LocalizedDisplayName("Lod0Vertices", typeof (Localization.MorphHead))]
		public List<Vector> Lod0Vertices
		{
			get { return _Lod0Vertices; }
			set
			{
				if (value != _Lod0Vertices)
				{
					_Lod0Vertices = value;
					NotifyPropertyChanged("Lod0Vertices");
				}
			}
		}

		[LocalizedDisplayName("Lod1Vertices", typeof (Localization.MorphHead))]
		public List<Vector> Lod1Vertices
		{
			get { return _Lod1Vertices; }
			set
			{
				if (value != _Lod1Vertices)
				{
					_Lod1Vertices = value;
					NotifyPropertyChanged("Lod1Vertices");
				}
			}
		}

		[LocalizedDisplayName("Lod2Vertices", typeof (Localization.MorphHead))]
		public List<Vector> Lod2Vertices
		{
			get { return _Lod2Vertices; }
			set
			{
				if (value != _Lod2Vertices)
				{
					_Lod2Vertices = value;
					NotifyPropertyChanged("Lod2Vertices");
				}
			}
		}

		[LocalizedDisplayName("Lod3Vertices", typeof (Localization.MorphHead))]
		public List<Vector> Lod3Vertices
		{
			get { return _Lod3Vertices; }
			set
			{
				if (value != _Lod3Vertices)
				{
					_Lod3Vertices = value;
					NotifyPropertyChanged("Lod3Vertices");
				}
			}
		}

		[LocalizedDisplayName("ScalarParameters", typeof (Localization.MorphHead))]
		public List<ScalarParameter> ScalarParameters
		{
			get { return _ScalarParameters; }
			set
			{
				if (value != _ScalarParameters)
				{
					_ScalarParameters = value;
					NotifyPropertyChanged("ScalarParameters");
				}
			}
		}

		[LocalizedDisplayName("VectorParameters", typeof (Localization.MorphHead))]
		public BindingList<VectorParameter> VectorParameters
		{
			get { return _VectorParameters; }
			set
			{
				if (value != _VectorParameters)
				{
					_VectorParameters = value;
					NotifyPropertyChanged("VectorParameters");
				}
			}
		}

		[LocalizedDisplayName("TextureParameters", typeof (Localization.MorphHead))]
		public List<TextureParameter> TextureParameters
		{
			get { return _TextureParameters; }
			set
			{
				if (value != _TextureParameters)
				{
					_TextureParameters = value;
					NotifyPropertyChanged("TextureParameters");
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
		[OriginalName("MorphFeatureSaveRecord")]
		public class MorphFeature : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("Feature")]
			private string _Feature;

			[OriginalName("Offset")]
			private float _Offset;

			#endregion

			// for CollectionEditor
			[Browsable(false)]
			public string Name
			{
				get { return _Feature; }
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Feature);
				stream.Serialize(ref _Offset);
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

			[LocalizedDisplayName("MorphFeature_Feature", typeof (Localization.MorphHead))]
			public string Feature
			{
				get { return _Feature; }
				set
				{
					if (value != _Feature)
					{
						_Feature = value;
						NotifyPropertyChanged("Feature");
					}
				}
			}

			[LocalizedDisplayName("MorphFeature_Offset", typeof (Localization.MorphHead))]
			public float Offset
			{
				get { return _Offset; }
				set
				{
					if (Equals(value, _Offset) == false)
					{
						_Offset = value;
						NotifyPropertyChanged("Offset");
					}
				}
			}

			#endregion
		}

		[TypeConverter(typeof (ExpandableObjectConverter))]
		[OriginalName("OffsetBoneSaveRecord")]
		public class OffsetBone : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("Name")]
			private string _Name;

			[OriginalName("Name")]
			private Vector _Offset = new Vector();

			#endregion

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Name);
				stream.Serialize(ref _Offset);
			}

			// for CollectionEditor
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

			[LocalizedDisplayName("OffsetBone_Name", typeof (Localization.MorphHead))]
			public string Name
			{
				get { return _Name; }
				set
				{
					if (value != _Name)
					{
						_Name = value;
						NotifyPropertyChanged("Name");
					}
				}
			}

			[LocalizedDisplayName("OffsetBone_Offset", typeof (Localization.MorphHead))]
			public Vector Offset
			{
				get { return _Offset; }
				set
				{
					if (value != _Offset)
					{
						_Offset = value;
						NotifyPropertyChanged("Offset");
					}
				}
			}

			#endregion
		}

		[TypeConverter(typeof (ExpandableObjectConverter))]
		[OriginalName("ScalarParameterSaveRecord")]
		public class ScalarParameter : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("Name")]
			private string _Name;

			[OriginalName("Value")]
			private float _Value;

			#endregion

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Name);
				stream.Serialize(ref _Value);
			}

			// for CollectionEditor
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

			[LocalizedDisplayName("ScalarParameter_Name", typeof (Localization.MorphHead))]
			public string Name
			{
				get { return _Name; }
				set
				{
					if (value != _Name)
					{
						_Name = value;
						NotifyPropertyChanged("Name");
					}
				}
			}

			[LocalizedDisplayName("ScalarParameter_Value", typeof (Localization.MorphHead))]
			public float Value
			{
				get { return _Value; }
				set
				{
					if (Equals(value, _Value) == false)
					{
						_Value = value;
						NotifyPropertyChanged("Value");
					}
				}
			}

			#endregion
		}

		[TypeConverter(typeof (ExpandableObjectConverter))]
		[OriginalName("TextureParameterSaveRecord")]
		public class TextureParameter : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("Name")]
			private string _Name;

			[OriginalName("Value")]
			private string _Value;

			#endregion

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Name);
				stream.Serialize(ref _Value);
			}

			// for CollectionEditor
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

			[LocalizedDisplayName("TextureParameter_Name", typeof (Localization.MorphHead))]
			public string Name
			{
				get { return _Name; }
				set
				{
					if (value != _Name)
					{
						_Name = value;
						NotifyPropertyChanged("Name");
					}
				}
			}

			[LocalizedDisplayName("TextureParameter_Value", typeof (Localization.MorphHead))]
			public string Value
			{
				get { return _Value; }
				set
				{
					if (value != _Value)
					{
						_Value = value;
						NotifyPropertyChanged("Value");
					}
				}
			}

			#endregion
		}

		[TypeConverter(typeof (ExpandableObjectConverter))]
		[OriginalName("VectorParameterSaveRecord")]
		public class VectorParameter : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("Name")]
			private string _Name;

			[OriginalName("Value")]
			private LinearColor _Value = new LinearColor();

			#endregion

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Name);
				stream.Serialize(ref _Value);
			}

			// for CollectionEditor
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

			[LocalizedDisplayName("VectorParameter_Name", typeof (Localization.MorphHead))]
			public string Name
			{
				get { return _Name; }
				set
				{
					if (value != _Name)
					{
						_Name = value;
						NotifyPropertyChanged("Name");
					}
				}
			}

			[LocalizedDisplayName("VectorParameter_Value", typeof (Localization.MorphHead))]
			public LinearColor Value
			{
				get { return _Value; }
				set
				{
					if (value != _Value)
					{
						_Value = value;
						NotifyPropertyChanged("Value");
					}
				}
			}

			#endregion
		}

		#endregion
	}
}