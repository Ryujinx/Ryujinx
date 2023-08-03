using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;

namespace Ryujinx.Graphics.Metal
{
    class Sampler : ISampler
    {
        // private readonly MTLSamplerState _mtlSamplerState;

        public Sampler(MTLSamplerState mtlSamplerState)
        {
            // _mtlSamplerState = mtlSamplerState;
        }

        public void Dispose()
        {
        }
    }
}
