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
        public readonly MTLFunction VertexFunction;
        public readonly MTLFunction FragmentFunction;

        public HelperShader(MTLDevice device, MTLLibrary library, string vertex, string fragment)
        {
            VertexFunction = library.NewFunction(StringHelper.NSString(vertex));
            FragmentFunction = library.NewFunction(StringHelper.NSString(fragment));
        }
    }
}
