using Ryujinx.Common.Memory;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using System;

namespace Ryujinx.Graphics.Vulkan
{
    struct PipelineDynamicState
    {
        private float _depthBiasSlopeFactor;
        private float _depthBiasConstantFactor;
        private float _depthBiasClamp;
        private bool _depthBiasEnable;

        public int ScissorsCount;
        private Array16<Rect2D> _scissors;

        private uint _backCompareMask;
        private uint _backWriteMask;
        private uint _backReference;
        private uint _frontCompareMask;
        private uint _frontWriteMask;
        private uint _frontReference;

        private bool _opToo;
        private StencilOp _backFailOp;
        private StencilOp _backPassOp;
        private StencilOp _backDepthFailOp;
        private CompareOp _backCompareOp;
        private StencilOp _frontFailOp;
        private StencilOp _frontPassOp;
        private StencilOp _frontDepthFailOp;
        private CompareOp _frontCompareOp;

        private float _lineWidth;

        public bool StencilTestEnable;

        public bool DepthTestEnable;
        public bool DepthWriteEnable;
        private CompareOp _depthCompareOp;

        private Array4<float> _blendConstants;

        public uint ViewportsCount;
        public Array16<Viewport> Viewports;

        public CullModeFlags CullMode;
        private FrontFace _frontFace;

        private bool _discard;

        private LogicOp _logicOp;

        private uint _patchControlPoints;

        public PrimitiveTopology Topology;

        private bool _primitiveRestartEnable;

        [Flags]
        private enum DirtyFlags
        {
            None = 0,
            Blend = 1 << 0,
            DepthBias = 1 << 1,
            Scissor = 1 << 2,
            Stencil = 1 << 3,
            Viewport = 1 << 4,
            CullMode = 1 << 5,
            FrontFace = 1 << 6,
            DepthTestBool = 1 << 7,
            DepthTestCompareOp = 1 << 8,
            StencilTestEnable = 1 << 9,
            LineWidth = 1 << 10,
            RasterDiscard = 1 << 11,
            LogicOp = 1 << 12,
            PatchControlPoints = 1 << 13,
            PrimitiveRestart = 1 << 14,
            PrimitiveTopology = 1 << 15,
            Standard = Blend | DepthBias | Scissor | Stencil | Viewport | LineWidth,
            Extended = CullMode | FrontFace | DepthTestBool | DepthTestCompareOp | StencilTestEnable | PrimitiveTopology,
            Extended2 = RasterDiscard | LogicOp | PatchControlPoints | PrimitiveRestart,
        }

        private DirtyFlags _dirty;

        public void SetBlendConstants(float r, float g, float b, float a)
        {
            _blendConstants[0] = r;
            _blendConstants[1] = g;
            _blendConstants[2] = b;
            _blendConstants[3] = a;
            _dirty |= DirtyFlags.Blend;
        }

        public void SetDepthBias(float slopeFactor, float constantFactor, float clamp, bool enable)
        {
            _depthBiasSlopeFactor = slopeFactor;
            _depthBiasConstantFactor = constantFactor;
            _depthBiasClamp = clamp;

            _depthBiasEnable = enable;
            _dirty |= DirtyFlags.DepthBias;
        }

        public void SetScissor(int index, Rect2D scissor)
        {
            _scissors[index] = scissor;
            _dirty |= DirtyFlags.Scissor;
        }

        public void SetDepthTestBool(bool testEnable, bool writeEnable)
        {
            DepthTestEnable = testEnable;
            DepthWriteEnable = writeEnable;
            _dirty |= DirtyFlags.DepthTestBool;
        }

        public void SetDepthTestCompareOp(CompareOp depthTestOp)
        {
            _depthCompareOp = depthTestOp;
            _dirty |= DirtyFlags.DepthTestCompareOp;
        }

        public void SetStencilOp(StencilOp backFailOp, StencilOp backPassOp, StencilOp backDepthFailOp,
            CompareOp backCompareOp, StencilOp frontFailOp, StencilOp frontPassOp, StencilOp frontDepthFailOp,
            CompareOp frontCompareOp)
        {
            _backFailOp = backFailOp;
            _backPassOp = backPassOp;
            _backDepthFailOp = backDepthFailOp;
            _backCompareOp = backCompareOp;
            _frontFailOp = frontFailOp;
            _frontPassOp = frontPassOp;
            _frontDepthFailOp = frontDepthFailOp;
            _frontCompareOp = frontCompareOp;
            _opToo = true;
        }

        public void SetStencilMask(uint backCompareMask, uint backWriteMask, uint backReference,
            uint frontCompareMask, uint frontWriteMask, uint frontReference)
        {
            _backCompareMask = backCompareMask;
            _backWriteMask = backWriteMask;
            _backReference = backReference;
            _frontCompareMask = frontCompareMask;
            _frontWriteMask = frontWriteMask;
            _frontReference = frontReference;
            _dirty |= DirtyFlags.Stencil;
        }

