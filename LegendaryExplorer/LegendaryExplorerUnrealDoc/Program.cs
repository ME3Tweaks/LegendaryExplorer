using System;
using System.Text;
using CommandLine;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Documentation;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerUnrealDoc
{
    internal class Program
    {
        // For compiling docs.
        static int Main(string[] args)
        {
            bool success = false;
            Parser.Default.ParseArguments<CLIOptions>(args)
                .WithParsed<CLIOptions>(o =>
                {
#if DEBUG
                    // Test only.
                    //Console.WriteLine("Generating dbs...");
                    //LegendaryExplorerUnrealDoc.debug.Testing.GenerateBlankDocuDBs();
#endif
                    BuildDocs(o);
                    Console.WriteLine("Documentation built.");
                    success = true;
                })
                .WithNotParsed(x =>
                {
                    Console.WriteLine("Invalid arguments specified.");
                });

            if (success)
            {
                return 0;
            }

            return -1;
        }

        static void BuildDocs(CLIOptions options)
        {
            LoadTemplates();

            var inputDirs = Directory.GetDirectories(options.InputFolder);
            var games = new List<MEGame>();
            foreach (var inputDir in inputDirs)
            {
                if (Enum.TryParse<MEGame>(Path.GetFileName(inputDir), out var game))
                {
                    Console.WriteLine($"Detected documentation source for {game}");
                    games.Add(game);
                }
            }

            foreach (var game in games)
            {
                Console.WriteLine($"Generating documentation for {game}");
                var htmlPath = Path.Combine(options.OutputFolder, game.ToString());
                Directory.CreateDirectory(Path.Combine(htmlPath, "classes"));
                Directory.CreateDirectory(Path.Combine(htmlPath, "enums"));
                Directory.CreateDirectory(Path.Combine(htmlPath, "structs"));

                // Clear directory
                Console.WriteLine($"\tRemoving existing files");
                foreach (var f in Directory.GetFiles(htmlPath, "*", SearchOption.AllDirectories))
                {
                    File.Delete(f);
                }

                Console.WriteLine($"\tLoading documentation files");
                // Load source DocuDB.
                DocuDB db = DocuDB.LoadDocuDB(game, Path.Combine(options.InputFolder, game.ToString()));

                Console.WriteLine($"\tGenerating class html");
                foreach (var classD in db.ClassDocumentation)
                {
                    OutputClassDocumentation(htmlPath, db, classD);
                }

                GenerateIndexPage(htmlPath, db, "classes");

                Console.WriteLine($"\tGenerating enum html");
                foreach (var enumD in db.EnumDocumentation)
                {
                    OutputEnumDocumentation(htmlPath, db, enumD);
                }
                GenerateIndexPage(htmlPath, db, "enums");

                Console.WriteLine($"\tGenerating struct html");
                foreach (var structD in db.StructDocumentation)
                {
                    OutputStructDocumentation(htmlPath, db, structD);
                }
                GenerateIndexPage(htmlPath, db, "structs");

                Console.WriteLine($"\tGenerating packages html");
                GeneratePackagesPage(htmlPath, db);

                Console.WriteLine($"\tGenerating game homepage html");
                GenerateGameHomepage(htmlPath, db);

                // Binary output.
                Console.WriteLine($"\tSerializing Binary DocuDB");
                Directory.CreateDirectory(options.BinaryOutputFolder);
                var outPath = Path.Combine(options.BinaryOutputFolder, $"DocuDB_{game}.bin");
                var binary = BinaryDocuDB.Serialize(db);
                binary.WriteToFile(outPath);
            }

            // Shared content



            // Copy files.

            // CSS
            File.Copy(Path.Combine(AppContext.BaseDirectory, "css", "lexdoc.css"), Path.Combine(options.OutputFolder, "lexdoc.css"), true);

            // Images
            var imagesPath = Path.Combine(AppContext.BaseDirectory, "images", "gameicons");
            var outIconsPath = Directory.CreateDirectory(Path.Combine(options.OutputFolder, "images", "gameicons")).FullName;
            foreach (var img in Directory.GetFiles(imagesPath))
            {
                File.Copy(img, Path.Combine(outIconsPath, Path.GetFileName(img)), true);
            }

            File.Copy(Path.Combine(AppContext.BaseDirectory, "images", "home.jpg"), Path.Combine(outIconsPath, Path.Combine(options.OutputFolder, "images", "home.jpg")), true);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "images", "docudb.png"), Path.Combine(outIconsPath, Path.Combine(options.OutputFolder, "images", "docudb.png")), true);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "images", "favicon.ico"), Path.Combine(outIconsPath, Path.Combine(options.OutputFolder, "images", "favicon.ico")), true);

            OutputHomePage(options.OutputFolder);
        }

        private static void GenerateGameHomepage(string htmlPath, DocuDB db)
        {
            var outputPath = Path.Combine(htmlPath, "index.html");

            var html = HTML_TEMPLATE;
            var index = GAME_HOME_TEMPLATE;

            html = html.Replace("%LD_CONTENT%", index);
            html = html.Replace("%LD_RELPATH%", "");
            html = html.Replace("%LD_GAME%", db.Game.ToString());
            html = html.Replace("%LD_TITLE%", $"{db.Game} DocuDB");
            html = html.Replace("%LD_DESCRIPTION%", $"Homepage for community documentation for {db.Game} UnrealScript");
            html = html.Replace("%LD_NAV_ITEMS%", "");
            html = html.Replace("%LD_EDIT_BULLET%", "");

            SaveFile(htmlPath, outputPath, html);
        }

        private static void GeneratePackagesPage(string htmlPath, DocuDB db)
        {
            var packagesPath = Path.Combine(htmlPath, "packages");
            Directory.CreateDirectory(packagesPath);

            List<string> packages;
            // Index page
            {
                var html = HTML_TEMPLATE;
                var index = INDEX_TEMPLATE;

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("<ul>");

                // Structs are always in classes, so this will get them all.
                packages = db.ClassDocumentation.Values.Select(x => x.Package).Distinct().OrderBy(x => x).ToList();
                foreach (var package in packages)
                {
                    sb.AppendLine($"<li><a href=\"{package}.html\">{package}</a></href>");
                }

                sb.AppendLine("</ul>");

                index = index.Replace("%LD_LIST%", sb.ToString());
                index = index.Replace("%LD_LISTTYPE%", "Packages");

                html = html.Replace("%LD_CONTENT%", index);
                html = html.Replace("%LD_RELPATH%", "../");
                html = html.Replace("%LD_GAME%", db.Game.ToString());
                html = html.Replace("%LD_TITLE%", "Package list");
                html = html.Replace("%LD_DESCRIPTION%", $"List of packages containing UnrealScript in {db.Game}.");
                html = html.Replace("%LD_NAV_ITEMS%", "");
                html = html.Replace("%LD_EDIT_BULLET%", "");

                var outputPath = Path.Combine(packagesPath, "index.html");
                SaveFile(htmlPath, outputPath, html);

            }
            // Generate each package page
            foreach (var package in packages)
            {
                var html = HTML_TEMPLATE;
                var content = PACKAGE_CONTENT_TEMPLATE;

                {
                    // CLASSES
                    StringBuilder sb = new StringBuilder();
                    var classes = db.ClassDocumentation.Where(x => x.Value.Package == package).OrderBy(x => x.Key).ToList();
                    sb.AppendLine("<ul>");
                    foreach (var cls in classes)
                    {
                        sb.AppendLine($"<li><a href=\"../classes/{cls.Key}.html\">{cls.Key}</a></href></li>");
                    }

                    sb.AppendLine("</ul>");

                    content = content.Replace("%LD_CLASSESLIST%", sb.ToString());
                }

                {
                    // STRUCTS
                    StringBuilder sb = new StringBuilder();
                    var structs = db.StructDocumentation.Where(x => db.ClassDocumentation[x.Value.DefinedInClass].Package == package).OrderBy(x => x.Key).ToList();
                    if (structs.Any())
                    {
                        sb.AppendLine("<ul>");
                        foreach (var stct in structs)
                        {
                            sb.AppendLine($"<li><a href=\"../structs/{stct.Key}.html\">{stct.Key}</a></href></li>");
                        }
                        sb.AppendLine("</ul>");
                    }
                    else
                    {
                        sb.AppendLine("<p>This package does not define any structs.</p>");
                    }

                    content = content.Replace("%LD_STRUCTSLIST%", sb.ToString());

                    content = content.Replace("%LD_PACKAGENAME%", package);
                }

                html = html.Replace("%LD_CONTENT%", content);
                html = html.Replace("%LD_RELPATH%", "../");
                html = html.Replace("%LD_GAME%", db.Game.ToString());
                html = html.Replace("%LD_TITLE%", $"{package} content");
                html = html.Replace("%LD_DESCRIPTION%", $"List of UnrealScript content contained within {package}.");

                // Navigation.
                string navItems = "";
                navItems += "<li><a href=\"#classes\">Classes&nbsp;</a></li>";
                navItems += "<li><a href=\"#structs\">Structs&nbsp;</a></li>";

                html = html.Replace("%LD_NAV_ITEMS%", navItems);
                html = html.Replace("%LD_EDIT_BULLET%", "");

                var outputPath = Path.Combine(packagesPath, $"{package}.html");
                SaveFile(htmlPath, outputPath, html);
            }
        }

        private static void GenerateIndexPage(string htmlPath, DocuDB db, string subpath)
        {
            var inputPath = Path.Combine(htmlPath, subpath);
            var outputPath = Path.Combine(inputPath, "index.html");

            var html = HTML_TEMPLATE;
            var index = INDEX_TEMPLATE;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<ul>");
            switch (subpath)
            {
                case "classes":
                    foreach (var cls in db.ClassDocumentation)
                    {
                        sb.AppendLine($"<li><a data-type=\"class\">{cls.Key}</a></li>");
                    }
                    break;
                case "structs":
                    foreach (var cls in db.StructDocumentation)
                    {
                        sb.AppendLine($"<li><a data-type=\"struct\">{cls.Key}</a></li>");
                    }
                    break;
                case "enums":
                    foreach (var cls in db.EnumDocumentation)
                    {
                        sb.AppendLine($"<li><a data-type=\"enum\">{cls.Key}</a></li>");
                    }
                    break;
            }
            sb.AppendLine("</ul>");

            index = index.Replace("%LD_LIST%", sb.ToString());
            index = index.Replace("%LD_LISTTYPE%", subpath.UpperFirst());


            html = html.Replace("%LD_CONTENT%", index);
            html = html.Replace("%LD_RELPATH%", "../");
            html = html.Replace("%LD_GAME%", db.Game.ToString());
            html = html.Replace("%LD_TITLE%", $"All {subpath.UpperFirst()}");
            html = html.Replace("%LD_DESCRIPTION%", $"List of all {subpath} in {db.Game}.");
            html = html.Replace("%LD_NAV%", $"List of all {subpath} in {db.Game}.");
            html = html.Replace("%LD_NAV_ITEMS%", "");
            html = html.Replace("%LD_EDIT_BULLET%", "");

            SaveFile(htmlPath, outputPath, html);
        }

        private static string HOME_TEMPLATE;
        private static string GAME_HOME_TEMPLATE;
        private static string HTML_TEMPLATE;
        private static string PACKAGE_CONTENT_TEMPLATE;
        private static string INDEX_TEMPLATE;
        private static string CLASS_TEMPLATE;
        private static string FUNCTIONROW_TEMPLATE;
        private static string FUNCTIONSPEC_TEMPLATE;
        private static string ENUM_TEMPLATE;
        private static string ENUMROW_TEMPLATE;
        private static string STRUCT_TEMPLATE;
        private static string MEMBER_TEMPLATE;
        private static string VARIABLECONTAINER_TEMPLATE;
        private static string FUNCTIONCONTAINER_TEMPLATE;

        private static void LoadTemplates()
        {
            PACKAGE_CONTENT_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "PackageContent.lextd"));
            GAME_HOME_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "GameHome.lextd"));
            HOME_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Home.lextd"));
            INDEX_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "index.lextd"));
            HTML_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Html.lextd"));
            CLASS_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Class.lextd"));
            MEMBER_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "MemberRow.lextd"));
            FUNCTIONROW_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "FunctionRow.lextd"));
            FUNCTIONSPEC_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "FunctionSpec.lextd"));
            ENUM_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Enum.lextd"));
            ENUMROW_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "EnumRow.lextd"));
            STRUCT_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "Struct.lextd"));
            VARIABLECONTAINER_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "VariableContainer.lextd"));
            FUNCTIONCONTAINER_TEMPLATE = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "FunctionContainer.lextd"));
        }

        private static void OutputClassDocumentation(string htmlPath, DocuDB db, KeyValuePair<string, DocuClassEntry> classD)
        {
            var outputPath = Path.Combine(htmlPath, "classes", $"{classD.Key}.html");

            var html = HTML_TEMPLATE;


            var content = CLASS_TEMPLATE;

            var inheritanceTree = BuildInheritanceTree(db, classD.Key);

            content = content.Replace("%LD_INHERITANCETREE%", inheritanceTree);
            var modifiers = ConvertModifers(classD.Value.ClassFlags);
            if (classD.Value.ConfigName != null)
            {
                modifiers = modifiers.Replace("Config", $"Config({classD.Value.ConfigName})");
            }
            content = content.Replace("%LD_MODIFIERS%", modifiers);
            content = content.Replace("%LD_CLASSNAME%", classD.Key);

            string navItems = "";
            #region VARIABLES
            if (classD.Value.Variables.Any())
            {
                navItems += "<li><a href=\"#variables\">Variables&nbsp;</a></li>";
                // Has variables
                StringBuilder memberHtml = new StringBuilder();
                string rowClass = "row";
                foreach (var member in classD.Value.Variables)
                {
                    var mrow = MEMBER_TEMPLATE;

                    var memberModifier = "";
                    var flags = (EPropertyFlags)member.Value.Flags;
                    mrow = mrow.Replace("%LD_MEMBERMODIFIER%", string.Join(" ", flags.GetFlags()));

                    string typeText = GetTypeText(db, member.Value.MemberType);
                    mrow = mrow.Replace("%LD_MEMBERTYPE%", typeText);
                    mrow = mrow.Replace("%LD_MEMBERNAME%", member.Key);
                    mrow = mrow.Replace("%LD_MEMBERDOC%", member.Value.MemberDocumentation);

                    // Formatting
                    mrow = mrow.Replace("%LD_ROWCLASS%", $"{rowClass}Color");

                    memberHtml.AppendLine(mrow);

                    if (rowClass == "row")
                    {
                        rowClass = "alt";
                    }
                    else
                    {
                        rowClass = "row";
                    }
                }

                content = content.Replace("%LD_VARIABLECONTAINER%", VARIABLECONTAINER_TEMPLATE.Replace("%LD_MEMBERLIST%", memberHtml.ToString()));
            }
            else
            {
                // No variables
                content = content.Replace("%LD_VARIABLECONTAINER%", "<h3>Variables</h3><p>This class has no defined variables.</p>");
            }
            #endregion

            #region FUNCTIONS
            if (classD.Value.Functions.Any())
            {
                // Has functions
                navItems += "<li><a href=\"#functions\">Functions&nbsp;</a></li>";

                StringBuilder functionHtml = new StringBuilder();
                string rowClass = "row";
                foreach (var member in classD.Value.Functions)
                {
                    var mrow = FUNCTIONROW_TEMPLATE;


                    string returnTypeText = member.Value.ReturnType;
                    if (db.ClassDocumentation.TryGetValue(member.Value.ReturnType, out var ci))
                    {
                        returnTypeText = $"<a href=\"{member.Value.ReturnType}.html\">{member.Value.ReturnType}</a>";
                    }

                    mrow = mrow.Replace("%LD_FUNCTIONRETURNTYPE%", returnTypeText);

                    var sig = member.Value.FunctionSignature;
                    if (member.Value.ReturnType != "None")
                    {
                        // Remove return type from signature
                        sig = sig.Substring(sig.IndexOf(' ') + 1);
                    }

                    var buildingSig = sig[..(sig.IndexOf('(') + 1)];

                    // HTML-ify the params.
                    // This is a hack.
                    var parms = sig[(sig.IndexOf('(') + 1)..^1].Split(',');
                    foreach (var parm in parms)
                    {
                        var parm2 = parm.Trim();
                        // Debug.WriteLine(parm2);
                        var words = parm2.Split(' ');

                        string param = "";
                        foreach (var word in words)
                        {
                            param += " " + GetTypeText(db, word);
                        }

                        buildingSig += param.Trim() + ", ";
                    }

                    buildingSig = buildingSig.TrimEnd(',', ' ');
                    buildingSig += ')';


                    mrow = mrow.Replace("%LD_FUNCTIONSIGNATURE%", buildingSig);
                    mrow = mrow.Replace("%LD_FUNCTIONDOC%", member.Value.MemberDocumentation);

                    // Formatting
                    mrow = mrow.Replace("%LD_ROWCLASS%", $"{rowClass}Color");

                    functionHtml.AppendLine(mrow);

                    if (rowClass == "row")
                    {
                        rowClass = "alt";
                    }
                    else
                    {
                        rowClass = "row";
                    }
                }

                content = content.Replace("%LD_FUNCTIONCONTAINER%", FUNCTIONCONTAINER_TEMPLATE.Replace("%LD_FUNCTIONLIST%", functionHtml.ToString()));
            }
            else
            {
                // No functions
                content = content.Replace("%LD_FUNCTIONCONTAINER%", "<h3>Functions</h3><p>This class has no defined functions.</p>");
            }

            #endregion

            // Install content
            html = html.Replace("%LD_CONTENT%", content);

            // Shared on page
            html = html.Replace("%LD_TITLE%", classD.Key);
            html = html.Replace("%LD_DESCRIPTION%", classD.Value.ClassDocumentation);
            html = html.Replace("%LD_GAME%", db.Game.ToString());

            // We are one folder deep.
            html = html.Replace("%LD_RELPATH%", "../");

            // Navigation links
            html = html.Replace("%LD_NAV_ITEMS%", navItems);

            // Edit this page
            var editLi = $"<li id=\"update_page_bullet\" title=\"Edit this documentation\"><a id=\"update_page\" href=\"https://github.com/ME3Tweaks/LegendaryExplorer/edit/UnrealDoc/DocSources/{db.Game}/classes/{classD.Key}.json\" target=\"_blank\">Edit</a></li>";
            html = html.Replace("%LD_EDIT_BULLET%", editLi);

            SaveFile(htmlPath, outputPath, html);
        }

        private static string ConvertModifers(Enum classFlags)
        {
            string str = "";
            foreach (var cf in classFlags.GetFlags())
            {
                str += cf + " ";
            }

            return str.Trim();
        }

        /// <summary>
        /// Looks at database to see if the given value is a member type we can link to
        /// </summary>
        /// <param name="valueMemberType"></param>
        /// <returns></returns>
        private static string GetTypeText(DocuDB db, string type)
        {
            if (db.ClassDocumentation.TryGetValue(type, out var ci))
            {
                return $"<a data-type=\"class\">{type}</a>";
            }

            if (db.EnumDocumentation.TryGetValue(type, out var ei))
            {
                return $"<a data-type=\"enum\">{type}</a>";
            }

            if (db.StructDocumentation.TryGetValue(type, out var si))
            {
                return $"<a data-type=\"struct\">{type}</a>";
            }

            return type;
        }

        private static string? BuildInheritanceTree(DocuDB db, string classKey)
        {
            db.ClassDocumentation.TryGetValue(classKey, out var classInfo);
            Stack<string> classStack = new Stack<string>();
            classStack.Add(classKey);
            while (classInfo != null)
            {
                var parentName = classInfo.ParentClass;
                if (parentName == null || !db.ClassDocumentation.TryGetValue(parentName, out classInfo))
                {
                    break;
                }
                classStack.Add(parentName);
            }

            return BuildInheritanceItem(db, classStack);
        }

        private static string BuildInheritanceItem(DocuDB db, Stack<string> items)
        {
            if (items.TryPop(out var item))
            {
                var info = db.ClassDocumentation[item];
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("<ul class=\"inheritance\">");
                sb.AppendLine($"<li><a href=\"{item}.html\">{info.Package}.{item}</a></li>");

                var sub = BuildInheritanceItem(db, items);
                if (sub != null)
                {
                    sb.AppendLine("<li>");
                    sb.AppendLine(sub);
                    sb.AppendLine("</li>");
                }

                sb.AppendLine("</ul>");
                return sb.ToString();
            }

            return null;
        }

        private static void OutputStructDocumentation(string htmlPath, DocuDB db, KeyValuePair<string, DocuStructEntry> structD)
        {
            var outputPath = Path.Combine(htmlPath, "structs", $"{structD.Key}.html");

            var html = HTML_TEMPLATE;


            var content = STRUCT_TEMPLATE;


            if (structD.Value.Extends != null)
            {
                content = content.Replace("%LD_STRUCTEXTENSIONINFO%", $"<p class=\"structextends\">Extends {GetTypeText(db, structD.Value.Extends)}</p>");
            }
            else
            {
                content = content.Replace("%LD_STRUCTEXTENSIONINFO%", "");
            }

            #region VARIABLES
            {
                StringBuilder memberHtml = new StringBuilder();
                string rowClass = "row";

                var inheritanceStack = new Stack<(string, DocuStructEntry)>();
                inheritanceStack.Add((structD.Key, structD.Value));

                var se = structD.Value.Extends;
                while (se != null)
                {
                    var parentEntry = db.StructDocumentation[se];
                    inheritanceStack.Add((se, parentEntry));
                    se = parentEntry.Extends;
                }

                foreach (var entry in inheritanceStack)
                {
                    foreach (var member in entry.Item2.Members)
                    {
                        var mrow = MEMBER_TEMPLATE;

                        string typeText = GetTypeText(db, member.Value.MemberType);

                        mrow = mrow.Replace("%LD_MEMBERTYPE%", typeText);
                        mrow = mrow.Replace("%LD_MEMBERNAME%", member.Key);

                        var memberDoc = member.Value.MemberDocumentation;
                        if (entry.Item1 != structD.Key)
                        {
                            // List parent struct items
                            memberDoc = $"(Defined in {entry.Item1}) {memberDoc}";
                        }

                        mrow = mrow.Replace("%LD_MEMBERDOC%", memberDoc);
                        var memberModifier = "";
                        var flags = (EPropertyFlags)member.Value.Flags;
                        mrow = mrow.Replace("%LD_MEMBERMODIFIER%", string.Join(" ", flags.GetFlags()));


                        // Formatting
                        mrow = mrow.Replace("%LD_ROWCLASS%", $"{rowClass}Color");

                        memberHtml.AppendLine(mrow);

                        if (rowClass == "row")
                        {
                            rowClass = "alt";
                        }
                        else
                        {
                            rowClass = "row";
                        }
                    }
                }

                content = content.Replace("%LD_MEMBERLIST%", memberHtml.ToString());
            }
            #endregion

            content = content.Replace("%LD_STRUCTNAME%", structD.Key);
            content = content.Replace("%LD_STRUCTCLASSDEF%", GetTypeText(db, structD.Value.DefinedInClass));

            // Install content
            html = html.Replace("%LD_CONTENT%", content);

            // Shared on page
            html = html.Replace("%LD_TITLE%", structD.Key);
            html = html.Replace("%LD_DESCRIPTION%", structD.Value.MemberDocumentation);
            html = html.Replace("%LD_GAME%", db.Game.ToString());

            // We are one folder deep.
            html = html.Replace("%LD_RELPATH%", "../");

            // Navigation.
            string navItems = "";
            navItems += "<li><a href=\"#values\">Variables&nbsp;</a></li>";
            html = html.Replace("%LD_NAV_ITEMS%", navItems);

            // Edit this page
            var editLi = $"<li id=\"update_page_bullet\" title=\"Edit this documentation\"><a id=\"update_page\" href=\"https://github.com/ME3Tweaks/LegendaryExplorer/edit/UnrealDoc/DocSources/{db.Game}/structs/{structD.Key}.json\" target=\"_blank\">Edit</a></li>";
            html = html.Replace("%LD_EDIT_BULLET%", editLi);

            SaveFile(htmlPath, outputPath, html);
        }

        private static void OutputHomePage(string htmlPath)
        {
            var outputPath = Path.Combine(htmlPath, "index.html");
            SaveFile(htmlPath, outputPath, HOME_TEMPLATE);
        }

        /// <summary>
        /// Outputs enum documentation
        /// </summary>
        /// <param name="htmlPath"></param>
        /// <param name="db"></param>
        /// <param name="enumD"></param>
        private static void OutputEnumDocumentation(string htmlPath, DocuDB db, KeyValuePair<string, DocuEnumEntry> enumD)
        {
            var outFile = Path.Combine(htmlPath, "enums", $"{enumD.Key}.html");

            var html = HTML_TEMPLATE;

            var content = ENUM_TEMPLATE;

            #region VALUES
            {
                StringBuilder memberHtml = new StringBuilder();
                string rowClass = "row";
                foreach (var member in enumD.Value.EnumValues)
                {
                    var mrow = ENUMROW_TEMPLATE;

                    mrow = mrow.Replace("%LD_ENUMINDEX%", member.Value.EnumValue.ToString());
                    mrow = mrow.Replace("%LD_MEMBERNAME%", member.Key);
                    mrow = mrow.Replace("%LD_MEMBERDOC%", member.Value.MemberDocumentation);

                    // Formatting
                    mrow = mrow.Replace("%LD_ROWCLASS%", $"{rowClass}Color");

                    memberHtml.AppendLine(mrow);

                    if (rowClass == "row")
                    {
                        rowClass = "alt";
                    }
                    else
                    {
                        rowClass = "row";
                    }
                }

                content = content.Replace("%LD_MEMBERLIST%", memberHtml.ToString());
            }
            #endregion

            content = content.Replace("%LD_ENUMNAME%", enumD.Key);

            // Install content
            html = html.Replace("%LD_CONTENT%", content);

            // Shared on page
            html = html.Replace("%LD_TITLE%", enumD.Key);
            html = html.Replace("%LD_DESCRIPTION%", enumD.Value.MemberDocumentation);
            html = html.Replace("%LD_GAME%", db.Game.ToString());

            // We are one folder deep.
            html = html.Replace("%LD_RELPATH%", "../");

            // Navigation.
            string navItems = "";
            navItems += "<li><a href=\"#values\">Values&nbsp;</a></li>";
            html = html.Replace("%LD_NAV_ITEMS%", navItems);


            // Edit this page
            var editLi = $"<li id=\"update_page_bullet\" title=\"Edit this documentation\"><a id=\"update_page\" href=\"https://github.com/ME3Tweaks/LegendaryExplorer/edit/UnrealDoc/DocSources/{db.Game}/enums/{enumD.Key}.json\" target=\"_blank\">Edit</a></li>";
            html = html.Replace("%LD_EDIT_BULLET%", editLi);

            SaveFile(htmlPath, outFile, html);
        }

        /// <summary>
        /// Saves a page, inserting the %LD_SUBPATH% variable
        /// </summary>
        /// <param name="basePath"></param>
        /// <param name="path"></param>
        /// <param name="contents"></param>
        private static void SaveFile(string basePath, string path, string contents)
        {
            var subPath = Path.GetRelativePath(basePath, path).Replace("\\", "/");
            contents = contents.Replace("%LD_SUBPATH%", subPath);
            File.WriteAllText(path, contents);
        }
    }
}
