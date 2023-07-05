using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SonicAudioLib.Archives;
using SonicAudioLib.CriMw;
using VGAudio.Containers.Hca;
using VGAudio.Containers.Wave;

namespace SpellBubbleMusicEnablePatcher
{
    class Program
    {
        static void Main(string[] args)
        {
            // Encoding Setup
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // PatchShareDataRes();
            // Test1(args);
            Test3();
        }

        static string AcbToString(CriTable acbFile)
        {
            var keys = acbFile.Fields.Select(x => x.FieldName);
            var values = acbFile.Rows[0];

            var str = "";
            
            foreach (var key in keys)
            {
                str += key;
                str += "\n";
                
                var value = values[key];
                if (value.GetType() == typeof(byte[]))
                {
                    str += BitConverter.ToString(value as byte[]);
                }
                else
                {
                    str += value;
                }
                str += "\n";
            }

            return str;
        }
        
        static void Test3()
        {
            var acbPath = "C:\\Users\\datasone\\Downloads\\BGM_AUNDO.acb";
            
            CriTable acbFile = new CriTable();
            acbFile.Load(acbPath, 4096);
            
            Console.WriteLine(AcbToString(acbFile));
            
            byte[] awbFile = (byte[])acbFile.Rows[0]["AwbFile"];
            byte[] streamAwbAfs2Header = (byte[])acbFile.Rows[0]["StreamAwbAfs2Header"];

            bool cpkMode = !(awbFile != null && awbFile.Length >= 4 && Encoding.ASCII.GetString(awbFile, 0, 4) == "AFS2") && (streamAwbAfs2Header == null || streamAwbAfs2Header.Length == 0);

            var acb = acbFile.ToString();
            
            using (CriTableReader reader = CriTableReader.Create((byte[]) acbFile.Rows[0]["WaveformTable"]))
            {
                while (reader.Read())
                {
                    bool streaming = true;

                    ushort id =
                        reader.ContainsField("MemoryAwbId") ?
                            streaming ? reader.GetUInt16("StreamAwbId") : reader.GetUInt16("MemoryAwbId") :
                            reader.GetUInt16("Id");
                }
            }
        }
        
        static void Test1(string[] args)
        {
            Console.WriteLine("This tool is only for testing, it does not do anything meaningful...");

            var assetBundlePath = "C:\\Users\\datasone\\work\\TouhouSB_Hack\\share_data";
            var assetToReplace = "TPZ_MusicData";
            var filePathForReplace = "";

            var am = new AssetsManager();
            am.LoadClassPackage("C:\\Users\\datasone\\source\\repos\\UnityAssetBundleHelper\\UnityAssetBundleHelper\\classdata.tpk");

            var assetBundle = am.LoadBundleFile(assetBundlePath, unpackIfPacked: true);
            
            var assetsCount = assetBundle.file.bundleInf6.directoryCount;


            am.LoadClassDatabaseFromPackage(am.LoadAssetsFileFromBundle(assetBundle, 0).file.typeTree.unityVersion);

            for (var i = 0; i < assetsCount; ++i)
            {
                var assets = am.LoadAssetsFileFromBundle(assetBundle, i);
                var info = assets.table.GetAssetInfo(assetToReplace);
                var baseField = am.GetTypeInstance(assets.file, info).GetBaseField();

                var sheetsValue = baseField.Get("sheets");


                var sheetsArrayValue = sheetsValue.Get(0).Get(0);
                        var listValue = sheetsArrayValue.Get("list").Get(0);
                        var values6 = listValue.GetChildrenList();
                        foreach (var value6 in values6)
                        {
                            value6.Get("IsDefault").GetValue().Set(1);
                        }

                        var newGoBytes = baseField.WriteToByteArray();
                        var assetsReplacer = new AssetsReplacerFromMemory(0, info.index, (int) info.curFileType,
                            AssetHelper.GetScriptIndex(assets.file, info), newGoBytes);


                            byte[] newAssetsData;
                using (var stream = new MemoryStream())
                using (var writer = new AssetsFileWriter(stream))
                {
                    assets.file.Write(writer, 0, new List<AssetsReplacer> { assetsReplacer }, 0);
                    newAssetsData = stream.ToArray();
                }

                var bundleReplacer = new BundleReplacerFromMemory(assets.name, null, true, newAssetsData, -1);

                using (var bundleWriter = new AssetsFileWriter(File.OpenWrite("C:\\Users\\datasone\\work\\TouhouSB_Hack\\share_data.patched")))
                using (var newStream = new MemoryStream())
                using (var writer = new AssetsFileWriter(newStream))
                {
                    assetBundle.file.Write(writer, new List<BundleReplacer> { bundleReplacer });
                    using (var reader = new AssetsFileReader(newStream))
                    {
                        var newBundle = new AssetBundleFile();
                        newBundle.Read(new AssetsFileReader(newStream), false);
                        newBundle.Pack(reader, bundleWriter, AssetBundleCompressionType.LZ4);
                    }
                }

            }
        }

