﻿using System;
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

        private static string GetResourceString(string location)
        {
            using Stream resourceStream = Assembly.GetManifestResourceStream(location);
            if (resourceStream is null)
            {
                throw new ArgumentException($"Could not load {location}", nameof(location));
            }
            using var reader = new StreamReader(resourceStream);
            return reader.ReadToEnd();
        }

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

        public static string StandardShader => GetResourceString("LegendaryExplorer.Resources.StandardShader.hlsl");

        public static string TextureShader => GetResourceString("LegendaryExplorer.Resources.TextureShader.hlsl");
    }
}
