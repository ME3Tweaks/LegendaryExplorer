using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3LibWV.UnrealClasses
{
    public class Sequence
    {
        public int MyIndex;
        public PCCPackage pcc;
        public List<PropertyReader.Property> Props;
        public List<int> Objects;

        public Sequence(PCCPackage Pcc, int index)
        {
            pcc = Pcc;
            MyIndex = index;
            byte[] buff = pcc.GetObjectData(index + 1);
            Props = PropertyReader.getPropList(pcc, buff);
            Objects = new List<int>();
            foreach (PropertyReader.Property p in Props)
            {
                string s = pcc.GetName(p.Name);
                switch (s)
                {
                    case "SequenceObjects":
                        int count = BitConverter.ToInt32(p.raw, 24);                        
                        for (int i = 0; i < count; i++)
                            Objects.Add(BitConverter.ToInt32(p.raw, 28 + i * 4));
                        break;
                }
            }
            for (int i = 0; i < pcc.Exports.Count; i++)
                if (pcc.Exports[i].idxLink == MyIndex + 1)
                    Objects.Add(i + 1);
            Objects.Sort();
        }

        public TreeNode ToTree()
        {
            TreeNode t = new TreeNode("#E" + MyIndex.ToString("d6") + " : " + pcc.GetObject(MyIndex + 1) + "(" + pcc.GetObjectClass(MyIndex + 1) + ")");
            t.Name = (MyIndex + 1).ToString();
            for (int i = 0; i < Objects.Count; i++)
            {
                if (Objects[i] == 0)
                    continue;
                TreeNode t2 = new TreeNode();
                if (Objects[i] > 0)
                    t2.Text = "#E" + (Objects[i] - 1).ToString("d6") + " : " + pcc.GetObject(Objects[i]) + "(" + pcc.GetObjectClass(Objects[i]) + ")";
                else
                    t2.Text = "#I" + (-Objects[i] - 1).ToString("d6") + " : " + pcc.GetObject(Objects[i]) + "(" + pcc.GetObjectClass(Objects[i]) + ")";
                t2.Name = Objects[i].ToString();
                if (Objects[i] > 0 && pcc.GetObjectClass(Objects[i]) != "Sequence")
                    t2 = MakeSubObj(t2, Objects[i] - 1);
                t.Nodes.Add(t2);
            }
            return t;
        }

        public TreeNode MakeSubObj(TreeNode t, int index)
        {
            byte[] buff = pcc.GetObjectData(index);
            List<PropertyReader.Property> Pr = PropertyReader.getPropList(pcc, buff);
            int count, pos, count2, pos2;
            TreeNode t2, t3, t4;
            List<PropertyReader.Property> Pr2, Pr3;
            foreach (PropertyReader.Property p in Pr)
            {
                string s = pcc.GetName(p.Name);
                switch (s)
                {
                    #region InputLinks
                    case "InputLinks":
                        t2 = new TreeNode("Input Links");
                        count = BitConverter.ToInt32(p.raw, 24);
                        pos = 28;
                        for (int i = 0; i < count; i++)
                        {
                            t3 = new TreeNode(i.ToString());
                            Pr2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                            foreach (PropertyReader.Property pp in Pr2)
                            {
                                string s2 = pcc.GetName(pp.Name);
                                switch (s2)
                                {
                                    case "LinkDesc":
                                        t3.Nodes.Add("Link Description : " + pp.Value.StringValue);
                                        break;
                                    case "LinkAction":
                                        t3.Nodes.Add("Link Action : " + pcc.GetName(pp.Value.IntValue));
                                        break;
                                    case "LinkedOp":
                                        t3.Nodes.Add("Linked Operation : #" + pp.Value.IntValue + " " + pcc.GetObjectPath(pp.Value.IntValue) + pcc.GetObject(pp.Value.IntValue));
                                        break;
                                }
                            }
                            t2.Nodes.Add(t3);
                            pos = Pr2[Pr2.Count - 1].offend;
                        }
                        t.Nodes.Add(t2);
                        break;
                    #endregion
                    #region Output Links
                    case "OutputLinks":
                        t2 = new TreeNode("Output Links");
                        count = BitConverter.ToInt32(p.raw, 24);
                        pos = 28;
                        for (int i = 0; i < count; i++)
                        {
                            t3 = new TreeNode(i.ToString());
                            Pr2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                            foreach (PropertyReader.Property pp in Pr2)
                            {
                                string s2 = pcc.GetName(pp.Name);
                                switch (s2)
                                {
                                    case "LinkDesc":
                                        t3.Nodes.Add("Link Description : " + pp.Value.StringValue);
                                        break;
                                    case "LinkAction":
                                        t3.Nodes.Add("Link Action : " + pcc.GetName(pp.Value.IntValue));
                                        break;
                                    case "LinkedOp":
                                        t3.Nodes.Add("Linked Operation : #" + pp.Value.IntValue + " " + pcc.GetObjectPath(pp.Value.IntValue) + pcc.GetObject(pp.Value.IntValue));
                                        break;
                                    case "Links":
                                        t4 = new TreeNode("Links");
                                        count2 = BitConverter.ToInt32(pp.raw, 24);
                                        pos2 = 28;
                                        for (int i2 = 0; i2 < count2; i2++)
                                        {
                                            Pr3 = PropertyReader.ReadProp(pcc, pp.raw, pos2);
                                            string res = "#" + i2.ToString() + " : ";
                                            foreach (PropertyReader.Property ppp in Pr3)
                                            {
                                                string s3 = pcc.GetName(ppp.Name);                                                
                                                switch (s3)
                                                {
                                                    case "LinkedOp":
                                                        res += "Linked Operation (" + ppp.Value.IntValue + " " + pcc.GetObjectPath(ppp.Value.IntValue) + pcc.GetObject(ppp.Value.IntValue) + ") ";
                                                        break;
                                                    case "InputLinkIdx":
                                                        res += "Input Link Index(" + ppp.Value.IntValue + ")";
                                                        break;
                                                }
                                            }
                                            t4.Nodes.Add(res);
                                            pos2 = Pr3[Pr3.Count - 1].offend;
                                        }
                                        t3.Nodes.Add(t4);
                                        break;
                                }
                            }
                            t2.Nodes.Add(t3);
                            pos = Pr2[Pr2.Count - 1].offend;
                        }
                        t.Nodes.Add(t2);
                        break;
                    #endregion
                    #region Variable Links
                    case "VariableLinks":
                        t2 = new TreeNode("Variable Links");
                        count = BitConverter.ToInt32(p.raw, 24);
                        pos = 28;
                        for (int i = 0; i < count; i++)
                        {
                            t3 = new TreeNode(i.ToString());
                            Pr2 = PropertyReader.ReadProp(pcc, p.raw, pos);
                            foreach (PropertyReader.Property pp in Pr2)
                            {
                                string s2 = pcc.GetName(pp.Name);
                                switch (s2)
                                {
                                    case "LinkDesc":
                                        t3.Nodes.Add("Link Description : " + pp.Value.StringValue);
                                        break;
                                    case "LinkVar":
                                        t3.Nodes.Add("Link Variable : " + pcc.GetName(pp.Value.IntValue));
                                        break;
                                    case "PropertyName":
                                        t3.Nodes.Add("Property Name : " + pcc.GetName(pp.Value.IntValue));
                                        break;
                                    case "ExpectedType":
                                        t3.Nodes.Add("Expected Type : #" + pp.Value.IntValue + " " + pcc.GetObjectPath(pp.Value.IntValue) + pcc.GetObject(pp.Value.IntValue));
                                        break;
                                    case "LinkedVariables":
                                        t4 = new TreeNode("Linked Variables");
                                        count2 = BitConverter.ToInt32(pp.raw, 24);
                                        for (int i2 = 0; i2 < count2; i2++)
                                        {
                                            int idx = BitConverter.ToInt32(pp.raw, 28 + i2 * 4);
                                            t4.Nodes.Add("#" + i2.ToString() + " : #" + idx + " " + pcc.GetObjectPath(idx) + pcc.GetObject(idx));
                                        }
                                        t3.Nodes.Add(t4);
                                        break;
                                }
                            }
                            t2.Nodes.Add(t3);
                            pos = Pr2[Pr2.Count - 1].offend;
                        }
                        t.Nodes.Add(t2);
                        break;
                    #endregion
                }
            }
            return t;
        }
    }
}
