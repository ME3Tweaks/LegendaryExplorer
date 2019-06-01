using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal
{
    public class PropGrid : CollectionBase, ICustomTypeDescriptor
    {
        public void Add(CustomProperty Value)
		{
			base.List.Add(Value);
		}

		/// <summary>
		/// Remove item from List
		/// </summary>
		/// <param name="Name"></param>
		public void Remove(string Name)
		{
			foreach(CustomProperty prop in base.List)
			{
				if(prop.Name == Name)
				{
					base.List.Remove(prop);
					return;
				}
			}
		}

		/// <summary>
		/// Indexer
		/// </summary>
		public CustomProperty this[int index] 
		{
			get 
			{
				return (CustomProperty)base.List[index];
			}
			set
			{
				base.List[index] = value;
			}
		}



        public static void propGridPropertyValueChanged(PropertyValueChangedEventArgs e, int n, IMEPackage pcc)
        {
            string name = e.ChangedItem.Label;
            GridItem parent = e.ChangedItem.Parent;
            //if (parent != null) name = parent.Label;
            if (parent.Label == "data")
            {
                GridItem parent2 = parent.Parent;
                if (parent2 != null) name = parent2.Label;
            }
            Type parentVal = null;
            if (parent.Value != null)
            {
                parentVal = parent.Value.GetType();
            }
            if (name == "nameindex" || name == "index" || parentVal == typeof(ColorProp) || parentVal == typeof(VectorProp) || parentVal == typeof(Unreal.RotatorProp) || parentVal == typeof(Unreal.LinearColorProp))
            {
                name = parent.Label;
            }
            IExportEntry ent = pcc.getExport(n);
            byte[] data = ent.Data;
            List<PropertyReader.Property> p = PropertyReader.getPropList(ent);
            int m = -1;
            for (int i = 0; i < p.Count; i++)
                if (pcc.getNameEntry(p[i].Name) == name)
                    m = i;
            if (m == -1)
                return;
            byte[] buff2;
            switch (p[m].TypeVal)
            {
                case PropertyType.BoolProperty:
                    byte res = 0;
                    if ((bool)e.ChangedItem.Value == true)
                        res = 1;
                    data[p[m].offsetval] = res;
                    break;
                case PropertyType.FloatProperty:
                    buff2 = BitConverter.GetBytes((float)e.ChangedItem.Value);
                    for (int i = 0; i < 4; i++)
                        data[p[m].offsetval + i] = buff2[i];
                    break;
                case PropertyType.IntProperty:
                case PropertyType.StringRefProperty:
                    int newv = Convert.ToInt32(e.ChangedItem.Value);
                    int oldv = Convert.ToInt32(e.OldValue);
                    buff2 = BitConverter.GetBytes(newv);
                    for (int i = 0; i < 4; i++)
                        data[p[m].offsetval + i] = buff2[i];
                    break;
                case PropertyType.StrProperty:
                    string s = Convert.ToString(e.ChangedItem.Value);
                    int stringMultiplier = 1;
                    int oldLength = BitConverter.ToInt32(data, p[m].offsetval);
                    if (oldLength < 0)
                    {
                        stringMultiplier = 2;
                        oldLength *= -2;
                    }
                    int oldSize = 4 + oldLength;
                    List<byte> stringBuff = new List<byte>(s.Length * stringMultiplier);
                    if (stringMultiplier == 2)
                    {
                        for (int j = 0; j < s.Length; j++)
                        {
                            stringBuff.AddRange(BitConverter.GetBytes(s[j]));
                        }
                        stringBuff.Add(0);
                    }
                    else
                    {
                        for (int j = 0; j < s.Length; j++)
                        {
                            stringBuff.Add(BitConverter.GetBytes(s[j])[0]);
                        }
                    }
                    stringBuff.Add(0);
                    buff2 = BitConverter.GetBytes((s.Length + 1) * stringMultiplier + 4);
                    for (int j = 0; j < 4; j++)
                        data[p[m].offsetval - 8 + j] = buff2[j];
                    buff2 = BitConverter.GetBytes((s.Length + 1) * stringMultiplier == 1 ? 1 : -1);
                    for (int j = 0; j < 4; j++)
                        data[p[m].offsetval + j] = buff2[j];
                    buff2 = new byte[data.Length - oldLength + stringBuff.Count];
                    int startLength = p[m].offsetval + 4;
                    int startLength2 = startLength + oldLength;
                    for (int i = 0; i < startLength; i++)
                    {
                        buff2[i] = data[i];
                    }
                    for (int i = 0; i < stringBuff.Count; i++)
                    {
                        buff2[i + startLength] = stringBuff[i];
                    }
                    startLength += stringBuff.Count;
                    for (int i = 0; i < data.Length - startLength2; i++)
                    {
                        buff2[i + startLength] = data[i + startLength2];
                    }
                    data = buff2;
                    break;
                case PropertyType.StructProperty:
                    if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(ColorProp))
                    {
                        switch (e.ChangedItem.Label)
                        {
                            case "Alpha":
                                data[p[m].offsetval + 11] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Red":
                                data[p[m].offsetval + 10] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Green":
                                data[p[m].offsetval + 9] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            case "Blue":
                                data[p[m].offsetval + 8] = Convert.ToByte(e.ChangedItem.Value);
                                break;
                            default:
                                break;
                        }
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(VectorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "X":
                                offset = 8;
                                break;
                            case "Y":
                                offset = 12;
                                break;
                            case "Z":
                                offset = 16;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            buff2 = BitConverter.GetBytes(Convert.ToSingle(e.ChangedItem.Value));
                            for (int i = 0; i < 4; i++)
                                data[p[m].offsetval + offset + i] = buff2[i];
                        }
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.RotatorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "Pitch":
                                offset = 8;
                                break;
                            case "Yaw":
                                offset = 12;
                                break;
                            case "Roll":
                                offset = 16;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            int val = Convert.ToSingle(e.ChangedItem.Value).ToUnrealRotationUnits();
                            buff2 = BitConverter.GetBytes(val);
                            for (int i = 0; i < 4; i++)
                                data[p[m].offsetval + offset + i] = buff2[i];
                        }
                    }
                    else if (e.ChangedItem.Label != "nameindex" && parentVal == typeof(Unreal.LinearColorProp))
                    {
                        int offset = 0;
                        switch (e.ChangedItem.Label)
                        {
                            case "Red":
                                offset = 8;
                                break;
                            case "Green":
                                offset = 12;
                                break;
                            case "Blue":
                                offset = 16;
                                break;
                            case "Alpha":
                                offset = 20;
                                break;
                            default:
                                break;
                        }
                        if (offset != 0)
                        {
                            buff2 = BitConverter.GetBytes(Convert.ToSingle(e.ChangedItem.Value));
                            for (int i = 0; i < 4; i++)
                                data[p[m].offsetval + offset + i] = buff2[i];
                        }
                    }
                    else if (e.ChangedItem.Value is int)
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        if (e.ChangedItem.Label == "nameindex")
                        {
                            int val1 = Convert.ToInt32(e.ChangedItem.Value);
                            buff2 = BitConverter.GetBytes(val1);
                            for (int i = 0; i < 4; i++)
                                data[p[m].offsetval + i] = buff2[i];
                        }
                        else
                        {
                            string sidx = e.ChangedItem.Label.Replace("[", "");
                            sidx = sidx.Replace("]", "");
                            int index = Convert.ToInt32(sidx);
                            buff2 = BitConverter.GetBytes(val);
                            for (int i = 0; i < 4; i++)
                                data[p[m].offsetval + i + index * 4 + 8] = buff2[i];
                        }
                    }
                    break;
                case PropertyType.ByteProperty:
                case PropertyType.NameProperty:
                    if (e.ChangedItem.Value is int)
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        buff2 = BitConverter.GetBytes(val);
                        for (int i = 0; i < 4; i++)
                            data[p[m].offsetval + i] = buff2[i];
                    }
                    break;
                case PropertyType.ObjectProperty:
                    if (e.ChangedItem.Value is int)
                    {
                        int val = Convert.ToInt32(e.ChangedItem.Value);
                        buff2 = BitConverter.GetBytes(val);
                        for (int i = 0; i < 4; i++)
                            data[p[m].offsetval + i] = buff2[i];
                    }
                    break;
                default:
                    return;
            }
            ent.Data = data;
        }


        #region "TypeDescriptor Implementation"
        /// <summary>
        /// Get Class Name
        /// </summary>
        /// <returns>String</returns>
        public string GetClassName()
		{
			return TypeDescriptor.GetClassName(this,true);
		}

		/// <summary>
		/// GetAttributes
		/// </summary>
		/// <returns>AttributeCollection</returns>
		public AttributeCollection GetAttributes()
		{
			return TypeDescriptor.GetAttributes(this,true);
		}

		/// <summary>
		/// GetComponentName
		/// </summary>
		/// <returns>String</returns>
		public string GetComponentName()
		{
			return TypeDescriptor.GetComponentName(this, true);
		}

		/// <summary>
		/// GetConverter
		/// </summary>
		/// <returns>TypeConverter</returns>
		public TypeConverter GetConverter()
		{
			return TypeDescriptor.GetConverter(this, true);
		}

		/// <summary>
		/// GetDefaultEvent
		/// </summary>
		/// <returns>EventDescriptor</returns>
		public EventDescriptor GetDefaultEvent() 
		{
			return TypeDescriptor.GetDefaultEvent(this, true);
		}

		/// <summary>
		/// GetDefaultProperty
		/// </summary>
		/// <returns>PropertyDescriptor</returns>
		public PropertyDescriptor GetDefaultProperty() 
		{
			return TypeDescriptor.GetDefaultProperty(this, true);
		}

		/// <summary>
		/// GetEditor
		/// </summary>
		/// <param name="editorBaseType">editorBaseType</param>
		/// <returns>object</returns>
		public object GetEditor(Type editorBaseType) 
		{
			return TypeDescriptor.GetEditor(this, editorBaseType, true);
		}

		public EventDescriptorCollection GetEvents(Attribute[] attributes) 
		{
			return TypeDescriptor.GetEvents(this, attributes, true);
		}

		public EventDescriptorCollection GetEvents()
		{
			return TypeDescriptor.GetEvents(this, true);
		}

		public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			PropertyDescriptor[] newProps = new PropertyDescriptor[this.Count];
			for (int i = 0; i < this.Count; i++)
			{
				CustomProperty  prop = this[i];
				newProps[i] = new CustomPropertyDescriptor(ref prop, attributes);
			}

			return new PropertyDescriptorCollection(newProps);
		}

		public PropertyDescriptorCollection GetProperties()
		{
			return TypeDescriptor.GetProperties(this, true);
		}

		public object GetPropertyOwner(PropertyDescriptor pd) 
		{
			return this;
		}
		#endregion
	
    }

    public class CustomProperty
    {
        private string sName = string.Empty;
        private string sCat = string.Empty;
        private bool bReadOnly = false;
        private bool bVisible = true;
        private object objValue = null;


        public CustomProperty(string sName, string Category, object value, Type type, bool bReadOnly, bool bVisible)
        {
            this.sName = sName;
            this.sCat = Category;
            this.objValue = value;
            this.type = type;
            this.bReadOnly = bReadOnly;
            this.bVisible = bVisible;
        }        

        private Type type;
        public Type Type
        {
            get { return type; }
        }

        public bool ReadOnly
        {
            get
            {
                return bReadOnly;
            }
        }

        public string Name
        {
            get
            {
                return sName;
            }
        }

        public string Category
        {
            get
            {
                return sCat;
            }
        }

        public bool Visible
        {
            get
            {
                return bVisible;
            }
        }

        public object Value
        {
            get
            {
                return objValue;
            }
            set
            {
                objValue = value;
            }
        }

    }

    public class CustomPropertyDescriptor : PropertyDescriptor
    {
        CustomProperty m_Property;
        public CustomPropertyDescriptor(ref CustomProperty myProperty, Attribute[] attrs)
            : base(myProperty.Name, attrs)
        {
            m_Property = myProperty;
        }

        #region PropertyDescriptor specific

        public override bool CanResetValue(object component)
        {
            return false;
        }

        public override Type ComponentType
        {
            get { return null; }
        }

        public override object GetValue(object component)
        {
            return m_Property.Value;
        }

        public override string Description
        {
            get { return m_Property.Name; }
        }

        public override string Category
        {
            get { return m_Property.Category; }
        }

        public override string DisplayName
        {
            get { return m_Property.Name; }
        }

        public override bool IsReadOnly
        {
            get { return m_Property.ReadOnly; }
        }

        public override void ResetValue(object component)
        {
            //Have to implement
        }

        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }

        public override void SetValue(object component, object value)
        {
            m_Property.Value = value;
        }

        public override Type PropertyType
        {
            get { return m_Property.Type; }
        }

        #endregion


    }
}
