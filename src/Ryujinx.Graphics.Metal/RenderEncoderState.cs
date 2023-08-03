using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct RenderEncoderState
    {
        private readonly MTLDevice _device;
        // TODO: Work with more than one pipeline state
        private readonly MTLRenderPipelineState _copyPipeline;
        private MTLDepthStencilState _depthStencilState = null;

        private MTLCompareFunction _depthCompareFunction = MTLCompareFunction.Always;
        private bool _depthWriteEnabled = false;

        private MTLStencilDescriptor _backFaceStencil = null;
        private MTLStencilDescriptor _frontFaceStencil = null;

        public PrimitiveTopology Topology = PrimitiveTopology.Triangles;
        public MTLCullMode CullMode = MTLCullMode.None;
        public MTLWinding Winding = MTLWinding.Clockwise;

        public RenderEncoderState(MTLRenderPipelineState copyPipeline, MTLDevice device)
        {
            _device = device;
            _copyPipeline = copyPipeline;
        }

        public readonly void SetEncoderState(MTLRenderCommandEncoder renderCommandEncoder)
        {
            renderCommandEncoder.SetRenderPipelineState(_copyPipeline);
            renderCommandEncoder.SetCullMode(CullMode);
            renderCommandEncoder.SetFrontFacingWinding(Winding);

            if (_depthStencilState != null)
            {
                renderCommandEncoder.SetDepthStencilState(_depthStencilState);
            }
        }

        public MTLDepthStencilState UpdateStencilState(MTLStencilDescriptor backFace, MTLStencilDescriptor frontFace)
        {
            _backFaceStencil = backFace;
            _frontFaceStencil = frontFace;

            return _depthStencilState = _device.NewDepthStencilState(new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = _depthCompareFunction,
                DepthWriteEnabled = _depthWriteEnabled,
                BackFaceStencil = _backFaceStencil,
                FrontFaceStencil = _frontFaceStencil
            });
        }

        public MTLDepthStencilState UpdateDepthState(MTLCompareFunction depthCompareFunction, bool depthWriteEnabled)
        {
            _depthCompareFunction = depthCompareFunction;
            _depthWriteEnabled = depthWriteEnabled;

            return _depthStencilState = _device.NewDepthStencilState(new MTLDepthStencilDescriptor
            {
                DepthCompareFunction = _depthCompareFunction,
                DepthWriteEnabled = _depthWriteEnabled,
                BackFaceStencil = _backFaceStencil,
                FrontFaceStencil = _frontFaceStencil
            });
        }
    }
}
