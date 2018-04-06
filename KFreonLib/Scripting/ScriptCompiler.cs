using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.CodeDom.Compiler;
using System.IO;
using KFreonLib.GUI;

namespace KFreonLib.Scripting
{
    public static class ScriptCompiler
    {
        static Assembly CompileCode(string code)
        {
            Microsoft.CSharp.CSharpCodeProvider csProvider = new Microsoft.CSharp.CSharpCodeProvider();
            CompilerParameters options = new CompilerParameters();
            options.GenerateExecutable = false;
            options.GenerateInMemory = true;
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            options.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
            options.ReferencedAssemblies.Add(Path.Combine(path, "lib", "AmaroK86Lib.dll"));
            options.ReferencedAssemblies.Add(Path.Combine(path, "lib", "Gibbed.IO.dll"));
            options.ReferencedAssemblies.Add(Path.Combine(path, "lib", "Gibbed.MassEffect3.FileFormats.dll"));
            options.ReferencedAssemblies.Add(Path.Combine(path, "lib", "SaltTPF.dll"));
            options.ReferencedAssemblies.Add(Path.Combine(path, "ME3Explorer.exe"));
            options.ReferencedAssemblies.Add("System.dll");
            options.ReferencedAssemblies.Add("System.Core.dll");
            options.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            options.ReferencedAssemblies.Add("System.Data.Linq.dll");
            CompilerResults result = null;
            try
            {
                result = csProvider.CompileAssemblyFromSource(options, code);
            }
            catch (Exception exc)
            {
                MessageBox.Show("Exception caught: " + exc.Message);
            }
            if (result.Errors.HasErrors)
            {
                string error = "";
                error += "Line: " + result.Errors[0].Line + "  Column: " + result.Errors[0].Column + "\n";
                error += "(" + result.Errors[0].ErrorNumber + ")\n" + result.Errors[0].ErrorText;
                MessageBox.Show(error, "Script Compiler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            if (result.Errors.HasWarnings)
            {
            }
            return result.CompiledAssembly;
        }

        static string RunScript(Assembly script)
        {
            string res = "Error";
            foreach (Type type in script.GetExportedTypes())
                foreach (Type iface in type.GetInterfaces())
                    if (iface == typeof(IScript))
                    {
                        ConstructorInfo constructor = type.GetConstructor(System.Type.EmptyTypes);
                        if (constructor != null && constructor.IsPublic)
                        {
                            IScript scriptObject = constructor.Invoke(null) as IScript;
                            if (scriptObject != null)
                            {
                                res = scriptObject.RunScript();
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                    }
            return res;
        }

        [STAThread]
        public static string CompileAndRun(string script)
        {
            string res = "Error";
            Assembly compiledScript = CompileCode(script);
            if (compiledScript != null)
                res = RunScript(compiledScript);
            if(res == "Code Finished")
            {
                res = "Success";
            }
            GC.Collect();
            return res;
        }
    }
}
