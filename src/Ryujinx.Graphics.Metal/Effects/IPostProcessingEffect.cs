using System;

namespace Ryujinx.Graphics.Metal.Effects
{
    internal interface IPostProcessingEffect : IDisposable
    {
        const int LocalGroupSize = 64;
        Texture Run(Texture view, int width, int height);
    }
}
