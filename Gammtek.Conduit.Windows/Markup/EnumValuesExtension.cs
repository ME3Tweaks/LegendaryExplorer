using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Markup;

namespace Gammtek.Conduit.Windows.Markup
{
	public class EnumValuesExtension : MarkupExtension
	{
		private readonly Dictionary<Type, List<EnumDisplayValue>> _cache = new Dictionary<Type, List<EnumDisplayValue>>();

		public EnumValuesExtension(Type type)
		{
			TargetEnum = type;
		}

		public Type TargetEnum { get; set; }

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			var type = TargetEnum;

			return type != null
				? RetrieveFromCacheOrAddIt(type)
				: null;
		}

		private object RetrieveFromCacheOrAddIt(Type type)
		{
			List<EnumDisplayValue> result;

			if (_cache.TryGetValue(type, out result))
			{
				return result;
			}

			var fields = type.GetFields().Where(field => field.IsLiteral);
			var values = new List<EnumDisplayValue>();

			foreach (var field in fields)
			{
				var a = (DisplayAttribute[]) field.GetCustomAttributes(typeof (DisplayAttribute), false);
				var valueOfField = field.GetValue(type);

				if (a.Length > 0)
				{
					values.Add(new EnumDisplayValue(valueOfField, a[0].GetName()));
				}
				else
				{
					values.Add(new EnumDisplayValue(valueOfField, valueOfField));
				}
			}

			_cache[type] = values;

			return _cache[type];
		}
	}
}
