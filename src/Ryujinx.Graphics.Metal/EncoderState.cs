using Ryujinx.Common.Memory;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Metal.State;
using Ryujinx.Graphics.Shader;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [Flags]
    enum DirtyFlags
    {
        None = 0,
        RenderPipeline = 1 << 0,
        ComputePipeline = 1 << 1,
        DepthStencil = 1 << 2,
        DepthClamp = 1 << 3,
        DepthBias = 1 << 4,
        CullMode = 1 << 5,
        FrontFace = 1 << 6,
        StencilRef = 1 << 7,
        Viewports = 1 << 8,
        Scissors = 1 << 9,
        Uniforms = 1 << 10,
        Storages = 1 << 11,
        Textures = 1 << 12,
        Images = 1 << 13,

        ArgBuffers = Uniforms | Storages | Textures | Images,

        RenderAll = RenderPipeline | DepthStencil | DepthClamp | DepthBias | CullMode | FrontFace | StencilRef | Viewports | Scissors | ArgBuffers,
        ComputeAll = ComputePipeline | ArgBuffers,
        All = RenderAll | ComputeAll,
    }

    record struct BufferRef
    {
        public Auto<DisposableBuffer> Buffer;
        public BufferRange? Range;

        public BufferRef(Auto<DisposableBuffer> buffer)
        {
            Buffer = buffer;
        }

        public BufferRef(Auto<DisposableBuffer> buffer, ref BufferRange range)
        {
            Buffer = buffer;
            Range = range;
        }
    }

    record struct TextureRef
    {
        public ShaderStage Stage;
        public TextureBase Storage;
        public Sampler Sampler;
        public Format ImageFormat;

        public TextureRef(ShaderStage stage, TextureBase storage, Sampler sampler)
        {
            Stage = stage;
            Storage = storage;
            Sampler = sampler;
        }
    }

    record struct ImageRef
    {
        public ShaderStage Stage;
        public Texture Storage;

        public ImageRef(ShaderStage stage, Texture storage)
        {
            Stage = stage;
            Storage = storage;
        }
    }

    struct PredrawState
    {
        public MTLCullMode CullMode;
        public DepthStencilUid DepthStencilUid;
        public PrimitiveTopology Topology;
        public MTLViewport[] Viewports;
    }

    struct RenderTargetCopy
    {
        public MTLScissorRect[] Scissors;
        public Texture DepthStencil;
        public Texture[] RenderTargets;
    }

    [SupportedOSPlatform("macos")]
    class EncoderState
    {
        public Program RenderProgram = null;
        public Program ComputeProgram = null;

        public PipelineState Pipeline;
        public DepthStencilUid DepthStencilUid;

        public readonly record struct ArrayRef<T>(ShaderStage Stage, T Array);

        public readonly BufferRef[] UniformBufferRefs = new BufferRef[Constants.MaxUniformBufferBindings];
        public readonly BufferRef[] StorageBufferRefs = new BufferRef[Constants.MaxStorageBufferBindings];
        public readonly TextureRef[] TextureRefs = new TextureRef[Constants.MaxTextureBindings * 2];
        public readonly ImageRef[] ImageRefs = new ImageRef[Constants.MaxImageBindings * 2];

        public ArrayRef<TextureArray>[] TextureArrayRefs = [];
        public ArrayRef<ImageArray>[] ImageArrayRefs = [];

        public ArrayRef<TextureArray>[] TextureArrayExtraRefs = [];
        public ArrayRef<ImageArray>[] ImageArrayExtraRefs = [];

        public IndexBufferState IndexBuffer = default;

        public MTLDepthClipMode DepthClipMode = MTLDepthClipMode.Clip;

        public float DepthBias;
        public float SlopeScale;
        public float Clamp;

        public int BackRefValue = 0;
        public int FrontRefValue = 0;

        public PrimitiveTopology Topology = PrimitiveTopology.Triangles;
        public MTLCullMode CullMode = MTLCullMode.None;
        public MTLWinding Winding = MTLWinding.CounterClockwise;
        public bool CullBoth = false;

        public MTLViewport[] Viewports = new MTLViewport[Constants.MaxViewports];
        public MTLScissorRect[] Scissors = new MTLScissorRect[Constants.MaxViewports];

        // Changes to attachments take recreation!
        public Texture DepthStencil;
        public Texture[] RenderTargets = new Texture[Constants.MaxColorAttachments];
        public ITexture PreMaskDepthStencil = default;
        public ITexture[] PreMaskRenderTargets;
        public bool FramebufferUsingColorWriteMask;

        public Array8<ColorBlendStateUid> StoredBlend;
        public ColorF BlendColor = new();

        public readonly VertexBufferState[] VertexBuffers = new VertexBufferState[Constants.MaxVertexBuffers];
        public readonly VertexAttribDescriptor[] VertexAttribs = new VertexAttribDescriptor[Constants.MaxVertexAttributes];
        // Dirty flags
        public DirtyFlags Dirty = DirtyFlags.None;

        // Only to be used for present
        public bool ClearLoadAction = false;

        public RenderEncoderResources RenderEncoderResources = new();
        public ComputeEncoderResources ComputeEncoderResources = new();

        public EncoderState()
        {
            Pipeline.Initialize();
            DepthStencilUid.DepthCompareFunction = MTLCompareFunction.Always;
        }

        public RenderTargetCopy InheritForClear(EncoderState other, bool depth, int singleIndex = -1)
        {
            // Inherit render target related information without causing a render encoder split.

            var oldState = new RenderTargetCopy
            {
                Scissors = other.Scissors,
                RenderTargets = other.RenderTargets,
                DepthStencil = other.DepthStencil
            };

            Scissors = other.Scissors;
            RenderTargets = other.RenderTargets;
            DepthStencil = other.DepthStencil;

            Pipeline.ColorBlendAttachmentStateCount = other.Pipeline.ColorBlendAttachmentStateCount;
            Pipeline.Internal.ColorBlendState = other.Pipeline.Internal.ColorBlendState;
            Pipeline.DepthStencilFormat = other.Pipeline.DepthStencilFormat;

            ref var blendStates = ref Pipeline.Internal.ColorBlendState;

            // Mask out irrelevant attachments.
            for (int i = 0; i < blendStates.Length; i++)
            {
                if (depth || (singleIndex != -1 && singleIndex != i))
                {
                    blendStates[i].WriteMask = MTLColorWriteMask.None;
                }
            }

            return oldState;
        }

        public void Restore(RenderTargetCopy copy)
        {
            Scissors = copy.Scissors;
            RenderTargets = copy.RenderTargets;
            DepthStencil = copy.DepthStencil;

            Pipeline.Internal.ResetColorState();
        }
    }
}
