using System.Collections.Generic;

namespace Ryujinx.Graphics.Shader.StructuredIr
{
    class StructuredProgramInfo
    {
        public List<StructuredFunction> Functions { get; }

        public HashSet<IoDefinition> IoDefinitions { get; }

        public HelperFunctionsMask HelperFunctionsMask { get; set; }

        public StructuredProgramInfo(bool precise)
        {
            Functions = new List<StructuredFunction>();

            IoDefinitions = new HashSet<IoDefinition>();

            if (precise)
            {
                HelperFunctionsMask |= HelperFunctionsMask.Precise;
            }
        }
    }
}
