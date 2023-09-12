using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace SpellBubbleModToolHelper;

public partial class BridgeLib
{
    [UnmanagedCallersOnly(EntryPoint = "patch_features")]
    public static void PatchFeatures(IntPtr shareDataPath, IntPtr outPath, int patchMusic,
            ArrayWrapper excludedDLCs, IntPtr leftMusicID, int patchCharacter, int characterTargetDLC,
            int patchSpecialRules)
        // string shareDataPath, string outPath, bool patchMusic, int[] excludedDLCs, string leftMusicID
        // bool patchCharacter, int characterTargetDLC, bool patchSpecialRules
    {
        var path = Marshal.PtrToStringUTF8(shareDataPath);
        var outputPath = Marshal.PtrToStringUTF8(outPath);
        var excludedDLCIds = WrapperToArray_int(excludedDLCs); // TODO: Split it for music and character
        var leftMusic = Marshal.PtrToStringUTF8(leftMusicID);

        var (am, bundle, assets) = LoadAssetsFromBundlePath(path);
        var replacerList = new List<AssetsReplacer>();

        if (patchMusic != 0)
        {
            var musicInfo = assets.table.GetAssetInfo("TPZ_MusicData");
            var musicBaseField = am.GetTypeInstance(assets.file, musicInfo).GetBaseField();

            UnlockDLCsForMusic(ref musicBaseField, excludedDLCIds, leftMusic);

            var musicNewBytes = musicBaseField.WriteToByteArray();
            var musicAssetReplacer = new AssetsReplacerFromMemory(0, musicInfo.index, (int) musicInfo.curFileType,
                AssetHelper.GetScriptIndex(assets.file, musicInfo), musicNewBytes);
            replacerList.Add(musicAssetReplacer);
        }

        if (patchCharacter != 0)
        {
            var characterInfo = assets.table.GetAssetInfo("TPZ_CharacterData");
            var characterBaseField = am.GetTypeInstance(assets.file, characterInfo).GetBaseField();

            UnlockDLCsForCharacters(ref characterBaseField, excludedDLCIds, characterTargetDLC);

            var characterNewBytes = characterBaseField.WriteToByteArray();
            var characterAssetReplacer = new AssetsReplacerFromMemory(0, characterInfo.index,
                (int) characterInfo.curFileType, AssetHelper.GetScriptIndex(assets.file, characterInfo),
                characterNewBytes);
            replacerList.Add(characterAssetReplacer);
        }

        if (patchSpecialRules != 0)
        {
            var specialRuleInfo = assets.table.GetAssetInfo("TPZ_SpecialRuleData");
            var specialRuleBaseField = am.GetTypeInstance(assets.file, specialRuleInfo).GetBaseField();

            UnlockSpecialRules(ref specialRuleBaseField);

            var specialRuleNewBytes = specialRuleBaseField.WriteToByteArray();
            var specialRuleReplacer = new AssetsReplacerFromMemory(0, specialRuleInfo.index,
                (int) specialRuleInfo.curFileType,
                AssetHelper.GetScriptIndex(assets.file, specialRuleInfo), specialRuleNewBytes);
            replacerList.Add(specialRuleReplacer);
        }

        PatchAssetBundle(bundle, assets, replacerList, outputPath);
    }

    private static void UnlockDLCsForMusic(ref AssetTypeValueField baseField, int[] excludedDLCIds,
        string leftMusic)
    {
        var musicList = baseField.Get("sheets").Get(0).Get(0).Get("list").Get(0).GetChildrenList();

        var fallBackID = "Lostword";
        var leftFlag = false;

        foreach (var musicItem in musicList)
            if (musicItem.Get("IsGame").GetValue().AsInt() == 1)
            {
                if (musicItem.Get("ID").GetValue().AsString() != leftMusic)
                {
                    musicItem.Get("IsDefault").GetValue().Set(1);
                    musicItem.Get("Price").GetValue().Set(0);
                }
                else
                {
                    leftFlag = true;
                }

                if (!excludedDLCIds.Contains(musicItem.Get("DLCIndex").GetValue().AsInt()))
                    musicItem.Get("DLCIndex").GetValue().Set(0);
            }

        if (leftFlag) return;

        var fallBackItem = Array.Find(musicList, field => field.Get("ID").GetValue().AsString() == fallBackID);
        fallBackItem.Get("IsDefault").GetValue().Set(0);
        fallBackItem.Get("Price").GetValue().Set(1000);
    }

    private static void UnlockDLCsForCharacters(ref AssetTypeValueField baseField, int[] excludedDLCIds,
        int characterTargetDLC)
    {
        var characterList = baseField.Get("sheets").Get(0).Get(0).Get("list").Get(0).GetChildrenList();

        foreach (var characterItem in characterList)
        {
            if (excludedDLCIds.Contains(characterItem.Get("DLCIndex").GetValue().AsInt())) continue;

            if (characterItem.Get("DLCIndex").GetValue().AsInt() != 0)
                characterItem.Get("DLCIndex").GetValue().Set(characterTargetDLC);
        }
    }

    private static void UnlockSpecialRules(ref AssetTypeValueField baseField)
    {
        var excludePrefixList = new[] {"TwoColors", "FourColors", "NONE"};

        var specialRuleList = baseField.Get("sheets").Get(0).Get(0).Get("list").Get(0).GetChildrenList();

        var index = 1;
        foreach (var specialRuleItem in specialRuleList)
        {
            if (excludePrefixList.Any(prefix =>
                    specialRuleItem.Get("ID").GetValue().AsString().StartsWith(prefix))) continue;

            specialRuleItem.Get("Index").GetValue().Set(index);
            ++index;

            specialRuleItem.Get("IsInRuleSetting").GetValue().Set(1);
            specialRuleItem.Get("IsDefault").GetValue().Set(1);
        }
    }
}