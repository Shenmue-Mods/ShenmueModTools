using Ookii.Dialogs;
using ShenmueDKSharp.Files.Containers;
using ShenmueDKSharp.Files.Images;
using ShenmueDKSharp.Files.Misc;
using ShenmueDKSharp.Files.Models;
using ShenmueDKSharp.Structs;
using ShenmueDKSharp.Utils;
using ShenmueHDArchiver.Dialogs;
using ShenmueTextureOverride.Properties;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShenmueDKSharp.Files.Images._DDS;

namespace ShenmueTextureOverride
{
    public partial class TextureOverride : Form
    {
        readonly string SM_ARCHIVE_DATA = "\\archives\\dx11\\data\\";
        readonly string SM_TEXTURES = "\\textures";
        readonly string SM_TEXOVER = "SDTextureOverride.json";

        readonly string SM1_DISK = "disk_5be2c578";
        readonly string SM2_DISK = "disk_5be2c4e2";


        public class TexEntry
        {
            public TextureID texID;
            public BaseImage image;
        }

        public List<TexEntry> textures = new List<TexEntry>();

        public class HashEntry
        {
            public string Filepath;
            public TextureID texID;
        }

        public List<HashEntry> SM1_Hashes = new List<HashEntry>();
        public List<HashEntry> SM2_Hashes = new List<HashEntry>();

        public TextureOverride()
        {
            TextureDatabase.Automatic = false;
            MT5.UVMirrorTextureResize = false;
            MT5.UseTextureDatabase = false;
            MT7.UseTextureDatabase = false;
            DDSGeneral.EnableThreading = true;

            InitializeComponent();
            InitializeJSON();
        }

        private void InitializeJSON()
        {
            SM1_Hashes.Clear();
            SM2_Hashes.Clear();

            JSONNode rootNode = JSON.Parse(Resources.SDTextureOverride_SM1);
            foreach (JSONNode node in rootNode["Mappings"].AsArray)
            {
                HashEntry newEntry = new HashEntry();
                foreach (KeyValuePair<string, JSONNode> entry in node.Linq)
                {
                    if (entry.Key == "Destination")
                    {
                        newEntry.Filepath = entry.Value.Value;
                    }
                    if (entry.Key == "TextureID")
                    {
                        newEntry.texID = new TextureID(entry.Value.Value);
                    }
                }
                SM1_Hashes.Add(newEntry);
            }
            Console.WriteLine("SM1 hash database initialized: {0} entries.", SM1_Hashes.Count);

            JSONNode rootNode2 = JSON.Parse(Resources.SDTextureOverride_SM2);
            foreach (JSONNode node in rootNode2["Mappings"].AsArray)
            {
                HashEntry newEntry = new HashEntry();
                foreach (KeyValuePair<string, JSONNode> entry in node.Linq)
                {
                    if (entry.Key == "Destination")
                    {
                        newEntry.Filepath = entry.Value.Value;
                    }
                    if (entry.Key == "TextureID")
                    {
                        newEntry.texID = new TextureID(entry.Value.Value);
                    }
                }
                SM2_Hashes.Add(newEntry);
            }

            Console.WriteLine("SM2 hash database initialized: {0} entries.", SM2_Hashes.Count);
        }

        private void ReadFileRecursive(byte[] data)
        {
            if (GZ.IsValid(data))
            {
                using (MemoryStream memstream = new MemoryStream(data))
                {
                    GZ gz = new GZ(memstream);
                    ReadFileRecursive(gz.ContentBuffer);
                }
            }
            if (AFS.IsValid(data))
            {
                using (MemoryStream memstream = new MemoryStream(data))
                {
                    AFS afs = new AFS(memstream);
                    foreach (var entry in afs.Entries)
                    {
                        ReadFileRecursive(entry.Buffer);
                    }
                }
            }
            if (PKF.IsValid(data))
            {
                using (MemoryStream memstream = new MemoryStream(data))
                {
                    PKF pkf = new PKF(memstream);
                    foreach (var entry in pkf.Entries)
                    {
                        ReadFileRecursive(entry.Buffer);
                    }
                }
            }
            if (PKS.IsValid(data))
            {
                using (MemoryStream memstream = new MemoryStream(data))
                {
                    PKS pks = new PKS(memstream);
                    foreach (var entry in pks.IPAC.Entries)
                    {
                        ReadFileRecursive(entry.Buffer);
                    }
                }
            }
            if (TEXN.IsValid(data))
            {
                using (MemoryStream memstream = new MemoryStream(data))
                {
                    TEXN tex = new TEXN(memstream);
                    TexEntry entry = new TexEntry();
                    entry.texID = tex.TextureID;
                    entry.image = tex.Texture;
                    textures.Add(entry);
                }
            }
            if (MT5.IsValid(data))
            {
                using (MemoryStream memstream = new MemoryStream(data))
                {
                    MT5 mt5 = new MT5(memstream);
                    foreach (var tex in mt5.Textures)
                    {
                        if (tex.Image != null)
                        {
                            TexEntry entry = new TexEntry();
                            entry.texID = tex.TextureID;
                            entry.image = tex.Image;
                            textures.Add(entry);
                        }
                    }
                }
            }
            if (MT7.IsValid(data))
            {
                using (MemoryStream memstream = new MemoryStream(data))
                {
                    MT7 mt7 = new MT7(memstream);
                    foreach (var tex in mt7.Textures)
                    {
                        if (tex.Image != null)
                        {
                            TexEntry entry = new TexEntry();
                            entry.texID = tex.TextureID;
                            entry.image = tex.Image;
                            textures.Add(entry);
                        }
                        
                    }
                }
            }
        }

