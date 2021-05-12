using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Resources
{
    public static class EmbeddedResources
    {
        private static Assembly Assembly => typeof(EmbeddedResources).GetTypeInfo().Assembly;

        public static byte[] KismetFont
        {
            get
            {
                const string resourceLocation = "LegendaryExplorer.Resources.Fonts.KismetFont.ttf";
                var tmpStream = new MemoryStream();
                using Stream resourceStream = Assembly.GetManifestResourceStream(resourceLocation);
                if (resourceStream is null)
                {
                    throw new Exception($"Could not load {resourceLocation}");
                }
                resourceStream.CopyTo(tmpStream);
                return tmpStream.ToArray();
            }
        }
        
        public static string StandardShader
        {
            get
            {
                const string resourceLocation = "LegendaryExplorer.Resources.StandardShader.hlsl";
                using Stream resourceStream = Assembly.GetManifestResourceStream(resourceLocation);
                if (resourceStream is null)
                {
                    throw new Exception($"Could not load {resourceLocation}");
                }
                using var reader = new StreamReader(resourceStream);
                return reader.ReadToEnd();
            }
        }
    }
}
