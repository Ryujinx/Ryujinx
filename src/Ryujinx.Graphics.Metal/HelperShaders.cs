using Ryujinx.Common;
using Ryujinx.Common.Logging;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public class HelperShaders
    {
        private const string ShadersSourcePath = "/Ryujinx.Graphics.Metal/HelperShadersSource.metal";

        public HelperShader BlitShader;

        public HelperShaders(MTLDevice device)
        {
            var error = new NSError(IntPtr.Zero);

            var shaderSource = EmbeddedResources.ReadAllText(ShadersSourcePath);
            var library = device.NewLibrary(StringHelper.NSString(shaderSource), new(IntPtr.Zero), ref error);
            if (error != IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Library: {StringHelper.String(error.LocalizedDescription)}");
            }

            BlitShader = new HelperShader(device, library, "vertexBlit", "fragmentBlit");
        }
    }

    [SupportedOSPlatform("macos")]
    public readonly struct HelperShader
    {
        private readonly MTLRenderPipelineState _pipelineState;
        public static implicit operator MTLRenderPipelineState(HelperShader shader) => shader._pipelineState;

        public HelperShader(MTLDevice device, MTLLibrary library, string vertex, string fragment)
        {
            var renderPipelineDescriptor = new MTLRenderPipelineDescriptor
            {
                VertexFunction = library.NewFunction(StringHelper.NSString(vertex)),
                FragmentFunction = library.NewFunction(StringHelper.NSString(fragment))
            };
            renderPipelineDescriptor.ColorAttachments.Object(0).SetBlendingEnabled(true);
            renderPipelineDescriptor.ColorAttachments.Object(0).PixelFormat = MTLPixelFormat.BGRA8Unorm;
            renderPipelineDescriptor.ColorAttachments.Object(0).SourceAlphaBlendFactor = MTLBlendFactor.SourceAlpha;
            renderPipelineDescriptor.ColorAttachments.Object(0).DestinationAlphaBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;
            renderPipelineDescriptor.ColorAttachments.Object(0).SourceRGBBlendFactor = MTLBlendFactor.SourceAlpha;
            renderPipelineDescriptor.ColorAttachments.Object(0).DestinationRGBBlendFactor = MTLBlendFactor.OneMinusSourceAlpha;

            var error = new NSError(IntPtr.Zero);
            _pipelineState = device.NewRenderPipelineState(renderPipelineDescriptor, ref error);
            if (error != IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Render Pipeline State: {StringHelper.String(error.LocalizedDescription)}");
            }
        }
    }
}
