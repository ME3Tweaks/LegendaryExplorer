using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorer.Tests;

public static class TestData
{
    public static Stream GetTestDataStream(params string[] args)
    {
        var file = GetTestDataFilePath(args);
        if (!File.Exists(file))
        {
            throw new FileNotFoundException();
        }
        
        return File.OpenRead(file);
    }

    public static byte[] GetTestDataBytes(params string[] args)
    {
        var file = GetTestDataFilePath(args);
        if (!File.Exists(file))
        {
            throw new FileNotFoundException();
        }
        
        return File.ReadAllBytes(file);
    }
    
    public static string GetTestDataFilePath(params string[] args)
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string[] pathPart = { assemblyDir, "TestData" };
        return Path.Combine(pathPart.Concat(args).ToArray());
    }
}