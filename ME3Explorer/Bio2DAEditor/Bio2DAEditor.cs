using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using ME3Explorer.Packages;
using KFreonLib.MEDirectories;
using System.Diagnostics;
using ME1Explorer.Unreal;
using ME2Explorer.Unreal;
using ME1Explorer.Unreal.Classes;
using System.Xml;
using System.Xml.Linq;

namespace ME3Explorer
{
    public partial class Bio2DAEditor : UserControl
    {
        public IMEPackage Pcc { get { return pcc; } set { pcc = value; defaultStructValues.Clear(); } }

        public int InterpreterMode { get; private set; }
        public IExportEntry export;
        public string className;
        public byte[] memory;
        public int memsize;
        //public int readerpos;

        public struct PropHeader
        {
            public int name;
            public int type;
            public int size;
            public int index;
            public int offset;
        }

        public string[] Types =
        {
            "StructProperty", //0
            "IntProperty",
            "FloatProperty",
            "ObjectProperty",
            "NameProperty",
            "BoolProperty",  //5
            "ByteProperty",
            "ArrayProperty",
            "StrProperty",
            "StringRefProperty",
            "DelegateProperty",//10
            "None",
            "BioMask4Property",
        };

        public enum nodeType
        {
            Unknown = -1,
            StructProperty = 0,
            IntProperty = 1,
            FloatProperty = 2,
            ObjectProperty = 3,
            NameProperty = 4,
            BoolProperty = 5,
            ByteProperty = 6,
            ArrayProperty = 7,
            StrProperty = 8,
            StringRefProperty = 9,
            DelegateProperty = 10,
            None,
            BioMask4Property,

            ArrayLeafObject,
            ArrayLeafName,
            ArrayLeafEnum,
            ArrayLeafStruct,
            ArrayLeafBool,
            ArrayLeafString,
            ArrayLeafFloat,
            ArrayLeafInt,
            ArrayLeafByte,

            StructLeafByte,
            StructLeafFloat,
            StructLeafDeg, //indicates this is a StructProperty leaf that is in degrees (actually unreal rotation units)
            StructLeafInt,
            StructLeafObject,
            StructLeafName,
            StructLeafBool,
            StructLeafStr,
            StructLeafArray,
            StructLeafEnum,
            StructLeafStruct,

            Root,
        }

        Dictionary<int, string> me1TLK = new Dictionary<int, string>();
        private int lastSetOffset = -1; //offset set by program, used for checking if user changed since set 
        private const int HEXBOX_MAX_WIDTH = 650;
        private IMEPackage pcc;

        int? selectedNodePos = null;
        private Dictionary<string, string> ME1_TLK_DICT;
        private Dictionary<string, List<PropertyReader.Property>> defaultStructValues;
        private Bio2DA table2da;
        public static readonly string[] ParsableBinaryClasses = { "Bio2DA", "Bio2DANumberedRows" };

        public Bio2DAEditor()
        {
            InitializeComponent();
            SetTopLevel(false);
            defaultStructValues = new Dictionary<string, List<PropertyReader.Property>>();

            //Load ME1TLK
            string tlkxmlpath = @"C:\users\mgame\desktop\me1tlk.xml";
            if (File.Exists(tlkxmlpath))
            {
                XDocument xmlDocument = XDocument.Load(tlkxmlpath);
                ME1_TLK_DICT =
                    (from strings in xmlDocument.Descendants("string")
                     select new
                     {
                         ID = strings.Element("id").Value,
                         Data = strings.Element("data").Value,
                     }).Distinct().ToDictionary(o => o.ID, o => o.Data);
            }
        }



        public void InitInterpreter()
        {
            memory = export.Data;
            memsize = memory.Length;
            DynamicByteProvider db = new DynamicByteProvider(export.Data);
            hb1.ByteProvider = db;
            className = export.ClassName;
            StartBio2DAScan();
        }

        private void StartBio2DAScan()
        {
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();
            table2da = new Bio2DA(export);
            //Add columns
            for (int j = 0; j < table2da.columnNames.Count(); j++)
            {
                dataGridView1.Columns.Add(table2da.columnNames[j], table2da.columnNames[j]);
            }


            //Add rows
            for (int i = 0; i < table2da.rowNames.Count(); i++)
            {
                //defines row data. If you add columns, you need to add them here in order
                List<Object> rowData = new List<object>();
                for (int j = 0; j < table2da.columnNames.Count(); j++)
                {
                    Bio2DACell cell = table2da[i, j];
                    if (cell != null)
                    {
                        rowData.Add(cell.GetDisplayableValue());
                    }
                    else
                    {
                        rowData.Add(null);
                    }
                    //rowData.Add(table2da[i, j]);
                }
                dataGridView1.Rows.Add(rowData.ToArray());
                dataGridView1.Rows[dataGridView1.Rows.Count - 1].HeaderCell.Value = table2da.rowNames[i];
            }

            //Add row headers
            for (int i = 0; i < table2da.rowNames.Count(); i++)
            {
                dataGridView1.Rows[i].HeaderCell.Value = table2da.rowNames[i];
            }
        }

        public new void Show()
        {
            base.Show();
            //StartScan();
        }

