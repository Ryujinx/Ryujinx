using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    struct RenderEncoderState
    {
        private readonly MTLDevice _device;
        private readonly MTLFunction _vertexFunction = null;
        private readonly MTLFunction _fragmentFunction = null;
        private MTLDepthStencilState _depthStencilState = null;

        private MTLCompareFunction _depthCompareFunction = MTLCompareFunction.Always;
        private bool _depthWriteEnabled = false;

        private MTLStencilDescriptor _backFaceStencil = null;
        private MTLStencilDescriptor _frontFaceStencil = null;

        public PrimitiveTopology Topology = PrimitiveTopology.Triangles;
        public MTLCullMode CullMode = MTLCullMode.None;
        public MTLWinding Winding = MTLWinding.Clockwise;

        public RenderEncoderState(MTLFunction vertexFunction, MTLFunction fragmentFunction, MTLDevice device)
        {
            _vertexFunction = vertexFunction;
            _fragmentFunction = fragmentFunction;
            _device = device;
        }

        public readonly void SetEncoderState(MTLRenderCommandEncoder renderCommandEncoder, MTLVertexDescriptor vertexDescriptor)
        {
            var renderPipelineDescriptor = new MTLRenderPipelineDescriptor
            {
                VertexDescriptor = vertexDescriptor
            };

            if (_vertexFunction != null)
            {
                renderPipelineDescriptor.VertexFunction = _vertexFunction;
            }

            if (_fragmentFunction != null)
            {
                renderPipelineDescriptor.FragmentFunction = _fragmentFunction;
            }

            var attachment = renderPipelineDescriptor.ColorAttachments.Object(0);
            attachment.SetBlendingEnabled(true);
            attachment.PixelFormat = MTLPixelFormat.BGRA8Unorm;
            attachment.SourceAlphaBlendFactor = MTLBlendFactor.SourceAlpha;
            attachment.DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
            attachment.SourceRGBBlendFactor = MTLBlendFactor.SourceAlpha;
            attachment.DestinationRGBBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;

            var error = new NSError(IntPtr.Zero);
            var pipelineState = _device.NewRenderPipelineState(renderPipelineDescriptor, ref error);
            if (error != IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Render Pipeline State: {StringHelper.String(error.LocalizedDescription)}");
            }

            renderCommandEncoder.SetRenderPipelineState(pipelineState);
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
