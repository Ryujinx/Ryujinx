using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.OpenGL.Image;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.Graphics.OpenGL
{
    class DisposedTexture
    {
        public TextureCreateInfo Info;
        public TextureView View;
        public int RemainingFrames;
    }

    /// <summary>
    /// A structure for pooling resources that can be reused without recreation, such as textures.
    /// </summary>
    class ResourcePool : IDisposable
    {
        private const int DisposedLiveFrames = 2;

        private readonly Lock _lock = new();
        private readonly Dictionary<TextureCreateInfo, List<DisposedTexture>> _textures = new();

        /// <summary>
        /// Add a texture that is not being used anymore to the resource pool to be used later.
        /// Both the texture's view and storage should be completely unused.
        /// </summary>
        /// <param name="view">The texture's view</param>
        public void AddTexture(TextureView view)
        {
            lock (_lock)
            {
                if (!_textures.TryGetValue(view.Info, out List<DisposedTexture> list))
                {
                    list = new List<DisposedTexture>();
                    _textures.Add(view.Info, list);
                }

                list.Add(new DisposedTexture()
                {
                    Info = view.Info,
                    View = view,
                    RemainingFrames = DisposedLiveFrames,
                });
            }
        }

        /// <summary>
        /// Attempt to obtain a texture from the resource cache with the desired parameters.
        /// </summary>
        /// <param name="info">The creation info for the desired texture</param>
        /// <returns>A TextureView with the description specified, or null if one was not found.</returns>
        public TextureView GetTextureOrNull(TextureCreateInfo info)
        {
            lock (_lock)
            {
                if (!_textures.TryGetValue(info, out List<DisposedTexture> list))
                {
                    return null;
                }

                foreach (DisposedTexture texture in list)
                {
                    list.Remove(texture);
                    return texture.View;
                }

                return null;
            }
        }

        /// <summary>
        /// Update the pool, removing any resources that have expired.
        /// </summary>
        public void Tick()
        {
            lock (_lock)
            {
                foreach (List<DisposedTexture> list in _textures.Values)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        DisposedTexture tex = list[i];

                        if (--tex.RemainingFrames < 0)
                        {
                            tex.View.Dispose();
                            list.RemoveAt(i--);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Disposes the resource pool.
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                foreach (List<DisposedTexture> list in _textures.Values)
                {
                    foreach (DisposedTexture texture in list)
                    {
                        texture.View.Dispose();
                    }
                }
                _textures.Clear();
            }
        }
    }
}
