using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SonicAudioLib.Archives;
using SonicAudioLib.CriMw;
using VGAudio.Containers.Hca;
using VGAudio.Containers.Wave;

namespace SpellBubbleModToolHelper
{
    public class BridgeLib
    {
        private static string classPackagePath;

        [StructLayout(LayoutKind.Sequential)]
        public struct ArrayWrapper
        {
            public uint size;
            public IntPtr array; // The type of array element is defined as "usize" in Rust
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DualArrayWrapper
        {
            public uint size;
            public IntPtr array;
            public uint size2;
            public IntPtr array2;
        }

        private static DualArrayWrapper PackWrappers(ArrayWrapper wrapper1, ArrayWrapper wrapper2)
        {
            var dualWrapper = new DualArrayWrapper
            {
                size = wrapper1.size,
                array = wrapper1.array,
                size2 = wrapper2.size,
                array2 = wrapper2.array
            };
            return dualWrapper;
        }

        [UnmanagedCallersOnly(EntryPoint = "initialize")]
        public static void Initialize(IntPtr classPackagePathPtr) // string classPackagePathPtr
        {
            // Encoding Setup
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            classPackagePath = Marshal.PtrToStringUTF8(classPackagePathPtr);
        }

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

        private static (AssetsManager, BundleFileInstance, AssetsFileInstance) LoadAssetsFromBundlePath(
            string assetBundlePath)
        {
            var am = new AssetsManager();
            am.LoadClassPackage(classPackagePath);
            var assetBundle = am.LoadBundleFile(assetBundlePath, unpackIfPacked: true);
            am.LoadClassDatabaseFromPackage(am.LoadAssetsFileFromBundle(assetBundle, 0).file.typeTree.unityVersion);
            var assets = am.LoadAssetsFileFromBundle(assetBundle, 0);
            return (am, assetBundle, assets);
        }

        private static ArrayWrapper ArrayToWrapper_IntPtr(IntPtr[] array)
        {
            var arrayPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<IntPtr>() * array.Length);
            Marshal.Copy(array, 0, arrayPointer, array.Length);
            var wrapper = new ArrayWrapper();
            wrapper.size = (uint) array.Length;
            wrapper.array = arrayPointer;
            return wrapper;
        }

        private static int[] WrapperToArray_Int(ArrayWrapper wrapper)
        {
            var array = new int[wrapper.size];
            Marshal.Copy(wrapper.array, array, 0, (int) wrapper.size);
            return array;
        }

        [UnmanagedCallersOnly(EntryPoint = "patch_special_rules")]
        public static void PatchSpecialRule(IntPtr shareDataPath, IntPtr outPath) // string shareDataPath, outPath
        {
            var path = Marshal.PtrToStringUTF8(shareDataPath);
            var outputPath = Marshal.PtrToStringUTF8(outPath);
            
            var (am, bundle, assets) = LoadAssetsFromBundlePath(path);
            var replacerList = new List<AssetsReplacer>();
            
            var specialRuleInfo = assets.table.GetAssetInfo("TPZ_SpecialRuleData");
            var specialRuleBaseField = am.GetTypeInstance(assets.file, specialRuleInfo).GetBaseField();

            UnlockSpecialRules(ref specialRuleBaseField);

            var specialRuleNewBytes = specialRuleBaseField.WriteToByteArray();
            var specialRuleReplacer = new AssetsReplacerFromMemory(0, specialRuleInfo.index, (int) specialRuleInfo.curFileType,
                AssetHelper.GetScriptIndex(assets.file, specialRuleInfo), specialRuleNewBytes);
            
            replacerList.Add(specialRuleReplacer);
            PatchAssetBundle(bundle, assets, replacerList, outputPath);
        }

