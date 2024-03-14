using Spv.Generator;

namespace Ryujinx.Graphics.Shader.CodeGen.Spirv
{
    readonly struct SamplerDeclaration
    {
        public readonly Instruction ImageType;
        public readonly Instruction SampledImageType;
        public readonly Instruction SampledImagePointerType;
        public readonly Instruction Image;
        public readonly bool IsIndexed;

        public SamplerDeclaration(
            Instruction imageType,
            Instruction sampledImageType,
            Instruction sampledImagePointerType,
            Instruction image,
            bool isIndexed)
        {
            ImageType = imageType;
            SampledImageType = sampledImageType;
            SampledImagePointerType = sampledImagePointerType;
            Image = image;
            IsIndexed = isIndexed;
        }
    }
}
