using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace SpellBubbleModToolHelper;

public partial class BridgeLib
{
    [UnmanagedCallersOnly(EntryPoint = "patch_main_asset_bundle_internal")]
    public static void PatchMainAssetBundleInternal(IntPtr mainAbPathPtr, IntPtr outAbPathPtr,
        ArrayWrapper addedSongIdsWrapper)
    {
        var mainAbPath = Marshal.PtrToStringUTF8(mainAbPathPtr);
        var outAbPath = Marshal.PtrToStringUTF8(outAbPathPtr);
        var addedSongIds = WrapperToArray_IntPtr(addedSongIdsWrapper).Select(s => Marshal.PtrToStringUTF8(s)).ToList();

        var (am, bundle, assets) = LoadAssetsFromBundlePath(mainAbPath);
        var info = assets.table.assetFileInfo.Single(info =>
            am.GetTypeInstance(assets.file, info).GetBaseField().Get("AssetBundleNames").GetChildrenList() != null);
        var baseField = am.GetTypeInstance(assets.file, info).GetBaseField();

        var abNamesField = baseField.Get("AssetBundleNames").Get("Array");
        var abNames = abNamesField.GetChildrenList();
        var abNameTemplateField = abNames.Last(f => f.Get("second").GetValue().AsString().Contains("score_"));

        var maxIndex = abNames.Select(f => f.Get("first").GetValue().AsInt()).Max();

        var abInfosField = baseField.Get("AssetBundleInfos").Get("Array");
        var abInfos = abInfosField.GetChildrenList();
        var abInfoTemplateField = abInfos.Single(f =>
            f.Get("first").GetValue().AsInt() == abNameTemplateField.Get("first").GetValue().AsInt());

        var abNameAppendFields = new List<AssetTypeValueField>();
        var abInfoAppendFields = new List<AssetTypeValueField>();

        for (var i = 0; i < addedSongIds.Count; ++i)
        {
            var songId = addedSongIds[i];
            var abNameField = (AssetTypeValueField) abNameTemplateField.Clone();

            abNameField.Get("first").GetValue().Set(maxIndex + i + 1);
            abNameField.Get("second").GetValue().Set($"share_scores/score_{songId.ToLower()}");
            abNameAppendFields.Add(abNameField);

            var abInfoField = (AssetTypeValueField) abInfoTemplateField.Clone();
            abInfoField.Get("first").GetValue().Set(maxIndex + i + 1);
            abInfoAppendFields.Add(abInfoField);
        }

        var newAbNames = abNames.ToList();
        newAbNames.AddRange(abNameAppendFields);
        var newAbInfos = abInfos.ToList();
        newAbInfos.AddRange(abInfoAppendFields);

        abNamesField.SetChildrenList(newAbNames.ToArray());
        abInfosField.SetChildrenList(newAbInfos.ToArray());

        var newBytes = baseField.WriteToByteArray();
        var assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
            AssetHelper.GetScriptIndex(assets.file, info), newBytes);
        var replacerList = new List<AssetsReplacer> {assetsReplacer};

        PatchAssetBundle(bundle, assets, replacerList, outAbPath);
    }
}