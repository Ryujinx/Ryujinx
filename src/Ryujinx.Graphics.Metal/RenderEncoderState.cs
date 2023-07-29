using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct RenderEncoderState
    {
        public MTLRenderPipelineState RenderPipelineState;
        public PrimitiveTopology Topology = PrimitiveTopology.Triangles;
        public MTLCullMode CullMode = MTLCullMode.None;
        public MTLWinding Winding = MTLWinding.Clockwise;
        public MTLDepthStencilState DepthStencilState = null;

        public RenderEncoderState(MTLRenderPipelineState renderPipelineState)
        {
            RenderPipelineState = renderPipelineState;
        }

        public void SetEncoderState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetRenderPipelineState(RenderPipelineState);
            renderCommandEncoder.SetCullMode(CullMode);
            renderCommandEncoder.SetFrontFacingWinding(Winding);
            renderCommandEncoder.SetDepthStencilState(DepthStencilState);
        }
    }
}
