using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MassEffect3.ProjectData
{
	public class Manager : IEnumerable<Project>
	{
		private readonly List<Project> _Projects = new List<Project>();
		private Project _ActiveProject;
		private string _ProjectPath;

		private Manager()
		{}

		public Project ActiveProject
		{
			get { return _ActiveProject; }

			set
			{
				if (value == null)
				{
					File.Delete(Path.Combine(_ProjectPath, "current.txt"));
				}
				else
				{
					using (var output = File.Create(Path.Combine(_ProjectPath, "current.txt")))
					{
						using (var writer = new StreamWriter(output))
						{
							writer.WriteLine(value.Name);
						}
					}
				}

				_ActiveProject = value;
			}
		}

		public Project this[string name]
		{
			get
			{
				return _Projects.SingleOrDefault(
					p => p.Name.ToLowerInvariant() == name.ToLowerInvariant());
			}
		}

		public IEnumerator<Project> GetEnumerator()
		{
			return _Projects.Where(
				p =>
					p.Hidden == false &&
					p.InstallPath != null
				).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return _Projects.Where(
				p =>
					p.Hidden == false &&
					p.InstallPath != null
				).GetEnumerator();
		}

		public static Manager Load()
		{
			return Load(null);
		}

		public static Manager Load(string currentProject)
		{
			var manager = new Manager();

			string projectPath;
			projectPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			projectPath = Path.Combine(projectPath, "projects");

			manager._ProjectPath = projectPath;

			if (Directory.Exists(projectPath))
			{
				foreach (var xmlPath in Directory.GetFiles(projectPath, "*.xml", SearchOption.TopDirectoryOnly))
				{
					manager._Projects.Add(Project.Create(xmlPath, manager));
				}
			}

			if (currentProject != null)
			{
				manager._ActiveProject = null;

				currentProject = currentProject.Trim();
				if (manager[currentProject] != null)
				{
					manager._ActiveProject = manager[currentProject];
				}
			}
			else
			{
				var currentPath = Path.Combine(projectPath, "current.txt");

				manager._ActiveProject = null;
				if (File.Exists(currentPath))
				{
					using (var input = File.OpenRead(currentPath))
					{
						var reader = new StreamReader(input);

						var name = reader.ReadLine();
						if (name != null)
						{
							name = name.Trim();
							if (manager[name] != null)
							{
								manager._ActiveProject = manager[name];
							}
						}
					}
				}
			}

			return manager;
		}

		public HashList<TType> LoadLists<TType>(
			string filter,
			Func<string, TType> hasher,
			Func<string, string> modifier)
		{
			if (ActiveProject == null)
			{
				return HashList<TType>.Dummy;
			}

			return ActiveProject.LoadLists(filter, hasher, modifier);
		}

		public TType GetSetting<TType>(string name, TType defaultValue)
			where TType : struct
		{
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}

			if (ActiveProject == null)
			{
				return defaultValue;
			}

			return ActiveProject.GetSetting(name, defaultValue);
		}
	}
}