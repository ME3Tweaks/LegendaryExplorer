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
                    DialogResult dialogResult = MessageBox.Show("Do you want to make a backup file?", "Make Backup", MessageBoxButtons.YesNo);
                    try
                    {
                        if (dialogResult == DialogResult.Yes)
                        {
                            File.Copy(fileName, backupFile);
                        }

                        PCCObject pccObj = new PCCObject(fileName);
                        pccObj.saveToFile(true);

                        //PCCObject pccFile = new PCCObject();
                        //pccFile.LoadFile(fileName);
                        /*main function that compress the file
                        PCCHandler.CompressAndSave(pccFile.SaveFile(),fileName);*/
                        /*byte[] buffer;
                        using (FileStream inputStream = File.OpenRead(fileName))
                        {
                            buffer = new byte[inputStream.Length];
                            inputStream.Read(buffer, 0, buffer.Length);
                        }
                        if (dialogResult == DialogResult.Yes)
                        {
                            File.Copy(fileName, backupFile);
                        }

                        //main function that compress the file
                        PCCHandler.CompressAndSave(buffer,fileName);*/


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
                    DialogResult dialogResult = MessageBox.Show("Do you want to make a backup file?", "Make Backup", MessageBoxButtons.YesNo);
                    try
                    {
                        if (dialogResult == DialogResult.Yes)
                        {
                            File.Copy(fileName, backupFile);
                        }

                        /*PCCObject pccObj = new PCCObject();
                        pccObj.LoadFile(fileName);
                        pccObj.SaveFile(fileName);*/
                        //main function that compress the file
                        /*byte[] buffer = PCCHandler.Decompress(fileName);
                        FileStream outputStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                        outputStream.Write(buffer, 0, buffer.Length);
                        outputStream.Dispose();*/

                        PCCObject pccObj = new PCCObject(fileName);
                        pccObj.saveToFile(false);

                        MessageBox.Show("File " + Path.GetFileName(fileName) + " was successfully decompressed.", "Succeed", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void PCCRepack_FormClosing(object sender, FormClosingEventArgs e)
        {
            taskbar.RemoveTool(this);
        }
    }
}