        private List<string> GetFiles(string folder)
        {
            List<string> result = new List<string>();
            result.AddRange(Directory.GetFiles(folder));
            foreach (var dir in Directory.GetDirectories(folder))
            {
                result.AddRange(GetFiles(dir));
            }
            return result;
        }

        private void button_Unpack_Click(object sender, EventArgs e)
        {
            InitializeJSON();

            // create json
            string sdtextureoverridePath = textBox_Folder.Text + SM_TEXTURES + "\\" + SM_TEXOVER;
            if (!Directory.Exists(Path.GetDirectoryName(sdtextureoverridePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sdtextureoverridePath));
            }
            using (FileStream stream = File.Open(sdtextureoverridePath, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(Resources.SDTextureOverride_SM1_Clean);
                }
            }

            // unpack mapped data from disk tac
            string tadFilepath = textBox_Folder.Text + SM_ARCHIVE_DATA + SM1_DISK + ".tad";
            string tacFilepath = textBox_Folder.Text + SM_ARCHIVE_DATA + SM1_DISK + ".tac";

            TAD tad = new TAD(tadFilepath);
            TAC tac = new TAC(tad);

            textures.Clear();
            foreach (var entry in tad.Entries)
            {
                if (entry.FileName == "/scene/01/d000/sprt.pks") continue; // corrupt file
                byte[] buffer = tac.GetFileBuffer(entry);
                ReadFileRecursive(buffer);
            }

            bool first = true;
            string prevDdsPath = "";
            string prevPngPath = "";
            foreach (var tex in textures)
            {
                first = true;
                List<HashEntry> matches = new List<HashEntry>();
                foreach (var t in SM1_Hashes)
                {
                    if (tex.texID == t.texID)
                    {
                        if (first) // if first match, write DDS or PNG file
                        {
                            if (radioButton_DDS1.Checked)
                            {
                                DDS dds = new DDS(tex.image);
                                string filepath = textBox_Folder.Text + SM_TEXTURES + t.Filepath.Replace("/", "\\");
                                dds.MipHandling = DDSGeneral.MipHandling.KeepTopOnly;
                                dds.AlphaSettings = DDSGeneral.AlphaSettings.KeepAlpha;
                                dds.FormatDetails = new DDSFormats.DDSFormatDetails(DDSFormat.DDS_DXT3);
                                dds.Write(filepath);
                                prevDdsPath = filepath;
                            }

                            if (radioButton_PNG1.Checked)
                            {
                                PNG png = new PNG(tex.image);
                                string filepath = textBox_Folder.Text + SM_TEXTURES + t.Filepath.Replace("/", "\\");
                                string filepathPng = Path.ChangeExtension(filepath, ".png");
                                png.Write(filepathPng);
                                prevPngPath = filepathPng;
                            }
                            
                            first = false;
                        }
                        else // if duplicate, copy/paste the texture file (DDS and PNG writing costs alot of resources)
                        {
                            string filepath = textBox_Folder.Text + SM_TEXTURES + t.Filepath.Replace("/", "\\");
                            string filepathPng = Path.ChangeExtension(filepath, ".png");
                            string dir = Path.GetDirectoryName(filepath);
                            if (!Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }
                            if (radioButton_DDS1.Checked)
                            {
                                File.Copy(prevDdsPath, filepath, true);
                            }
                            if (radioButton_PNG1.Checked)
                            {
                                File.Copy(prevPngPath, filepathPng, true);
                            }
                        }
                        matches.Add(t);
                    }
                }

                // Remove matches to increase iteration speed, will result in the first found texture being used
                foreach (var match in matches)
                {
                    SM1_Hashes.Remove(match);
                }
            }
            textures.Clear();
            foreach (var hash in SM1_Hashes)
            {
                Console.WriteLine("Texture not found: {0} = {1}", hash.texID.HexStr, hash.Filepath);
            }
        }

