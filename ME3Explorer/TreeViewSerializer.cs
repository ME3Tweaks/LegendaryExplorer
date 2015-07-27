using System;
using System.IO;
using System.Xml;
using System.Windows.Forms;
using System.Text;

namespace ME3Explorer
{
	/// <summary>
	/// Summary description for TreeViewSerializer.
	/// </summary>
	public class TreeViewSerializer
	{
		
		// Xml tag for node, e.g. 'node' in case of <node></node>
		private const string XmlNodeTag = "n";
		
		// Xml attributes for node e.g. <node text="Asia" tag="" imageindex="1"></node>
		private const string XmlNodeTextAtt = "tx";
		private const string XmlNodeTagAtt = "t";
        private const string XmlNodeImageIndexAtt = "i";

		public TreeViewSerializer()
		{
			//
			// TODO: Add constructor logic here
			//
		}

		//System.IO.StringWriter s;
		public void SerializeTreeView(TreeView treeView, string fileName) 
		{
            if(File.Exists(fileName)) File.Delete(fileName);
			XmlTextWriter textWriter = new XmlTextWriter(fileName, System.Text.Encoding.ASCII);
			// writing the xml declaration tag
			textWriter.WriteStartDocument();
			//textWriter.WriteRaw("\r\n");
			// writing the main tag that encloses all node tags
			textWriter.WriteStartElement("TreeView");
			
			// save the nodes, recursive method
			SaveNodes(treeView.Nodes, textWriter);
			
			textWriter.WriteEndElement();
					
			textWriter.Close();
		}

        public void SerializeTreeView(TreeNode t, string fileName)
        {
            if (File.Exists(fileName)) File.Delete(fileName);
            XmlTextWriter textWriter = new XmlTextWriter(fileName, System.Text.Encoding.ASCII);
            // writing the xml declaration tag
            textWriter.WriteStartDocument();
            //textWriter.WriteRaw("\r\n");
            // writing the main tag that encloses all node tags
            textWriter.WriteStartElement("TreeView");

            // save the nodes, recursive method
            SaveNodes(t.Nodes, textWriter);

            textWriter.WriteEndElement();

            textWriter.Close();
        }

		private void SaveNodes(TreeNodeCollection nodesCollection, 
			XmlTextWriter textWriter)
		{
			for(int i = 0; i < nodesCollection.Count; i++)
			{
				TreeNode node = nodesCollection[i];
				textWriter.WriteStartElement(XmlNodeTag);
				textWriter.WriteAttributeString(XmlNodeTextAtt, node.Text);
				if(node.Tag != null) 
					textWriter.WriteAttributeString(XmlNodeTagAtt, node.Tag.ToString());
				
				// add other node properties to serialize here
				
				if (node.Nodes.Count > 0)
				{
					
					SaveNodes(node.Nodes, textWriter);
					    
				} 				
				textWriter.WriteEndElement();
			}
		}		

		public void DeserializeTreeView(TreeView treeView, string fileName)
		{
			XmlTextReader reader = null;
			try
			{
                // disabling re-drawing of treeview till all nodes are added
				treeView.BeginUpdate();				
				reader =
					new XmlTextReader(fileName);

				TreeNode parentNode = null;
				
				while (reader.Read())
				{
					if (reader.NodeType == XmlNodeType.Element)
					{						
						if (reader.Name == XmlNodeTag)
						{
							TreeNode newNode = new TreeNode();
							bool isEmptyElement = reader.IsEmptyElement;

                            // loading node attributes
							int attributeCount = reader.AttributeCount;
							if (attributeCount > 0)
							{
								for (int i = 0; i < attributeCount; i++)
								{
									reader.MoveToAttribute(i);
									
									SetAttributeValue(newNode, reader.Name, reader.Value);
								}								
							}

                            // add new node to Parent Node or TreeView
                            if(parentNode != null)
                                parentNode.Nodes.Add(newNode);
                            else
                                treeView.Nodes.Add(newNode);

                            // making current node 'ParentNode' if its not empty
							if (!isEmptyElement)
							{
                                parentNode = newNode;
							}
														
						}						                    
					}
                    // moving up to in TreeView if end tag is encountered
					else if (reader.NodeType == XmlNodeType.EndElement)
					{
						if (reader.Name == XmlNodeTag)
						{
							parentNode = parentNode.Parent;
						}
					}
					else if (reader.NodeType == XmlNodeType.XmlDeclaration)
					{ //Ignore Xml Declaration                    
					}
					else if (reader.NodeType == XmlNodeType.None)
					{
						return;
					}
					else if (reader.NodeType == XmlNodeType.Text)
					{
						parentNode.Nodes.Add(reader.Value);
					}

				}
			}
			finally
			{
                // enabling redrawing of treeview after all nodes are added
				treeView.EndUpdate();      
                reader.Close();	
			}
		}

		/// <summary>
		/// Used by Deserialize method for setting properties of TreeNode from xml node attributes
		/// </summary>
		/// <param name="node"></param>
		/// <param name="propertyName"></param>
		/// <param name="value"></param>
		private void SetAttributeValue(TreeNode node, string propertyName, string value)
		{
			if (propertyName == XmlNodeTextAtt)
			{                
				node.Text = value;				
			}
			else if (propertyName == XmlNodeImageIndexAtt) 
			{
				node.ImageIndex = int.Parse(value);
			}
			else if (propertyName == XmlNodeTagAtt)
			{
				node.Tag = value;
			}		
		}

        public void LoadXmlFileInTreeView(TreeView treeView, string fileName)
        {
            XmlTextReader reader = null;
            try
            {
                treeView.BeginUpdate();
                reader = new XmlTextReader(fileName);

                TreeNode n = new TreeNode(fileName);
                treeView.Nodes.Add(n);
                while(reader.Read())
                {
                    if(reader.NodeType == XmlNodeType.Element)
                    {
                        bool isEmptyElement = reader.IsEmptyElement;
                        StringBuilder text = new StringBuilder();
                        int attributeCount = reader.AttributeCount;
                        if(attributeCount > 0)
                        {
                            for(int i = 0; i < attributeCount; i++)
                            {
                                reader.MoveToAttribute(i);
                                text.Append(reader.Value);
                            }
                        }
            
                        if(isEmptyElement)
                        {
                            n.Nodes.Add(text.ToString());
                        }
                        else
                        {
                            n = n.Nodes.Add(text.ToString());
                        }
                    }
                    else if(reader.NodeType == XmlNodeType.EndElement)
                    {
                        n = n.Parent;                    
                    }
                    else if(reader.NodeType == XmlNodeType.XmlDeclaration)
                    {
                    
                    }
                    else if(reader.NodeType == XmlNodeType.None) 
                    {
                        return;
                    }
                    else if(reader.NodeType == XmlNodeType.Text)
                    {
                        n.Nodes.Add(reader.Value);
                    }

                }        
            }
            finally
            {
                treeView.EndUpdate();
                reader.Close();
            }
        }
	}
}
