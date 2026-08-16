using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LegendaryExplorerCore.Coalesced
{
	public partial class CoalesceSettings
	{
		[GeneratedRegex(@"\s+")]
		private static partial Regex WhitespaceRegex { get; }

		public CoalesceSettings(IEnumerable<int> compileTypes = null, int overrideCompileValueTypes = -1)
		{
			CompileTypes = compileTypes ?? new []{0, 1, 2, 3, 4};
			OverrideCompileValueTypes = overrideCompileValueTypes;
		}

		public IEnumerable<int> CompileTypes { get; set; }

		public int OverrideCompileValueTypes { get; set; }

		public static CoalesceSettings FromDictionary(Dictionary<string, string> settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			var result = new CoalesceSettings();

			result.Parse(settings);

			return result;
		}

		public static CoalesceSettings FromXml(IEnumerable<XElement> settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			var result = new CoalesceSettings();

			result.Parse(settings);

			return result;
		}

		public void Parse(Dictionary<string, string> settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			foreach (var setting in settings)
			{
				SetValue(setting.Key, setting.Value);
			}
		}

		public void Parse(IEnumerable<XElement> settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			foreach (var setting in settings)
			{
				if (!setting.Name.LocalName.Equals("Setting"))
				{
					continue;
				}

				if (!setting.HasAttributes)
				{
					continue;
				}

				var name = (string)setting.Attribute("name");

				if (string.IsNullOrEmpty(name))
				{
					continue;
				}

				var value = (string)setting.Attribute("value") ?? setting.Value;
					
				SetValue(name, value);
			}
		}

		public void SetValue(string name, object value)
		{
			ArgumentException.ThrowIfNullOrEmpty(name);
			ArgumentNullException.ThrowIfNull(value);

			switch (name)
			{
				case "CompileTypes":
				{
					var str = (value as string);

					if (str == null)
					{
						return;
					}

					str = WhitespaceRegex.Replace(str, "");

					//CompileTypes = str.Split(',').Select(n => Convert.ToInt32(n)).ToArray();
					CompileTypes = str.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(n => Convert.ToInt32(n)).ToArray();

					break;
				}
				case "OverrideCompileValueTypes":
				{
					OverrideCompileValueTypes = Convert.ToInt32(value);

					break;
				}
			}
		}

		public bool TrySetValue(string name, object value)
		{
			ArgumentException.ThrowIfNullOrEmpty(name);
			ArgumentNullException.ThrowIfNull(value);

			switch (name)
			{
				case "CompileTypes":
				{
					var str = (value as string);

					if (str == null)
					{
						return false;
					}

					str = WhitespaceRegex.Replace(str, "");

					//CompileTypes = str.Split(',').Select(n => Convert.ToInt32(n)).ToArray(); 
					CompileTypes = str.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(n => Convert.ToInt32(n)).ToArray(); 
					
					break;
				}
				case "OverrideCompileValueTypes":
				{
					OverrideCompileValueTypes = Convert.ToInt32(value);

					break;
				}
				default:
				{
					return false;
				}
			}

			return true;
		}
	}
}