        public void SetStencilTest(bool stencilTestEnable)
        {
            StencilTestEnable = stencilTestEnable;
            _dirty |= DirtyFlags.StencilTestEnable;
        }

        public void SetViewport(int index, Viewport viewport)
        {
            Viewports[index] = viewport;
            _dirty |= DirtyFlags.Viewport;
        }

        public void SetViewports(ref Array16<Viewport> viewports, uint viewportsCount)
        {
            if (!Viewports.Equals(viewports) || ViewportsCount != viewportsCount)
            {
                Viewports = viewports;
                ViewportsCount = viewportsCount;
                if (ViewportsCount != 0)
                {
                    _dirty |= DirtyFlags.Viewport;
                }
            }
        }

        public void SetCullMode(CullModeFlags cullMode)
        {
            CullMode = cullMode;
            _dirty |= DirtyFlags.CullMode;
        }

        public void SetFrontFace(FrontFace frontFace)
        {
            _frontFace = frontFace;
            _dirty |= DirtyFlags.FrontFace;
        }

        public void SetLineWidth(float width)
        {
            _lineWidth = width;

            _dirty |= DirtyFlags.LineWidth;
        }

        public void SetRasterizerDiscard(bool discard)
        {
            _discard = discard;
            _dirty |= DirtyFlags.RasterDiscard;
        }

        public void SetPrimitiveRestartEnable(bool primitiveRestart)
        {
            _primitiveRestartEnable = primitiveRestart;
            _dirty |= DirtyFlags.PrimitiveRestart;
        }

        public void SetPrimitiveTopology(PrimitiveTopology primitiveTopology)
        {
            Topology = primitiveTopology;
            _dirty |= DirtyFlags.PrimitiveTopology;
        }

        public void SetLogicOp(LogicOp op)
        {
            _logicOp = op;
            _dirty |= DirtyFlags.LogicOp;
        }

        public void SetPatchControlPoints(uint points)
        {
            _patchControlPoints = points;
            _dirty |= DirtyFlags.PatchControlPoints;
        }

        public void ForceAllDirty(VulkanRenderer gd)
        {
            _dirty = DirtyFlags.Standard;

            if (gd.Capabilities.SupportsExtendedDynamicState)
            {
                _dirty |= DirtyFlags.Extended;
            }

            if (gd.Capabilities.SupportsExtendedDynamicState2)
            {
                _dirty |= DirtyFlags.Extended2;
            }

            if (gd.IsMoltenVk)
            {
                _dirty &= ~DirtyFlags.LineWidth;
            }

            if (!gd.ExtendedDynamicState2Features.ExtendedDynamicState2LogicOp)
            {
                _dirty &= ~DirtyFlags.LogicOp;
            }

            if (!gd.ExtendedDynamicState2Features.ExtendedDynamicState2PatchControlPoints)
            {
                _dirty &= ~DirtyFlags.PatchControlPoints;
            }
        }

        public void ReplayIfDirty(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            if (_dirty.HasFlag(DirtyFlags.Blend))
            {
                RecordBlend(gd.Api, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.DepthBias))
            {
                RecordDepthBias(gd, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.Scissor))
            {
                RecordScissor(gd, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.Stencil))
            {
                RecordStencil(gd, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.Viewport))
            {
                RecordViewport(gd, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.CullMode))
            {
                RecordCullMode(gd.ExtendedDynamicStateApi, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.FrontFace))
            {
                RecordFrontFace(gd.ExtendedDynamicStateApi, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.DepthTestBool))
            {
                RecordDepthTestBool(gd.ExtendedDynamicStateApi, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.DepthTestCompareOp))
            {
                RecordDepthTestCompareOp(gd.ExtendedDynamicStateApi, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.StencilTestEnable))
            {
                RecordStencilTestEnable(gd.ExtendedDynamicStateApi, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.LineWidth))
            {
                RecordLineWidth(gd.Api, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.RasterDiscard))
            {
                RecordRasterizationDiscard(gd, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.PrimitiveRestart))
            {
                RecordPrimitiveRestartEnable(gd, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.PrimitiveTopology))
            {
                RecordPrimitiveTopology(gd, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.LogicOp))
            {
                RecordLogicOp(gd, commandBuffer);
            }

            if (_dirty.HasFlag(DirtyFlags.PatchControlPoints))
            {
                RecordPatchControlPoints(gd, commandBuffer);
            }

            _dirty = DirtyFlags.None;
        }

        private void RecordBlend(Vk api, CommandBuffer commandBuffer)
        {
            api.CmdSetBlendConstants(commandBuffer, _blendConstants.AsSpan());
        }

        private readonly void RecordDepthBias(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            if (gd.Capabilities.SupportsExtendedDynamicState2)
            {
                gd.ExtendedDynamicState2Api.CmdSetDepthBiasEnable(commandBuffer, _depthBiasEnable);

                if (!_depthBiasEnable)
                {
                    return;
                }
            }

            gd.Api.CmdSetDepthBias(commandBuffer, _depthBiasConstantFactor, _depthBiasClamp, _depthBiasSlopeFactor);
        }

