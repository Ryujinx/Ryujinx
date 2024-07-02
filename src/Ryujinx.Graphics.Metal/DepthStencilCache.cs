using Ryujinx.Graphics.Metal.State;
using SharpMetal.Metal;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class DepthStencilCache : StateCache<MTLDepthStencilState, DepthStencilUid, DepthStencilUid>
    {
        private readonly MTLDevice _device;

        public DepthStencilCache(MTLDevice device)
        {
            _device = device;
        }

        protected override DepthStencilUid GetHash(DepthStencilUid descriptor)
        {
            return descriptor;
        }

        protected override MTLDepthStencilState CreateValue(DepthStencilUid descriptor)
        {
            // Create descriptors

            ref StencilUid frontUid = ref descriptor.FrontFace;

            using var frontFaceStencil = new MTLStencilDescriptor
            {
                StencilFailureOperation = frontUid.StencilFailureOperation,
                DepthFailureOperation = frontUid.DepthFailureOperation,
                DepthStencilPassOperation = frontUid.DepthStencilPassOperation,
                StencilCompareFunction = frontUid.StencilCompareFunction,
                ReadMask = frontUid.ReadMask,
                WriteMask = frontUid.WriteMask
            };

            ref StencilUid backUid = ref descriptor.BackFace;

            using var backFaceStencil = new MTLStencilDescriptor
            {
                StencilFailureOperation = backUid.StencilFailureOperation,
                DepthFailureOperation = backUid.DepthFailureOperation,
                DepthStencilPassOperation = backUid.DepthStencilPassOperation,
                StencilCompareFunction = backUid.StencilCompareFunction,
                ReadMask = backUid.ReadMask,
                WriteMask = backUid.WriteMask
            };

            var mtlDescriptor = new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = descriptor.DepthCompareFunction,
                DepthWriteEnabled = descriptor.DepthWriteEnabled
            };

            if (descriptor.StencilTestEnabled)
            {
                mtlDescriptor.BackFaceStencil = backFaceStencil;
                mtlDescriptor.FrontFaceStencil = frontFaceStencil;
            }

            using (mtlDescriptor)
            {
                return _device.NewDepthStencilState(mtlDescriptor);
            }
        }
    }
}
