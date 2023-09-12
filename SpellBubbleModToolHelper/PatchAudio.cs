using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SonicAudioLib.Archives;
using SonicAudioLib.CriMw;
using VGAudio.Containers.Hca;
using VGAudio.Containers.Wave;

namespace SpellBubbleModToolHelper;

public partial class BridgeLib
{
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

        var extCpkArchive = new CriCpkArchive
        {
            Mode = CriCpkMode.Id
        };

        var awbFile = (byte[]) acbFile.Rows[0]["AwbFile"];
        var streamAwbAfs2Header = (byte[]) acbFile.Rows[0]["StreamAwbAfs2Header"];

        var cpkMode =
            !(awbFile is {Length: >= 4} && Encoding.ASCII.GetString(awbFile, 0, 4) == "AFS2") &&
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
}