        private void RecordScissor(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            if (ScissorsCount != 0)
            {
                if (gd.Capabilities.SupportsExtendedDynamicState)
                {

                    gd.ExtendedDynamicStateApi.CmdSetScissorWithCount(commandBuffer, (uint)ScissorsCount,
                        _scissors.AsSpan());
                }
                else
                {
                    gd.Api.CmdSetScissor(commandBuffer, 0, (uint)ScissorsCount,
                        _scissors.AsSpan());
                }
            }
        }

        private readonly void RecordStencil(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            if (_opToo)
            {
                gd.ExtendedDynamicStateApi.CmdSetStencilOp(commandBuffer, StencilFaceFlags.FaceBackBit, _backFailOp, _backPassOp,
                    _backDepthFailOp, _backCompareOp);
                gd.ExtendedDynamicStateApi.CmdSetStencilOp(commandBuffer, StencilFaceFlags.FaceFrontBit, _frontFailOp, _frontPassOp,
                    _frontDepthFailOp, _frontCompareOp);
            }

            gd.Api.CmdSetStencilCompareMask(commandBuffer, StencilFaceFlags.FaceBackBit, _backCompareMask);
            gd.Api.CmdSetStencilWriteMask(commandBuffer, StencilFaceFlags.FaceBackBit, _backWriteMask);
            gd.Api.CmdSetStencilReference(commandBuffer, StencilFaceFlags.FaceBackBit, _backReference);
            gd.Api.CmdSetStencilCompareMask(commandBuffer, StencilFaceFlags.FaceFrontBit, _frontCompareMask);
            gd.Api.CmdSetStencilWriteMask(commandBuffer, StencilFaceFlags.FaceFrontBit, _frontWriteMask);
            gd.Api.CmdSetStencilReference(commandBuffer, StencilFaceFlags.FaceFrontBit, _frontReference);
        }

        private readonly void RecordStencilTestEnable(ExtExtendedDynamicState api, CommandBuffer commandBuffer)
        {
            api.CmdSetStencilTestEnable(commandBuffer, StencilTestEnable);
        }

        private void RecordViewport(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            if (ViewportsCount == 0)
            {
                return;
            }

            if (gd.Capabilities.SupportsExtendedDynamicState)
            {

                gd.ExtendedDynamicStateApi.CmdSetViewportWithCount(commandBuffer, ViewportsCount,
                    Viewports.AsSpan());
            }
            else
            {
                gd.Api.CmdSetViewport(commandBuffer, 0, ViewportsCount,
                    Viewports.AsSpan());
            }
        }

        private readonly void RecordCullMode(ExtExtendedDynamicState api, CommandBuffer commandBuffer)
        {
            api.CmdSetCullMode(commandBuffer, CullMode);
        }

        private readonly void RecordFrontFace(ExtExtendedDynamicState api, CommandBuffer commandBuffer)
        {
            api.CmdSetFrontFace(commandBuffer, _frontFace);
        }

        private readonly void RecordDepthTestBool(ExtExtendedDynamicState api, CommandBuffer commandBuffer)
        {
            api.CmdSetDepthTestEnable(commandBuffer, DepthTestEnable);

            if (DepthTestEnable)
            {
                api.CmdSetDepthWriteEnable(commandBuffer, DepthWriteEnable);
            }
        }

        private readonly void RecordDepthTestCompareOp(ExtExtendedDynamicState api, CommandBuffer commandBuffer)
        {
            api.CmdSetDepthCompareOp(commandBuffer, _depthCompareOp);
        }

        private readonly void RecordRasterizationDiscard(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            gd.ExtendedDynamicState2Api.CmdSetRasterizerDiscardEnable(commandBuffer, _discard);
        }

        private readonly void RecordPrimitiveRestartEnable(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            gd.ExtendedDynamicState2Api.CmdSetPrimitiveRestartEnable(commandBuffer, _primitiveRestartEnable);
        }

        private readonly void RecordPrimitiveTopology(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            gd.ExtendedDynamicStateApi.CmdSetPrimitiveTopology(commandBuffer, Topology);
        }

        private readonly void RecordLogicOp(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            gd.ExtendedDynamicState2Api.CmdSetLogicOp(commandBuffer, _logicOp);
        }

        private readonly void RecordPatchControlPoints(VulkanRenderer gd, CommandBuffer commandBuffer)
        {
            gd.ExtendedDynamicState2Api.CmdSetPatchControlPoints(commandBuffer, _patchControlPoints);
        }

        private readonly void RecordLineWidth(Vk api, CommandBuffer commandBuffer)
        {
            if (!OperatingSystem.IsMacOS())
            {
                api.CmdSetLineWidth(commandBuffer, _lineWidth);
            }
        }
    }
}
