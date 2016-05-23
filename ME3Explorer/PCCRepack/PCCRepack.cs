using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;

namespace ME3Explorer
{
    public partial class PCCRepack : Form
    {
        public PCCRepack()
        {
            InitializeComponent();
        }

        private void buttonCompressPCC_Click(object sender, EventArgs e)
        {
            if (openPccDialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = openPccDialog.FileName;
                string backupFile = fileName + ".bak";
                if (File.Exists(fileName))
                {
                    try
                    {
                        PCCObject pccObj = new PCCObject(fileName);
                        if (pccObj.Exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"))
                        {
                            var res = MessageBox.Show("This file contains a SeekFreeShaderCache. Compressing will cause a crash when ME3 attempts to load this file.\n" +
                                "Do you want to visit a forum thread with more information and a possible solution?",
                                "I'm sorry, Dave. I'm afraid I can't do that.", MessageBoxButtons.YesNo, MessageBoxIcon.Stop);
                            if (res == DialogResult.Yes)
                            {
                                System.Diagnostics.Process.Start("http://me3explorer.freeforums.org/research-how-to-turn-your-dlc-pcc-into-a-vanilla-one-t2264.html");
                            }
                            return;
                        }
                        DialogResult dialogResult = MessageBox.Show("Do you want to make a backup file?", "Make Backup", MessageBoxButtons.YesNo);
                        if (dialogResult == DialogResult.Yes)
                        {
                            File.Copy(fileName, backupFile);
                        }

                        pccObj.saveByReconstructing(fileName, true);

                        MessageBox.Show("File " + Path.GetFileName(fileName) + " was successfully compressed.", "Succeed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception exc)
                    {
                        MessageBox.Show("An error occurred while compressing " + Path.GetFileName(fileName) + ":\n" + exc.Message, "Exception Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //recovering backup file
                        if (File.Exists(backupFile))
                        {
                            File.Delete(fileName);
                            File.Move(backupFile, fileName);
                        }
                    }
                }
            }
        }

        private void buttonDecompressPCC_Click(object sender, EventArgs e)
        {
            if (openPccDialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = openPccDialog.FileName;
                string backupFile = fileName + ".bak";
                if (File.Exists(fileName))
                {
                    try
                    {
                        DialogResult dialogResult = MessageBox.Show("Do you want to make a backup file?", "Make Backup", MessageBoxButtons.YesNo);
                        if (dialogResult == DialogResult.Yes)
                        {
                            File.Copy(fileName, backupFile);
                        }

                        PCCObject pccObj = new PCCObject(fileName);
                        pccObj.saveByReconstructing(fileName);

                        MessageBox.Show("File " + Path.GetFileName(fileName) + " was successfully decompressed.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception exc)
                    {
                        MessageBox.Show("An error occurred while compressing " + Path.GetFileName(fileName) + ":\n" + exc.Message, "Exception Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        //recovering backup file
                        if (File.Exists(backupFile))
                        {
                            File.Delete(fileName);
                            File.Move(backupFile, fileName);
                        }
                    }
                }
            }
        }

        /*
         * This method is called when using the -decompresspcc command line argument
         */
        public static int autoDecompressPcc(string sourceFile, string outputFile)
        {
            if (!File.Exists(sourceFile))
            {
                MessageBox.Show("PCC to decompress does not exist:\n" + sourceFile, "Auto Decompression Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            Console.WriteLine("Automating Pcc Decompressor: " + sourceFile + " => " + outputFile);
            try
            {
                PCCObject pccObj = new PCCObject(sourceFile);
                pccObj.saveByReconstructing(outputFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error:\n" + ex.Message);
                return 1;
            }
            return 0;
        }

        /*
         * This method is called when using the -compresspcc command line argument
         */
        public static int autoCompressPcc(string sourceFile, string outputFile)
        {
            if (!File.Exists(sourceFile))
            {
                MessageBox.Show("PCC to compress does not exist:\n" + sourceFile, "Auto Compression Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            Console.WriteLine("Automating Pcc Compressor: " + sourceFile + " => " + outputFile);
            try
            {
                PCCObject pccObj = new PCCObject(sourceFile);
                if (pccObj.Exports.Exists(x => x.ObjectName == "SeekFreeShaderCache" && x.ClassName == "ShaderCache"))
                {
                    throw new Exception("Cannot compress files with a SeekFreeShaderCache");
                }
                pccObj.saveByReconstructing(outputFile, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error:\n" + ex.Message);
                return 1;
            }
            return 0;
        }
    }
}
