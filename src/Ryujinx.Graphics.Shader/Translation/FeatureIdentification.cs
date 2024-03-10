using Ryujinx.Graphics.Shader.IntermediateRepresentation;

namespace Ryujinx.Graphics.Shader.Translation
{
    static class FeatureIdentification
    {
        public static void RunPass(BasicBlock[] blocks, ShaderStage stage, ref FeatureFlags usedFeatures)
        {
            if (stage == ShaderStage.Fragment)
            {
                bool endsWithDiscardOnly = true;

                for (int blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
                {
                    BasicBlock block = blocks[blockIndex];

                    if (block.HasSuccessor)
                    {
                        continue;
                    }

                    if (block.Operations.Count == 0 ||
                        block.Operations.Last.Value is not Operation operation ||
                        operation.Inst != Instruction.Discard)
                    {
                        endsWithDiscardOnly = false;
                        break;
                    }
                }

                if (endsWithDiscardOnly)
                {
                    usedFeatures |= FeatureFlags.UnconditionalDiscard;
                }
            }
        }
    }
}
