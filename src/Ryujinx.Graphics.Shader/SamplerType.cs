using Ryujinx.Graphics.Shader.Translation;
using System;

namespace Ryujinx.Graphics.Shader
{
    [Flags]
    public enum SamplerType
    {
        None = 0,
        Texture1D,
        TextureBuffer,
        Texture2D,
        Texture3D,
        TextureCube,

        Mask = 0xff,

        Array = 1 << 8,
        Multisample = 1 << 9,
        Shadow = 1 << 10,
    }

    static class SamplerTypeExtensions
    {
        public static int GetDimensions(this SamplerType type)
        {
            return (type & SamplerType.Mask) switch
            {
                SamplerType.Texture1D => 1,
                SamplerType.TextureBuffer => 1,
                SamplerType.Texture2D => 2,
                SamplerType.Texture3D => 3,
                SamplerType.TextureCube => 3,
                _ => throw new ArgumentException($"Invalid sampler type \"{type}\"."),
            };
        }

        public static string ToShortSamplerType(this SamplerType type)
        {
            string typeName = (type & SamplerType.Mask) switch
            {
                SamplerType.Texture1D => "1d",
                SamplerType.TextureBuffer => "b",
                SamplerType.Texture2D => "2d",
                SamplerType.Texture3D => "3d",
                SamplerType.TextureCube => "cube",
                _ => throw new ArgumentException($"Invalid sampler type \"{type}\"."),
            };

            if ((type & SamplerType.Multisample) != 0)
            {
                typeName += "ms";
            }

            if ((type & SamplerType.Array) != 0)
            {
                typeName += "a";
            }

            if ((type & SamplerType.Shadow) != 0)
            {
                typeName += "s";
            }

            return typeName;
        }

        public static string ToGlslSamplerType(this SamplerType type)
        {
            string typeName = (type & SamplerType.Mask) switch
            {
                SamplerType.None => "sampler",
                SamplerType.Texture1D => "sampler1D",
                SamplerType.TextureBuffer => "samplerBuffer",
                SamplerType.Texture2D => "sampler2D",
                SamplerType.Texture3D => "sampler3D",
                SamplerType.TextureCube => "samplerCube",
                _ => throw new ArgumentException($"Invalid sampler type \"{type}\"."),
            };

            if ((type & SamplerType.Multisample) != 0)
            {
                typeName += "MS";
            }

            if ((type & SamplerType.Array) != 0)
            {
                typeName += "Array";
            }

            if ((type & SamplerType.Shadow) != 0)
            {
                typeName += "Shadow";
            }

            return typeName;
        }

        public static string ToGlslTextureType(this SamplerType type)
        {
            string typeName = (type & SamplerType.Mask) switch
            {
                SamplerType.Texture1D => "texture1D",
                SamplerType.TextureBuffer => "textureBuffer",
                SamplerType.Texture2D => "texture2D",
                SamplerType.Texture3D => "texture3D",
                SamplerType.TextureCube => "textureCube",
                _ => throw new ArgumentException($"Invalid texture type \"{type}\"."),
            };

            if ((type & SamplerType.Multisample) != 0)
            {
                typeName += "MS";
            }

            if ((type & SamplerType.Array) != 0)
            {
                typeName += "Array";
            }

            return typeName;
        }

        public static string ToGlslImageType(this SamplerType type, AggregateType componentType)
        {
            string typeName = (type & SamplerType.Mask) switch
            {
                SamplerType.Texture1D => "image1D",
                SamplerType.TextureBuffer => "imageBuffer",
                SamplerType.Texture2D => "image2D",
                SamplerType.Texture3D => "image3D",
                SamplerType.TextureCube => "imageCube",
                _ => throw new ArgumentException($"Invalid sampler type \"{type}\"."),
            };

            if ((type & SamplerType.Multisample) != 0)
            {
                typeName += "MS";
            }

            if ((type & SamplerType.Array) != 0)
            {
                typeName += "Array";
            }

            switch (componentType)
            {
                case AggregateType.U32:
                    typeName = 'u' + typeName;
                    break;
                case AggregateType.S32:
                    typeName = 'i' + typeName;
                    break;
            }

            return typeName;
        }

        public static string ToMslTextureType(this SamplerType type, AggregateType aggregateType, bool image = false)
        {
            string typeName;

            if ((type & SamplerType.Shadow) != 0)
            {
                typeName = (type & SamplerType.Mask) switch
                {
                    SamplerType.Texture2D => "depth2d",
                    SamplerType.TextureCube => "depthcube",
                    _ => throw new ArgumentException($"Invalid shadow texture type \"{type}\"."),
                };
            }
            else
            {
                typeName = (type & SamplerType.Mask) switch
                {
                    SamplerType.Texture1D => "texture1d",
                    SamplerType.TextureBuffer => "texture_buffer",
                    SamplerType.Texture2D => "texture2d",
                    SamplerType.Texture3D => "texture3d",
                    SamplerType.TextureCube => "texturecube",
                    _ => throw new ArgumentException($"Invalid texture type \"{type}\"."),
                };
            }

            if ((type & SamplerType.Multisample) != 0)
            {
                typeName += "_ms";
            }

            if ((type & SamplerType.Array) != 0)
            {
                typeName += "_array";
            }

            var format = aggregateType switch
            {
                AggregateType.S32 => "int",
                AggregateType.U32 => "uint",
                _ => "float"
            };

            return $"{typeName}<{format}{(image ? ", access::read_write" : "")}>";
        }
    }
}
