using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Metal.Effects;
using SharpMetal.ObjectiveCCore;
using SharpMetal.QuartzCore;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class Window : IWindow, IDisposable
    {
        public bool ScreenCaptureRequested { get; set; }

        private readonly MetalRenderer _renderer;
        private readonly CAMetalLayer _metalLayer;

        private int _width;
        private int _height;

        private int _requestedWidth;
        private int _requestedHeight;

        // private bool _vsyncEnabled;
        private AntiAliasing _currentAntiAliasing;
        private bool _updateEffect;
        private IPostProcessingEffect _effect;
        private IScalingFilter _scalingFilter;
        private bool _isLinear;
        // private float _scalingFilterLevel;
        private bool _updateScalingFilter;
        private ScalingFilter _currentScalingFilter;
        // private bool _colorSpacePassthroughEnabled;

        public Window(MetalRenderer renderer, CAMetalLayer metalLayer)
        {
            _renderer = renderer;
            _metalLayer = metalLayer;
        }

        private unsafe void ResizeIfNeeded()
        {
            if (_requestedWidth != 0 && _requestedHeight != 0)
            {
                // TODO: This is actually a CGSize, but there is no overload for that, so fill the first two fields of rect with the size.
                var rect = new NSRect(_requestedWidth, _requestedHeight, 0, 0);

                ObjectiveC.objc_msgSend(_metalLayer, "setDrawableSize:", rect);

                _requestedWidth = 0;
                _requestedHeight = 0;
            }
        }

        public unsafe void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback)
        {
            if (_renderer.Pipeline is Pipeline pipeline && texture is Texture tex)
            {
                ResizeIfNeeded();

                var drawable = new CAMetalDrawable(ObjectiveC.IntPtr_objc_msgSend(_metalLayer, "nextDrawable"));

                _width = (int)drawable.Texture.Width;
                _height = (int)drawable.Texture.Height;

                UpdateEffect();

                if (_effect != null)
                {
                    // TODO: Run Effects
                    // view = _effect.Run()
                }

                int srcX0, srcX1, srcY0, srcY1;

                if (crop.Left == 0 && crop.Right == 0)
                {
                    srcX0 = 0;
                    srcX1 = tex.Width;
                }
                else
                {
                    srcX0 = crop.Left;
                    srcX1 = crop.Right;
                }

                if (crop.Top == 0 && crop.Bottom == 0)
                {
                    srcY0 = 0;
                    srcY1 = tex.Height;
                }
                else
                {
                    srcY0 = crop.Top;
                    srcY1 = crop.Bottom;
                }

                if (ScreenCaptureRequested)
                {
                    // TODO: Support screen captures

                    ScreenCaptureRequested = false;
                }

                float ratioX = crop.IsStretched ? 1.0f : MathF.Min(1.0f, _height * crop.AspectRatioX / (_width * crop.AspectRatioY));
                float ratioY = crop.IsStretched ? 1.0f : MathF.Min(1.0f, _width * crop.AspectRatioY / (_height * crop.AspectRatioX));

                int dstWidth = (int)(_width * ratioX);
                int dstHeight = (int)(_height * ratioY);

                int dstPaddingX = (_width - dstWidth) / 2;
                int dstPaddingY = (_height - dstHeight) / 2;

                int dstX0 = crop.FlipX ? _width - dstPaddingX : dstPaddingX;
                int dstX1 = crop.FlipX ? dstPaddingX : _width - dstPaddingX;

                int dstY0 = crop.FlipY ? _height - dstPaddingY : dstPaddingY;
                int dstY1 = crop.FlipY ? dstPaddingY : _height - dstPaddingY;

                if (_scalingFilter != null)
                {
                    // TODO: Run scaling filter
                }

                pipeline.Present(
                    drawable,
                    tex,
                    new Extents2D(srcX0, srcY0, srcX1, srcY1),
                    new Extents2D(dstX0, dstY0, dstX1, dstY1),
                    _isLinear);
            }
        }

        public void SetSize(int width, int height)
        {
            _requestedWidth = width;
            _requestedHeight = height;
        }

        public void ChangeVSyncMode(bool vsyncEnabled)
        {
            // _vsyncEnabled = vsyncEnabled;
        }

        public void SetAntiAliasing(AntiAliasing effect)
        {
            if (_currentAntiAliasing == effect && _effect != null)
            {
                return;
            }

            _currentAntiAliasing = effect;

            _updateEffect = true;
        }

        public void SetScalingFilter(ScalingFilter type)
        {
            if (_currentScalingFilter == type && _effect != null)
            {
                return;
            }

            _currentScalingFilter = type;

            _updateScalingFilter = true;
        }

        public void SetScalingFilterLevel(float level)
        {
            // _scalingFilterLevel = level;
            _updateScalingFilter = true;
        }

        public void SetColorSpacePassthrough(bool colorSpacePassThroughEnabled)
        {
            // _colorSpacePassthroughEnabled = colorSpacePassThroughEnabled;
        }

        private void UpdateEffect()
        {
            if (_updateEffect)
            {
                _updateEffect = false;

                switch (_currentAntiAliasing)
                {
                    case AntiAliasing.Fxaa:
                        _effect?.Dispose();
                        Logger.Warning?.PrintMsg(LogClass.Gpu, "FXAA not implemented for Metal backend!");
                        break;
                    case AntiAliasing.None:
                        _effect?.Dispose();
                        _effect = null;
                        break;
                    case AntiAliasing.SmaaLow:
                    case AntiAliasing.SmaaMedium:
                    case AntiAliasing.SmaaHigh:
                    case AntiAliasing.SmaaUltra:
                        // var quality = _currentAntiAliasing - AntiAliasing.SmaaLow;
                        Logger.Warning?.PrintMsg(LogClass.Gpu, "SMAA not implemented for Metal backend!");
                        break;
                }
            }

            if (_updateScalingFilter)
            {
                _updateScalingFilter = false;

                switch (_currentScalingFilter)
                {
                    case ScalingFilter.Bilinear:
                    case ScalingFilter.Nearest:
                        _scalingFilter?.Dispose();
                        _scalingFilter = null;
                        _isLinear = _currentScalingFilter == ScalingFilter.Bilinear;
                        break;
                    case ScalingFilter.Fsr:
                        Logger.Warning?.PrintMsg(LogClass.Gpu, "FSR not implemented for Metal backend!");
                        break;
                }
            }
        }

        public void Dispose()
        {
            _metalLayer.Dispose();
        }
    }
}
