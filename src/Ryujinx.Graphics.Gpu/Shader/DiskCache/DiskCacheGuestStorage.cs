using Ryujinx.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Ryujinx.Graphics.Gpu.Shader.DiskCache
{
    /// <summary>
    /// On-disk shader cache storage for guest code.
    /// </summary>
    class DiskCacheGuestStorage
    {
        private const uint TocMagic = (byte)'T' | ((byte)'O' << 8) | ((byte)'C' << 16) | ((byte)'G' << 24);

        private const ushort VersionMajor = 1;
        private const ushort VersionMinor = 1;
        private const uint VersionPacked = ((uint)VersionMajor << 16) | VersionMinor;

        private const string TocFileName = "guest.toc";
        private const string DataFileName = "guest.data";

        private readonly string _basePath;

        /// <summary>
        /// TOC (Table of contents) file header.
        /// </summary>
        private struct TocHeader
        {
            /// <summary>
            /// Magic value, for validation and identification purposes.
            /// </summary>
            public uint Magic;

            /// <summary>
            /// File format version.
            /// </summary>
            public uint Version;

            /// <summary>
            /// Header padding.
            /// </summary>
            public uint Padding;

            /// <summary>
            /// Number of modifications to the file, also the shaders count.
            /// </summary>
            public uint ModificationsCount;

            /// <summary>
            /// Reserved space, to be used in the future. Write as zero.
            /// </summary>
            public ulong Reserved;

            /// <summary>
            /// Reserved space, to be used in the future. Write as zero.
            /// </summary>
            public ulong Reserved2;
        }

        /// <summary>
        /// TOC (Table of contents) file entry.
        /// </summary>
        private struct TocEntry
        {
            /// <summary>
            /// Offset of the data on the data file.
            /// </summary>
            public uint Offset;

            /// <summary>
            /// Code size.
            /// </summary>
            public uint CodeSize;

            /// <summary>
            /// Constant buffer 1 data size.
            /// </summary>
            public uint Cb1DataSize;

            /// <summary>
            /// Hash of the code and constant buffer data.
            /// </summary>
            public uint Hash;
        }

        /// <summary>
        /// TOC (Table of contents) memory cache entry.
        /// </summary>
        private struct TocMemoryEntry
        {
            /// <summary>
            /// Offset of the data on the data file.
            /// </summary>
            public uint Offset;

            /// <summary>
            /// Code size.
            /// </summary>
            public uint CodeSize;

            /// <summary>
            /// Constant buffer 1 data size.
            /// </summary>
            public uint Cb1DataSize;

            /// <summary>
            /// Index of the shader on the cache.
            /// </summary>
            public readonly int Index;

            /// <summary>
            /// Creates a new TOC memory entry.
            /// </summary>
            /// <param name="offset">Offset of the data on the data file</param>
            /// <param name="codeSize">Code size</param>
            /// <param name="cb1DataSize">Constant buffer 1 data size</param>
            /// <param name="index">Index of the shader on the cache</param>
            public TocMemoryEntry(uint offset, uint codeSize, uint cb1DataSize, int index)
            {
                Offset = offset;
                CodeSize = codeSize;
                Cb1DataSize = cb1DataSize;
                Index = index;
            }
        }

        private Dictionary<uint, List<TocMemoryEntry>> _toc;
        private uint _tocModificationsCount;

        private (byte[], byte[])[] _cache;

        /// <summary>
        /// Creates a new disk cache guest storage.
        /// </summary>
        /// <param name="basePath">Base path of the disk shader cache</param>
        public DiskCacheGuestStorage(string basePath)
        {
            _basePath = basePath;
        }

        /// <summary>
        /// Checks if the TOC (table of contents) file for the guest cache exists.
        /// </summary>
        /// <returns>True if the file exists, false otherwise</returns>
        public bool TocFileExists()
        {
            return File.Exists(Path.Combine(_basePath, TocFileName));
        }

        /// <summary>
        /// Checks if the data file for the guest cache exists.
        /// </summary>
        /// <returns>True if the file exists, false otherwise</returns>
        public bool DataFileExists()
        {
            return File.Exists(Path.Combine(_basePath, DataFileName));
        }

        /// <summary>
        /// Opens the guest cache TOC (table of contents) file.
        /// </summary>
        /// <returns>File stream</returns>
        public Stream OpenTocFileStream()
        {
            return DiskCacheCommon.OpenFile(_basePath, TocFileName, writable: false);
        }

        /// <summary>
        /// Opens the guest cache data file.
        /// </summary>
        /// <returns>File stream</returns>
        public Stream OpenDataFileStream()
        {
            return DiskCacheCommon.OpenFile(_basePath, DataFileName, writable: false);
        }

        /// <summary>
        /// Clear all content from the guest cache files.
        /// </summary>
        public void ClearCache()
        {
            using var tocFileStream = DiskCacheCommon.OpenFile(_basePath, TocFileName, writable: true);
            using var dataFileStream = DiskCacheCommon.OpenFile(_basePath, DataFileName, writable: true);

            tocFileStream.SetLength(0);
            dataFileStream.SetLength(0);
        }

        /// <summary>
        /// Loads the guest cache from file or memory cache.
        /// </summary>
        /// <param name="tocFileStream">Guest TOC file stream</param>
        /// <param name="dataFileStream">Guest data file stream</param>
        /// <param name="index">Guest shader index</param>
        /// <returns>Guest code and constant buffer 1 data</returns>
        public GuestCodeAndCbData LoadShader(Stream tocFileStream, Stream dataFileStream, int index)
        {
            if (_cache == null || index >= _cache.Length)
            {
                _cache = new (byte[], byte[])[Math.Max(index + 1, GetShadersCountFromLength(tocFileStream.Length))];
            }

            (byte[] guestCode, byte[] cb1Data) = _cache[index];

            if (guestCode == null || cb1Data == null)
            {
                BinarySerializer tocReader = new(tocFileStream);
                tocFileStream.Seek(Unsafe.SizeOf<TocHeader>() + index * Unsafe.SizeOf<TocEntry>(), SeekOrigin.Begin);

                TocEntry entry = new();
                tocReader.Read(ref entry);

                guestCode = new byte[entry.CodeSize];
                cb1Data = new byte[entry.Cb1DataSize];

                if (entry.Offset >= (ulong)dataFileStream.Length)
                {
                    throw new DiskCacheLoadException(DiskCacheLoadResult.FileCorruptedGeneric);
                }

                dataFileStream.Seek((long)entry.Offset, SeekOrigin.Begin);
                dataFileStream.ReadExactly(cb1Data);
                BinarySerializer.ReadCompressed(dataFileStream, guestCode);

                _cache[index] = (guestCode, cb1Data);
            }

            return new GuestCodeAndCbData(guestCode, cb1Data);
        }

        /// <summary>
        /// Clears guest code memory cache, forcing future loads to be from file.
        /// </summary>
        public void ClearMemoryCache()
        {
            _cache = null;
        }

        /// <summary>
        /// Calculates the guest shaders count from the TOC file length.
        /// </summary>
        /// <param name="length">TOC file length</param>
        /// <returns>Shaders count</returns>
        private static int GetShadersCountFromLength(long length)
        {
            return (int)((length - Unsafe.SizeOf<TocHeader>()) / Unsafe.SizeOf<TocEntry>());
        }

        /// <summary>
        /// Adds a guest shader to the cache.
        /// </summary>
        /// <remarks>
        /// If the shader is already on the cache, the existing index will be returned and nothing will be written.
        /// </remarks>
        /// <param name="data">Guest code</param>
        /// <param name="cb1Data">Constant buffer 1 data accessed by the code</param>
        /// <returns>Index of the shader on the cache</returns>
        public int AddShader(ReadOnlySpan<byte> data, ReadOnlySpan<byte> cb1Data)
        {
            using var tocFileStream = DiskCacheCommon.OpenFile(_basePath, TocFileName, writable: true);
            using var dataFileStream = DiskCacheCommon.OpenFile(_basePath, DataFileName, writable: true);

            TocHeader header = new();

            LoadOrCreateToc(tocFileStream, ref header);

            uint hash = CalcHash(data, cb1Data);

            if (_toc.TryGetValue(hash, out var list))
            {
                foreach (var entry in list)
                {
                    if (data.Length != entry.CodeSize || cb1Data.Length != entry.Cb1DataSize)
                    {
                        continue;
                    }

                    dataFileStream.Seek((long)entry.Offset, SeekOrigin.Begin);
                    byte[] cachedCode = new byte[entry.CodeSize];
                    byte[] cachedCb1Data = new byte[entry.Cb1DataSize];
                    dataFileStream.ReadExactly(cachedCb1Data);
                    BinarySerializer.ReadCompressed(dataFileStream, cachedCode);

                    if (data.SequenceEqual(cachedCode) && cb1Data.SequenceEqual(cachedCb1Data))
                    {
                        return entry.Index;
                    }
                }
            }

            return WriteNewEntry(tocFileStream, dataFileStream, ref header, data, cb1Data, hash);
        }

        /// <summary>
        /// Loads the guest cache TOC file, or create a new one if not present.
        /// </summary>
        /// <param name="tocFileStream">Guest TOC file stream</param>
        /// <param name="header">Set to the TOC file header</param>
        private void LoadOrCreateToc(Stream tocFileStream, ref TocHeader header)
        {
            BinarySerializer reader = new(tocFileStream);

            if (!reader.TryRead(ref header) || header.Magic != TocMagic || header.Version != VersionPacked)
            {
                CreateToc(tocFileStream, ref header);
            }

            if (_toc == null || header.ModificationsCount != _tocModificationsCount)
            {
                if (!LoadTocEntries(tocFileStream, ref reader))
                {
                    CreateToc(tocFileStream, ref header);
                }

                _tocModificationsCount = header.ModificationsCount;
            }
        }

        /// <summary>
        /// Creates a new guest cache TOC file.
        /// </summary>
        /// <param name="tocFileStream">Guest TOC file stream</param>
        /// <param name="header">Set to the TOC header</param>
        private static void CreateToc(Stream tocFileStream, ref TocHeader header)
        {
            BinarySerializer writer = new(tocFileStream);

            header.Magic = TocMagic;
            header.Version = VersionPacked;
            header.Padding = 0;
            header.ModificationsCount = 0;
            header.Reserved = 0;
            header.Reserved2 = 0;

            if (tocFileStream.Length > 0)
            {
                tocFileStream.Seek(0, SeekOrigin.Begin);
                tocFileStream.SetLength(0);
            }

            writer.Write(ref header);
        }

        /// <summary>
        /// Reads all the entries on the guest TOC file.
        /// </summary>
        /// <param name="tocFileStream">Guest TOC file stream</param>
        /// <param name="reader">TOC file reader</param>
        /// <returns>True if the operation was successful, false otherwise</returns>
        private bool LoadTocEntries(Stream tocFileStream, ref BinarySerializer reader)
        {
            _toc = new Dictionary<uint, List<TocMemoryEntry>>();

            TocEntry entry = new();
            int index = 0;

            while (tocFileStream.Position < tocFileStream.Length)
            {
                if (!reader.TryRead(ref entry))
                {
                    return false;
                }

                AddTocMemoryEntry(entry.Offset, entry.CodeSize, entry.Cb1DataSize, entry.Hash, index++);
            }

            return true;
        }

        /// <summary>
        /// Writes a new guest code entry into the file.
        /// </summary>
        /// <param name="tocFileStream">TOC file stream</param>
        /// <param name="dataFileStream">Data file stream</param>
        /// <param name="header">TOC header, to be updated with the new count</param>
        /// <param name="data">Guest code</param>
        /// <param name="cb1Data">Constant buffer 1 data accessed by the guest code</param>
        /// <param name="hash">Code and constant buffer data hash</param>
        /// <returns>Entry index</returns>
        private int WriteNewEntry(
            FileStream tocFileStream,
            FileStream dataFileStream,
            ref TocHeader header,
            ReadOnlySpan<byte> data,
            ReadOnlySpan<byte> cb1Data,
            uint hash)
        {
            BinarySerializer tocWriter = new(tocFileStream);

            dataFileStream.Seek(0, SeekOrigin.End);
            uint dataOffset = checked((uint)dataFileStream.Position);
            uint codeSize = (uint)data.Length;
            uint cb1DataSize = (uint)cb1Data.Length;
            dataFileStream.Write(cb1Data);
            BinarySerializer.WriteCompressed(dataFileStream, data, DiskCacheCommon.GetCompressionAlgorithm());

            _tocModificationsCount = ++header.ModificationsCount;
            tocFileStream.Seek(0, SeekOrigin.Begin);
            tocWriter.Write(ref header);

            TocEntry entry = new()
            {
                Offset = dataOffset,
                CodeSize = codeSize,
                Cb1DataSize = cb1DataSize,
                Hash = hash,
            };

            tocFileStream.Seek(0, SeekOrigin.End);
            int index = (int)((tocFileStream.Position - Unsafe.SizeOf<TocHeader>()) / Unsafe.SizeOf<TocEntry>());

            tocWriter.Write(ref entry);

            AddTocMemoryEntry(dataOffset, codeSize, cb1DataSize, hash, index);

            return index;
        }

        /// <summary>
        /// Adds an entry to the memory TOC cache. This can be used to avoid reading the TOC file all the time.
        /// </summary>
        /// <param name="dataOffset">Offset of the code and constant buffer data in the data file</param>
        /// <param name="codeSize">Code size</param>
        /// <param name="cb1DataSize">Constant buffer 1 data size</param>
        /// <param name="hash">Code and constant buffer data hash</param>
        /// <param name="index">Index of the data on the cache</param>
        private void AddTocMemoryEntry(uint dataOffset, uint codeSize, uint cb1DataSize, uint hash, int index)
        {
            if (!_toc.TryGetValue(hash, out var list))
            {
                _toc.Add(hash, list = new List<TocMemoryEntry>());
            }

            list.Add(new TocMemoryEntry(dataOffset, codeSize, cb1DataSize, index));
        }

        /// <summary>
        /// Calculates the hash for a data pair.
        /// </summary>
        /// <param name="data">Data 1</param>
        /// <param name="data2">Data 2</param>
        /// <returns>Hash of both data</returns>
        private static uint CalcHash(ReadOnlySpan<byte> data, ReadOnlySpan<byte> data2)
        {
            return CalcHash(data2) * 23 ^ CalcHash(data);
        }

        /// <summary>
        /// Calculates the hash for data.
        /// </summary>
        /// <param name="data">Data to be hashed</param>
        /// <returns>Hash of the data</returns>
        private static uint CalcHash(ReadOnlySpan<byte> data)
        {
            return (uint)XXHash128.ComputeHash(data).Low;
        }
    }
}
