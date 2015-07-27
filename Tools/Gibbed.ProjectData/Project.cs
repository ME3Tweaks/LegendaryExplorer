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
using System.Collections.Generic;
using System.IO;
using System.Xml.XPath;
using Microsoft.Win32;

namespace Gibbed.ProjectData
{
    public sealed class Project
    {
        public string Name { get; private set; }
        public bool Hidden { get; private set; }
        public string InstallPath { get; private set; }
        public string ListsPath { get; private set; }
        
        internal List<string> Dependencies { get; private set; }
        internal Dictionary<string, string> Settings { get; private set; }
        internal Manager Manager;

        private Project()
        {
            this.Dependencies = new List<string>();
            this.Settings = new Dictionary<string, string>();
        }

        internal static Project Create(string path, Manager manager)
        {
            var project = new Project { Manager = manager };

            var doc = new XPathDocument(path);
            var nav = doc.CreateNavigator();

            project.Name = nav.SelectSingleNode("/project/name").Value;
            project.ListsPath = nav.SelectSingleNode("/project/list_location").Value;

            project.Hidden = nav.SelectSingleNode("/project/hidden") != null;

            if (Path.IsPathRooted(project.ListsPath) == false)
            {
                project.ListsPath = Path.Combine(Path.GetDirectoryName(path), project.ListsPath);
            }

            project.Dependencies.Clear();
            var dependencies = nav.Select("/project/dependencies/dependency");
            while (dependencies.MoveNext() == true)
            {
                project.Dependencies.Add(dependencies.Current.Value);
            }

            project.Settings.Clear();
            var settings = nav.Select("/project/settings/setting");
            while (settings.MoveNext() == true)
            {
                var name = settings.Current.GetAttribute("name", "");
                var value = settings.Current.Value;

                if (string.IsNullOrWhiteSpace(name) == true)
                {
                    throw new InvalidOperationException("setting name cannot be empty");
                }

                project.Settings[name.ToLowerInvariant()] = value;
            }

            project.InstallPath = null;
            var locations = nav.Select("/project/install_locations/install_location");
            while (locations.MoveNext() == true)
            {
                bool failed = true;

                var actions = locations.Current.Select("action");
                string locationPath = null;
                while (actions.MoveNext() == true)
                {
                    string type = actions.Current.GetAttribute("type", "");

                    switch (type)
                    {
                        case "registry":
                        {
                            string key = actions.Current.GetAttribute("key", "");
                            string value = actions.Current.GetAttribute("value", "");

                            path = (string)Registry.GetValue(key, value, null);

                            if (path != null) // && Directory.Exists(path) == true)
                            {
                                locationPath = path;
                                failed = false;
                            }

                            break;
                        }

                        case "path":
                        {
                            locationPath = actions.Current.Value;

                            if (Directory.Exists(locationPath) == true)
                            {
                                failed = false;
                            }

                            break;
                        }

                        case "combine":
                        {
                            locationPath = Path.Combine(locationPath, actions.Current.Value);

                            if (Directory.Exists(locationPath) == true)
                            {
                                failed = false;
                            }

                            break;
                        }

                        case "directory_name":
                        {
                            locationPath = Path.GetDirectoryName(locationPath);

                            if (Directory.Exists(locationPath) == true)
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

                    if (failed == true)
                    {
                        break;
                    }
                }

                if (failed == false && Directory.Exists(locationPath) == true)
                {
                    project.InstallPath = locationPath;
                    break;
                }
            }

            return project;
        }

        public override string ToString()
        {
            return this.Name;
        }

        public TType GetSetting<TType>(string name, TType defaultValue)
            where TType: struct
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            name = name.ToLowerInvariant();
            if (this.Settings.ContainsKey(name) == false)
            {
                return defaultValue;
            }

            var type = typeof(TType);

            if (type.IsEnum == true)
            {
                TType result;
                if (Enum.TryParse(this.Settings[name], out result) == false)
                {
                    throw new ArgumentException("bad enum value", "name");
                }

                return result;
            }

            return (TType)Convert.ChangeType(this.Settings[name], type);
        }

        #region LoadLists
        public HashList<TType> LoadLists<TType>(
            string filter,
            Func<string, TType> hasher,
            Func<string, string> modifier)
        {
            var list = new HashList<TType>();

            foreach (var name in this.Dependencies)
            {
                var dependency = this.Manager[name];
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
                this.ListsPath,
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

            foreach (string listPath in Directory.GetFiles(basePath, filter, SearchOption.AllDirectories))
            {
                using (var input = File.Open(listPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var reader = new StreamReader(input);

                    while (true)
                    {
                        string line = reader.ReadLine();
                        if (line == null)
                        {
                            break;
                        }

                        if (line.StartsWith(";") == true)
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

                        TType hash = hasher(line);

                        if (list.Lookup.ContainsKey(hash) == true &&
                            list.Lookup[hash] != line)
                        {
                            string otherLine = list.Lookup[hash];
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
