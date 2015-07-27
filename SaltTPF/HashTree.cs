using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SaltTPF
{
    public class HashTree
    {
        public class Node
        {
            public String Name;
            public List<Node> Nodes;
            public List<UInt32> Hashes;
            public List<String> Files;

            public Node(StreamReader reader)
            {
                Nodes = new List<Node>();
                Hashes = new List<uint>();
                Files = new List<string>();

                Name = reader.ReadLine();

                String ln = reader.ReadLine();
                while (String.Compare(ln, "!--") == 0)
                {
                    Nodes.Add(new Node(reader));
                    ln = reader.ReadLine();
                }

                while (String.Compare(ln, "--!") != 0)
                {
                    String[] strs = ln.Split('|');
                    strs[0] = strs[0].Substring(2);
                    Hashes.Add(uint.Parse(strs[0], System.Globalization.NumberStyles.AllowHexSpecifier));
                    Files.Add(strs[1]);
                    ln = reader.ReadLine();
                }
            }

            public void FindHash(List<String> matches, uint val, String path)
            {
                foreach (Node n in Nodes)
                {
                    n.FindHash(matches, val, path + "." + Name);
                }
                for (int i = 0; i < Hashes.Count; i++)
                {
                    if (Hashes[i] == val)
                    {
                        matches.Add(path + "." + Name + "." + Files[i]);
                    }
                }
            }
        }

        private Node baseNode;

        public HashTree(String file)
        {
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(fs))
                {
                    reader.ReadLine();
                    baseNode = new Node(reader);
                }
            }
        }

        public List<String> FindHash(uint val)
        {
            List<String> matches = new List<string>();
            baseNode.FindHash(matches, val, "");

            if (matches.Count == 0)
                return null;
            else
                return matches;
        }
    }
}
