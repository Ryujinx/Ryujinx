using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
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

            MTLSamplerBorderColor borderColor = GetConstrainedBorderColor(info.BorderColor, out _);

            using var descriptor = new MTLSamplerDescriptor
            {
                BorderColor = borderColor,
                MinFilter = minFilter,
                MagFilter = info.MagFilter.Convert(),
                MipFilter = mipFilter,
                CompareFunction = info.CompareOp.Convert(),
                LodMinClamp = info.MinLod,
                LodMaxClamp = info.MaxLod,
                LodAverage = false,
                MaxAnisotropy = Math.Max((uint)info.MaxAnisotropy, 1),
                SAddressMode = info.AddressU.Convert(),
                TAddressMode = info.AddressV.Convert(),
                RAddressMode = info.AddressP.Convert(),
                SupportArgumentBuffers = true
            };

            var samplerState = device.NewSamplerState(descriptor);

            _mtlSamplerState = samplerState;
        }

        public Sampler(MTLSamplerState samplerState)
        {
            _mtlSamplerState = samplerState;
        }

        private static MTLSamplerBorderColor GetConstrainedBorderColor(ColorF arbitraryBorderColor, out bool cantConstrain)
        {
            float r = arbitraryBorderColor.Red;
            float g = arbitraryBorderColor.Green;
            float b = arbitraryBorderColor.Blue;
            float a = arbitraryBorderColor.Alpha;

            if (r == 0f && g == 0f && b == 0f)
            {
                if (a == 1f)
                {
                    cantConstrain = false;
                    return MTLSamplerBorderColor.OpaqueBlack;
                }

                if (a == 0f)
                {
                    cantConstrain = false;
                    return MTLSamplerBorderColor.TransparentBlack;
                }
            }
            else if (r == 1f && g == 1f && b == 1f && a == 1f)
            {
                cantConstrain = false;
                return MTLSamplerBorderColor.OpaqueWhite;
            }

            cantConstrain = true;
            return MTLSamplerBorderColor.OpaqueBlack;
        }

        public MTLSamplerState GetSampler()
        {
            return _mtlSamplerState;
        }

        public void Dispose()
        {
            _mtlSamplerState.Dispose();
        }
    }
}
