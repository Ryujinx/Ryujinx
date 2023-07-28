using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;

namespace Ryujinx.Graphics.Metal
{
    public class Sampler : ISampler
    {
        private MTLSamplerState _mtlSamplerState;

        public Sampler(MTLSamplerState mtlSamplerState)
        {
            _mtlSamplerState = mtlSamplerState;
        }

        public void Dispose()
        {
        }
    }
}