        private void button_Pack_Click(object sender, EventArgs e)
        {
            InitializeJSON();

            string folder = textBox_Folder.Text + SM_TEXTURES;
            List<string> files = GetFiles(folder);
            List<TADEntry> entries = new List<TADEntry>();

            // iterate through files in /textures/ and create TAD entry for each of them
            foreach (var file in files)
            {
                FileHash hash;
                string f = file.Replace(folder, "");

                hash = MurmurHash2.GetFileHash(f, true);
                Console.WriteLine("{0} -> {1} : {2} -> {3}", hash.FilePath, hash.FilePathWithHash, hash.Hash.ToString("x8"), hash.FinalHash.ToString("x8"));

                FileInfo fileInfo = new FileInfo(file);
                TADEntry entry = new TADEntry();
                entry.FilePath = file;
                entry.FileName = file;
                entry.FileSize = (uint)fileInfo.Length;
                entry.Index = 0;
                entry.FirstHash = hash.FinalHash;
                entry.SecondHash = hash.FilePathHash;
                entries.Add(entry);
            }

            // create tac/tad
            string tadFilepath = textBox_Folder.Text + SM_ARCHIVE_DATA + "texture_mod.tad";
            string tacFilepath = Path.ChangeExtension(tadFilepath, ".tac");

            TAD tad = new TAD();
            tad.FilePath = tadFilepath;
            foreach (TADEntry entry in entries)
            {
                tad.Entries.Add(entry);
            }
            TAC tac = new TAC();
            tac.TAD = tad;

            LoadingDialog loadingDialog = new LoadingDialog();
            loadingDialog.SetProgessable(tac);
            Thread thread = new Thread(delegate () {
                tac.Pack(tacFilepath);
            });
            loadingDialog.ShowDialog(thread);

            tad.UnixTimestamp = DateTime.Now;
            tad.Write(tadFilepath);

        }

        private void button_TexFolder_Click(object sender, EventArgs e)
        {
            string texFolder = textBox_Folder.Text + SM_TEXTURES;
            Process.Start(texFolder);
        }

        private void button_Browse_Click(object sender, EventArgs e)
        {
            VistaFolderBrowserDialog folderDialog = new VistaFolderBrowserDialog();
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                string[] files = Directory.GetFiles(folderDialog.SelectedPath);
                foreach(var file in files)
                {
                    if (Path.GetFileName(file).ToLower() == "shenmue.exe")
                    {
                        textBox_Folder.Text = folderDialog.SelectedPath;
                        return;
                    }
                }
                MessageBox.Show("NOT AN SM1 FOLDER");
            }
        }

