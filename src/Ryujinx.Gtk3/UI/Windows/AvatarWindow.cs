using Gtk;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common.Memory;
using Ryujinx.HLE.FileSystem;
using Ryujinx.UI.Common.Configuration;
using SkiaSharp;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Ryujinx.UI.Windows
{
    public class AvatarWindow : Window
    {
        public byte[] SelectedProfileImage;
        public bool NewUser;

        private static readonly Dictionary<string, byte[]> _avatarDict = new();

        private readonly ListStore _listStore;
        private readonly IconView _iconView;
        private readonly Button _setBackgroungColorButton;
        private Gdk.RGBA _backgroundColor;

        public AvatarWindow() : base($"Ryujinx {Program.Version} - Manage Accounts - Avatar")
        {
            Icon = new Gdk.Pixbuf(Assembly.GetAssembly(typeof(ConfigurationState)), "Ryujinx.UI.Common.Resources.Logo_Ryujinx.png");

            CanFocus = false;
            Resizable = false;
            Modal = true;
            TypeHint = Gdk.WindowTypeHint.Dialog;

            SetDefaultSize(740, 400);
            SetPosition(WindowPosition.Center);

            Box vbox = new(Orientation.Vertical, 0);
            Add(vbox);

            ScrolledWindow scrolledWindow = new()
            {
                ShadowType = ShadowType.EtchedIn,
            };
            scrolledWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);

            Box hbox = new(Orientation.Horizontal, 0);

            Button chooseButton = new()
            {
                Label = "Choose",
                CanFocus = true,
                ReceivesDefault = true,
            };
            chooseButton.Clicked += ChooseButton_Pressed;

            _setBackgroungColorButton = new Button()
            {
                Label = "Set Background Color",
                CanFocus = true,
            };
            _setBackgroungColorButton.Clicked += SetBackgroungColorButton_Pressed;

            _backgroundColor.Red = 1;
            _backgroundColor.Green = 1;
            _backgroundColor.Blue = 1;
            _backgroundColor.Alpha = 1;

            Button closeButton = new()
            {
                Label = "Close",
                CanFocus = true,
            };
            closeButton.Clicked += CloseButton_Pressed;

            vbox.PackStart(scrolledWindow, true, true, 0);
            hbox.PackStart(chooseButton, true, true, 0);
            hbox.PackStart(_setBackgroungColorButton, true, true, 0);
            hbox.PackStart(closeButton, true, true, 0);
            vbox.PackStart(hbox, false, false, 0);

            _listStore = new ListStore(typeof(string), typeof(Gdk.Pixbuf));
            _listStore.SetSortColumnId(0, SortType.Ascending);

            _iconView = new IconView(_listStore)
            {
                ItemWidth = 64,
                ItemPadding = 10,
                PixbufColumn = 1,
            };

            _iconView.SelectionChanged += IconView_SelectionChanged;

            scrolledWindow.Add(_iconView);

            _iconView.GrabFocus();

            ProcessAvatars();

            ShowAll();
        }

        public static void PreloadAvatars(ContentManager contentManager, VirtualFileSystem virtualFileSystem)
        {
            if (_avatarDict.Count > 0)
            {
                return;
            }

            string contentPath = contentManager.GetInstalledContentPath(0x010000000000080A, StorageId.BuiltInSystem, NcaContentType.Data);
            string avatarPath = VirtualFileSystem.SwitchPathToSystemPath(contentPath);

            if (!string.IsNullOrWhiteSpace(avatarPath))
            {
                using IStorage ncaFileStream = new LocalStorage(avatarPath, FileAccess.Read, FileMode.Open);

                Nca nca = new(virtualFileSystem.KeySet, ncaFileStream);
                IFileSystem romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

                foreach (var item in romfs.EnumerateEntries())
                {
                    // TODO: Parse DatabaseInfo.bin and table.bin files for more accuracy.

                    if (item.Type == DirectoryEntryType.File && item.FullPath.Contains("chara") && item.FullPath.Contains("szs"))
                    {
                        using var file = new UniqueRef<IFile>();

                        romfs.OpenFile(ref file.Ref, ("/" + item.FullPath).ToU8Span(), OpenMode.Read).ThrowIfFailure();

                        using MemoryStream stream = MemoryStreamManager.Shared.GetStream();
                        using MemoryStream streamPng = MemoryStreamManager.Shared.GetStream();
                        file.Get.AsStream().CopyTo(stream);

                        stream.Position = 0;

                        using var avatarImage = new SKBitmap(new SKImageInfo(256, 256, SKColorType.Rgba8888));
                        var data = DecompressYaz0(stream);
                        Marshal.Copy(data, 0, avatarImage.GetPixels(), data.Length);

                        avatarImage.Encode(streamPng, SKEncodedImageFormat.Png, 80);

                        _avatarDict.Add(item.FullPath, streamPng.ToArray());
                    }
                }
            }
        }

        private void ProcessAvatars()
        {
            _listStore.Clear();

            foreach (var avatar in _avatarDict)
            {
                _listStore.AppendValues(avatar.Key, new Gdk.Pixbuf(ProcessImage(avatar.Value), 96, 96));
            }

            _iconView.SelectPath(new TreePath(new[] { 0 }));
        }

        private byte[] ProcessImage(byte[] data)
        {
            using MemoryStream streamJpg = MemoryStreamManager.Shared.GetStream();

            using var avatarImage = SKBitmap.Decode(data);
            using var surface = SKSurface.Create(avatarImage.Info);

            var background = new SKColor(
                (byte)(_backgroundColor.Red * 255),
                (byte)(_backgroundColor.Green * 255),
                (byte)(_backgroundColor.Blue * 255),
                (byte)(_backgroundColor.Alpha * 255)
            );
            var canvas = surface.Canvas;
            canvas.Clear(background);
            canvas.DrawBitmap(avatarImage, new SKPoint());

            surface.Flush();
            using var snapshot = surface.Snapshot();
            using var encoded = snapshot.Encode(SKEncodedImageFormat.Jpeg, 80);
            encoded.SaveTo(streamJpg);

            return streamJpg.ToArray();
        }

        private void CloseButton_Pressed(object sender, EventArgs e)
        {
            SelectedProfileImage = null;

            Close();
        }

        private void IconView_SelectionChanged(object sender, EventArgs e)
        {
            if (_iconView.SelectedItems.Length > 0)
            {
                _listStore.GetIter(out TreeIter iter, _iconView.SelectedItems[0]);

                SelectedProfileImage = ProcessImage(_avatarDict[(string)_listStore.GetValue(iter, 0)]);
            }
        }

        private void SetBackgroungColorButton_Pressed(object sender, EventArgs e)
        {
            using ColorChooserDialog colorChooserDialog = new("Set Background Color", this);

            colorChooserDialog.UseAlpha = false;
            colorChooserDialog.Rgba = _backgroundColor;

            if (colorChooserDialog.Run() == (int)ResponseType.Ok)
            {
                _backgroundColor = colorChooserDialog.Rgba;

                ProcessAvatars();
            }

            colorChooserDialog.Hide();
        }

        private void ChooseButton_Pressed(object sender, EventArgs e)
        {
            Close();
        }

        private static byte[] DecompressYaz0(Stream stream)
        {
            using BinaryReader reader = new(stream);

            reader.ReadInt32(); // Magic

            uint decodedLength = BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());

            reader.ReadInt64(); // Padding

            byte[] input = new byte[stream.Length - stream.Position];
            stream.ReadExactly(input, 0, input.Length);

            long inputOffset = 0;

            byte[] output = new byte[decodedLength];
            long outputOffset = 0;

            ushort mask = 0;
            byte header = 0;

            while (outputOffset < decodedLength)
            {
                if ((mask >>= 1) == 0)
                {
                    header = input[inputOffset++];
                    mask = 0x80;
                }

                if ((header & mask) > 0)
                {
                    if (outputOffset == output.Length)
                    {
                        break;
                    }

                    output[outputOffset++] = input[inputOffset++];
                }
                else
                {
                    byte byte1 = input[inputOffset++];
                    byte byte2 = input[inputOffset++];

                    int dist = ((byte1 & 0xF) << 8) | byte2;
                    int position = (int)outputOffset - (dist + 1);

                    int length = byte1 >> 4;
                    if (length == 0)
                    {
                        length = input[inputOffset++] + 0x12;
                    }
                    else
                    {
                        length += 2;
                    }

                    while (length-- > 0)
                    {
                        output[outputOffset++] = output[position++];
                    }
                }
            }

            return output;
        }
    }
}