        private static void UnlockSpecialRules(ref AssetTypeValueField baseField)
        {
            var excludePrefixList = new string[] { "TwoColors", "FourColors", "NONE" };
            
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
        
        [UnmanagedCallersOnly(EntryPoint = "patch_music_and_character")]
        public static void PatchMusicAndCharacter(IntPtr shareDataPath, IntPtr outPath, ArrayWrapper excludedDLCs,
            IntPtr leftMusicID, int characterEnabled, int characterTargetDLC) 
        // string shareDataPath, outPath, int[] excludedDLCs, string leftMusicID
        // bool characterEnabled, int characterTargetDLC
        {
            var path = Marshal.PtrToStringUTF8(shareDataPath);
            var outputPath = Marshal.PtrToStringUTF8(outPath);
            var excludedDLCIds = WrapperToArray_Int(excludedDLCs); // TODO: Split it for music and character
            var leftMusic = Marshal.PtrToStringUTF8(leftMusicID);

            var (am, bundle, assets) = LoadAssetsFromBundlePath(path);
            var replacerList = new List<AssetsReplacer>();

            var musicInfo = assets.table.GetAssetInfo("TPZ_MusicData");
            var musicBaseField = am.GetTypeInstance(assets.file, musicInfo).GetBaseField();

            UnlockDLCsForMusic(ref musicBaseField, excludedDLCIds, leftMusic);

            var musicNewBytes = musicBaseField.WriteToByteArray();
            var musicAssetReplacer = new AssetsReplacerFromMemory(0, musicInfo.index, (int) musicInfo.curFileType,
                AssetHelper.GetScriptIndex(assets.file, musicInfo), musicNewBytes);
            replacerList.Add(musicAssetReplacer);

            if (characterEnabled != 0)
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

            PatchAssetBundle(bundle, assets, replacerList, outputPath);
        }

        private static void UnlockDLCsForMusic(ref AssetTypeValueField baseField, int[] excludedDLCIds,
            string leftMusic)
        {
            var musicList = baseField.Get("sheets").Get(0).Get(0).Get("list").Get(0).GetChildrenList();

            var fallBackID = "Lostword";
            var leftFlag = false;

            foreach (var musicItem in musicList)
            {
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
                    {
                        musicItem.Get("DLCIndex").GetValue().Set(0);
                    }
                }
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
                if (excludedDLCIds.Contains(characterItem.Get("DLCIndex").GetValue().AsInt()))
                {
                    continue;
                }
                
                if (characterItem.Get("DLCIndex").GetValue().AsInt() != 0)
                {
                    characterItem.Get("DLCIndex").GetValue().Set(characterTargetDLC);
                }
            }
        }

        private static void PatchAssetBundle(BundleFileInstance bundle, AssetsFileInstance assets,
            List<AssetsReplacer> replacer, string outputPath)
        {
            byte[] newAssetsData;
            using (var stream = new MemoryStream())
            using (var writer = new AssetsFileWriter(stream))
            {
                assets.file.Write(writer, 0, replacer, 0);
                newAssetsData = stream.ToArray();
            }

            var bundleReplacer = new BundleReplacerFromMemory(assets.name, null, true, newAssetsData, -1);

            using (var bundleWriter = new AssetsFileWriter(File.OpenWrite(outputPath)))
            using (var newStream = new MemoryStream())
            using (var writer = new AssetsFileWriter(newStream))
            {
                bundle.file.Write(writer, new List<BundleReplacer> {bundleReplacer});
                using (var reader = new AssetsFileReader(newStream))
                {
                    var newBundle = new AssetBundleFile();
                    newBundle.Read(new AssetsFileReader(newStream), false);
                    newBundle.Pack(reader, bundleWriter, AssetBundleCompressionType.LZMA);
                }
            }
        }

        [UnmanagedCallersOnly(EntryPoint = "patch_acb")]
        public static void PatchAcb(IntPtr wavPathPtr, IntPtr acbPathPtr, IntPtr acbOutPathPtr, IntPtr awbOutPathPtr)
        {
            var wavPath = Marshal.PtrToStringUTF8(wavPathPtr);
            var acbPath = Marshal.PtrToStringUTF8(acbPathPtr);
            var acbOutPath = Marshal.PtrToStringUTF8(acbOutPathPtr);
            var awbOutPath = Marshal.PtrToStringUTF8(awbOutPathPtr);

            PatchAcb_Internal(wavPath, acbPath, acbOutPath, awbOutPath);
        }

