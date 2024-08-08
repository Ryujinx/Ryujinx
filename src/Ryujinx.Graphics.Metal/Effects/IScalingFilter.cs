using Ryujinx.Graphics.GAL;
using System;

namespace Ryujinx.Graphics.Metal.Effects
{
    internal interface IScalingFilter : IDisposable
    {
        float Level { get; set; }
        void Run(
            Texture view,
            Texture destinationTexture,
            Format format,
            int width,
            int height,
            Extents2D source,
            Extents2D destination);
    }
}
