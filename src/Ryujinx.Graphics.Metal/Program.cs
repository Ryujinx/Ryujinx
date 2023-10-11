using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Program : IProgram
    {
        private ProgramLinkStatus _status;
        public MTLFunction VertexFunction;
        public MTLFunction FragmentFunction;
        public MTLFunction ComputeFunction;

        public Program(ShaderSource[] shaders, MTLDevice device)
        {
            for (int index = 0; index < shaders.Length; index++)
            {
                ShaderSource shader = shaders[index];

                var libraryError = new NSError(IntPtr.Zero);
                var shaderLibrary = device.NewLibrary(StringHelper.NSString(shader.Code), new MTLCompileOptions(IntPtr.Zero), ref libraryError);
                if (libraryError != IntPtr.Zero)
                {
                    Logger.Warning?.Print(LogClass.Gpu, $"Shader linking failed: \n{StringHelper.String(libraryError.LocalizedDescription)}");
                    _status = ProgramLinkStatus.Failure;
                    return;
                }

                switch (shaders[index].Stage)
                {
                    case ShaderStage.Compute:
                        ComputeFunction = shaderLibrary.NewFunction(StringHelper.NSString("computeMain"));
                        break;
                    case ShaderStage.Vertex:
                        VertexFunction = shaderLibrary.NewFunction(StringHelper.NSString("vertexMain"));
                        break;
                    case ShaderStage.Fragment:
                        FragmentFunction = shaderLibrary.NewFunction(StringHelper.NSString("fragmentMain"));
                        break;
                    default:
                        Logger.Warning?.Print(LogClass.Gpu, $"Cannot handle stage {shaders[index].Stage}!");
                        break;
                }
            }

            _status = ProgramLinkStatus.Success;
        }

        public ProgramLinkStatus CheckProgramLink(bool blocking)
        {
            return _status;
        }

        public byte[] GetBinary()
        {
            return ""u8.ToArray();
        }

        public void Dispose()
        {
            return;
        }
    }
}
