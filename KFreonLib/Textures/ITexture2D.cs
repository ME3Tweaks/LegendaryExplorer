using AmaroK86.ImageFormat;
using KFreonLib.PCCObjects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace KFreonLib.Textures
{
    public interface ITexture2D
    {
        void DumpTexture(string filename);

        byte[] GetImageData(int size = -1);

        Bitmap GetImage(int size = -1);

        IImageInfo GenerateImageInfo();

        List<IImageInfo> imgList { get; set; }

        string texName { get; set; }

        byte[] DumpImg(ImageSize imageSize, string ArcPath);

        string texFormat { get; set; }

        List<string> allPccs { get; set; }

        uint pccOffset { get; set; }

        string getFileFormat();

        byte[] extractImage(string imgSize, bool NoOutput, string archiveDir = null, string filename = null);

        byte[] extractMaxImage(bool NoOutput, string archiveDir = null, string fileName = null);

        void replaceImage(string imgSize, ImageFile im, string archiveDir);

        bool hasChanged { get; set; }

        string GetTexArchive(string dir);

        List<int> expIDs { get; set; }

        void addBiggerImage(ImageFile im, string archiveDir);

        void singleImageUpscale(ImageFile im, string archiveDir);

        void OneImageToRuleThemAll(ImageFile im, string archiveDir, byte[] imgData);

        string arcName { get; set; }

        string LODGroup { get; set; }

        uint Hash { get; set; }

        void removeImage();

        byte[] ToArray(uint pccExportDataOffset, IPCCObject pcc);

        int pccExpIdx { get; set; }

        void CopyImgList(ITexture2D tex2D, IPCCObject PCC);

        List<string> allFiles { get; set; }

        int Mips { get; set; }

        bool NoRenderFix { get; set; }

        void LowResFix();
    }
}
