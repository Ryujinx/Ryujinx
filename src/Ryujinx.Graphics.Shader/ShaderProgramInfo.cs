using System;
using System.Collections.ObjectModel;

namespace Ryujinx.Graphics.Shader
{
    public class ShaderProgramInfo
    {
        public ReadOnlyCollection<BufferDescriptor> CBuffers { get; }
        public ReadOnlyCollection<BufferDescriptor> SBuffers { get; }
        public ReadOnlyCollection<TextureDescriptor> Textures { get; }
        public ReadOnlyCollection<TextureDescriptor> Images { get; }

        public ShaderStage Stage { get; }
        public int GeometryVerticesPerPrimitive { get; }
        public int GeometryMaxOutputVertices { get; }
        public int ThreadsPerInputPrimitive { get; }
        public bool UsesFragCoord { get; }
        public bool UsesInstanceId { get; }
        public bool UsesDrawParameters { get; }
        public bool UsesRtLayer { get; }
        public bool HasUnconditionalDiscard { get; }
        public byte ClipDistancesWritten { get; }
        public int FragmentOutputMap { get; }

        public ShaderProgramInfo(
            BufferDescriptor[] cBuffers,
            BufferDescriptor[] sBuffers,
            TextureDescriptor[] textures,
            TextureDescriptor[] images,
            ShaderStage stage,
            int geometryVerticesPerPrimitive,
            int geometryMaxOutputVertices,
            int threadsPerInputPrimitive,
            bool usesFragCoord,
            bool usesInstanceId,
            bool usesDrawParameters,
            bool usesRtLayer,
            bool hasUnconditionalDiscard,
            byte clipDistancesWritten,
            int fragmentOutputMap)
        {
            CBuffers = Array.AsReadOnly(cBuffers);
            SBuffers = Array.AsReadOnly(sBuffers);
            Textures = Array.AsReadOnly(textures);
            Images = Array.AsReadOnly(images);

            Stage = stage;
            GeometryVerticesPerPrimitive = geometryVerticesPerPrimitive;
            GeometryMaxOutputVertices = geometryMaxOutputVertices;
            ThreadsPerInputPrimitive = threadsPerInputPrimitive;
            UsesFragCoord = usesFragCoord;
            UsesInstanceId = usesInstanceId;
            UsesDrawParameters = usesDrawParameters;
            UsesRtLayer = usesRtLayer;
            HasUnconditionalDiscard = hasUnconditionalDiscard;
            ClipDistancesWritten = clipDistancesWritten;
            FragmentOutputMap = fragmentOutputMap;
        }
    }
}
