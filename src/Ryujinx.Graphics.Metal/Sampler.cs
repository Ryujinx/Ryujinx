using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Sampler : ISampler
    {
        private readonly MTLSamplerState _mtlSamplerState;

        public Sampler(MTLDevice device, SamplerCreateInfo info)
        {
            (MTLSamplerMinMagFilter minFilter, MTLSamplerMipFilter mipFilter) = info.MinFilter.Convert();

            var samplerState = device.NewSamplerState(new MTLSamplerDescriptor
            {
                BorderColor = MTLSamplerBorderColor.TransparentBlack,
                MinFilter = minFilter,
                MagFilter = info.MagFilter.Convert(),
                MipFilter = mipFilter,
                CompareFunction = info.CompareOp.Convert(),
                LodMinClamp = info.MinLod,
                LodMaxClamp = info.MaxLod,
                LodAverage = false,
                MaxAnisotropy = (uint)info.MaxAnisotropy,
                SAddressMode = info.AddressU.Convert(),
                TAddressMode = info.AddressV.Convert(),
                RAddressMode = info.AddressP.Convert()
            });

            _mtlSamplerState = samplerState;
        }

        public MTLSamplerState GetSampler()
        {
            return _mtlSamplerState;
        }

        public void Dispose()
        {
        }
    }
}
