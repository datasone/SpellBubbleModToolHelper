using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AssetsTools.NET.Extra;
using Il2CppDumper;

namespace SpellBubbleModToolHelper;

public partial class BridgeLib
{
    [UnmanagedCallersOnly(EntryPoint = "get_area_music_list")]
    public static DualArrayWrapper GetAreaAndMusicList(IntPtr shareDataPath) // string shareDataPath
    {
        var path = Marshal.PtrToStringUTF8(shareDataPath);
        var (am, _, assets) = LoadAssetsFromBundlePath(path);
        var info = assets.table.GetAssetInfo("TPZ_MusicData");
        var baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
        var musicList = baseField.Get("sheets").Get(0).Get(0).Get("list").Get(0).GetChildrenList();

        var musicIDSet = new SortedSet<string>();
        var areaIDSet = new SortedSet<string>();

        foreach (var musicItem in musicList)
        {
            musicIDSet.Add(musicItem.Get("ID").GetValue().AsString());
            areaIDSet.Add(musicItem.Get("Area").GetValue().AsString());
        }

        var musicIDs = musicIDSet.ToArray().Select(Marshal.StringToCoTaskMemUTF8).ToArray();
        var areaIDs = areaIDSet.ToArray().Select(Marshal.StringToCoTaskMemUTF8).ToArray();

        var result = new ArrayWrapper[2];
        result[0] = ArrayToWrapper_IntPtr(musicIDs);
        result[1] = ArrayToWrapper_IntPtr(areaIDs);
        return PackWrappers(result[0], result[1]); // Array[Array[String]]
    }

    [UnmanagedCallersOnly(EntryPoint = "get_dlc_list")]
    public static ArrayWrapper GetDlcList(IntPtr shareDataPath)
    {
        var path = Marshal.PtrToStringUTF8(shareDataPath);
        var (am, _, assets) = LoadAssetsFromBundlePath(path);
        var info = assets.table.GetAssetInfo("TPZ_DLCData");
        var baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
        var dlcList = baseField.Get("sheets").Get(0).Get(0).Get("list").Get(0).GetChildrenList();

        var dlcNames = dlcList.Select(dlc => dlc.Get("Name").GetValue().AsString())
            .Select(Marshal.StringToCoTaskMemUTF8).ToArray();
        return ArrayToWrapper_IntPtr(dlcNames);
    }

