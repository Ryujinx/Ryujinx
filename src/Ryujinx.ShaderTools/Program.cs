using CommandLine;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Ryujinx.ShaderTools
{
    class Program
    {
        private class GpuAccessor : IGpuAccessor
        {
            private const int DefaultArrayLength = 32;

            private readonly byte[] _data;

            private int _texturesCount;
            private int _imagesCount;

            public GpuAccessor(byte[] data)
            {
                _data = data;
                _texturesCount = 0;
                _imagesCount = 0;
            }

            public SetBindingPair CreateConstantBufferBinding(int index)
            {
                return new SetBindingPair(0, index + 1);
            }

            public SetBindingPair CreateImageBinding(int count, bool isBuffer)
            {
                int binding = _imagesCount;

                _imagesCount += count;

                return new SetBindingPair(3, binding);
            }

            public SetBindingPair CreateStorageBufferBinding(int index)
            {
                return new SetBindingPair(1, index);
            }

            public SetBindingPair CreateTextureBinding(int count, bool isBuffer)
            {
                int binding = _texturesCount;

                _texturesCount += count;

                return new SetBindingPair(2, binding);
            }

            public ReadOnlySpan<ulong> GetCode(ulong address, int minimumSize)
            {
                return MemoryMarshal.Cast<byte, ulong>(new ReadOnlySpan<byte>(_data)[(int)address..]);
            }

            public int QuerySamplerArrayLengthFromPool()
            {
                return DefaultArrayLength;
            }

            public int QueryTextureArrayLengthFromBuffer(int slot)
            {
                return DefaultArrayLength;
            }

            public int QueryTextureArrayLengthFromPool()
            {
                return DefaultArrayLength;
            }
        }

        private class Options
        {
            [Option("compute", Required = false, Default = false, HelpText = "Indicate that the shader is a compute shader.")]
            public bool Compute { get; set; }

            [Option("vertex-as-compute", Required = false, Default = false, HelpText = "Indicate that the shader is a vertex shader and should be converted to compute.")]
            public bool VertexAsCompute { get; set; }

            [Option("vertex-passthrough", Required = false, Default = false, HelpText = "Indicate that the shader is a vertex passthrough shader for compute output.")]
            public bool VertexPassthrough { get; set; }

            [Option("target-language", Required = false, Default = TargetLanguage.Glsl, HelpText = "Indicate the target shader language to use.")]
            public TargetLanguage TargetLanguage { get; set; }

            [Option("target-api", Required = false, Default = TargetApi.OpenGL, HelpText = "Indicate the target graphics api to use.")]
            public TargetApi TargetApi { get; set; }

            [Value(0, MetaName = "input", HelpText = "Binary Maxwell shader input path.", Required = true)]
            public string InputPath { get; set; }

            [Value(1, MetaName = "output", HelpText = "Decompiled shader output path.", Required = false)]
            public string OutputPath { get; set; }
        }

        static void HandleArguments(Options options)
        {
            TranslationFlags flags = TranslationFlags.DebugMode;

            if (options.Compute)
            {
                flags |= TranslationFlags.Compute;
            }

            byte[] data = File.ReadAllBytes(options.InputPath);

            TranslationOptions translationOptions = new(options.TargetLanguage, options.TargetApi, flags);
            TranslatorContext translatorContext = Translator.CreateContext(0, new GpuAccessor(data), translationOptions);

            ShaderProgram program;

            if (options.VertexPassthrough)
            {
                (program, _) = translatorContext.GenerateVertexPassthroughForCompute();
            }
            else
            {
                program = translatorContext.Translate(options.VertexAsCompute);
            }

            if (options.OutputPath == null)
            {
                if (program.BinaryCode != null)
                {
                    using Stream outputStream = Console.OpenStandardOutput();

                    outputStream.Write(program.BinaryCode);
                }
                else
                {
                    Console.WriteLine(program.Code);
                }
            }
            else
            {
                if (program.BinaryCode != null)
                {
                    File.WriteAllBytes(options.OutputPath, program.BinaryCode);
                }
                else
                {
                    File.WriteAllText(options.OutputPath, program.Code);
                }
            }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options => HandleArguments(options))
                .WithNotParsed(errors => errors.Output());
        }
    }
}
