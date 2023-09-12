using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace SpellBubbleModToolHelper;

public partial class BridgeLib
{
    [UnmanagedCallersOnly(EntryPoint = "add_share_data_music_data")]
    public static void AddShareDataMusicData(IntPtr shareDataPathPtr, IntPtr outShareDataPathPtr,
        ArrayWrapper param)
    {
        var shareDataPath = Marshal.PtrToStringUTF8(shareDataPathPtr);
        var outShareDataPath = Marshal.PtrToStringUTF8(outShareDataPathPtr);

        var (am, bundle, assets) = LoadAssetsFromBundlePath(shareDataPath);
        var replacerList = new List<AssetsReplacer>();

        var musicInfo = assets.table.GetAssetInfo("TPZ_MusicData");
        var musicBaseField = am.GetTypeInstance(assets.file, musicInfo).GetBaseField();

        var wordInfo = assets.table.GetAssetInfo("TPZ_WordData");
        var wordBaseField = am.GetTypeInstance(assets.file, wordInfo).GetBaseField();

        var songEntries = WrapperToArray_Struct<SongEntry>(param);

        var musicField = musicBaseField.Get("sheets").Get(0).Get(0).Get("list").Get(0);
        var musicFieldList = musicField.GetChildrenList().ToList();
        var maxRelease = musicFieldList.Select(f => f.Get("Release").GetValue().AsInt()).Max();

        var wordFieldArray = wordBaseField.Get("sheets").Get(0).GetChildrenList();

        var titleField = Array.Find(wordFieldArray, a => a.Get("name").GetValue().AsString() == "MusicTitle")
            .Get("list").Get(0);
        var titleFieldList = titleField.GetChildrenList().ToList();

        var subTitleField = Array.Find(wordFieldArray, a => a.Get("name").GetValue().AsString() == "MusicSubTitle")
            .Get("list").Get(0);
        var subTitleFieldList = subTitleField.GetChildrenList().ToList();

        var titleKanaField = Array
            .Find(wordFieldArray, a => a.Get("name").GetValue().AsString() == "MusicTitleKana").Get("list").Get(0);
        var titleKanaFieldList = titleKanaField.GetChildrenList().ToList();

        var artistField = Array.Find(wordFieldArray, a => a.Get("name").GetValue().AsString() == "MusicArtist")
            .Get("list").Get(0);
        var artistFieldList = artistField.GetChildrenList().ToList();

        var artist2Field = Array.Find(wordFieldArray, a => a.Get("name").GetValue().AsString() == "MusicArtist2")
            .Get("list").Get(0);
        var artist2FieldList = artist2Field.GetChildrenList().ToList();

        var artistKanaField = Array
            .Find(wordFieldArray, a => a.Get("name").GetValue().AsString() == "MusicArtistKana").Get("list").Get(0);
        var artistKanaFieldList = artistKanaField.GetChildrenList().ToList();

        var originalField = Array.Find(wordFieldArray, a => a.Get("name").GetValue().AsString() == "MusicOriginal")
            .Get("list").Get(0);
        var originalFieldList = originalField.GetChildrenList().ToList();

        var musicFieldsToAdd = new List<AssetTypeValueField>();
        var titleFieldsToAdd = new List<AssetTypeValueField>();
        var subTitleFieldsToAdd = new List<AssetTypeValueField>();
        var titleKanaFieldsToAdd = new List<AssetTypeValueField>();
        var artistFieldsToAdd = new List<AssetTypeValueField>();
        var artist2FieldsToAdd = new List<AssetTypeValueField>();
        var artistKanaFieldsToAdd = new List<AssetTypeValueField>();
        var originalFieldsToAdd = new List<AssetTypeValueField>();

        var musicSongTemplateField = musicFieldList.Single(f => f.Get("ID").GetValue().AsString() == "Karisuma");
        var titleSongTemplateField = titleFieldList.Single(f => f.Get("key").GetValue().AsString() == "Karisuma");
        var subTitleSongTemplateField =
            subTitleFieldList.Single(f => f.Get("key").GetValue().AsString() == "Karisuma");
        var titleKanaSongTemplateField =
            titleKanaFieldList.Single(f => f.Get("key").GetValue().AsString() == "Karisuma");
        var artistSongTemplateField = artistFieldList.Single(f => f.Get("key").GetValue().AsString() == "Karisuma");
        var artist2SongTemplateField =
            artist2FieldList.Single(f => f.Get("key").GetValue().AsString() == "Karisuma");
        var artistKanaSongTemplateField =
            artistKanaFieldList.Single(f => f.Get("key").GetValue().AsString() == "Karisuma");
        var originalSongTemplateField =
            originalFieldList.Single(f => f.Get("key").GetValue().AsString() == "Karisuma");

        foreach (var songEntry in songEntries)
        {
            var songID = Marshal.PtrToStringUTF8(songEntry.id);
            var musicEntry = songEntry.musicEntry;

            var musicSongField = (AssetTypeValueField) musicSongTemplateField.Clone();
            musicSongField.Get("ID").GetValue().Set(songID);

            var area = Marshal.PtrToStringUTF8(musicEntry.area);
            if (area.Length != 0) musicSongField.Get("Area").GetValue().Set(area);

            musicSongField.Get("BPM").GetValue().Set(musicEntry.bpm);
            musicSongField.Get("Length").GetValue().Set(musicEntry.length);
            musicSongField.Get("Offset").GetValue().Set(musicEntry.offset);
            musicSongField.Get("Release").GetValue().Set(maxRelease + 1);
            musicSongField.Get("DLCIndex").GetValue().Set(0);
            musicSongField.Get("IsDefault").GetValue().Set(1);
            musicSongField.Get("Price").GetValue().Set(0);

            musicFieldsToAdd.Add(musicSongField);

            var titleSongField = (AssetTypeValueField) titleSongTemplateField.Clone();
            var subTitleSongField = (AssetTypeValueField) subTitleSongTemplateField.Clone();
            var titleKanaSongField = (AssetTypeValueField) titleKanaSongTemplateField.Clone();
            var artistSongField = (AssetTypeValueField) artistSongTemplateField.Clone();
            var artist2SongField = (AssetTypeValueField) artist2SongTemplateField.Clone();
            var artistKanaSongField = (AssetTypeValueField) artistKanaSongTemplateField.Clone();
            var originalSongField = (AssetTypeValueField) originalSongTemplateField.Clone();

            foreach (var field in new[]
                     {
                         titleSongField, subTitleSongField, titleKanaSongField, artistSongField, artist2SongField,
                         artistKanaSongField, originalSongField
                     })
            {
                field.Get("key").GetValue().Set(songID);
                foreach (var lang in new[] {"ja", "en", "ko", "chs", "cht"}) field.Get(lang).GetValue().Set("");
            }

            var wordEntries = WrapperToArray_Struct<WordEntry>(songEntry.wordEntry).Select(e => new WordEntryStr(e))
                .ToArray();

            foreach (var wordEntry in wordEntries)
            {
                titleSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.title);
                subTitleSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.subTitle);
                titleKanaSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.titleKana);
                artistSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.artist);
                artist2SongField.Get(wordEntry.lang).GetValue().Set(wordEntry.artist2);
                artistKanaSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.artistKana);
                originalSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.original);
            }

            titleFieldsToAdd.Add(titleSongField);
            subTitleFieldsToAdd.Add(subTitleSongField);
            titleKanaFieldsToAdd.Add(titleKanaSongField);
            artistFieldsToAdd.Add(artistSongField);
            artist2FieldsToAdd.Add(artist2SongField);
            artistKanaFieldsToAdd.Add(artistKanaSongField);
            originalFieldsToAdd.Add(originalSongField);
        }

        musicFieldList.AddRange(musicFieldsToAdd);
        musicField.SetChildrenList(musicFieldList.ToArray());

        titleFieldList.AddRange(titleFieldsToAdd);
        titleField.SetChildrenList(titleFieldList.ToArray());

        subTitleFieldList.AddRange(subTitleFieldsToAdd);
        subTitleField.SetChildrenList(subTitleFieldList.ToArray());

        titleKanaFieldList.AddRange(titleKanaFieldsToAdd);
        titleKanaField.SetChildrenList(titleKanaFieldList.ToArray());

        artistFieldList.AddRange(artistFieldsToAdd);
        artistField.SetChildrenList(artistFieldList.ToArray());

        artist2FieldList.AddRange(artist2FieldsToAdd);
        artist2Field.SetChildrenList(artist2FieldList.ToArray());

        artistKanaFieldList.AddRange(artistKanaFieldsToAdd);
        artistKanaField.SetChildrenList(artistKanaFieldList.ToArray());

        originalFieldList.AddRange(originalFieldsToAdd);
        originalField.SetChildrenList(originalFieldList.ToArray());

        var musicBytes = musicBaseField.WriteToByteArray();
        var wordBytes = wordBaseField.WriteToByteArray();
        var musicAssetsReplacer = new AssetsReplacerFromMemory(0, musicInfo.index, (int) musicInfo.curFileType,
            AssetHelper.GetScriptIndex(assets.file, musicInfo), musicBytes);
        var wordAssetsReplacer = new AssetsReplacerFromMemory(0, wordInfo.index, (int) wordInfo.curFileType,
            AssetHelper.GetScriptIndex(assets.file, wordInfo), wordBytes);

        replacerList.Add(musicAssetsReplacer);
        replacerList.Add(wordAssetsReplacer);

        PatchAssetBundle(bundle, assets, replacerList, outShareDataPath);
    }

    [UnmanagedCallersOnly(EntryPoint = "patch_share_data_music_data")]
    public static void PatchShareDataMusicData(IntPtr shareDataPathPtr, IntPtr outShareDataPathPtr,
        ArrayWrapper param)
    {
        var shareDataPath = Marshal.PtrToStringUTF8(shareDataPathPtr);
        var outShareDataPath = Marshal.PtrToStringUTF8(outShareDataPathPtr);

        var (am, bundle, assets) = LoadAssetsFromBundlePath(shareDataPath);
        var replacerList = new List<AssetsReplacer>();

        var musicInfo = assets.table.GetAssetInfo("TPZ_MusicData");
        var musicBaseField = am.GetTypeInstance(assets.file, musicInfo).GetBaseField();

        var wordInfo = assets.table.GetAssetInfo("TPZ_WordData");
        var wordBaseField = am.GetTypeInstance(assets.file, wordInfo).GetBaseField();

        var songEntries = WrapperToArray_Struct<SongEntry>(param);

        var musicFieldList = musicBaseField.Get("sheets").Get(0).Get(0).Get("list").Get(0).GetChildrenList();
        var maxRelease = musicFieldList.Select(f => f.Get("Release").GetValue().AsInt()).Max();

        var wordFieldArray = wordBaseField.Get("sheets").Get(0).GetChildrenList();
        var titleFieldList = Array.Find(wordFieldArray,
            a => a.Get("name").GetValue().AsString() == "MusicTitle").Get("list").Get(0).GetChildrenList();
        var subTitleFieldList = Array.Find(wordFieldArray,
            a => a.Get("name").GetValue().AsString() == "MusicSubTitle").Get("list").Get(0).GetChildrenList();
        var titleKanaFieldList = Array.Find(wordFieldArray,
            a => a.Get("name").GetValue().AsString() == "MusicTitleKana").Get("list").Get(0).GetChildrenList();
        var artistFieldList = Array.Find(wordFieldArray,
            a => a.Get("name").GetValue().AsString() == "MusicArtist").Get("list").Get(0).GetChildrenList();
        var artist2FieldList = Array.Find(wordFieldArray,
            a => a.Get("name").GetValue().AsString() == "MusicArtist2").Get("list").Get(0).GetChildrenList();
        var artistKanaFieldList = Array.Find(wordFieldArray,
            a => a.Get("name").GetValue().AsString() == "MusicArtistKana").Get("list").Get(0).GetChildrenList();
        var originalFieldList = Array.Find(wordFieldArray,
            a => a.Get("name").GetValue().AsString() == "MusicOriginal").Get("list").Get(0).GetChildrenList();

        foreach (var songEntry in songEntries)
        {
            var songID = Marshal.PtrToStringUTF8(songEntry.id);
            var musicEntry = songEntry.musicEntry;

            var musicSongField = Array.Find(musicFieldList, f => f.Get("ID").GetValue().AsString() == songID);

            var area = Marshal.PtrToStringUTF8(musicEntry.area);
            if (area.Length != 0) musicSongField.Get("Area").GetValue().Set(area);

            musicSongField.Get("BPM").GetValue().Set(musicEntry.bpm);
            musicSongField.Get("Length").GetValue().Set(musicEntry.length);
            musicSongField.Get("Offset").GetValue().Set(musicEntry.offset);
            musicSongField.Get("Release").GetValue().Set(maxRelease + 1);
            musicSongField.Get("DLCIndex").GetValue().Set(0);
            musicSongField.Get("IsDefault").GetValue().Set(1);
            musicSongField.Get("Price").GetValue().Set(0);

            var titleSongField = Array.Find(titleFieldList, f => f.Get("key").GetValue().AsString() == songID);
            var subTitleSongField =
                Array.Find(subTitleFieldList, f => f.Get("key").GetValue().AsString() == songID);
            var titleKanaSongField =
                Array.Find(titleKanaFieldList, f => f.Get("key").GetValue().AsString() == songID);
            var artistSongField = Array.Find(artistFieldList, f => f.Get("key").GetValue().AsString() == songID);
            var artist2SongField = Array.Find(artist2FieldList, f => f.Get("key").GetValue().AsString() == songID);
            var artistKanaSongField =
                Array.Find(artistKanaFieldList, f => f.Get("key").GetValue().AsString() == songID);
            var originalSongField =
                Array.Find(originalFieldList, f => f.Get("key").GetValue().AsString() == songID);

            var wordEntries = WrapperToArray_Struct<WordEntry>(songEntry.wordEntry).Select(e => new WordEntryStr(e))
                .ToArray();

            foreach (var wordEntry in wordEntries)
            {
                titleSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.title);
                subTitleSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.subTitle);
                titleKanaSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.titleKana);
                artistSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.artist);
                artist2SongField.Get(wordEntry.lang).GetValue().Set(wordEntry.artist2);
                artistKanaSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.artistKana);
                originalSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.original);
            }
        }

        var musicBytes = musicBaseField.WriteToByteArray();
        var wordBytes = wordBaseField.WriteToByteArray();
        var musicAssetsReplacer = new AssetsReplacerFromMemory(0, musicInfo.index, (int) musicInfo.curFileType,
            AssetHelper.GetScriptIndex(assets.file, musicInfo), musicBytes);
        var wordAssetsReplacer = new AssetsReplacerFromMemory(0, wordInfo.index, (int) wordInfo.curFileType,
            AssetHelper.GetScriptIndex(assets.file, wordInfo), wordBytes);

        replacerList.Add(musicAssetsReplacer);
        replacerList.Add(wordAssetsReplacer);

        PatchAssetBundle(bundle, assets, replacerList, outShareDataPath);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SongEntry
    {
        public uint managed;
        public IntPtr id;
        public MusicEntry musicEntry;
        public ArrayWrapper wordEntry;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MusicEntry
    {
        public uint managed;
        public IntPtr area;
        public float bpm;
        public ushort length;
        public ushort dlcIdx;
        public float offset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WordEntry
    {
        public uint managed;
        public IntPtr lang;
        public IntPtr title;
        public IntPtr subTitle;
        public IntPtr titleKana;
        public IntPtr artist;
        public IntPtr artist2;
        public IntPtr artistKana;
        public IntPtr original;

        public WordEntry(WordEntryStr wordEntryStr, bool managed)
        {
            this.managed = (uint) (managed ? 1 : 0);
            lang = Marshal.StringToCoTaskMemUTF8(wordEntryStr.lang);
            title = Marshal.StringToCoTaskMemUTF8(wordEntryStr.title);
            subTitle = Marshal.StringToCoTaskMemUTF8(wordEntryStr.subTitle);
            titleKana = Marshal.StringToCoTaskMemUTF8(wordEntryStr.titleKana);
            artist = Marshal.StringToCoTaskMemUTF8(wordEntryStr.artist);
            artist2 = Marshal.StringToCoTaskMemUTF8(wordEntryStr.artist2);
            artistKana = Marshal.StringToCoTaskMemUTF8(wordEntryStr.artistKana);
            original = Marshal.StringToCoTaskMemUTF8(wordEntryStr.original);
        }
    }

    public struct WordEntryStr
    {
        public string lang;
        public string title;
        public string subTitle;
        public string titleKana;
        public string artist;
        public string artist2;
        public string artistKana;
        public string original;

        public WordEntryStr(WordEntry wordEntry)
        {
            lang = Marshal.PtrToStringUTF8(wordEntry.lang);
            title = Marshal.PtrToStringUTF8(wordEntry.title);
            subTitle = Marshal.PtrToStringUTF8(wordEntry.subTitle);
            titleKana = Marshal.PtrToStringUTF8(wordEntry.titleKana);
            artist = Marshal.PtrToStringUTF8(wordEntry.artist);
            artist2 = Marshal.PtrToStringUTF8(wordEntry.artist2);
            artistKana = Marshal.PtrToStringUTF8(wordEntry.artistKana);
            original = Marshal.PtrToStringUTF8(wordEntry.original);
        }
    }
}