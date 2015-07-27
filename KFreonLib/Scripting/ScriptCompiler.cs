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
    public partial class ScriptCompiler : Form
    {
        public bool selectPCCModifier { get; set; }
        public ScriptCompiler()
        {
            InitializeComponent();
            selectPCCModifier = false;
        }

        static Assembly CompileCode(string code)
        {
            Microsoft.CSharp.CSharpCodeProvider csProvider = new Microsoft.CSharp.CSharpCodeProvider();
            CompilerParameters options = new CompilerParameters();
            options.GenerateExecutable = false;
            options.GenerateInMemory = true;
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string[] dlls1 = Directory.GetFiles(path, "Amarok86*.dll");
            string[] dlls2 = Directory.GetFiles(path, "Gibbed.*.dll");
            string[] dlls3 = Directory.GetFiles(path, "SaltTPF.dll");
            options.ReferencedAssemblies.AddRange(dlls1.Select(f => Path.Combine(path, f)).ToArray());
            options.ReferencedAssemblies.AddRange(dlls2.Select(f => Path.Combine(path, f)).ToArray());
            options.ReferencedAssemblies.AddRange(dlls2.Select(f => Path.Combine(path, f)).ToArray());
            options.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
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

        static void RunScript(Assembly script, RichTextBox r)
        {
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
                                r.Text = scriptObject.RunScript();
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                    }
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog FileDialog1 = new OpenFileDialog();
            FileDialog1.Filter = "text files (*.txt)|*.txt";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
                rtb1.LoadFile(FileDialog1.FileName);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog FileDialog1 = new SaveFileDialog();
            FileDialog1.Filter = "text files (*.txt)|*.txt";
            if (FileDialog1.ShowDialog() == DialogResult.OK)
                rtb1.SaveFile(FileDialog1.FileName);
        }

        private void compileToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Compile();
        }

        [STAThread]
        public void Compile()
        {
            string temp = rtb1.Text;
            /*temp = temp.Replace("Texplorer tex = new Texplorer();", "Texplorer tex = new Texplorer(true);");
            if (selectPCCModifier)
            {
                temp = temp.Replace("Texplorer tex = new Texplorer(true);", "Texplorer tex = new Texplorer(true);\ntex.selectPCCs = true;");
            }*/
            //MessageBox.Show(temp);
            Assembly compiledScript = CompileCode(temp);
            if (compiledScript != null)
                RunScript(compiledScript, rtb2);
            GC.Collect();
        }

        private void rtb1_SelectionChanged(object sender, EventArgs e)
        {
            int index = rtb1.SelectionStart;
            int line = rtb1.GetLineFromCharIndex(index) + 1;
            int firstChar = rtb1.GetFirstCharIndexFromLine(line - 1);
            int column = index - firstChar;
            Label1.Text = "Line: " + line + "  Col: " + column;
        }

        private void ScriptCompiler_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }

    }
}
