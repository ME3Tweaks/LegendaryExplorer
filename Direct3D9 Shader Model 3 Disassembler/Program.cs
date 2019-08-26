using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Direct3D9_Shader_Model_3_Disassembler
{
    internal class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: Shader3DeComp.exe infile outfile");
                return 1;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine("Input file does not exist!");
                return 1;
            }

            using (FileStream inFile = new FileStream(args[0], FileMode.Open, FileAccess.Read))
            using (FileStream outFile = new FileStream(args[1], FileMode.Create, FileAccess.Write))
            using (StreamWriter writer = new StreamWriter(outFile))
            {
                var info = ShaderReader.DisassembleShader(inFile, writer);
                return info == null ? 1 : 0;
            }
        }}
}