        static void Test2(string[] args)
        {
            var wavPath = "C:\\Users\\datasone\\Music\\グランド・リグレッション.wav";
            var hcaPath = Path.GetTempPath() + wavPath.Split('\\').Last() + ".hca";
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

            var acbPath = "C:\\Users\\datasone\\work\\TouhouSB_Hack\\BGM_BOO.acb";
            var awbPath = "C:\\Users\\datasone\\work\\TouhouSB_Hack\\BGM_BOO.awb";

            CriTable acbFile = new CriTable();
            acbFile.Load(acbPath, 4096);
            
            CriAfs2Archive extAfs2Archive = new CriAfs2Archive();
            
            CriCpkArchive extCpkArchive = new CriCpkArchive();
            extCpkArchive.Mode = CriCpkMode.Id;

            byte[] awbFile = (byte[])acbFile.Rows[0]["AwbFile"];
            byte[] streamAwbAfs2Header = (byte[])acbFile.Rows[0]["StreamAwbAfs2Header"];

            bool cpkMode = !(awbFile != null && awbFile.Length >= 4 && Encoding.ASCII.GetString(awbFile, 0, 4) == "AFS2") && (streamAwbAfs2Header == null || streamAwbAfs2Header.Length == 0);

            using (CriTableReader reader = CriTableReader.Create((byte[]) acbFile.Rows[0]["WaveformTable"]))
            {
                while (reader.Read())
                {
                    bool streaming = true;

                    ushort id =
                        reader.ContainsField("MemoryAwbId") ?
                            streaming ? reader.GetUInt16("StreamAwbId") : reader.GetUInt16("MemoryAwbId") :
                            reader.GetUInt16("Id");

                    if (cpkMode)
                    {
                        CriCpkEntry entry = new CriCpkEntry();
                        entry.FilePath = new FileInfo(hcaPath);
                        entry.Id = id;
                        
                        extCpkArchive.Add(entry);
                    }
                    else
                    {
                        CriAfs2Entry entry = new CriAfs2Entry();
                        entry.FilePath = new FileInfo(hcaPath);
                        entry.Id = id;
                        
                        extAfs2Archive.Add(entry);
                    }
                }
            }

            if (cpkMode)
            {
                extCpkArchive.Save(awbPath, 4096);
            }
            else
            {
                extAfs2Archive.Save(awbPath, 4096);
            }

            File.Delete(hcaPath);
        }

        private static (AssetsManager, BundleFileInstance, AssetsFileInstance) LoadAssetsFromBundlePath(
            string assetBundlePath)
        {
            var am = new AssetsManager();
            am.LoadClassPackage("C:\\Users\\datasone\\source\\repos\\UnityAssetBundleHelper\\UnityAssetBundleHelper\\classdata.tpk");
            var assetBundle = am.LoadBundleFile(assetBundlePath, unpackIfPacked: true);
            am.LoadClassDatabaseFromPackage(am.LoadAssetsFileFromBundle(assetBundle, 0).file.typeTree.unityVersion);
            var assets = am.LoadAssetsFileFromBundle(assetBundle, 0);
            return (am, assetBundle, assets);
        }

