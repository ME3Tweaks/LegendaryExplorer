using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Xml.XPath;
using Microsoft.Win32;

namespace MassEffect3.ProjectData
{
	public sealed class Project
	{
		internal Manager Manager;

		private Project()
		{
			Dependencies = new List<string>();
			Settings = new Dictionary<string, string>();
		}

		public string Name { get; private set; }
		public bool Hidden { get; private set; }
		public string InstallPath { get; private set; }
		public string ListsPath { get; private set; }

		internal List<string> Dependencies { get; private set; }
		internal Dictionary<string, string> Settings { get; private set; }

		internal static Project Create(string path, Manager manager)
		{
			path = Path.GetFullPath(path);
			if (path == null)
			{
				throw new InvalidOperationException();
			}

			var dir = Path.GetDirectoryName(path);
			if (dir == null)
			{
				throw new InvalidOperationException();
			}

			var project = new Project
			{
				Manager = manager
			};

			var doc = new XPathDocument(path);
			var nav = doc.CreateNavigator();

			var projectNameNode = nav.SelectSingleNode("/project/name");
			if (projectNameNode == null)
			{
				throw new InvalidOperationException();
			}
			project.Name = projectNameNode.Value;

			var listsPathNode = nav.SelectSingleNode("/project/list_location");
			if (listsPathNode == null)
			{
				throw new InvalidOperationException();
			}
			project.ListsPath = listsPathNode.Value;

			project.Hidden = nav.SelectSingleNode("/project/hidden") != null;

			if (Path.IsPathRooted(project.ListsPath) == false)
			{
				project.ListsPath = Path.Combine(dir, project.ListsPath);
			}

			project.Dependencies.Clear();
			var dependencies = nav.Select("/project/dependencies/dependency");
			while (dependencies.MoveNext() &&
					dependencies.Current != null)
			{
				project.Dependencies.Add(dependencies.Current.Value);
			}

			project.Settings.Clear();
			var settings = nav.Select("/project/settings/setting");
			while (settings.MoveNext() &&
					settings.Current != null)
			{
				var name = settings.Current.GetAttribute("name", "");
				var value = settings.Current.Value;

				if (string.IsNullOrWhiteSpace(name))
				{
					throw new InvalidOperationException("setting name cannot be empty");
				}

				project.Settings[name.ToLowerInvariant()] = value;
			}

			project.InstallPath = null;
			var locations = nav.Select("/project/install_locations/install_location");
			while (locations.MoveNext() &&
					locations.Current != null)
			{
				var failed = true;

				var actions = locations.Current.Select("action");
				string locationPath = null;
				while (actions.MoveNext() &&
						actions.Current != null)
				{
					var type = actions.Current.GetAttribute("type", "");

					switch (type)
					{
						case "registry":
						{
							var keyName = actions.Current.GetAttribute("key", "");
							var valueName = actions.Current.GetAttribute("value", "");

							try
							{
								var value = (string) Registry.GetValue(keyName, valueName, null);
								if (value != null) // && Directory.Exists(path) == true)
								{
									locationPath = value;
									failed = false;
								}
							}
							catch (SecurityException)
							{
								failed = true;
								throw;
							}

							break;
						}

						case "registryview":
						{
							RegistryView view;
							if (Enum.TryParse(actions.Current.GetAttribute("view", ""), out view) == false)
							{
								throw new InvalidOperationException();
							}

							RegistryHive hive;
							if (Enum.TryParse(actions.Current.GetAttribute("hive", ""), out hive) == false)
							{
								throw new InvalidOperationException();
							}

							try
							{
								var localKey = RegistryKey.OpenBaseKey(hive, view);
								//if (localKey != null)
								{
									var keyName = actions.Current.GetAttribute("subkey", "");
									localKey = localKey.OpenSubKey(keyName);
									if (localKey != null)
									{
										var valueName = actions.Current.GetAttribute("value", "");
										var value = (string) localKey.GetValue(valueName, null);
										if (string.IsNullOrEmpty(value) == false)
										{
											locationPath = value;
											failed = false;
										}
									}
								}
							}
							catch (SecurityException)
							{
								failed = true;
							}

							break;
						}

						case "path":
						{
							locationPath = actions.Current.Value;

							if (Directory.Exists(locationPath))
							{
								failed = false;
							}

							break;
						}

						case "combine":
						{
							locationPath = Path.Combine(locationPath, actions.Current.Value);

							if (Directory.Exists(locationPath))
							{
								failed = false;
							}

							break;
						}

						case "directory_name":
						{
							locationPath = Path.GetDirectoryName(locationPath);

							if (Directory.Exists(locationPath))
							{
								failed = false;
							}

							break;
						}

						case "fix":
						{
							locationPath = locationPath.Replace('/', '\\');
							failed = false;
							break;
						}

						default:
						{
							throw new InvalidOperationException("unhandled install location action type");
						}
					}

					if (failed)
					{
						break;
					}
				}

				if (failed == false && Directory.Exists(locationPath))
				{
					project.InstallPath = locationPath;
					break;
				}
			}

			return project;
		}

		public override string ToString()
		{
			return Name;
		}

		public TType GetSetting<TType>(string name, TType defaultValue)
			where TType : struct
		{
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}

			name = name.ToLowerInvariant();
			if (Settings.ContainsKey(name) == false)
			{
				return defaultValue;
			}

			var type = typeof (TType);

			if (type.IsEnum)
			{
				TType result;
				if (Enum.TryParse(Settings[name], out result) == false)
				{
					throw new ArgumentException("bad enum value", "name");
				}

				return result;
			}

			return (TType) Convert.ChangeType(Settings[name], type);
		}

		#region LoadLists

		public HashList<TType> LoadLists<TType>(
			string filter,
			Func<string, TType> hasher,
			Func<string, string> modifier)
		{
			var list = new HashList<TType>();

			foreach (var name in Dependencies)
			{
				var dependency = Manager[name];
				if (dependency != null)
				{
					LoadListsFrom(
						dependency.ListsPath,
						filter,
						hasher,
						modifier,
						list);
				}
			}

			LoadListsFrom(
				ListsPath,
				filter,
				hasher,
				modifier,
				list);

			return list;
		}

		#endregion

		#region LoadListsFrom

		private static void LoadListsFrom<TType>(
			string basePath,
			string filter,
			Func<string, TType> hasher,
			Func<string, string> modifier,
			HashList<TType> list)
		{
			if (Directory.Exists(basePath) == false)
			{
				return;
			}

			foreach (var listPath in Directory.GetFiles(basePath, filter, SearchOption.AllDirectories))
			{
				using (var input = File.Open(listPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					var reader = new StreamReader(input);

					while (true)
					{
						var line = reader.ReadLine();
						if (line == null)
						{
							break;
						}

						if (line.StartsWith(";"))
						{
							continue;
						}

						line = line.Trim();
						if (line.Length <= 0)
						{
							continue;
						}

						if (modifier != null)
						{
							line = modifier(line);
						}

						var hash = hasher(line);

						if (list.Lookup.ContainsKey(hash) &&
							list.Lookup[hash] != line)
						{
							var otherLine = list.Lookup[hash];
							throw new InvalidOperationException(
								string.Format(
									"hash collision ('{0}' vs '{1}')",
									line,
									otherLine));
						}

						list.Lookup[hash] = line;
					}
				}
			}
		}

		#endregion
	}
}