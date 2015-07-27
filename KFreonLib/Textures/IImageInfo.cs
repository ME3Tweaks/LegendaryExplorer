using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KFreonLib.Textures
{
    public interface IImageInfo
    {
        bool CompareStorage(string storage);

        AmaroK86.ImageFormat.ImageSize imgSize { get; set; }

        int offset { get; set; }

        int GameVersion { get; set; }

        int storageType { get; set; }

        int uncSize { get; set; }

        int cprSize { get; set; }
    }
}
