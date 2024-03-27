using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.Graphics.Texture;
using Ryujinx.Memory.Range;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// Texture pool.
    /// </summary>
    class TexturePool : Pool<Texture, TextureDescriptor>, IPool<TexturePool>
    {
        /// <summary>
        /// A request to dereference a texture from a pool.
        /// </summary>
        private readonly struct DereferenceRequest
        {
            /// <summary>
            /// Whether the dereference is due to a mapping change or not.
            /// </summary>
            public readonly bool IsRemapped;

            /// <summary>
            /// The texture being dereferenced.
            /// </summary>
            public readonly Texture Texture;

            /// <summary>
            /// The ID of the pool entry this reference belonged to.
            /// </summary>
            public readonly int ID;

            /// <summary>
            /// Create a dereference request for a texture with a specific pool ID, and remapped flag.
            /// </summary>
            /// <param name="isRemapped">Whether the dereference is due to a mapping change or not</param>
            /// <param name="texture">The texture being dereferenced</param>
            /// <param name="id">The ID of the pool entry, used to restore remapped textures</param>
            private DereferenceRequest(bool isRemapped, Texture texture, int id)
            {
                IsRemapped = isRemapped;
                Texture = texture;
                ID = id;
            }

            /// <summary>
            /// Create a dereference request for a texture removal.
            /// </summary>
            /// <param name="texture">The texture being removed</param>
            /// <returns>A texture removal dereference request</returns>
            public static DereferenceRequest Remove(Texture texture)
            {
                return new DereferenceRequest(false, texture, 0);
            }

            /// <summary>
            /// Create a dereference request for a texture remapping with a specific pool ID.
            /// </summary>
            /// <param name="texture">The texture being remapped</param>
            /// <param name="id">The ID of the pool entry, used to restore remapped textures</param>
            /// <returns>A remap dereference request</returns>
            public static DereferenceRequest Remap(Texture texture, int id)
            {
                return new DereferenceRequest(true, texture, id);
            }
        }

        private readonly GpuChannel _channel;
        private readonly ConcurrentQueue<DereferenceRequest> _dereferenceQueue = new();
        private TextureDescriptor _defaultDescriptor;

        /// <summary>
        /// Linked list node used on the texture pool cache.
        /// </summary>
        public LinkedListNode<TexturePool> CacheNode { get; set; }

        /// <summary>
        /// Timestamp used by the texture pool cache, updated on every use of this texture pool.
        /// </summary>
        public ulong CacheTimestamp { get; set; }

        /// <summary>
        /// Creates a new instance of the texture pool.
        /// </summary>
        /// <param name="context">GPU context that the texture pool belongs to</param>
        /// <param name="channel">GPU channel that the texture pool belongs to</param>
        /// <param name="address">Address of the texture pool in guest memory</param>
        /// <param name="maximumId">Maximum texture ID of the texture pool (equal to maximum textures minus one)</param>
        public TexturePool(GpuContext context, GpuChannel channel, ulong address, int maximumId) : base(context, channel.MemoryManager.Physical, address, maximumId)
        {
            _channel = channel;
        }

        /// <summary>
        /// Gets the texture descripor and texture with the given ID with no bounds check or synchronization.
        /// </summary>
        /// <param name="id">ID of the texture. This is effectively a zero-based index</param>
        /// <param name="texture">The texture with the given ID</param>
        /// <returns>The texture descriptor with the given ID</returns>
        private ref readonly TextureDescriptor GetInternal(int id, out Texture texture)
        {
            texture = Items[id];

            ref readonly TextureDescriptor descriptor = ref GetDescriptorRef(id);

            if (texture == null)
            {
                texture = PhysicalMemory.TextureCache.FindShortCache(descriptor);

                if (texture == null)
                {
                    TextureInfo info = GetInfo(descriptor, out int layerSize);

                    // The dereference queue can put our texture back on the cache.
                    if ((texture = ProcessDereferenceQueue(id)) != null)
                    {
                        return ref descriptor;
                    }

                    texture = PhysicalMemory.TextureCache.FindOrCreateTexture(_channel.MemoryManager, TextureSearchFlags.ForSampler, info, layerSize);

                    // If this happens, then the texture address is invalid, we can't add it to the cache.
                    if (texture == null)
                    {
                        return ref descriptor;
                    }
                }
                else
                {
                    texture.SynchronizeMemory();
                }

                Items[id] = texture;

                texture.IncrementReferenceCount(this, id, descriptor.UnpackAddress());

                DescriptorCache[id] = descriptor;
            }
            else
            {
                // On the path above (texture not yet in the pool), memory is automatically synchronized on texture creation.
                texture.SynchronizeMemory();
            }

            return ref descriptor;
        }

        /// <summary>
        /// Gets the texture with the given ID.
        /// </summary>
        /// <param name="id">ID of the texture. This is effectively a zero-based index</param>
        /// <returns>The texture with the given ID</returns>
        public override Texture Get(int id)
        {
            if ((uint)id >= Items.Length)
            {
                return null;
            }

            if (SequenceNumber != Context.SequenceNumber)
            {
                SequenceNumber = Context.SequenceNumber;

                SynchronizeMemory();
            }

            GetInternal(id, out Texture texture);

            return texture;
        }

        /// <summary>
        /// Gets the texture descriptor and texture with the given ID.
        /// </summary>
        /// <remarks>
        /// This method assumes that the pool has been manually synchronized before doing binding.
        /// </remarks>
        /// <param name="id">ID of the texture. This is effectively a zero-based index</param>
        /// <param name="texture">The texture with the given ID</param>
        /// <returns>The texture descriptor with the given ID</returns>
        public ref readonly TextureDescriptor GetForBinding(int id, out Texture texture)
        {
            if ((uint)id >= Items.Length)
            {
                texture = null;
                return ref _defaultDescriptor;
            }

            // When getting for binding, assume the pool has already been synchronized.

            return ref GetInternal(id, out texture);
        }

        /// <summary>
        /// Checks if the pool was modified, and returns the last sequence number where a modification was detected.
        /// </summary>
        /// <returns>A number that increments each time a modification is detected</returns>
        public int CheckModified()
        {
            if (SequenceNumber != Context.SequenceNumber)
            {
                SequenceNumber = Context.SequenceNumber;

                SynchronizeMemory();
            }

            return ModifiedSequenceNumber;
        }

        /// <summary>
        /// Forcibly remove a texture from this pool's items.
        /// If deferred, the dereference will be queued to occur on the render thread.
        /// </summary>
        /// <param name="texture">The texture being removed</param>
        /// <param name="id">The ID of the texture in this pool</param>
        /// <param name="deferred">If true, queue the dereference to happen on the render thread, otherwise dereference immediately</param>
        public void ForceRemove(Texture texture, int id, bool deferred)
        {
            var previous = Interlocked.Exchange(ref Items[id], null);

            if (deferred)
            {
                if (previous != null)
                {
                    _dereferenceQueue.Enqueue(DereferenceRequest.Remove(texture));
                }
            }
            else
            {
                texture.DecrementReferenceCount();
            }
        }

        /// <summary>
        /// Queues a request to update a texture's mapping.
        /// Mapping is updated later to avoid deleting the texture if it is still sparsely mapped.
        /// </summary>
        /// <param name="texture">Texture with potential mapping change</param>
        /// <param name="id">ID in cache of texture with potential mapping change</param>
        public void QueueUpdateMapping(Texture texture, int id)
        {
            if (Interlocked.Exchange(ref Items[id], null) == texture)
            {
                _dereferenceQueue.Enqueue(DereferenceRequest.Remap(texture, id));
            }
        }

        /// <summary>
        /// Process the dereference queue, decrementing the reference count for each texture in it.
        /// This is used to ensure that texture disposal happens on the render thread.
        /// </summary>
        /// <param name="id">The ID of the entry that triggered this method</param>
        /// <returns>Texture that matches the entry ID if it has been readded to the cache.</returns>
        private Texture ProcessDereferenceQueue(int id = -1)
        {
            while (_dereferenceQueue.TryDequeue(out DereferenceRequest request))
            {
                Texture texture = request.Texture;

                // Unmapped storage textures can swap their ranges. The texture must be storage with no views or dependencies.
                // TODO: Would need to update ranges on views, or guarantee that ones where the range changes can be instantly deleted.

                if (request.IsRemapped && texture.Group.Storage == texture && !texture.HasViews && !texture.Group.HasCopyDependencies)
                {
                    // Has the mapping for this texture changed?
                    ref readonly TextureDescriptor descriptor = ref GetDescriptorRef(request.ID);

                    ulong address = descriptor.UnpackAddress();

                    if (!descriptor.Equals(ref DescriptorCache[request.ID]))
                    {
                        // If the pool entry has already been replaced, just remove the texture.

                        texture.DecrementReferenceCount();
                        continue;
                    }

                    MultiRange range = _channel.MemoryManager.Physical.TextureCache.UpdatePartiallyMapped(_channel.MemoryManager, address, texture);

                    // If the texture is not mapped at all, delete its reference.

                    if (range.Count == 1 && range.GetSubRange(0).Address == MemoryManager.PteUnmapped)
                    {
                        texture.DecrementReferenceCount();
                        continue;
                    }

                    Items[request.ID] = texture;

                    // Create a new pool reference, as the last one was removed on unmap.

                    texture.IncrementReferenceCount(this, request.ID, address);
                    texture.DecrementReferenceCount();

                    // Refetch the range. Changes since the last check could have been lost
                    // as the cache entry was not restored (required to queue mapping change).

                    range = _channel.MemoryManager.GetPhysicalRegions(address, texture.Size);

                    if (!range.Equals(texture.Range))
                    {
                        // Part of the texture was mapped or unmapped. Replace the range and regenerate tracking handles.
                        if (!_channel.MemoryManager.Physical.TextureCache.UpdateMapping(texture, range))
                        {
                            // Texture could not be remapped due to a collision, just delete it.
                            if (Interlocked.Exchange(ref Items[request.ID], null) != null)
                            {
                                // If this is null, a request was already queued to decrement reference.
                                texture.DecrementReferenceCount(this, request.ID);
                            }
                            continue;
                        }
                    }

                    if (request.ID == id)
                    {
                        return texture;
                    }
                }
                else
                {
                    texture.DecrementReferenceCount();
                }
            }

            return null;
        }

        /// <summary>
        /// Implementation of the texture pool range invalidation.
        /// </summary>
        /// <param name="address">Start address of the range of the texture pool</param>
        /// <param name="size">Size of the range being invalidated</param>
        protected override void InvalidateRangeImpl(ulong address, ulong size)
        {
            ProcessDereferenceQueue();

            ulong endAddress = address + size;

            for (; address < endAddress; address += DescriptorSize)
            {
                int id = (int)((address - Address) / DescriptorSize);

                Texture texture = Items[id];

                if (texture != null)
                {
                    ref TextureDescriptor cachedDescriptor = ref DescriptorCache[id];
                    ref readonly TextureDescriptor descriptor = ref GetDescriptorRefAddress(address);

                    // If the descriptors are the same, the texture is the same,
                    // we don't need to remove as it was not modified. Just continue.
                    if (descriptor.Equals(ref cachedDescriptor))
                    {
                        continue;
                    }

                    if (texture.HasOneReference())
                    {
                        _channel.MemoryManager.Physical.TextureCache.AddShortCache(texture, ref cachedDescriptor);
                    }

                    if (Interlocked.Exchange(ref Items[id], null) != null)
                    {
                        texture.DecrementReferenceCount(this, id);
                    }
                }
            }
        }

        /// <summary>
        /// Gets texture information from a texture descriptor.
        /// </summary>
        /// <param name="descriptor">The texture descriptor</param>
        /// <param name="layerSize">Layer size for textures using a sub-range of mipmap levels, otherwise 0</param>
        /// <returns>The texture information</returns>
        private static TextureInfo GetInfo(in TextureDescriptor descriptor, out int layerSize)
        {
            int depthOrLayers = descriptor.UnpackDepth();
            int levels = descriptor.UnpackLevels();

            TextureMsaaMode msaaMode = descriptor.UnpackTextureMsaaMode();

            int samplesInX = msaaMode.SamplesInX();
            int samplesInY = msaaMode.SamplesInY();

            int stride = descriptor.UnpackStride();

            TextureDescriptorType descriptorType = descriptor.UnpackTextureDescriptorType();

            bool isLinear = descriptorType == TextureDescriptorType.Linear;

            Target target = descriptor.UnpackTextureTarget().Convert((samplesInX | samplesInY) != 1);

            int width = target == Target.TextureBuffer ? descriptor.UnpackBufferTextureWidth() : descriptor.UnpackWidth();
            int height = descriptor.UnpackHeight();

            if (target == Target.Texture2DMultisample || target == Target.Texture2DMultisampleArray)
            {
                // This is divided back before the backend texture is created.
                width *= samplesInX;
                height *= samplesInY;
            }

            // We use 2D targets for 1D textures as that makes texture cache
            // management easier. We don't know the target for render target
            // and copies, so those would normally use 2D targets, which are
            // not compatible with 1D targets. By doing that we also allow those
            // to match when looking for compatible textures on the cache.
            if (target == Target.Texture1D)
            {
                target = Target.Texture2D;
                height = 1;
            }
            else if (target == Target.Texture1DArray)
            {
                target = Target.Texture2DArray;
                height = 1;
            }

            uint format = descriptor.UnpackFormat();
            bool srgb = descriptor.UnpackSrgb();

            ulong gpuVa = descriptor.UnpackAddress();

            if (!FormatTable.TryGetTextureFormat(format, srgb, out FormatInfo formatInfo))
            {
                if (gpuVa != 0 && format != 0)
                {
                    // Logger.Error?.Print(LogClass.Gpu, $"Invalid texture format 0x{format:X} (sRGB: {srgb}).");
                }

                formatInfo = FormatInfo.Default;
            }

            int gobBlocksInY = descriptor.UnpackGobBlocksInY();
            int gobBlocksInZ = descriptor.UnpackGobBlocksInZ();

            int gobBlocksInTileX = descriptor.UnpackGobBlocksInTileX();

            layerSize = 0;

            int minLod = descriptor.UnpackBaseLevel();
            int maxLod = descriptor.UnpackMaxLevelInclusive();

            // Linear textures don't support mipmaps, so we don't handle this case here.
            if ((minLod != 0 || maxLod + 1 != levels) && target != Target.TextureBuffer && !isLinear)
            {
                int depth = TextureInfo.GetDepth(target, depthOrLayers);
                int layers = TextureInfo.GetLayers(target, depthOrLayers);

                SizeInfo sizeInfo = SizeCalculator.GetBlockLinearTextureSize(
                    width,
                    height,
                    depth,
                    levels,
                    layers,
                    formatInfo.BlockWidth,
                    formatInfo.BlockHeight,
                    formatInfo.BytesPerPixel,
                    gobBlocksInY,
                    gobBlocksInZ,
                    gobBlocksInTileX);

                layerSize = sizeInfo.LayerSize;

                if (minLod != 0 && minLod < levels)
                {
                    // If the base level is not zero, we additionally add the mip level offset
                    // to the address, this allows the texture manager to find the base level from the
                    // address if there is a overlapping texture on the cache that can contain the new texture.
                    gpuVa += (ulong)sizeInfo.GetMipOffset(minLod);

                    width = Math.Max(1, width >> minLod);
                    height = Math.Max(1, height >> minLod);

                    if (target == Target.Texture3D)
                    {
                        depthOrLayers = Math.Max(1, depthOrLayers >> minLod);
                    }

                    (gobBlocksInY, gobBlocksInZ) = SizeCalculator.GetMipGobBlockSizes(height, depth, formatInfo.BlockHeight, gobBlocksInY, gobBlocksInZ, minLod);
                }

                levels = (maxLod - minLod) + 1;
            }

            SwizzleComponent swizzleR = descriptor.UnpackSwizzleR().Convert();
            SwizzleComponent swizzleG = descriptor.UnpackSwizzleG().Convert();
            SwizzleComponent swizzleB = descriptor.UnpackSwizzleB().Convert();
            SwizzleComponent swizzleA = descriptor.UnpackSwizzleA().Convert();

            DepthStencilMode depthStencilMode = GetDepthStencilMode(
                formatInfo.Format,
                swizzleR,
                swizzleG,
                swizzleB,
                swizzleA);

            if (formatInfo.Format.IsDepthOrStencil())
            {
                swizzleR = SwizzleComponent.Red;
                swizzleG = SwizzleComponent.Red;
                swizzleB = SwizzleComponent.Red;

                if (depthStencilMode == DepthStencilMode.Depth)
                {
                    swizzleA = SwizzleComponent.One;
                }
                else
                {
                    swizzleA = SwizzleComponent.Red;
                }
            }

            return new TextureInfo(
                gpuVa,
                width,
                height,
                depthOrLayers,
                levels,
                samplesInX,
                samplesInY,
                stride,
                isLinear,
                gobBlocksInY,
                gobBlocksInZ,
                gobBlocksInTileX,
                target,
                formatInfo,
                depthStencilMode,
                swizzleR,
                swizzleG,
                swizzleB,
                swizzleA);
        }

        /// <summary>
        /// Gets the texture depth-stencil mode, based on the swizzle components of each color channel.
        /// The depth-stencil mode is determined based on how the driver sets those parameters.
        /// </summary>
        /// <param name="format">The format of the texture</param>
        /// <param name="components">The texture swizzle components</param>
        /// <returns>The depth-stencil mode</returns>
        private static DepthStencilMode GetDepthStencilMode(Format format, params SwizzleComponent[] components)
        {
            // R = Depth, G = Stencil.
            // On 24-bits depth formats, this is inverted (Stencil is R etc).
            // NVN setup:
            // For depth, A is set to 1.0f, the other components are set to Depth.
            // For stencil, all components are set to Stencil.
            SwizzleComponent component = components[0];

            for (int index = 1; index < 4 && !IsRG(component); index++)
            {
                component = components[index];
            }

            if (!IsRG(component))
            {
                return DepthStencilMode.Depth;
            }

            if (format == Format.D24UnormS8Uint)
            {
                return component == SwizzleComponent.Red
                    ? DepthStencilMode.Stencil
                    : DepthStencilMode.Depth;
            }
            else
            {
                return component == SwizzleComponent.Red
                    ? DepthStencilMode.Depth
                    : DepthStencilMode.Stencil;
            }
        }

        /// <summary>
        /// Checks if the swizzle component is equal to the red or green channels.
        /// </summary>
        /// <param name="component">The swizzle component to check</param>
        /// <returns>True if the swizzle component is equal to the red or green, false otherwise</returns>
        private static bool IsRG(SwizzleComponent component)
        {
            return component == SwizzleComponent.Red ||
                   component == SwizzleComponent.Green;
        }

        /// <summary>
        /// Decrements the reference count of the texture.
        /// This indicates that the texture pool is not using it anymore.
        /// </summary>
        /// <param name="item">The texture to be deleted</param>
        protected override void Delete(Texture item)
        {
            item?.DecrementReferenceCount(this);
        }

        public override void Dispose()
        {
            ProcessDereferenceQueue();

            base.Dispose();
        }
    }
}
