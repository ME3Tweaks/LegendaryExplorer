using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MassEffect.Windows.Presentation.ComplexDataBinding
{
	public class ComplexGroupDataTemplateSelector : DataTemplateSelector
	{
		public const string DefaultTemplateKeyFormat = "data-template[{0}]";
		public const string DefaultGroupTemplateKeyFormat = "IEnumerable[{0}]";

		public const DiscoveryMethods DefaultDiscoveryMethod =
			DiscoveryMethods.Key | DiscoveryMethods.Type | DiscoveryMethods.Interface | DiscoveryMethods.Hierarchy;

		private readonly Dictionary<object, DataTemplate> _cachedDataTemplates;

		#region DiscoveryMethod

		[Flags]
		public enum DiscoveryMethods
		{
			Key = 0x01,
			Type = 0x02,
			Interface = 0x04,
			Hierarchy = 0x08,
			GeneralToSpecific = 0x100,
			FullTypeName = 0x400,
			NoCache = 0x800,
		}

		public DiscoveryMethods DiscoveryMethod { get; set; }

		#endregion

		public ComplexGroupDataTemplateSelector()
		{
			_cachedDataTemplates = new Dictionary<object, DataTemplate>();
			DiscoveryMethod = DefaultDiscoveryMethod;
			TemplateKeyFormat = DefaultTemplateKeyFormat;
			GroupTemplateKeyFormat = DefaultGroupTemplateKeyFormat;
		}

		public string TemplateKeyFormat { get; set; }
		public string GroupTemplateKeyFormat { get; set; }

		private static DataTemplate SelectByKeyGeneralToSpecific(object resourceKey, DependencyObject container)
		{
			var dataTemplate = Application.Current.TryFindResource(resourceKey) as DataTemplate ??
							   Application.Current.MainWindow.TryFindResource(resourceKey) as DataTemplate;

			if (dataTemplate == null)
			{
				foreach (var window in Application.Current.Windows.Cast<Window>().Where(window => window.IsActive))
				{
					dataTemplate = window.TryFindResource(resourceKey) as DataTemplate;

					break;
				}
			}

			if (dataTemplate != null)
			{
				return dataTemplate;
			}

			var element = container as FrameworkElement;

			if (element != null)
			{
				dataTemplate = element.TryFindResource(resourceKey) as DataTemplate;
			}

			return dataTemplate;
		}

		private DataTemplate SelectByKey(object resourceKey, DependencyObject container)
		{
			if ((DiscoveryMethod & DiscoveryMethods.GeneralToSpecific) != 0)
			{
				return SelectByKeyGeneralToSpecific(resourceKey, container);
			}

			var element = container as FrameworkElement;

			if (element != null)
			{
				return element.TryFindResource(resourceKey) as DataTemplate;
			}

			return null;
		}

		private DataTemplate SelectThroughCacheByKey(object templateKey, DependencyObject container)
		{
			DataTemplate dataTemplate;

			if (!_cachedDataTemplates.TryGetValue(templateKey, out dataTemplate))
			{
				dataTemplate = SelectByKey(templateKey, container);

				_cachedDataTemplates.Add(templateKey, dataTemplate ?? NullDataTemplate.Instance);
			}
			else if (dataTemplate is NullDataTemplate)
			{
				return null;
			}

			return dataTemplate;
		}

		private DataTemplate SelectByTypeHierachy(Type type, FrameworkElement container)
		{
			DataTemplate dataTemplate = null;

			while (dataTemplate == null && type != typeof (object))
			{
				var dataTemplateKey = new DataTemplateKey(type);

				dataTemplate = SelectByKey(dataTemplateKey, container);

				if (type != null)
				{
					type = type.BaseType;
				}
			}

			return dataTemplate;
		}

		private DataTemplate SelectByType(Type itemType, FrameworkElement container)
		{
			DataTemplate dataTemplate = null;

			if (container == null)
			{
				return null;
			}

			if ((DiscoveryMethod & DiscoveryMethods.Type) == DiscoveryMethods.Type)
			{
				var dataTemplateKey = new DataTemplateKey(itemType);
				dataTemplate = SelectByKey(dataTemplateKey, container);
			}

			if (dataTemplate == null)
			{
				if ((DiscoveryMethod & DiscoveryMethods.Interface) == DiscoveryMethods.Interface)
				{
					var interfaces = itemType.GetInterfaces();

					for (var i = interfaces.Length - 1; i >= 0; i--)
					{
						var interfaceType = interfaces[i];
						var dataTemplateKey = new DataTemplateKey(interfaceType);
						dataTemplate = SelectByKey(dataTemplateKey, container);
						if (dataTemplate != null)
						{
							break;
						}
					}
				}
			}

			if (dataTemplate != null)
			{
				return dataTemplate;
			}

			if ((DiscoveryMethod & DiscoveryMethods.Hierarchy) == DiscoveryMethods.Hierarchy)
			{
				dataTemplate = SelectByTypeHierachy(itemType.BaseType, container);
			}

			return dataTemplate;
		}

		private DataTemplate SelectThroughCacheByType(Type itemType, FrameworkElement container)
		{
			DataTemplate dataTemplate;
			var dataTemplateKey = new DataTemplateKey(itemType);

			if (!_cachedDataTemplates.TryGetValue(dataTemplateKey, out dataTemplate))
			{
				dataTemplate = SelectByType(itemType, container);

				_cachedDataTemplates.Add(dataTemplateKey, dataTemplate ?? NullDataTemplate.Instance);
			}
			else if (dataTemplate is NullDataTemplate)
			{
				return null;
			}

			return dataTemplate;
		}

		private string GetTypeNameForKey(Type type)
		{
			return (DiscoveryMethod & DiscoveryMethods.FullTypeName) != 0 ? type.FullName : type.Name;
		}

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			string templateKey = null;
			DataTemplate dataTemplate = null;

			if (item == null || container == null)
			{
				return null;
			}

			try
			{
				if ((DiscoveryMethod & DiscoveryMethods.Key) == DiscoveryMethods.Key)
				{
					if (item is IBindingGroup)
					{
						var bindingGroup = item as IBindingGroup;
						var elementType = bindingGroup.ElementType;

						if (elementType != null)
						{
							templateKey = bindingGroup.Parameter ?? string.Format(GroupTemplateKeyFormat, GetTypeNameForKey(elementType));

							dataTemplate = (DiscoveryMethod & DiscoveryMethods.NoCache) != 0
								? SelectByKey(templateKey, container)
								: SelectThroughCacheByKey(templateKey, container);
						}
					}
					else if (item is IEnumerable)
					{
						var elementType = BindingGroup.GetElementType(item as IEnumerable);

						if (elementType != null)
						{
							templateKey = string.Format(GroupTemplateKeyFormat, GetTypeNameForKey(elementType));

							dataTemplate = (DiscoveryMethod & DiscoveryMethods.NoCache) != 0
								? SelectByKey(templateKey, container)
								: SelectThroughCacheByKey(templateKey, container);
						}
					}
					else
					{
						templateKey = string.Format(TemplateKeyFormat, GetTypeNameForKey(item.GetType()));

						dataTemplate = (DiscoveryMethod & DiscoveryMethods.NoCache) != 0
							? SelectByKey(templateKey, container)
							: SelectThroughCacheByKey(templateKey, container);
					}
				}

				if (dataTemplate == null)
				{
					if ((DiscoveryMethod & (DiscoveryMethods.Type | DiscoveryMethods.Interface | DiscoveryMethods.Hierarchy)) != 0)
					{
						dataTemplate = (DiscoveryMethod & DiscoveryMethods.NoCache) != 0
							? SelectByType(item.GetType(), container as FrameworkElement)
							: SelectThroughCacheByType(item.GetType(), container as FrameworkElement);
					}
				}
#if DEBUG
				if (dataTemplate == null)
				{
					var debugMessage = "DataTemplate not found for";

					if ((DiscoveryMethod & DiscoveryMethods.Key) != 0)
					{
						debugMessage += " Resource Key \"" + templateKey + "\"";
					}

					if ((DiscoveryMethod & (DiscoveryMethods.Type | DiscoveryMethods.Interface | DiscoveryMethods.Hierarchy)) != 0)
					{
						if ((DiscoveryMethod & DiscoveryMethods.Key) != 0)
						{
							debugMessage += " or";
						}
						debugMessage += " DataType \"" + item.GetType().FullName + "\"";
					}

					Debug.WriteLine(debugMessage, GetType().Name);
				}
#endif
			}
			catch
			{
				dataTemplate = base.SelectTemplate(item, container);
			}

			return dataTemplate;
		}

		private class NullDataTemplate : DataTemplate
		{
			public static readonly NullDataTemplate Instance = new NullDataTemplate();

			private NullDataTemplate() {}
		}
	}
}
