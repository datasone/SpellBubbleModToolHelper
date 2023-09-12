using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace SpellBubbleModToolHelper;

public partial class BridgeLib
{
    [UnmanagedCallersOnly(EntryPoint = "patch_score")]
    public static void PatchScore(IntPtr scorePathPtr, IntPtr outScorePathPtr, IntPtr songIDPtr,
        ArrayWrapper paramsWrapper)
    {
        var scorePath = Marshal.PtrToStringUTF8(scorePathPtr);
        var outScorePath = Marshal.PtrToStringUTF8(outScorePathPtr);
        var songID = Marshal.PtrToStringUTF8(songIDPtr);
        var paramArray = WrapperToArray_IntPtr(paramsWrapper);
        var paramStrArray = paramArray.Select(ptr => Marshal.PtrToStringUTF8(ptr)).ToArray();

        var (am, bundle, assets) = LoadAssetsFromBundlePath(scorePath);
        var replacerList = new List<AssetsReplacer>();

        var beatScript = paramStrArray[0];
        var info = assets.table.GetAssetInfo($"{songID}_beat");
        var baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
        baseField.Get("m_Script").GetValue().Set(Encoding.Unicode.GetBytes("\uFEFF" + beatScript));
        var newBytes = baseField.WriteToByteArray();
        var assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
            AssetHelper.GetScriptIndex(assets.file, info), newBytes);
        replacerList.Add(assetsReplacer);

        for (var i = 1; i < paramStrArray.Length; i += 2)
        {
            var difficulty = paramStrArray[i];
            var script = paramStrArray[i + 1];

            info = assets.table.GetAssetInfo($"{songID}_rhythm_{difficulty}");
            baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
            baseField.Get("m_Script").GetValue().Set(Encoding.Unicode.GetBytes("\uFEFF" + script));
            newBytes = baseField.WriteToByteArray();
            assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
                AssetHelper.GetScriptIndex(assets.file, info), newBytes);
            replacerList.Add(assetsReplacer);
        }

        PatchAssetBundle(bundle, assets, replacerList, outScorePath);
    }

    [UnmanagedCallersOnly(EntryPoint = "create_score")]
    public static void CreateScore(IntPtr scorePathPtr, IntPtr outScorePathPtr, IntPtr songIDPtr,
        IntPtr newSongIDPtr, ArrayWrapper paramsWrapper)
    {
        var scorePath = Marshal.PtrToStringUTF8(scorePathPtr);
        var outScorePath = Marshal.PtrToStringUTF8(outScorePathPtr);
        var songID = Marshal.PtrToStringUTF8(songIDPtr);
        var newSongID = Marshal.PtrToStringUTF8(newSongIDPtr);
        var paramArray = WrapperToArray_IntPtr(paramsWrapper);
        var paramStrArray = paramArray.Select(ptr => Marshal.PtrToStringUTF8(ptr)).ToArray();

        var (am, bundle, assets) = LoadAssetsFromBundlePath(scorePath);
        var replacerList = new List<AssetsReplacer>();

        var info = assets.table.assetFileInfo.Single(info =>
            am.GetTypeInstance(assets.file, info).GetBaseField().Get("m_Container").GetChildrenCount() != -1);
        var baseField = am.GetTypeInstance(assets.file, info).GetBaseField();

        var containerName = baseField.Get("m_Name").GetValue().AsString();
        containerName = containerName.Replace(songID.ToLower(), newSongID.ToLower());
        baseField.Get("m_Name").GetValue().Set(containerName);

        var newAssetsName = new MD4().Encode(containerName, false);
        newAssetsName = $"CAB-{newAssetsName}";

        containerName = baseField.Get("m_AssetBundleName").GetValue().AsString();
        containerName = containerName.Replace(songID.ToLower(), newSongID.ToLower());
        baseField.Get("m_AssetBundleName").GetValue().Set(containerName);

        for (var i = 0; i < 4; i++)
        {
            var textAssetPath = baseField.Get("m_Container").Get("Array").GetChildrenList()[i].GetChildrenList()[0]
                .GetValue().AsString();
            textAssetPath = textAssetPath.Replace(songID.ToLower(), newSongID.ToLower());
            baseField.Get("m_Container").Get("Array").GetChildrenList()[i].GetChildrenList()[0].GetValue()
                .Set(textAssetPath);
        }

        var newBytes = baseField.WriteToByteArray();
        var assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
            AssetHelper.GetScriptIndex(assets.file, info), newBytes);
        replacerList.Add(assetsReplacer);

        var beatScript = paramStrArray[0];
        info = assets.table.GetAssetInfo($"{songID}_beat");
        baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
        baseField.Get("m_Script").GetValue().Set(Encoding.Unicode.GetBytes("\uFEFF" + beatScript));
        baseField.Get("m_Name").GetValue().Set($"{newSongID}_beat");

        newBytes = baseField.WriteToByteArray();
        assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
            AssetHelper.GetScriptIndex(assets.file, info), newBytes);
        replacerList.Add(assetsReplacer);

        for (var i = 1; i < paramStrArray.Length; i += 2)
        {
            var difficulty = paramStrArray[i];
            var script = paramStrArray[i + 1];

            info = assets.table.GetAssetInfo($"{songID}_rhythm_{difficulty}");
            baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
            baseField.Get("m_Script").GetValue().Set(Encoding.Unicode.GetBytes("\uFEFF" + script));
            baseField.Get("m_Name").GetValue().Set($"{newSongID}_rhythm_{difficulty}");

            newBytes = baseField.WriteToByteArray();
            assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
                AssetHelper.GetScriptIndex(assets.file, info), newBytes);
            replacerList.Add(assetsReplacer);
        }

        PatchAssetBundle(bundle, assets, replacerList, outScorePath, newAssetsName);
    }
}