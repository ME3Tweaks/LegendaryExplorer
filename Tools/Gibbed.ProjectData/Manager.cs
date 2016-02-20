/* Copyright (c) 2011 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Gibbed.ProjectData
{
    public class Manager : IEnumerable<Project>
    {
        private Manager()
        {
        }

        private string _ProjectPath;

        private readonly List<Project> _Projects = new List<Project>();
        private Project _ActiveProject;
        public Project ActiveProject
        {
            get
            {
                return this._ActiveProject;
            }

            set
            {
                if (value == null)
                {
                    File.Delete(Path.Combine(this._ProjectPath, "current.txt"));
                }
                else
                {
                    using (var output = File.Create(Path.Combine(this._ProjectPath, "current.txt")))
                    {
                        using (var writer = new StreamWriter(output))
                        {
                            writer.WriteLine(value.Name);
                        }
                    }
                }

                this._ActiveProject = value;
            }
        }

        public Project this[string name]
        {
            get
            {
                return this._Projects.SingleOrDefault(
                    p => p.Name.ToLowerInvariant() == name.ToLowerInvariant());
            }
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

            if (Directory.Exists(projectPath) == true)
            {
                foreach (string xmlPath in Directory.GetFiles(projectPath, "*.xml", SearchOption.TopDirectoryOnly))
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
                if (File.Exists(currentPath) == true)
                {
                    using (var input = File.OpenRead(currentPath))
                    {
                        var reader = new StreamReader(input);

                        string name = reader.ReadLine();
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

        public IEnumerator<Project> GetEnumerator()
        {
            return this._Projects.Where(
                    p =>
                        p.Hidden == false &&
                        p.InstallPath != null
                ).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this._Projects.Where(
                    p =>
                        p.Hidden == false &&
                        p.InstallPath != null
                ).GetEnumerator();
        }

        public HashList<TType> LoadLists<TType>(
            string filter,
            Func<string, TType> hasher,
            Func<string, string> modifier)
        {
            if (this.ActiveProject == null)
            {
                return HashList<TType>.Dummy;
            }

            return this.ActiveProject.LoadLists(filter, hasher, modifier);
        }

        public TType GetSetting<TType>(string name, TType defaultValue)
            where TType: struct
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            if (this.ActiveProject == null)
            {
                return defaultValue;
            }

            return this.ActiveProject.GetSetting(name, defaultValue);
        }
    }
}
