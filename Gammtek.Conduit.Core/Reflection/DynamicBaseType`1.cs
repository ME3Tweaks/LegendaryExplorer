using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Gammtek.Conduit.Reflection
{
	public abstract class DynamicBaseType<T> : DynamicBaseType, ICustomTypeProvider, INotifyPropertyChanged
		where T : DynamicBaseType<T>
	{
		private static readonly IList<CustomPropertyInfo> CustomProperties;
		private readonly IDictionary<string, object> _customPropertyValues;
		private CustomType _customtype;

		static DynamicBaseType()
		{
			if (CustomProperties == null)
			{
				CustomProperties = new List<CustomPropertyInfo>();
			}
		}

		protected DynamicBaseType()
		{
			_customPropertyValues = new Dictionary<string, object>();

			foreach (var property in CustomProperties)
			{
				_customPropertyValues.Add(property.Name, null);
			}
		}

		public Type GetCustomType()
		{
			return _customtype ?? (_customtype = new CustomType(typeof (T)));
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public static void AddProperty(string name, Type type, object value = null, List<Attribute> attributes = null)
		{
			if (!CheckIfNameExists(name))
			{
				CustomProperties.Add(new CustomPropertyInfo(name, type, value, attributes));
			}
		}

		public static void AddProperty<TV>(string name, Func<T, TV> get, Action<T, TV> set = null, List<Attribute> attributes = null,
			string[] properties = null)
		{
			if (!CheckIfNameExists(name))
			{
				CustomProperties.Add(new CustomPropertyInfo<TV>(name, get, set, attributes, properties));
			}
		}

		private static bool CheckIfNameExists(string name)
		{
			if (CustomProperties.Select(p => p.Name).Contains(name) || typeof (T).GetProperties().Select(p => p.Name).Contains(name))
			{
				throw new Exception("The property with this name already exists: " + name);
			}
			return false;
		}

		private static bool ValidateValueType(object value, Type type)
		{
			if (value != null)
			{
				return type.IsInstanceOfType(value);
			}

			if (!type.IsValueType)
			{
				return true;
			}

			return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>));
		}

		public override object GetPropertyValue(string propertyName)
		{
			object customPropertyValue;
			if (_customPropertyValues.TryGetValue(propertyName, out customPropertyValue))
			{
				return customPropertyValue ?? CustomProperties.First(p => p.Name == propertyName).GetDefaultValue(this);
			}
			throw new Exception("There is no property " + propertyName);
		}

		public override void SetPropertyValue(string propertyName, object value)
		{
			var propertyInfo = CustomProperties.FirstOrDefault(prop => prop.Name == propertyName);

			if (propertyInfo == null)
			{
				return;
			}

			object customPropertyValue;

			if (!_customPropertyValues.TryGetValue(propertyName, out customPropertyValue))
			{
				throw new Exception("There is no property " + propertyName);
			}

			if (ValidateValueType(value, propertyInfo.PropertyType))
			{
				if (customPropertyValue == value)
				{
					return;
				}

				_customPropertyValues[propertyName] = value;

				OnPropertyChanged(propertyName);
			}
			else
			{
				throw new Exception("Value is of the wrong type or null for a non-nullable type.");
			}
		}

		public PropertyInfo[] GetProperties()
		{
			return GetCustomType().GetProperties();
		}

		[NotifyPropertyChangedInvocator]
		protected virtual bool SetProperty<TV>(ref TV storage, TV value, [CallerMemberName] string propertyName = null)
		{
			if (Equals(storage, value))
			{
				return false;
			}

			storage = value;

			if (propertyName != null)
			{
				OnPropertyChanged(propertyName);
			}

			return true;
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;

			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(propertyName));

				foreach (var dependantCustomPropertyInfo in
					CustomProperties.OfType<IDependantCustomPropertyInfo>().Where(dcpi => dcpi.Properties.Contains(propertyName)))
				{
					handler(this, new PropertyChangedEventArgs(dependantCustomPropertyInfo.Name));
				}
			}
		}

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged<TV>(Expression<Func<TV>> propertyExpression)
		{
			var propertyName = PropertySupport.ExtractPropertyName(propertyExpression);

			if (propertyName != null)
			{
				OnPropertyChanged(propertyName);
			}
		}

		internal class CustomPropertyInfo : PropertyInfo
		{
			private readonly List<Attribute> _attributes;
			private readonly object _defaultValue;
			private readonly string _name;
			private readonly Type _type;

			protected CustomPropertyInfo(string name, Type type, List<Attribute> attributes = null)
			{
				_name = name;
				_type = type;
				_attributes = attributes;
			}

			public CustomPropertyInfo(string name, Type type, object defaultValue = null, List<Attribute> attributes = null)
				: this(name, type, attributes)
			{
				_defaultValue = defaultValue;
			}

			public override PropertyAttributes Attributes
			{
				get { throw new NotImplementedException(); }
			}

			public override bool CanRead
			{
				get { return true; }
			}

			public override bool CanWrite
			{
				get { return true; }
			}

			public override Type PropertyType
			{
				get { return _type; }
			}

			public override Type DeclaringType
			{
				get { throw new NotImplementedException(); }
			}

			public override string Name
			{
				get { return _name; }
			}

			public override Type ReflectedType
			{
				get { throw new NotImplementedException(); }
			}

			public virtual object GetDefaultValue(DynamicBaseType entity)
			{
				return _defaultValue;
			}

			public override MethodInfo[] GetAccessors(bool nonPublic)
			{
				throw new NotImplementedException();
			}

			public override MethodInfo GetGetMethod(bool nonPublic)
			{
				throw new NotImplementedException();
			}

			public override ParameterInfo[] GetIndexParameters()
			{
				throw new NotImplementedException();
			}

			public override MethodInfo GetSetMethod(bool nonPublic)
			{
				throw new NotImplementedException();
			}

			public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
			{
				return ((DynamicBaseType) obj).GetPropertyValue(_name);
			}

			public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
			{
				((DynamicBaseType) obj).SetPropertyValue(_name, value);
			}

			public override object[] GetCustomAttributes(Type attributeType, bool inherit)
			{
				var attributes = _attributes.Where(a => a.GetType() == attributeType);

				return _attributes == null ? new object[0] : attributes.ToArray();
			}

			public override object[] GetCustomAttributes(bool inherit)
			{
				return _attributes == null ? new object[0] : _attributes.ToArray();
			}

			public override bool IsDefined(Type attributeType, bool inherit)
			{
				throw new NotImplementedException();
			}
		}

		internal class CustomPropertyInfo<TV> : CustomPropertyInfo, IDependantCustomPropertyInfo
		{
			private readonly Func<T, TV> _get;
			private readonly string[] _properties;
			private readonly Action<T, TV> _set;

			public CustomPropertyInfo(string name, Func<T, TV> get, Action<T, TV> set = null, List<Attribute> attributes = null,
				string[] properties = null)
				: base(name, typeof (TV), attributes)
			{
				_get = get;
				_set = set;
				_properties = properties;
			}

			public override bool CanWrite
			{
				get { return _set != null; }
			}

			public string[] Properties
			{
				get { return _properties; }
			}

			public override object GetDefaultValue(DynamicBaseType entity)
			{
				return _get((T) entity);
			}

			public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
			{
				return _get((T) obj);
			}

			public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
			{
				if (_set == null)
				{
					throw new InvalidOperationException();
				}
				_set((T) obj, (TV) value);
			}
		}

		internal class CustomType : Type
		{
			private readonly Type _baseType;

			public CustomType(Type delegatingType)
			{
				_baseType = delegatingType;
			}

			public override Assembly Assembly
			{
				get { return _baseType.Assembly; }
			}

			public override string AssemblyQualifiedName
			{
				get { return _baseType.AssemblyQualifiedName; }
			}

			public override Type BaseType
			{
				get { return _baseType.BaseType; }
			}

			public override string FullName
			{
				get { return _baseType.FullName; }
			}

			public override Guid GUID
			{
				get { return _baseType.GUID; }
			}

			public override Module Module
			{
				get { return _baseType.Module; }
			}

			public override string Namespace
			{
				get { return _baseType.Namespace; }
			}

			public override Type UnderlyingSystemType
			{
				get { return _baseType.UnderlyingSystemType; }
			}

			public override string Name
			{
				get { return _baseType.Name; }
			}

			protected override TypeAttributes GetAttributeFlagsImpl()
			{
				throw new NotImplementedException();
			}

			protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder, CallingConventions callConvention,
				Type[] types, ParameterModifier[] modifiers)
			{
				throw new NotImplementedException();
			}

			public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
			{
				return _baseType.GetConstructors(bindingAttr);
			}

			public override Type GetElementType()
			{
				return _baseType.GetElementType();
			}

			public override EventInfo GetEvent(string name, BindingFlags bindingAttr)
			{
				return _baseType.GetEvent(name, bindingAttr);
			}

			public override EventInfo[] GetEvents(BindingFlags bindingAttr)
			{
				return _baseType.GetEvents(bindingAttr);
			}

			public override FieldInfo GetField(string name, BindingFlags bindingAttr)
			{
				return _baseType.GetField(name, bindingAttr);
			}

			public override FieldInfo[] GetFields(BindingFlags bindingAttr)
			{
				return _baseType.GetFields(bindingAttr);
			}

			public override Type GetInterface(string name, bool ignoreCase)
			{
				return _baseType.GetInterface(name, ignoreCase);
			}

			public override Type[] GetInterfaces()
			{
				return _baseType.GetInterfaces();
			}

			public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
			{
				return _baseType.GetMembers(bindingAttr);
			}

			protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention,
				Type[] types, ParameterModifier[] modifiers)
			{
				throw new NotImplementedException();
			}

			public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
			{
				return _baseType.GetMethods(bindingAttr);
			}

			public override Type GetNestedType(string name, BindingFlags bindingAttr)
			{
				return _baseType.GetNestedType(name, bindingAttr);
			}

			public override Type[] GetNestedTypes(BindingFlags bindingAttr)
			{
				return _baseType.GetNestedTypes(bindingAttr);
			}

			public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
			{
				var clrProperties = _baseType.GetProperties(bindingAttr);

				return clrProperties.Concat(CustomProperties).ToArray();
			}

			protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types,
				ParameterModifier[] modifiers)
			{
				return GetProperties(bindingAttr).FirstOrDefault(prop => prop.Name == name) ??
					   CustomProperties.FirstOrDefault(prop => prop.Name == name);
			}

			protected override bool HasElementTypeImpl()
			{
				throw new NotImplementedException();
			}

			public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args,
				ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
			{
				return _baseType.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);
			}

			protected override bool IsArrayImpl()
			{
				throw new NotImplementedException();
			}

			protected override bool IsByRefImpl()
			{
				throw new NotImplementedException();
			}

			protected override bool IsCOMObjectImpl()
			{
				throw new NotImplementedException();
			}

			protected override bool IsPointerImpl()
			{
				throw new NotImplementedException();
			}

			protected override bool IsPrimitiveImpl()
			{
				return _baseType.IsPrimitive;
			}

			public override object[] GetCustomAttributes(Type attributeType, bool inherit)
			{
				return _baseType.GetCustomAttributes(attributeType, inherit);
			}

			public override object[] GetCustomAttributes(bool inherit)
			{
				return _baseType.GetCustomAttributes(inherit);
			}

			public override bool IsDefined(Type attributeType, bool inherit)
			{
				return _baseType.IsDefined(attributeType, inherit);
			}
		}

		internal interface IDependantCustomPropertyInfo
		{
			string Name { get; }

			string[] Properties { get; }
		}
	}
}