        private static void PatchAcb_Internal(string wavPath, string acbPath, string acbOutPath, string awbOutPath)
        {
            var hcaPath = Path.GetTempPath() + wavPath.Split(Path.DirectorySeparatorChar).Last() + ".hca";
            using (var stream = new FileStream(wavPath, FileMode.Open, FileAccess.Read))
            {
                var audio = new WaveReader().Read(stream);
                audio.SetLoop(false);

                var config = new HcaConfiguration();
                using (var writeStream = new FileStream(hcaPath, FileMode.Create))
                {
                    new HcaWriter().WriteToStream(audio, writeStream, config);
                }
            }

            var acbFile = new CriTable();
            acbFile.Load(acbPath, 4096);

            var extAfs2Archive = new CriAfs2Archive();

            var extCpkArchive = new CriCpkArchive();
            extCpkArchive.Mode = CriCpkMode.Id;

            var awbFile = (byte[]) acbFile.Rows[0]["AwbFile"];
            var streamAwbAfs2Header = (byte[]) acbFile.Rows[0]["StreamAwbAfs2Header"];

            var cpkMode =
                !(awbFile != null && awbFile.Length >= 4 && Encoding.ASCII.GetString(awbFile, 0, 4) == "AFS2") &&
                (streamAwbAfs2Header == null || streamAwbAfs2Header.Length == 0);

            using (var reader = CriTableReader.Create((byte[]) acbFile.Rows[0]["WaveformTable"]))
            {
                while (reader.Read())
                {
                    const bool streaming = true;

                    var id =
                        reader.ContainsField("MemoryAwbId")
                            ? streaming ? reader.GetUInt16("StreamAwbId") : reader.GetUInt16("MemoryAwbId")
                            : reader.GetUInt16("Id");

                    if (cpkMode)
                    {
                        var entry = new CriCpkEntry
                        {
                            FilePath = new FileInfo(hcaPath),
                            Id = id
                        };

                        extCpkArchive.Add(entry);
                    }
                    else
                    {
                        var entry = new CriAfs2Entry
                        {
                            FilePath = new FileInfo(hcaPath),
                            Id = id
                        };

                        extAfs2Archive.Add(entry);
                    }
                }
            }
            
            acbFile.Rows[0]["AwbFile"] = null;
            acbFile.Rows[0]["StreamAwbAfs2Header"] = null;
            
            if (cpkMode)
            {
                extCpkArchive.Save(awbOutPath, 4096);
            }
            else
            {
                extAfs2Archive.Save(awbOutPath, 4096);
                
                if (Encoding.UTF8.GetString(streamAwbAfs2Header, 0, 4) == "@UTF")
                {
                    var headerTable = new CriTable();
                    headerTable.Load(streamAwbAfs2Header);

                    headerTable.Rows[0]["Header"] = extAfs2Archive.Header;
                    headerTable.WriterSettings = CriTableWriterSettings.Adx2Settings;
                    acbFile.Rows[0]["StreamAwbAfs2Header"] = headerTable.Save();
                }

                else
                {
                    acbFile.Rows[0]["StreamAwbAfs2Header"] = extAfs2Archive.Header;
                }
            }

            acbFile.WriterSettings = CriTableWriterSettings.Adx2Settings;
            acbFile.Save(acbOutPath, 4096);
            File.Delete(hcaPath);
        }

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

            var info = assets.table.GetAssetInfo($"{songID}_beat");
            var baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
            baseField.Get("m_Script").GetValue().Set("");
            var newBytes = baseField.WriteToByteArray();
            var assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
                AssetHelper.GetScriptIndex(assets.file, info), newBytes);
            replacerList.Add(assetsReplacer);