        public static void PatchShareDataRes()
        {
            var shareDataPath = "C:\\Users\\datasone\\work\\TouhouSB_Hack\\share_data";
            var outShareDataPath = "C:\\Users\\datasone\\work\\TouhouSB_Hack\\share_data.patched";
            var unlockDLCsExclude = new int[0];

            var (am, bundle, assets) = LoadAssetsFromBundlePath(shareDataPath);
            var replacerList = new List<AssetsReplacer>();

            var musicInfo = assets.table.GetAssetInfo("TPZ_MusicData");
            var musicBaseField = am.GetTypeInstance(assets.file, musicInfo).GetBaseField();

            var wordInfo = assets.table.GetAssetInfo("TPZ_WordData");
            var wordBaseField = am.GetTypeInstance(assets.file, wordInfo).GetBaseField();

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


                var songID = "BadApple";

                var musicSongField = Array.Find(musicFieldList, f => f.Get("ID").GetValue().AsString() == songID);

                // var area = "";
                // if (area.Length != 0)
                // {
                //     musicSongField.Get("Area").GetValue().Set(area);
                // }
                //
                // if (musicEntry.starsEasy != 0)
                // {
                //     musicSongField.Get("Level_Easy").GetValue().Set(musicEntry.starsEasy);
                // }
                //
                // if (musicEntry.starsNormal != 0)
                // {
                //     musicSongField.Get("Level_Normal").GetValue().Set(musicEntry.starsNormal);
                // }
                //
                // if (musicEntry.starsHard != 0)
                // {
                //     musicSongField.Get("Level_Hard").GetValue().Set(musicEntry.starsHard);
                // }

                musicSongField.Get("BPM").GetValue().Set(100);
                musicSongField.Get("DurationSec").GetValue().Set(132.1789);

                var titleSongField = Array.Find(titleFieldList, f => f.Get("key").GetValue().AsString() == songID);
                var subTitleSongField = Array.Find(subTitleFieldList, f => f.Get("key").GetValue().AsString() == songID);
                var artistSongField = Array.Find(artistFieldList, f => f.Get("key").GetValue().AsString() == songID);
                var artist2SongField = Array.Find(artist2FieldList, f => f.Get("key").GetValue().AsString() == songID);
                var originalSongField = Array.Find(originalFieldList, f => f.Get("key").GetValue().AsString() == songID);

                titleSongField.Get("ja").GetValue().Set("TestingTitle");

                var musicBytes = musicBaseField.WriteToByteArray();
            var wordBytes = wordBaseField.WriteToByteArray();
            var musicAssetsReplacer = new AssetsReplacerFromMemory(0, musicInfo.index, (int)musicInfo.curFileType,
                AssetHelper.GetScriptIndex(assets.file, musicInfo), musicBytes);
            var wordAssetsReplacer = new AssetsReplacerFromMemory(0, wordInfo.index, (int)wordInfo.curFileType,
                AssetHelper.GetScriptIndex(assets.file, wordInfo), wordBytes);

            replacerList.Append(musicAssetsReplacer);
            replacerList.Append(wordAssetsReplacer);

            PatchAssetBundle(bundle, assets, replacerList, outShareDataPath);
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
                bundle.file.Write(writer, new List<BundleReplacer> { bundleReplacer });
                using (var reader = new AssetsFileReader(newStream))
                {
                    var newBundle = new AssetBundleFile();
                    newBundle.Read(new AssetsFileReader(newStream), false);
                    newBundle.Pack(reader, bundleWriter, AssetBundleCompressionType.LZ4);
                }
            }
        }

    }
}
