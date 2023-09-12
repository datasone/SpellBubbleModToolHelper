using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace SpellBubbleModToolHelper;

public static partial class BridgeLib
{
    private static string classPackagePath;

    [UnmanagedCallersOnly(EntryPoint = "initialize")]
    public static void Initialize(IntPtr classPackagePathPtr) // string classPackagePathPtr
    {
        // Encoding Setup
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        classPackagePath = Marshal.PtrToStringUTF8(classPackagePathPtr);
    }

    private static (AssetsManager, BundleFileInstance, AssetsFileInstance) LoadAssetsFromBundlePath(
        string assetBundlePath)
    {
        var am = new AssetsManager();
        am.LoadClassPackage(classPackagePath);
        var assetBundle = am.LoadBundleFile(assetBundlePath, true);
        am.LoadClassDatabaseFromPackage(am.LoadAssetsFileFromBundle(assetBundle, 0).file.typeTree.unityVersion);
        var assets = am.LoadAssetsFileFromBundle(assetBundle, 0);
        return (am, assetBundle, assets);
    }

    private static void PatchAssetBundle(BundleFileInstance bundle, AssetsFileInstance assets,
        List<AssetsReplacer> replacer, string outputPath, string newAssetsName = null)
    {
        byte[] newAssetsData;
        using (var stream = new MemoryStream())
        using (var writer = new AssetsFileWriter(stream))
        {
            assets.file.Write(writer, 0, replacer);
            newAssetsData = stream.ToArray();
        }

        var bundleReplacer = new BundleReplacerFromMemory(assets.name, newAssetsName, true, newAssetsData, -1);

        using (var bundleWriter = new AssetsFileWriter(File.OpenWrite(outputPath)))
        using (var newStream = new MemoryStream())
        using (var writer = new AssetsFileWriter(newStream))
        {
            bundle.file.Write(writer, new List<BundleReplacer> {bundleReplacer});
            using (var reader = new AssetsFileReader(newStream))
            {
                var newBundle = new AssetBundleFile();
                newBundle.Pack(reader, bundleWriter, AssetBundleCompressionType.LZMA, false);
            }
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "free_dotnet")]
    public static void Free(IntPtr ptr)
    {
        Marshal.FreeCoTaskMem(ptr);
    }
}