    [UnmanagedCallersOnly(EntryPoint = "get_music_info")]
    public static DualArrayWrapper GetMusicInfo(IntPtr romFsPath)
    {
        var romFsPathStr = Marshal.PtrToStringUTF8(romFsPath);
        var shareDataPath = romFsPathStr + "/StreamingAssets/Switch/share_data";

        var (am, _, assets) = LoadAssetsFromBundlePath(shareDataPath);
        var info = assets.table.GetAssetInfo("TPZ_MusicData");
        var baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
        var musicList = baseField.Get("sheets").Get(0).Get(0).Get("list").Get(0).GetChildrenList();

        var ids = new List<string>();

        var musics = musicList.Where(musicItem => musicItem.Get("IsGame").GetValue().AsInt() == 1).Select(
            musicItem =>
            {
                var id = musicItem.Get("ID").GetValue().AsString();
                ids.Add(id);

                var area = musicItem.Get("Area").GetValue().AsString();
                var bpm = musicItem.Get("BPM").GetValue().AsFloat();
                var length = musicItem.Get("Length").GetValue().AsInt();
                var offset = musicItem.Get("Offset").GetValue().AsFloat();
                var dlcIdx = musicItem.Get("DLCIndex").GetValue().AsInt();

                var wordInfo = assets.table.GetAssetInfo("TPZ_WordData");
                var wordBaseField = am.GetTypeInstance(assets.file, wordInfo).GetBaseField();

                var wordFieldArray = wordBaseField.Get("sheets").Get(0).GetChildrenList();
                var titleFieldList = Array.Find(wordFieldArray, a =>
                    a.Get("name").GetValue().AsString() == "MusicTitle"
                ).Get("list").Get(0).GetChildrenList();
                var subTitleFieldList = Array.Find(wordFieldArray, a =>
                    a.Get("name").GetValue().AsString() == "MusicSubTitle"
                ).Get("list").Get(0).GetChildrenList();
                var titleKanaFieldList = Array.Find(wordFieldArray, a =>
                    a.Get("name").GetValue().AsString() == "MusicTitleKana"
                ).Get("list").Get(0).GetChildrenList();
                var artistFieldList = Array.Find(wordFieldArray, a =>
                    a.Get("name").GetValue().AsString() == "MusicArtist"
                ).Get("list").Get(0).GetChildrenList();
                var artist2FieldList = Array.Find(wordFieldArray, a =>
                    a.Get("name").GetValue().AsString() == "MusicArtist2"
                ).Get("list").Get(0).GetChildrenList();
                var artistKanaFieldList = Array.Find(wordFieldArray, a =>
                    a.Get("name").GetValue().AsString() == "MusicArtistKana"
                ).Get("list").Get(0).GetChildrenList();
                var originalFieldList = Array.Find(wordFieldArray, a =>
                    a.Get("name").GetValue().AsString() == "MusicOriginal"
                ).Get("list").Get(0).GetChildrenList();

                var titleSongField = Array.Find(titleFieldList, f => f.Get("key").GetValue().AsString() == id);
                var subTitleSongField =
                    Array.Find(subTitleFieldList, f => f.Get("key").GetValue().AsString() == id);
                var titleKanaSongField =
                    Array.Find(titleKanaFieldList, f => f.Get("key").GetValue().AsString() == id);
                var artistSongField = Array.Find(artistFieldList, f => f.Get("key").GetValue().AsString() == id);
                var artist2SongField = Array.Find(artist2FieldList, f => f.Get("key").GetValue().AsString() == id);
                var artistKanaSongField =
                    Array.Find(artistKanaFieldList, f => f.Get("key").GetValue().AsString() == id);
                var originalSongField =
                    Array.Find(originalFieldList, f => f.Get("key").GetValue().AsString() == id);

                var wordEntryStrs = new[] {"ja", "en", "ko", "chs", "cht"}.Select(lang => new WordEntryStr
                {
                    lang = lang,
                    title = titleSongField.Get(lang).GetValue().AsString(),
                    subTitle = subTitleSongField.Get(lang).GetValue().AsString(),
                    titleKana = titleKanaSongField.Get(lang).GetValue().AsString(),
                    artist = artistSongField.Get(lang).GetValue().AsString(),
                    artist2 = artist2SongField.Get(lang).GetValue().AsString(),
                    artistKana = artistKanaSongField.Get(lang).GetValue().AsString(),
                    original = originalSongField.Get(lang).GetValue().AsString()
                });

                var wordEntries =
                    ArrayToWrapper_Struct(wordEntryStrs.Select(wes => new WordEntry(wes, true)).ToArray());

                var musicEntry = new MusicEntry
                {
                    managed = 1,
                    area = Marshal.StringToCoTaskMemUTF8(area),
                    bpm = bpm,
                    length = (ushort) length,
                    dlcIdx = (ushort) dlcIdx,
                    offset = offset
                };

                var songEntry = new SongEntry
                {
                    managed = 1,
                    id = Marshal.StringToCoTaskMemUTF8(id),
                    musicEntry = musicEntry,
                    wordEntry = wordEntries
                };

                return songEntry;
            });

        var musicsWrapper = ArrayToWrapper_Struct(musics.ToArray());

        var scoreData = new List<List<string>>();
        foreach (var id in ids)
        {
            var songScoreData = new List<string>();
            var scorePath = romFsPathStr + "/StreamingAssets/Switch/share_scores/score_" + id.ToLower();
            var (scoreAm, _, scoreAssets) = LoadAssetsFromBundlePath(scorePath);

            try
            {
                foreach (var assetName in new[]
                             {$"{id}_beat", $"{id}_rhythm_Easy", $"{id}_rhythm_Normal", $"{id}_rhythm_Hard"})
                {
                    var scoreInfo = scoreAssets.table.GetAssetInfo(assetName);
                    var scoreBaseField = scoreAm.GetTypeInstance(scoreAssets.file, scoreInfo).GetBaseField();

                    var content = scoreBaseField.Get("m_Script").GetValue().AsStringBytes();
                    songScoreData.Add(Encoding.Unicode.GetString(content).TrimStart('\uFEFF'));
                }
            }
            // Some assets are packed using pure lowercase IDs
            catch (Exception)
            {
                var newId = id.ToLower();
                foreach (var assetName in new[]
                         {
                             $"{newId}_beat", $"{newId}_rhythm_Easy", $"{newId}_rhythm_Normal",
                             $"{newId}_rhythm_Hard"
                         })
                {
                    var scoreInfo = scoreAssets.table.GetAssetInfo(assetName);
                    var scoreBaseField = scoreAm.GetTypeInstance(scoreAssets.file, scoreInfo).GetBaseField();

                    var content = scoreBaseField.Get("m_Script").GetValue().AsStringBytes();
                    songScoreData.Add(Encoding.Unicode.GetString(content).TrimStart('\uFEFF'));
                }
            }

            if (songScoreData.Count != 4) songScoreData = songScoreData.Take(4).ToList();

            scoreData.Add(songScoreData);
        }

        var scoreDataArray = ArrayToWrapper_Struct(scoreData
            .Select(song => ArrayToWrapper_IntPtr(song.Select(Marshal.StringToCoTaskMemUTF8).ToArray())).ToArray());

        return PackWrappers(musicsWrapper, scoreDataArray);
    }

