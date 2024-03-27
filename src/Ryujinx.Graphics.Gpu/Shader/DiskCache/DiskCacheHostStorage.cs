using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Ryujinx.Graphics.Gpu.Shader.DiskCache
{
    /// <summary>
    /// On-disk shader cache storage for host code.
    /// </summary>
    class DiskCacheHostStorage
    {
        private const uint TocsMagic = (byte)'T' | ((byte)'O' << 8) | ((byte)'C' << 16) | ((byte)'S' << 24);
        private const uint TochMagic = (byte)'T' | ((byte)'O' << 8) | ((byte)'C' << 16) | ((byte)'H' << 24);
        private const uint ShdiMagic = (byte)'S' | ((byte)'H' << 8) | ((byte)'D' << 16) | ((byte)'I' << 24);
        private const uint BufdMagic = (byte)'B' | ((byte)'U' << 8) | ((byte)'F' << 16) | ((byte)'D' << 24);
        private const uint TexdMagic = (byte)'T' | ((byte)'E' << 8) | ((byte)'X' << 16) | ((byte)'D' << 24);

        private const ushort FileFormatVersionMajor = 1;
        private const ushort FileFormatVersionMinor = 2;
        private const uint FileFormatVersionPacked = ((uint)FileFormatVersionMajor << 16) | FileFormatVersionMinor;
        private const uint CodeGenVersion = 6489;

        private const string SharedTocFileName = "shared.toc";
        private const string SharedDataFileName = "shared.data";

        private readonly string _basePath;

        public bool CacheEnabled => !string.IsNullOrEmpty(_basePath);

        /// <summary>
        /// TOC (Table of contents) file header.
        /// </summary>
        private struct TocHeader
        {
            /// <summary>
            /// Magic value, for validation and identification.
            /// </summary>
            public uint Magic;

            /// <summary>
            /// File format version.
            /// </summary>
            public uint FormatVersion;

            /// <summary>
            /// Generated shader code version.
            /// </summary>
            public uint CodeGenVersion;

            /// <summary>
            /// Header padding.
            /// </summary>
            public uint Padding;

            /// <summary>
            /// Timestamp of when the file was first created.
            /// </summary>
            public ulong Timestamp;

            /// <summary>
            /// Reserved space, to be used in the future. Write as zero.
            /// </summary>
            public ulong Reserved;
        }

        /// <summary>
        /// Offset and size pair.
        /// </summary>
        private struct OffsetAndSize
        {
            /// <summary>
            /// Offset.
            /// </summary>
            public ulong Offset;

            /// <summary>
            /// Size of uncompressed data.
            /// </summary>
            public uint UncompressedSize;

            /// <summary>
            /// Size of compressed data.
            /// </summary>
            public uint CompressedSize;
        }

        /// <summary>
        /// Per-stage data entry.
        /// </summary>
        private struct DataEntryPerStage
        {
            /// <summary>
            /// Index of the guest code on the guest code cache TOC file.
            /// </summary>
            public int GuestCodeIndex;
        }

        /// <summary>
        /// Per-program data entry.
        /// </summary>
        private struct DataEntry
        {
            /// <summary>
            /// Bit mask where each bit set is a used shader stage. Should be zero for compute shaders.
            /// </summary>
            public uint StagesBitMask;
        }

        /// <summary>
        /// Per-stage shader information, returned by the translator.
        /// </summary>
        private struct DataShaderInfo
        {
            /// <summary>
            /// Total constant buffers used.
            /// </summary>
            public ushort CBuffersCount;

            /// <summary>
            /// Total storage buffers used.
            /// </summary>
            public ushort SBuffersCount;

            /// <summary>
            /// Total textures used.
            /// </summary>
            public ushort TexturesCount;

            /// <summary>
            /// Total images used.
            /// </summary>
            public ushort ImagesCount;

            /// <summary>
            /// Shader stage.
            /// </summary>
            public ShaderStage Stage;

            /// <summary>
            /// Number of vertices that each output primitive has on a geometry shader.
            /// </summary>
            public byte GeometryVerticesPerPrimitive;

            /// <summary>
            /// Maximum number of vertices that a geometry shader may generate.
            /// </summary>
            public ushort GeometryMaxOutputVertices;

            /// <summary>
            /// Number of invocations per primitive on tessellation or geometry shaders.
            /// </summary>
            public ushort ThreadsPerInputPrimitive;

            /// <summary>
            /// Indicates if the fragment shader accesses the fragment coordinate built-in variable.
            /// </summary>
            public bool UsesFragCoord;

            /// <summary>
            /// Indicates if the shader accesses the Instance ID built-in variable.
            /// </summary>
            public bool UsesInstanceId;

            /// <summary>
            /// Indicates if the shader modifies the Layer built-in variable.
            /// </summary>
            public bool UsesRtLayer;

            /// <summary>
            /// Bit mask with the clip distances written on the vertex stage.
            /// </summary>
            public byte ClipDistancesWritten;

            /// <summary>
            /// Bit mask of the render target components written by the fragment stage.
            /// </summary>
            public int FragmentOutputMap;

            /// <summary>
            /// Indicates if the vertex shader accesses draw parameters.
            /// </summary>
            public bool UsesDrawParameters;
        }

        private readonly DiskCacheGuestStorage _guestStorage;

        /// <summary>
        /// Creates a disk cache host storage.
        /// </summary>
        /// <param name="basePath">Base path of the shader cache</param>
        public DiskCacheHostStorage(string basePath)
        {
            _basePath = basePath;
            _guestStorage = new DiskCacheGuestStorage(basePath);

            if (CacheEnabled)
            {
                Directory.CreateDirectory(basePath);
            }
        }

        /// <summary>
        /// Gets the total of host programs on the cache.
        /// </summary>
        /// <returns>Host programs count</returns>
        public int GetProgramCount()
        {
            string tocFilePath = Path.Combine(_basePath, SharedTocFileName);

            if (!File.Exists(tocFilePath))
            {
                return 0;
            }

            return Math.Max((int)((new FileInfo(tocFilePath).Length - Unsafe.SizeOf<TocHeader>()) / sizeof(ulong)), 0);
        }

        /// <summary>
        /// Guest the name of the host program cache file, with extension.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <returns>Name of the file, without extension</returns>
        private static string GetHostFileName(GpuContext context)
        {
            string apiName = context.Capabilities.Api.ToString().ToLowerInvariant();
            string vendorName = RemoveInvalidCharacters(context.Capabilities.VendorName.ToLowerInvariant());
            return $"{apiName}_{vendorName}";
        }

        /// <summary>
        /// Removes invalid path characters and spaces from a file name.
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <returns>Filtered file name</returns>
        private static string RemoveInvalidCharacters(string fileName)
        {
            int indexOfSpace = fileName.IndexOf(' ');
            if (indexOfSpace >= 0)
            {
                fileName = fileName[..indexOfSpace];
            }

            return string.Concat(fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// Gets the name of the TOC host file.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <returns>File name</returns>
        private static string GetHostTocFileName(GpuContext context)
        {
            return GetHostFileName(context) + ".toc";
        }

        /// <summary>
        /// Gets the name of the data host file.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <returns>File name</returns>
        private static string GetHostDataFileName(GpuContext context)
        {
            return GetHostFileName(context) + ".data";
        }

        /// <summary>
        /// Checks if a disk cache exists for the current application.
        /// </summary>
        /// <returns>True if a disk cache exists, false otherwise</returns>
        public bool CacheExists()
        {
            string tocFilePath = Path.Combine(_basePath, SharedTocFileName);
            string dataFilePath = Path.Combine(_basePath, SharedDataFileName);

            if (!File.Exists(tocFilePath) || !File.Exists(dataFilePath) || !_guestStorage.TocFileExists() || !_guestStorage.DataFileExists())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Loads all shaders from the cache.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="loader">Parallel disk cache loader</param>
        public void LoadShaders(GpuContext context, ParallelDiskCacheLoader loader)
        {
            if (!CacheExists())
            {
                return;
            }

            Stream hostTocFileStream = null;
            Stream hostDataFileStream = null;

            try
            {
                using var tocFileStream = DiskCacheCommon.OpenFile(_basePath, SharedTocFileName, writable: false);
                using var dataFileStream = DiskCacheCommon.OpenFile(_basePath, SharedDataFileName, writable: false);

                using var guestTocFileStream = _guestStorage.OpenTocFileStream();
                using var guestDataFileStream = _guestStorage.OpenDataFileStream();

                BinarySerializer tocReader = new(tocFileStream);
                BinarySerializer dataReader = new(dataFileStream);

                TocHeader header = new();

                if (!tocReader.TryRead(ref header) || header.Magic != TocsMagic)
                {
                    throw new DiskCacheLoadException(DiskCacheLoadResult.FileCorruptedGeneric);
                }

                if (header.FormatVersion != FileFormatVersionPacked)
                {
                    throw new DiskCacheLoadException(DiskCacheLoadResult.IncompatibleVersion);
                }

                bool loadHostCache = header.CodeGenVersion == CodeGenVersion;

                int programIndex = 0;

                DataEntry entry = new();

                while (tocFileStream.Position < tocFileStream.Length && loader.Active)
                {
                    ulong dataOffset = 0;
                    tocReader.Read(ref dataOffset);

                    if ((ulong)dataOffset >= (ulong)dataFileStream.Length)
                    {
                        throw new DiskCacheLoadException(DiskCacheLoadResult.FileCorruptedGeneric);
                    }

                    dataFileStream.Seek((long)dataOffset, SeekOrigin.Begin);

                    dataReader.BeginCompression();
                    dataReader.Read(ref entry);
                    uint stagesBitMask = entry.StagesBitMask;

                    if ((stagesBitMask & ~0x3fu) != 0)
                    {
                        throw new DiskCacheLoadException(DiskCacheLoadResult.FileCorruptedGeneric);
                    }

                    bool isCompute = stagesBitMask == 0;
                    if (isCompute)
                    {
                        stagesBitMask = 1;
                    }

                    GuestCodeAndCbData?[] guestShaders = new GuestCodeAndCbData?[isCompute ? 1 : Constants.ShaderStages + 1];

                    DataEntryPerStage stageEntry = new();

                    while (stagesBitMask != 0)
                    {
                        int stageIndex = BitOperations.TrailingZeroCount(stagesBitMask);

                        dataReader.Read(ref stageEntry);

                        guestShaders[stageIndex] = _guestStorage.LoadShader(
                            guestTocFileStream,
                            guestDataFileStream,
                            stageEntry.GuestCodeIndex);

                        stagesBitMask &= ~(1u << stageIndex);
                    }

                    ShaderSpecializationState specState = ShaderSpecializationState.Read(ref dataReader);
                    dataReader.EndCompression();

                    if (loadHostCache)
                    {
                        (byte[] hostCode, CachedShaderStage[] shaders) = ReadHostCode(
                            context,
                            ref hostTocFileStream,
                            ref hostDataFileStream,
                            guestShaders,
                            programIndex,
                            header.Timestamp);

                        if (hostCode != null)
                        {
                            ShaderInfo shaderInfo = ShaderInfoBuilder.BuildForCache(
                                context,
                                shaders,
                                specState.PipelineState,
                                specState.TransformFeedbackDescriptors != null);

                            IProgram hostProgram;

                            if (context.Capabilities.Api == TargetApi.Vulkan)
                            {
                                ShaderSource[] shaderSources = ShaderBinarySerializer.Unpack(shaders, hostCode);

                                hostProgram = context.Renderer.CreateProgram(shaderSources, shaderInfo);
                            }
                            else
                            {
                                bool hasFragmentShader = shaders.Length > 5 && shaders[5] != null;

                                hostProgram = context.Renderer.LoadProgramBinary(hostCode, hasFragmentShader, shaderInfo);
                            }

                            CachedShaderProgram program = new(hostProgram, specState, shaders);

                            loader.QueueHostProgram(program, hostCode, programIndex, isCompute);
                        }
                        else
                        {
                            loadHostCache = false;
                        }
                    }

                    if (!loadHostCache)
                    {
                        loader.QueueGuestProgram(guestShaders, specState, programIndex, isCompute);
                    }

                    loader.CheckCompilation();
                    programIndex++;
                }
            }
            finally
            {
                _guestStorage.ClearMemoryCache();

                hostTocFileStream?.Dispose();
                hostDataFileStream?.Dispose();
            }
        }

        /// <summary>
        /// Reads the host code for a given shader, if existent.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="tocFileStream">Host TOC file stream, intialized if needed</param>
        /// <param name="dataFileStream">Host data file stream, initialized if needed</param>
        /// <param name="guestShaders">Guest shader code for each active stage</param>
        /// <param name="programIndex">Index of the program on the cache</param>
        /// <param name="expectedTimestamp">Timestamp of the shared cache file. The host file must be newer than it</param>
        /// <returns>Host binary code, or null if not found</returns>
        private (byte[], CachedShaderStage[]) ReadHostCode(
            GpuContext context,
            ref Stream tocFileStream,
            ref Stream dataFileStream,
            GuestCodeAndCbData?[] guestShaders,
            int programIndex,
            ulong expectedTimestamp)
        {
            if (tocFileStream == null && dataFileStream == null)
            {
                string tocFilePath = Path.Combine(_basePath, GetHostTocFileName(context));
                string dataFilePath = Path.Combine(_basePath, GetHostDataFileName(context));

                if (!File.Exists(tocFilePath) || !File.Exists(dataFilePath))
                {
                    return (null, null);
                }

                tocFileStream = DiskCacheCommon.OpenFile(_basePath, GetHostTocFileName(context), writable: false);
                dataFileStream = DiskCacheCommon.OpenFile(_basePath, GetHostDataFileName(context), writable: false);

                BinarySerializer tempTocReader = new(tocFileStream);

                TocHeader header = new();

                tempTocReader.Read(ref header);

                if (header.Timestamp < expectedTimestamp)
                {
                    return (null, null);
                }
            }

            int offset = Unsafe.SizeOf<TocHeader>() + programIndex * Unsafe.SizeOf<OffsetAndSize>();
            if (offset + Unsafe.SizeOf<OffsetAndSize>() > tocFileStream.Length)
            {
                return (null, null);
            }

            if ((ulong)offset >= (ulong)dataFileStream.Length)
            {
                throw new DiskCacheLoadException(DiskCacheLoadResult.FileCorruptedGeneric);
            }

            tocFileStream.Seek(offset, SeekOrigin.Begin);

            BinarySerializer tocReader = new(tocFileStream);

            OffsetAndSize offsetAndSize = new();
            tocReader.Read(ref offsetAndSize);

            if (offsetAndSize.Offset >= (ulong)dataFileStream.Length)
            {
                throw new DiskCacheLoadException(DiskCacheLoadResult.FileCorruptedGeneric);
            }

            dataFileStream.Seek((long)offsetAndSize.Offset, SeekOrigin.Begin);

            byte[] hostCode = new byte[offsetAndSize.UncompressedSize];

            BinarySerializer.ReadCompressed(dataFileStream, hostCode);

            CachedShaderStage[] shaders = new CachedShaderStage[guestShaders.Length];
            BinarySerializer dataReader = new(dataFileStream);

            dataFileStream.Seek((long)(offsetAndSize.Offset + offsetAndSize.CompressedSize), SeekOrigin.Begin);

            dataReader.BeginCompression();

            for (int index = 0; index < guestShaders.Length; index++)
            {
                if (!guestShaders[index].HasValue)
                {
                    continue;
                }

                GuestCodeAndCbData guestShader = guestShaders[index].Value;
                ShaderProgramInfo info = index != 0 || guestShaders.Length == 1 ? ReadShaderProgramInfo(ref dataReader) : null;

                shaders[index] = new CachedShaderStage(info, guestShader.Code, guestShader.Cb1Data);
            }

            dataReader.EndCompression();

            return (hostCode, shaders);
        }

        /// <summary>
        /// Gets output streams for the disk cache, for faster batch writing.
        /// </summary>
        /// <param name="context">The GPU context, used to determine the host disk cache</param>
        /// <returns>A collection of disk cache output streams</returns>
        public DiskCacheOutputStreams GetOutputStreams(GpuContext context)
        {
            var tocFileStream = DiskCacheCommon.OpenFile(_basePath, SharedTocFileName, writable: true);
            var dataFileStream = DiskCacheCommon.OpenFile(_basePath, SharedDataFileName, writable: true);

            var hostTocFileStream = DiskCacheCommon.OpenFile(_basePath, GetHostTocFileName(context), writable: true);
            var hostDataFileStream = DiskCacheCommon.OpenFile(_basePath, GetHostDataFileName(context), writable: true);

            return new DiskCacheOutputStreams(tocFileStream, dataFileStream, hostTocFileStream, hostDataFileStream);
        }

        /// <summary>
        /// Adds a shader to the cache.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="program">Cached program</param>
        /// <param name="hostCode">Optional host binary code</param>
        /// <param name="streams">Output streams to use</param>
        public void AddShader(GpuContext context, CachedShaderProgram program, ReadOnlySpan<byte> hostCode, DiskCacheOutputStreams streams = null)
        {
            uint stagesBitMask = 0;

            for (int index = 0; index < program.Shaders.Length; index++)
            {
                var shader = program.Shaders[index];
                if (shader == null || (shader.Info != null && shader.Info.Stage == ShaderStage.Compute))
                {
                    continue;
                }

                stagesBitMask |= 1u << index;
            }

            var tocFileStream = streams != null ? streams.TocFileStream : DiskCacheCommon.OpenFile(_basePath, SharedTocFileName, writable: true);
            var dataFileStream = streams != null ? streams.DataFileStream : DiskCacheCommon.OpenFile(_basePath, SharedDataFileName, writable: true);

            ulong timestamp = (ulong)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

            if (tocFileStream.Length == 0)
            {
                TocHeader header = new();
                CreateToc(tocFileStream, ref header, TocsMagic, CodeGenVersion, timestamp);
            }

            tocFileStream.Seek(0, SeekOrigin.End);
            dataFileStream.Seek(0, SeekOrigin.End);

            BinarySerializer tocWriter = new(tocFileStream);
            BinarySerializer dataWriter = new(dataFileStream);

            ulong dataOffset = (ulong)dataFileStream.Position;
            tocWriter.Write(ref dataOffset);

            DataEntry entry = new()
            {
                StagesBitMask = stagesBitMask,
            };

            dataWriter.BeginCompression(DiskCacheCommon.GetCompressionAlgorithm());
            dataWriter.Write(ref entry);

            DataEntryPerStage stageEntry = new();

            for (int index = 0; index < program.Shaders.Length; index++)
            {
                var shader = program.Shaders[index];
                if (shader == null)
                {
                    continue;
                }

                stageEntry.GuestCodeIndex = _guestStorage.AddShader(shader.Code, shader.Cb1Data);

                dataWriter.Write(ref stageEntry);
            }

            program.SpecializationState.Write(ref dataWriter);
            dataWriter.EndCompression();

            if (streams == null)
            {
                tocFileStream.Dispose();
                dataFileStream.Dispose();
            }

            if (hostCode.IsEmpty)
            {
                return;
            }

            WriteHostCode(context, hostCode, program.Shaders, streams, timestamp);
        }

        /// <summary>
        /// Clears all content from the guest cache files.
        /// </summary>
        public void ClearGuestCache()
        {
            _guestStorage.ClearCache();
        }

        /// <summary>
        /// Clears all content from the shared cache files.
        /// </summary>
        /// <param name="context">GPU context</param>
        public void ClearSharedCache()
        {
            using var tocFileStream = DiskCacheCommon.OpenFile(_basePath, SharedTocFileName, writable: true);
            using var dataFileStream = DiskCacheCommon.OpenFile(_basePath, SharedDataFileName, writable: true);

            tocFileStream.SetLength(0);
            dataFileStream.SetLength(0);
        }

        /// <summary>
        /// Deletes all content from the host cache files.
        /// </summary>
        /// <param name="context">GPU context</param>
        public void ClearHostCache(GpuContext context)
        {
            using var tocFileStream = DiskCacheCommon.OpenFile(_basePath, GetHostTocFileName(context), writable: true);
            using var dataFileStream = DiskCacheCommon.OpenFile(_basePath, GetHostDataFileName(context), writable: true);

            tocFileStream.SetLength(0);
            dataFileStream.SetLength(0);
        }

        /// <summary>
        /// Writes the host binary code on the host cache.
        /// </summary>
        /// <param name="context">GPU context</param>
        /// <param name="hostCode">Host binary code</param>
        /// <param name="shaders">Shader stages to be added to the host cache</param>
        /// <param name="streams">Output streams to use</param>
        /// <param name="timestamp">File creation timestamp</param>
        private void WriteHostCode(
            GpuContext context,
            ReadOnlySpan<byte> hostCode,
            CachedShaderStage[] shaders,
            DiskCacheOutputStreams streams,
            ulong timestamp)
        {
            var tocFileStream = streams != null ? streams.HostTocFileStream : DiskCacheCommon.OpenFile(_basePath, GetHostTocFileName(context), writable: true);
            var dataFileStream = streams != null ? streams.HostDataFileStream : DiskCacheCommon.OpenFile(_basePath, GetHostDataFileName(context), writable: true);

            if (tocFileStream.Length == 0)
            {
                TocHeader header = new();
                CreateToc(tocFileStream, ref header, TochMagic, 0, timestamp);
            }

            tocFileStream.Seek(0, SeekOrigin.End);
            dataFileStream.Seek(0, SeekOrigin.End);

            BinarySerializer tocWriter = new(tocFileStream);
            BinarySerializer dataWriter = new(dataFileStream);

            OffsetAndSize offsetAndSize = new()
            {
                Offset = (ulong)dataFileStream.Position,
                UncompressedSize = (uint)hostCode.Length,
            };

            long dataStartPosition = dataFileStream.Position;

            BinarySerializer.WriteCompressed(dataFileStream, hostCode, DiskCacheCommon.GetCompressionAlgorithm());

            offsetAndSize.CompressedSize = (uint)(dataFileStream.Position - dataStartPosition);

            tocWriter.Write(ref offsetAndSize);

            dataWriter.BeginCompression(DiskCacheCommon.GetCompressionAlgorithm());

            for (int index = 0; index < shaders.Length; index++)
            {
                if (shaders[index] != null)
                {
                    WriteShaderProgramInfo(ref dataWriter, shaders[index].Info);
                }
            }

            dataWriter.EndCompression();

            if (streams == null)
            {
                tocFileStream.Dispose();
                dataFileStream.Dispose();
            }
        }

        /// <summary>
        /// Creates a TOC file for the host or shared cache.
        /// </summary>
        /// <param name="tocFileStream">TOC file stream</param>
        /// <param name="header">Set to the TOC file header</param>
        /// <param name="magic">Magic value to be written</param>
        /// <param name="codegenVersion">Shader codegen version, only valid for the host file</param>
        /// <param name="timestamp">File creation timestamp</param>
        private static void CreateToc(Stream tocFileStream, ref TocHeader header, uint magic, uint codegenVersion, ulong timestamp)
        {
            BinarySerializer writer = new(tocFileStream);

            header.Magic = magic;
            header.FormatVersion = FileFormatVersionPacked;
            header.CodeGenVersion = codegenVersion;
            header.Padding = 0;
            header.Reserved = 0;
            header.Timestamp = timestamp;

            if (tocFileStream.Length > 0)
            {
                tocFileStream.Seek(0, SeekOrigin.Begin);
                tocFileStream.SetLength(0);
            }

            writer.Write(ref header);
        }

        /// <summary>
        /// Reads the shader program info from the cache.
        /// </summary>
        /// <param name="dataReader">Cache data reader</param>
        /// <returns>Shader program info</returns>
        private static ShaderProgramInfo ReadShaderProgramInfo(ref BinarySerializer dataReader)
        {
            DataShaderInfo dataInfo = new();

            dataReader.ReadWithMagicAndSize(ref dataInfo, ShdiMagic);

            BufferDescriptor[] cBuffers = new BufferDescriptor[dataInfo.CBuffersCount];
            BufferDescriptor[] sBuffers = new BufferDescriptor[dataInfo.SBuffersCount];
            TextureDescriptor[] textures = new TextureDescriptor[dataInfo.TexturesCount];
            TextureDescriptor[] images = new TextureDescriptor[dataInfo.ImagesCount];

            for (int index = 0; index < dataInfo.CBuffersCount; index++)
            {
                dataReader.ReadWithMagicAndSize(ref cBuffers[index], BufdMagic);
            }

            for (int index = 0; index < dataInfo.SBuffersCount; index++)
            {
                dataReader.ReadWithMagicAndSize(ref sBuffers[index], BufdMagic);
            }

            for (int index = 0; index < dataInfo.TexturesCount; index++)
            {
                dataReader.ReadWithMagicAndSize(ref textures[index], TexdMagic);
            }

            for (int index = 0; index < dataInfo.ImagesCount; index++)
            {
                dataReader.ReadWithMagicAndSize(ref images[index], TexdMagic);
            }

            return new ShaderProgramInfo(
                cBuffers,
                sBuffers,
                textures,
                images,
                dataInfo.Stage,
                dataInfo.GeometryVerticesPerPrimitive,
                dataInfo.GeometryMaxOutputVertices,
                dataInfo.ThreadsPerInputPrimitive,
                dataInfo.UsesFragCoord,
                dataInfo.UsesInstanceId,
                dataInfo.UsesDrawParameters,
                dataInfo.UsesRtLayer,
                dataInfo.ClipDistancesWritten,
                dataInfo.FragmentOutputMap);
        }

        /// <summary>
        /// Writes the shader program info into the cache.
        /// </summary>
        /// <param name="dataWriter">Cache data writer</param>
        /// <param name="info">Program info</param>
        private static void WriteShaderProgramInfo(ref BinarySerializer dataWriter, ShaderProgramInfo info)
        {
            if (info == null)
            {
                return;
            }

            DataShaderInfo dataInfo = new()
            {
                CBuffersCount = (ushort)info.CBuffers.Count,
                SBuffersCount = (ushort)info.SBuffers.Count,
                TexturesCount = (ushort)info.Textures.Count,
                ImagesCount = (ushort)info.Images.Count,
                Stage = info.Stage,
                GeometryVerticesPerPrimitive = (byte)info.GeometryVerticesPerPrimitive,
                GeometryMaxOutputVertices = (ushort)info.GeometryMaxOutputVertices,
                ThreadsPerInputPrimitive = (ushort)info.ThreadsPerInputPrimitive,
                UsesFragCoord = info.UsesFragCoord,
                UsesInstanceId = info.UsesInstanceId,
                UsesDrawParameters = info.UsesDrawParameters,
                UsesRtLayer = info.UsesRtLayer,
                ClipDistancesWritten = info.ClipDistancesWritten,
                FragmentOutputMap = info.FragmentOutputMap,
            };

            dataWriter.WriteWithMagicAndSize(ref dataInfo, ShdiMagic);

            for (int index = 0; index < info.CBuffers.Count; index++)
            {
                var entry = info.CBuffers[index];
                dataWriter.WriteWithMagicAndSize(ref entry, BufdMagic);
            }

            for (int index = 0; index < info.SBuffers.Count; index++)
            {
                var entry = info.SBuffers[index];
                dataWriter.WriteWithMagicAndSize(ref entry, BufdMagic);
            }

            for (int index = 0; index < info.Textures.Count; index++)
            {
                var entry = info.Textures[index];
                dataWriter.WriteWithMagicAndSize(ref entry, TexdMagic);
            }

            for (int index = 0; index < info.Images.Count; index++)
            {
                var entry = info.Images[index];
                dataWriter.WriteWithMagicAndSize(ref entry, TexdMagic);
            }
        }
    }
}
