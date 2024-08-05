using Ryujinx.Graphics.Metal;
using SharpMetal.Metal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

interface IEncoderFactory
{
    MTLRenderCommandEncoder CreateRenderCommandEncoder();
    MTLComputeCommandEncoder CreateComputeCommandEncoder();
}

/// <summary>
/// Tracks active encoder object for a command buffer.
/// </summary>
[SupportedOSPlatform("macos")]
class CommandBufferEncoder
{
    public EncoderType CurrentEncoderType { get; private set; } = EncoderType.None;

    public MTLBlitCommandEncoder BlitEncoder => new(CurrentEncoder.Value);

    public MTLComputeCommandEncoder ComputeEncoder => new(CurrentEncoder.Value);

    public MTLRenderCommandEncoder RenderEncoder => new(CurrentEncoder.Value);

    internal MTLCommandEncoder? CurrentEncoder { get; private set; }

    private MTLCommandBuffer _commandBuffer;
    private IEncoderFactory _encoderFactory;

    public void Initialize(MTLCommandBuffer commandBuffer, IEncoderFactory encoderFactory)
    {
        _commandBuffer = commandBuffer;
        _encoderFactory = encoderFactory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MTLRenderCommandEncoder EnsureRenderEncoder()
    {
        if (CurrentEncoderType != EncoderType.Render)
        {
            return BeginRenderPass();
        }

        return RenderEncoder;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MTLBlitCommandEncoder EnsureBlitEncoder()
    {
        if (CurrentEncoderType != EncoderType.Blit)
        {
            return BeginBlitPass();
        }

        return BlitEncoder;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MTLComputeCommandEncoder EnsureComputeEncoder()
    {
        if (CurrentEncoderType != EncoderType.Compute)
        {
            return BeginComputePass();
        }

        return ComputeEncoder;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetRenderEncoder(out MTLRenderCommandEncoder encoder)
    {
        if (CurrentEncoderType != EncoderType.Render)
        {
            encoder = default;
            return false;
        }

        encoder = RenderEncoder;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetBlitEncoder(out MTLBlitCommandEncoder encoder)
    {
        if (CurrentEncoderType != EncoderType.Blit)
        {
            encoder = default;
            return false;
        }

        encoder = BlitEncoder;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetComputeEncoder(out MTLComputeCommandEncoder encoder)
    {
        if (CurrentEncoderType != EncoderType.Compute)
        {
            encoder = default;
            return false;
        }

        encoder = ComputeEncoder;
        return true;
    }

    public void EndCurrentPass()
    {
        if (CurrentEncoder != null)
        {
            switch (CurrentEncoderType)
            {
                case EncoderType.Blit:
                    BlitEncoder.EndEncoding();
                    CurrentEncoder = null;
                    break;
                case EncoderType.Compute:
                    ComputeEncoder.EndEncoding();
                    CurrentEncoder = null;
                    break;
                case EncoderType.Render:
                    RenderEncoder.EndEncoding();
                    CurrentEncoder = null;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            CurrentEncoderType = EncoderType.None;
        }
    }

    private MTLRenderCommandEncoder BeginRenderPass()
    {
        EndCurrentPass();

        var renderCommandEncoder = _encoderFactory.CreateRenderCommandEncoder();

        CurrentEncoder = renderCommandEncoder;
        CurrentEncoderType = EncoderType.Render;

        return renderCommandEncoder;
    }

    private MTLBlitCommandEncoder BeginBlitPass()
    {
        EndCurrentPass();

        using var descriptor = new MTLBlitPassDescriptor();
        var blitCommandEncoder = _commandBuffer.BlitCommandEncoder(descriptor);

        CurrentEncoder = blitCommandEncoder;
        CurrentEncoderType = EncoderType.Blit;
        return blitCommandEncoder;
    }

    private MTLComputeCommandEncoder BeginComputePass()
    {
        EndCurrentPass();

        var computeCommandEncoder = _encoderFactory.CreateComputeCommandEncoder();

        CurrentEncoder = computeCommandEncoder;
        CurrentEncoderType = EncoderType.Compute;
        return computeCommandEncoder;
    }
}
