using Avalonia.Media;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Ava.UI.Models;
using Ryujinx.HLE.FileSystem;
using SkiaSharp;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Color = Avalonia.Media.Color;
using Image = SkiaSharp.SKImage;

namespace Ryujinx.Ava.UI.ViewModels
{
    internal class UserFirmwareAvatarSelectorViewModel : BaseModel
    {
        private static readonly Dictionary<string, byte[]> _avatarStore = new();

        private ObservableCollection<ProfileImageModel> _images;
        private Color _backgroundColor = Colors.White;

        private int _selectedIndex;

        public UserFirmwareAvatarSelectorViewModel()
        {
            _images = new ObservableCollection<ProfileImageModel>();

            LoadImagesFromStore();
        }

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                OnPropertyChanged();
                ChangeImageBackground();
            }
        }

        public ObservableCollection<ProfileImageModel> Images
        {
            get => _images;
            set
            {
                _images = value;
                OnPropertyChanged();
            }
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;

                if (_selectedIndex == -1)
                {
                    SelectedImage = null;
                }
                else
                {
                    SelectedImage = _images[_selectedIndex].Data;
                }

                OnPropertyChanged();
            }
        }

        public byte[] SelectedImage { get; private set; }

        private void LoadImagesFromStore()
        {
            Images.Clear();

            foreach (var image in _avatarStore)
            {
                Images.Add(new ProfileImageModel(image.Key, image.Value));
            }
        }

        private void ChangeImageBackground()
        {
            foreach (var image in Images)
            {
                image.BackgroundColor = new SolidColorBrush(BackgroundColor);
            }
        }

        public static void PreloadAvatars(ContentManager contentManager, VirtualFileSystem virtualFileSystem)
        {
            if (_avatarStore.Count > 0)
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

                foreach (DirectoryEntryEx item in romfs.EnumerateEntries())
                {
                    // TODO: Parse DatabaseInfo.bin and table.bin files for more accuracy.
                    if (item.Type == DirectoryEntryType.File && item.FullPath.Contains("chara") && item.FullPath.Contains("szs"))
                    {
                        using var file = new UniqueRef<IFile>();

                        romfs.OpenFile(ref file.Ref, ("/" + item.FullPath).ToU8Span(), OpenMode.Read).ThrowIfFailure();

                        using MemoryStream stream = new();
                        using MemoryStream streamPng = new();

                        file.Get.AsStream().CopyTo(stream);

                        stream.Position = 0;

                        Image avatarImage = Image.FromPixelCopy(new SKImageInfo(256, 256, SKColorType.Rgba8888, SKAlphaType.Premul), DecompressYaz0(stream));

                        using (SKData data = avatarImage.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            data.SaveTo(streamPng);
                        }

                        _avatarStore.Add(item.FullPath, streamPng.ToArray());
                    }
                }
            }
        }

        private static byte[] DecompressYaz0(Stream stream)
        {
            using BinaryReader reader = new(stream);

            reader.ReadInt32(); // Magic

            uint decodedLength = BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());

            reader.ReadInt64(); // Padding

            byte[] input = new byte[stream.Length - stream.Position];
            stream.ReadExactly(input, 0, input.Length);

            uint inputOffset = 0;

            byte[] output = new byte[decodedLength];
            uint outputOffset = 0;

            ushort mask = 0;
            byte header = 0;

            while (outputOffset < decodedLength)
            {
                if ((mask >>= 1) == 0)
                {
                    header = input[inputOffset++];
                    mask = 0x80;
                }

                if ((header & mask) != 0)
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

                    uint dist = (uint)((byte1 & 0xF) << 8) | byte2;
                    uint position = outputOffset - (dist + 1);

                    uint length = (uint)byte1 >> 4;
                    if (length == 0)
                    {
                        length = (uint)input[inputOffset++] + 0x12;
                    }
                    else
                    {
                        length += 2;
                    }

                    uint gap = outputOffset - position;
                    uint nonOverlappingLength = length;

                    if (nonOverlappingLength > gap)
                    {
                        nonOverlappingLength = gap;
                    }

                    Buffer.BlockCopy(output, (int)position, output, (int)outputOffset, (int)nonOverlappingLength);
                    outputOffset += nonOverlappingLength;
                    position += nonOverlappingLength;
                    length -= nonOverlappingLength;

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