            for (var i = 0; i < paramStrArray.Length; i += 2)
            {
                var difficulty = paramStrArray[i];
                var script = paramStrArray[i + 1];

                info = assets.table.GetAssetInfo($"{songID}_rhythm_{difficulty}");
                baseField = am.GetTypeInstance(assets.file, info).GetBaseField();
                baseField.Get("m_Script").GetValue().Set(script);
                newBytes = baseField.WriteToByteArray();
                assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
                    AssetHelper.GetScriptIndex(assets.file, info), newBytes);
                replacerList.Add(assetsReplacer);
            }

            PatchAssetBundle(bundle, assets, replacerList, outScorePath);
        }

        private static IEnumerable<IntPtr> WrapperToArray_IntPtr(ArrayWrapper wrapper)
        {
            var array = new IntPtr[wrapper.size];
            Marshal.Copy(wrapper.array, array, 0, (int) wrapper.size);
            return array;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SongEntry
        {
            public IntPtr id;
            public MusicEntry musicEntry;
            public ArrayWrapper wordEntry;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MusicEntry
        {
            public IntPtr area;
            public byte starsEasy;
            public byte starsNormal;
            public byte starsHard;
            public ushort bpm;
            public ushort length;
            public float durationSec;
            public float offset;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WordEntry
        {
            public IntPtr lang;
            public IntPtr title;
            public IntPtr subTitle;
            public IntPtr artist;
            public IntPtr artist2;
            public IntPtr original;
        }

        private readonly struct WordEntryStr
        {
            public readonly string lang;
            public readonly string title;
            public readonly string subTitle;
            public readonly string artist;
            public readonly string artist2;
            public readonly string original;

            public WordEntryStr(WordEntry wordEntry)
            {
                lang = Marshal.PtrToStringUTF8(wordEntry.lang);
                title = Marshal.PtrToStringUTF8(wordEntry.title);
                subTitle = Marshal.PtrToStringUTF8(wordEntry.subTitle);
                artist = Marshal.PtrToStringUTF8(wordEntry.artist);
                artist2 = Marshal.PtrToStringUTF8(wordEntry.artist2);
                original = Marshal.PtrToStringUTF8(wordEntry.original);
            }
        }

        [UnmanagedCallersOnly(EntryPoint = "patch_share_data_res")]
        public static void PatchShareDataRes(IntPtr shareDataPathPtr, IntPtr outShareDataPathPtr, ArrayWrapper param)
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

            var wordFieldArray = wordBaseField.Get("sheets").Get(0).GetChildrenList();
            var titleFieldList = Array.Find(wordFieldArray,
                a => a.Get("name").GetValue().AsString() == "MusicTitle").Get("list").Get(0).GetChildrenList();
            var subTitleFieldList = Array.Find(wordFieldArray,
                a => a.Get("name").GetValue().AsString() == "MusicSubTitle").Get("list").Get(0).GetChildrenList();
            var artistFieldList = Array.Find(wordFieldArray,
                a => a.Get("name").GetValue().AsString() == "MusicArtist").Get("list").Get(0).GetChildrenList();
            var artist2FieldList = Array.Find(wordFieldArray,
                a => a.Get("name").GetValue().AsString() == "MusicArtist2").Get("list").Get(0).GetChildrenList();
            var originalFieldList = Array.Find(wordFieldArray,
                a => a.Get("name").GetValue().AsString() == "MusicOriginal").Get("list").Get(0).GetChildrenList();

            foreach (var songEntry in songEntries)
            {
                var songID = Marshal.PtrToStringUTF8(songEntry.id);
                var musicEntry = songEntry.musicEntry;

                var musicSongField = Array.Find(musicFieldList, f => f.Get("ID").GetValue().AsString() == songID);

                var area = Marshal.PtrToStringUTF8(musicEntry.area);
                if (area.Length != 0)
                {
                    musicSongField.Get("Area").GetValue().Set(area);
                }

                if (musicEntry.starsEasy != 0)
                {
                    musicSongField.Get("Level_Easy").GetValue().Set(musicEntry.starsEasy);
                }

                if (musicEntry.starsNormal != 0)
                {
                    musicSongField.Get("Level_Normal").GetValue().Set(musicEntry.starsNormal);
                }

                if (musicEntry.starsHard != 0)
                {
                    musicSongField.Get("Level_Hard").GetValue().Set(musicEntry.starsHard);
                }

                musicSongField.Get("BPM").GetValue().Set(musicEntry.bpm);
                musicSongField.Get("Length").GetValue().Set(musicEntry.length);
                musicSongField.Get("DurationSec").GetValue().Set((int) (musicEntry.durationSec * 10) / 10.0);
                musicSongField.Get("Offset").GetValue().Set(musicEntry.offset);

                var titleSongField = Array.Find(titleFieldList, f => f.Get("key").GetValue().AsString() == songID);
                var subTitleSongField =
                    Array.Find(subTitleFieldList, f => f.Get("key").GetValue().AsString() == songID);
                var artistSongField = Array.Find(artistFieldList, f => f.Get("key").GetValue().AsString() == songID);
                var artist2SongField = Array.Find(artist2FieldList, f => f.Get("key").GetValue().AsString() == songID);
                var originalSongField =
                    Array.Find(originalFieldList, f => f.Get("key").GetValue().AsString() == songID);

                var wordEntries = WrapperToArray_Struct<WordEntry>(songEntry.wordEntry).Select(e => new WordEntryStr(e))
                    .ToArray();

                foreach (var wordEntry in wordEntries)
                {
                    titleSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.title);
                    subTitleSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.subTitle);
                    artistSongField.Get(wordEntry.lang).GetValue().Set(wordEntry.artist);
                    artist2SongField.Get(wordEntry.lang).GetValue().Set(wordEntry.artist2);
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

        private static T[] WrapperToArray_Struct<T>(ArrayWrapper wrapper)
        {
            var array = new T[wrapper.size];
            var size = Marshal.SizeOf<T>();

            for (var i = 0; i < wrapper.size; ++i)
            {
                IntPtr element = new IntPtr(wrapper.array.ToInt64() + i * size);
                array[i] = Marshal.PtrToStructure<T>(element);
            }

            return array;
        }

        [UnmanagedCallersOnly(EntryPoint = "free_dotnet")]
        public static void Free(IntPtr ptr)
        {
            Marshal.FreeCoTaskMem(ptr);
        }
    }
}