        public nodeType getType(string s)
        {
            int ret = -1;
            for (int i = 0; i < Types.Length; i++)
                if (s == Types[i])
                    ret = i;
            return (nodeType)ret;
        }

        public void WriteString(FileStream fs, string s)
        {
            for (int i = 0; i < s.Length; i++)
                fs.WriteByte((byte)s[i]);
        }

        private void resetPropEditingControls()
        {
            objectNameLabel.Visible = nameEntry.Visible = proptext.Visible = setPropertyButton.Visible = propDropdown.Visible =
                addArrayElementButton.Visible = deleteArrayElementButton.Visible = moveDownButton.Visible =
                moveUpButton.Visible = addPropButton.Visible = false;
            nameEntry.AutoCompleteCustomSource.Clear();
            nameEntry.Clear();
            proptext.Clear();
        }

        private void WriteMem(int pos, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                memory[pos + i] = buff[i];
        }

        private void UpdateMem(int? _selectedNodePos = null)
        {
            export.Data = memory.TypedClone();
            selectedNodePos = _selectedNodePos;
        }

        public void RefreshMem()
        {
            //hb1.ByteProvider = new DynamicByteProvider(memory);
            ////adds rootnode to list
            ////List<TreeNode> allNodes = treeView1.Nodes.Cast<TreeNode>().ToList();
            ////flatten tree of nodes into list.
            //for (int i = 0; i < allNodes.Count(); i++)
            //{
            //    allNodes.AddRange(allNodes[i].Nodes.Cast<TreeNode>());
            //}

            //var expandedNodes = allNodes.Where(x => x.IsExpanded).Select(x => x.Name);
            ///StartScan(treeView1.TopNode?.Name, selectedNodePos?.ToString());

        }

        private void splitContainer1_SplitterMoving(object sender, SplitterCancelEventArgs e)
        {
            //a hack to set max width for SplitContainer1
            splitContainer1.Panel2MinSize = splitContainer1.Width - HEXBOX_MAX_WIDTH;
        }

        private void toggleHexWidthButton_Click(object sender, EventArgs e)
        {
            if (splitContainer1.SplitterDistance > splitContainer1.Panel1MinSize)
            {
                splitContainer1.SplitterDistance = splitContainer1.Panel1MinSize;
            }
            else
            {
                splitContainer1.SplitterDistance = HEXBOX_MAX_WIDTH;
            }
        }

        private void hb1_SelectionChanged(object sender, EventArgs e)
        {
            int start = (int)hb1.SelectionStart;
            int len = (int)hb1.SelectionLength;
            int size = (int)hb1.ByteProvider.Length;
            try
            {
                if (memory != null && start != -1 && start + len <= size)
                {
                    string s = $"Byte: {memory[start]}";
                    if (start <= memory.Length - 4)
                    {
                        s += $", Int: {BitConverter.ToInt32(memory, start)}";
                    }
                    s += $" | Start=0x{start.ToString("X8")} ";
                    if (len > 0)
                    {
                        s += $"Length=0x{len.ToString("X8")} ";
                        s += $"End=0x{(start + len - 1).ToString("X8")}";
                    }
                    selectionStatus.Text = s;
                }
                else
                {
                    selectionStatus.Text = "Nothing Selected";
                }
            }
            catch (Exception)
            {
            }
        }



        #region UnrealObjectInfo
        private PropertyInfo GetPropertyInfo(int propName)
        {
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(propName));
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(propName));
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getPropertyInfo(className, pcc.getNameEntry(propName));
            }
            return null;
        }

        private PropertyInfo GetPropertyInfo(string propname, string typeName, bool inStruct = false)
        {
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct);
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getPropertyInfo(typeName, propname, inStruct);
            }
            return null;
        }

        private ArrayType GetArrayType(PropertyInfo propInfo)
        {
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(propInfo);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getArrayType(propInfo);
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getArrayType(propInfo);
            }
            return ArrayType.Int;
        }

        private ArrayType GetArrayType(int propName, string typeName = null)
        {
            if (typeName == null)
            {
                typeName = className;
            }
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName));
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName));
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getArrayType(typeName, pcc.getNameEntry(propName));
            }
            return ArrayType.Int;
        }

        private List<string> GetEnumValues(string enumName, int propName)
        {
            switch (pcc.Game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumfromProp(className, pcc.getNameEntry(propName));
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumfromProp(className, pcc.getNameEntry(propName));
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getEnumValues(enumName, true);
            }
            return null;
        }
        #endregion

        private void FindButton_Click(object sender, EventArgs e)
        {

        }

        private void findButton_Pressed(object sender, KeyPressEventArgs e)
        {
            if (this.findBox.Focused && e.KeyChar == '\r')
            {
                // click the Go button
                this.findButton.PerformClick();
                // don't allow the Enter key to pass to textbox
                e.Handled = true;
            }
        }

        private void saveHexButton_Click(object sender, EventArgs e)
        {

        }

        private void exportToExcel_Click(object sender, EventArgs e)
        {
            if (pcc == null)
                return;
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = $"Excel file|*.xlsx";
            if (d.ShowDialog() == DialogResult.OK)
            {
                table2da.Write2DAToExcel(d.FileName);
                MessageBox.Show("Done");
            }
        }
    }
}