        private void button_Browse2_Click(object sender, EventArgs e)
        {
            VistaFolderBrowserDialog folderDialog = new VistaFolderBrowserDialog();
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                string[] files = Directory.GetFiles(folderDialog.SelectedPath);
                foreach (var file in files)
                {
                    if (Path.GetFileName(file).ToLower() == "shenmue2.exe")
                    {
                        textBox_Folder2.Text = folderDialog.SelectedPath;
                        return;
                    }
                }
                MessageBox.Show("NOT AN SM2 FOLDER");
            }
        }

        private void button_TexFolder2_Click(object sender, EventArgs e)
        {
            string texFolder = textBox_Folder2.Text + "\\textures\\";
            Process.Start(texFolder);
        }

        private void button_Unpack2_Click(object sender, EventArgs e)
        {
            InitializeJSON();

            // create json
            string sdtextureoverridePath = textBox_Folder2.Text + SM_TEXTURES + "\\" + SM_TEXOVER;
            if (!Directory.Exists(Path.GetDirectoryName(sdtextureoverridePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(sdtextureoverridePath));
            }
            using (FileStream stream = File.Open(sdtextureoverridePath, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(Resources.SDTextureOverride_SM2_Clean);
                }
            }

            // unpack mapped data from disk tac
            string tadFilepath = textBox_Folder2.Text + SM_ARCHIVE_DATA + SM2_DISK + ".tad";
            string tacFilepath = textBox_Folder2.Text + SM_ARCHIVE_DATA + SM2_DISK + ".tac";

            TAD tad = new TAD(tadFilepath);
            TAC tac = new TAC(tad);

            textures.Clear();
            foreach (var entry in tad.Entries)
            {
                if (entry.FileName == "/misc/cold.bin") continue; // not supported
                byte[] buffer = tac.GetFileBuffer(entry);
                ReadFileRecursive(buffer);
            }

            bool first = true;
            string prevDdsPath = "";
            string prevPngPath = "";
            foreach (var tex in textures)
            {
                first = true;
                List<HashEntry> matches = new List<HashEntry>();
                foreach (var t in SM2_Hashes)
                {
                    if (tex.texID == t.texID)
                    {
                        if (first) // if first match, write DDS or PNG file
                        {
                            if (radioButton_DDS1.Checked)
                            {
                                DDS dds = new DDS(tex.image);
                                string filepath = textBox_Folder2.Text + SM_TEXTURES + t.Filepath.Replace("/", "\\");
                                dds.MipHandling = DDSGeneral.MipHandling.KeepTopOnly;
                                dds.AlphaSettings = DDSGeneral.AlphaSettings.KeepAlpha;
                                dds.FormatDetails = new DDSFormats.DDSFormatDetails(DDSFormat.DDS_DXT3);
                                dds.Write(filepath);
                                prevDdsPath = filepath;
                            }

                            if (radioButton_PNG1.Checked)
                            {
                                PNG png = new PNG(tex.image);
                                string filepath = textBox_Folder2.Text + SM_TEXTURES + t.Filepath.Replace("/", "\\");
                                string filepathPng = Path.ChangeExtension(filepath, ".png");
                                png.Write(filepathPng);
                                prevPngPath = filepathPng;
                            }

                            first = false;
                        }
                        else // if duplicate, copy/paste the texture file (DDS and PNG writing costs alot of resources)
                        {
                            string filepath = textBox_Folder2.Text + SM_TEXTURES + t.Filepath.Replace("/", "\\");
                            string filepathPng = Path.ChangeExtension(filepath, ".png");
                            string dir = Path.GetDirectoryName(filepath);
                            if (!Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }
                            if (radioButton_DDS1.Checked)
                            {
                                File.Copy(prevDdsPath, filepath, true);
                            }
                            if (radioButton_PNG1.Checked)
                            {
                                File.Copy(prevPngPath, filepathPng, true);
                            }
                        }
                        matches.Add(t);
                    }
                }

                // Remove matches to increase iteration speed, will result in the first found texture being used
                foreach (var match in matches)
                {
                    SM2_Hashes.Remove(match);
                }
            }
            textures.Clear();
            foreach (var hash in SM2_Hashes)
            {
                Console.WriteLine("Texture not found: {0} = {1}", hash.texID.HexStr, hash.Filepath);
            }
        }

        private void button_Pack2_Click(object sender, EventArgs e)
        {
            InitializeJSON();

            string folder = textBox_Folder2.Text + SM_TEXTURES;
            List<string> files = GetFiles(folder);
            List<TADEntry> entries = new List<TADEntry>();

            // iterate through files in /textures/ and create TAD entry for each of them
            foreach (var file in files)
            {
                FileHash hash;
                string f = file.Replace(folder, "");

                hash = MurmurHash2.GetFileHash(f, true);
                Console.WriteLine("{0} -> {1} : {2} -> {3}", hash.FilePath, hash.FilePathWithHash, hash.Hash.ToString("x8"), hash.FinalHash.ToString("x8"));

                FileInfo fileInfo = new FileInfo(file);
                TADEntry entry = new TADEntry();
                entry.FilePath = file;
                entry.FileName = file;
                entry.FileSize = (uint)fileInfo.Length;
                entry.Index = 0;
                entry.FirstHash = hash.FinalHash;
                entry.SecondHash = hash.FilePathHash;
                entries.Add(entry);
            }

            // create tac/tad
            string tadFilepath = textBox_Folder2.Text + SM_ARCHIVE_DATA + "texture_mod.tad";
            string tacFilepath = Path.ChangeExtension(tadFilepath, ".tac");

            TAD tad = new TAD();
            tad.FilePath = tadFilepath;
            foreach (TADEntry entry in entries)
            {
                tad.Entries.Add(entry);
            }
            TAC tac = new TAC();
            tac.TAD = tad;

            LoadingDialog loadingDialog = new LoadingDialog();
            loadingDialog.SetProgessable(tac);
            Thread thread = new Thread(delegate () {
                tac.Pack(tacFilepath);
            });
            loadingDialog.ShowDialog(thread);

            tad.UnixTimestamp = DateTime.Now;
            tad.Write(tadFilepath);
        }
    }
}