    [UnmanagedCallersOnly(EntryPoint = "get_metadata_regions")]
    public static MetadataInformation GetMetadataRegions(IntPtr globalMetadataPath)
    {
        var globalMetadataPathStr = Marshal.PtrToStringUTF8(globalMetadataPath);

        var metadataBytes = File.ReadAllBytes(globalMetadataPathStr);
        var metadata = new Metadata(new MemoryStream(metadataBytes));

        var baField = metadata.fieldDefs.Single(fd => metadata.GetStringFromIndex(fd.nameIndex) == "BadApple");
        var eMusicIDTypeIdx = baField.typeIndex;
        var eMusicIDTypeDef =
            metadata.typeDefs.Single(td => metadata.GetStringFromIndex(td.nameIndex) == "eMusicID");
        var eMusicTypeDefIndex = Array.IndexOf(metadata.typeDefs, eMusicIDTypeDef);

        var tutorialField = metadata.fieldDefs.Select((x, i) => (x, i)).Single(fd =>
            fd.x.typeIndex == eMusicIDTypeIdx && metadata.GetStringFromIndex(fd.x.nameIndex) == "Tutorial");
        metadata.GetFieldDefaultValueFromIndex(tutorialField.i, out var tutorialFieldDefaultValue);
        var pointer = metadata.GetDefaultValueFromIndex(tutorialFieldDefaultValue.dataIndex);
        metadata.Position = pointer;
        var tutorialFieldValue = (uint) metadata.Reader.ReadInt32();

        var fieldNames = new[]
            {"Tutorial", "Menu", "Select", "Map", "Shop", "Calibration", "Result", "NUM", "NONE"};
        var fieldValueDataOffsets = fieldNames.Select(name =>
        {
            var field = metadata.fieldDefs.Select((x, i) => (x, i)).Single(fd =>
                fd.x.typeIndex == eMusicIDTypeIdx && metadata.GetStringFromIndex(fd.x.nameIndex) == name);
            metadata.GetFieldDefaultValueFromIndex(field.i, out var fieldDefaultValue);
            return fieldDefaultValue.dataIndex;
        }).ToArray();

        var maxFieldDefToken = metadata.fieldDefs.Select(fd => fd.token).Max();

        return new MetadataInformation
        {
            typeDefinitionsHeaderOffset = metadata.header.typeDefinitionsOffset,
            eMusicIDTypeIndex = (uint) eMusicIDTypeIdx,
            eMusicIDFieldStart = (uint) eMusicIDTypeDef.fieldStart,
            eMusicIDFieldCount = eMusicIDTypeDef.field_count,
            eMusicIDTypeDefIndex = (uint) eMusicTypeDefIndex,
            eMusicIDTutorialValue = tutorialFieldValue,
            eMusicIDValueDataOffsets = ArrayToWrapper_int(fieldValueDataOffsets),
            stringTableOffset = metadata.header.stringOffset,
            stringTableLength = (uint) metadata.header.stringSize,
            stringOffsetHeaderOffset = 6 * 4,
            fieldDefTableOffset = metadata.header.fieldsOffset,
            fieldDefTableLength = (uint) metadata.header.fieldsSize,
            fieldDefOffsetHeaderOffset = 24 * 4,
            maxFieldDefToken = maxFieldDefToken,
            maxFieldIndex = (uint) metadata.fieldDefs.Length - 1,
            fieldDefaultValueTableOffset = metadata.header.fieldDefaultValuesOffset,
            fieldDefaultValueTableLength = (uint) metadata.header.fieldDefaultValuesSize,
            fieldDefaultValueOffsetHeaderOffset = 16 * 4,
            defaultValueDataTableOffset = metadata.header.fieldAndParameterDefaultValueDataOffset,
            defaultValueDataTableLength = (uint) metadata.header.fieldAndParameterDefaultValueDataSize,
            defaultValueDataOffsetHeaderOffset = 18 * 4
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MetadataInformation
    {
        public uint typeDefinitionsHeaderOffset;

        public uint eMusicIDTypeIndex;
        public uint eMusicIDFieldStart;
        public ushort eMusicIDFieldCount;
        public uint eMusicIDTypeDefIndex;

        public uint eMusicIDTutorialValue;

        // "Tutorial", "Menu", "Select", "Map", "Shop", "Calibration", "Result", "NUM", "NONE"
        public ArrayWrapper eMusicIDValueDataOffsets;

        public uint stringTableOffset;
        public uint stringTableLength;
        public uint stringOffsetHeaderOffset;

        public uint fieldDefTableOffset;
        public uint fieldDefTableLength;
        public uint fieldDefOffsetHeaderOffset;
        public uint maxFieldDefToken;
        public uint maxFieldIndex;

        public uint fieldDefaultValueTableOffset;
        public uint fieldDefaultValueTableLength;
        public uint fieldDefaultValueOffsetHeaderOffset;

        public uint defaultValueDataTableOffset;
        public uint defaultValueDataTableLength;
        public uint defaultValueDataOffsetHeaderOffset;
    }
}