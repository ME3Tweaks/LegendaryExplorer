using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;

namespace SaltTPF
{
    /// <summary>
    /// Class to be used for working with TPF Files
    /// </summary>
    public class TPFExtract
    {
        /// <summary>
        /// The list of filenames found inside the TPF file
        /// </summary>
        public List<String> Files { get; private set; }

        private ZipReader _zippy;
        private List<HashTree> _trees;
        private bool _formview;
        
        /// <summary>
        /// Class Constructor
        /// </summary>
        /// <param name="file">The full path to the TPF file for reading</param>
        /// <param name="treefolder">The path to the directory containing the hash tree files</param>
        /// <param name="FormView">Should the class generate a form to view this TPF</param>
        public TPFExtract(String file, String treefolder, bool FormView)
        {
            _zippy = new ZipReader(file);

            Files = new List<string>();
            foreach (ZipReader.ZipEntryFull entry in _zippy.Entries)
                Files.Add(entry.Filename);

            _trees = new List<HashTree>();
            try { _trees.Add(new HashTree(Path.Combine(treefolder, "ME1Tree.hash"))); }
            catch (FileNotFoundException) { }
            try { _trees.Add(new HashTree(Path.Combine(treefolder, "ME2Tree.hash"))); }
            catch (FileNotFoundException) { }
            try { _trees.Add(new HashTree(Path.Combine(treefolder, "ME3Tree.hash"))); }
            catch (FileNotFoundException) { }

            _formview = FormView;
            if (_formview)
            {
                if (_trees.Count < 3)
                    MessageBox.Show("Some of the hash files weren't found, some of the auto-matching options won't be available", "Missing files", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                TPFView form = new TPFView(this);
                form.Show();
            }
        }

        /// <summary>
        /// Extract all files from the TPF to a directory
        /// </summary>
        /// <param name="path">The path to the extraction directory</param>
        public void ExtractAll(String path)
        {
            for (int i = 0; i < _zippy.Entries.Count; i++)
            {
                ExtractN(Path.Combine(path, _zippy.Entries[i].Filename), i);
            }
        }

        /// <summary>
        /// Extract one file from the TPF to a specified file
        /// </summary>
        /// <param name="fullpath">The full path to be used as the filename of the extracted file</param>
        /// <param name="n">The index of the file in the TPF's file list</param>
        public void ExtractN(String fullpath, int n)
        {
            try
            {
                _zippy.Entries[n].Extract(false, fullpath);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred during extraction of " + _zippy.Entries[n].Filename + ": " + exc.Message, "Extraction error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Return the raw data of one of the files in the TPF
        /// </summary>
        /// <param name="n">The index of the file in the TPF's filelist</param>
        /// <returns>The data array</returns>
        public byte[] PreviewN(int n)
        {
            try
            {
                return _zippy.Entries[n].Extract(true);
            }
            catch (Exception exc)
            {
                MessageBox.Show("An error occurred durin extraction: " + exc.Message, "Extraction error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>
        /// Get a file entry from the TPF at a given index
        /// </summary>
        /// <param name="n">The index to return</param>
        /// <returns>The file entry</returns>
        public ZipReader.ZipEntryFull GetEntry(int n)
        {
            if (n >= _zippy.Entries.Count)
                return null;
            return _zippy.Entries[n];
        }

        /// <summary>
        /// Return the entry that contains texmod.def
        /// </summary>
        /// <returns>The Texmod.def entry</returns>
        public ZipReader.ZipEntryFull GetTexmodDef()
        {
            return _zippy.Entries.Last();
        }

        /// <summary>
        /// Get the number of files in the archive
        /// </summary>
        /// <returns></returns>
        public int GetNumEntries()
        {
            return _zippy.Entries.Count();
        }

        /// <summary>
        /// Get the full path of the TPF file
        /// </summary>
        /// <returns></returns>
        public String GetFilename()
        {
            return _zippy._filename;
        }

        /// <summary>
        /// Get the comment of the TPF file
        /// </summary>
        /// <returns></returns>
        public String GetComment()
        {
            return _zippy.EOFStrct.Comment;
        }

        /// <summary>
        /// Search a hash tree for a particular hash value
        /// </summary>
        /// <param name="TreeIndex">The number of the tree to search (0 = ME1, 1 = ME2, 2 = ME3)</param>
        /// <param name="Hash">The value to search for</param>
        /// <returns>A list of matches</returns>
        public List<String> SearchHash(int TreeIndex, uint Hash)
        {
            if (_trees.Count > TreeIndex)
                return _trees[TreeIndex].FindHash(Hash);
            else
                return null;
        }
    }
}
