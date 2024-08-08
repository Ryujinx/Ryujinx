using OpenTK.Graphics.OpenGL;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.OpenGL.Effects;
using Ryujinx.Graphics.OpenGL.Effects.Smaa;
using Ryujinx.Graphics.OpenGL.Image;
using System;

namespace Ryujinx.Graphics.OpenGL
{
    class Window : IWindow, IDisposable
    {
        private readonly OpenGLRenderer _renderer;

        private bool _initialized;

        private int _width;
        private int _height;
        private bool _updateSize;
        private int _copyFramebufferHandle;
        private IPostProcessingEffect _antiAliasing;
        private FsrScalingFilter _scalingFilter;
        private bool _isLinear;
        private AntiAliasing _currentAntiAliasing;
        private bool _updateEffect;
        private ScalingFilter _currentScalingFilter;
        private float _scalingFilterLevel;
        private bool _updateScalingFilter;
        private bool _isBgra;
        private TextureView _upscaledTexture;

        internal BackgroundContextWorker BackgroundContext { get; private set; }

        internal bool ScreenCaptureRequested { get; set; }

        public Window(OpenGLRenderer renderer)
        {
            _renderer = renderer;
        }

        public void Present(ITexture texture, ImageCrop crop, Action swapBuffersCallback)
        {
            GL.Disable(EnableCap.FramebufferSrgb);

            (int oldDrawFramebufferHandle, int oldReadFramebufferHandle) = ((Pipeline)_renderer.Pipeline).GetBoundFramebuffers();

            CopyTextureToFrameBufferRGB(0, GetCopyFramebufferHandleLazy(), (TextureView)texture, crop, swapBuffersCallback);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, oldReadFramebufferHandle);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, oldDrawFramebufferHandle);

            GL.Enable(EnableCap.FramebufferSrgb);

            // Restore unpack alignment to 4, as performance overlays such as RTSS may change this to load their resources.
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }

        public void ChangeVSyncMode(bool vsyncEnabled) { }

        public void SetSize(int width, int height)
        {
            _width = width;
            _height = height;

            _updateSize = true;
        }

        private void CopyTextureToFrameBufferRGB(int drawFramebuffer, int readFramebuffer, TextureView view, ImageCrop crop, Action swapBuffersCallback)
        {
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFramebuffer);

            TextureView viewConverted = view.Format.IsBgr() ? _renderer.TextureCopy.BgraSwap(view) : view;

            UpdateEffect();

            if (_antiAliasing != null)
            {
                var oldView = viewConverted;

                viewConverted = _antiAliasing.Run(viewConverted, _width, _height);

                if (viewConverted.Format.IsBgr())
                {
                    var swappedView = _renderer.TextureCopy.BgraSwap(viewConverted);

                    viewConverted?.Dispose();

                    viewConverted = swappedView;
                }

                if (viewConverted != oldView && oldView != view)
                {
                    oldView.Dispose();
                }
            }

            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, drawFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, readFramebuffer);

            GL.FramebufferTexture(
                FramebufferTarget.ReadFramebuffer,
                FramebufferAttachment.ColorAttachment0,
                viewConverted.Handle,
                0);

            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            GL.Disable(EnableCap.RasterizerDiscard);
            GL.Disable(IndexedEnableCap.ScissorTest, 0);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            int srcX0, srcX1, srcY0, srcY1;

            if (crop.Left == 0 && crop.Right == 0)
            {
                srcX0 = 0;
                srcX1 = viewConverted.Width;
            }
            else
            {
                srcX0 = crop.Left;
                srcX1 = crop.Right;
            }

            if (crop.Top == 0 && crop.Bottom == 0)
            {
                srcY0 = 0;
                srcY1 = viewConverted.Height;
            }
            else
            {
                srcY0 = crop.Top;
                srcY1 = crop.Bottom;
            }

            float ratioX = crop.IsStretched ? 1.0f : MathF.Min(1.0f, _height * crop.AspectRatioX / (_width * crop.AspectRatioY));
            float ratioY = crop.IsStretched ? 1.0f : MathF.Min(1.0f, _width * crop.AspectRatioY / (_height * crop.AspectRatioX));

            int dstWidth = (int)(_width * ratioX);
            int dstHeight = (int)(_height * ratioY);

            int dstPaddingX = (_width - dstWidth) / 2;
            int dstPaddingY = (_height - dstHeight) / 2;

            int dstX0 = crop.FlipX ? _width - dstPaddingX : dstPaddingX;
            int dstX1 = crop.FlipX ? dstPaddingX : _width - dstPaddingX;

            int dstY0 = crop.FlipY ? dstPaddingY : _height - dstPaddingY;
            int dstY1 = crop.FlipY ? _height - dstPaddingY : dstPaddingY;

            if (ScreenCaptureRequested)
            {
                CaptureFrame(srcX0, srcY0, srcX1, srcY1, view.Format.IsBgr(), crop.FlipX, crop.FlipY);

                ScreenCaptureRequested = false;
            }

            if (_scalingFilter != null)
            {
                if (viewConverted.Format.IsBgr() && !_isBgra)
                {
                    RecreateUpscalingTexture(true);
                }

                _scalingFilter.Run(
                    viewConverted,
                    _upscaledTexture,
                    _width,
                    _height,
                    new Extents2D(
                        srcX0,
                        srcY0,
                        srcX1,
                        srcY1),
                    new Extents2D(
                        dstX0,
                        dstY0,
                        dstX1,
                        dstY1)
                    );

                srcX0 = dstX0;
                srcY0 = dstY0;
                srcX1 = dstX1;
                srcY1 = dstY1;

                GL.FramebufferTexture(
                    FramebufferTarget.ReadFramebuffer,
                    FramebufferAttachment.ColorAttachment0,
                    _upscaledTexture.Handle,
                    0);
            }

            GL.BlitFramebuffer(
                srcX0,
                srcY0,
                srcX1,
                srcY1,
                dstX0,
                dstY0,
                dstX1,
                dstY1,
                ClearBufferMask.ColorBufferBit,
                _isLinear ? BlitFramebufferFilter.Linear : BlitFramebufferFilter.Nearest);

            // Remove Alpha channel
            GL.ColorMask(false, false, false, true);
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            for (int i = 0; i < Constants.MaxRenderTargets; i++)
            {
                ((Pipeline)_renderer.Pipeline).RestoreComponentMask(i);
            }

            // Set clip control, viewport and the framebuffer to the output to placate overlays and OBS capture.
            GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.NegativeOneToOne);
            GL.Viewport(0, 0, _width, _height);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, drawFramebuffer);

            swapBuffersCallback();

            ((Pipeline)_renderer.Pipeline).RestoreClipControl();
            ((Pipeline)_renderer.Pipeline).RestoreScissor0Enable();
            ((Pipeline)_renderer.Pipeline).RestoreRasterizerDiscard();
            ((Pipeline)_renderer.Pipeline).RestoreViewport0();

            if (viewConverted != view)
            {
                viewConverted.Dispose();
            }
        }

        private int GetCopyFramebufferHandleLazy()
        {
            int handle = _copyFramebufferHandle;

            if (handle == 0)
            {
                handle = GL.GenFramebuffer();

                _copyFramebufferHandle = handle;
            }

            return handle;
        }

        public void InitializeBackgroundContext(IOpenGLContext baseContext)
        {
            BackgroundContext = new BackgroundContextWorker(baseContext);
            _initialized = true;
        }

        public void CaptureFrame(int x, int y, int width, int height, bool isBgra, bool flipX, bool flipY)
        {
            long size = Math.Abs(4 * width * height);
            byte[] bitmap = new byte[size];

            GL.ReadPixels(x, y, width, height, isBgra ? PixelFormat.Bgra : PixelFormat.Rgba, PixelType.UnsignedByte, bitmap);

            _renderer.OnScreenCaptured(new ScreenCaptureImageInfo(width, height, isBgra, bitmap, flipX, flipY));
        }

        public void Dispose()
        {
            if (!_initialized)
            {
                return;
            }

            BackgroundContext.Dispose();

            if (_copyFramebufferHandle != 0)
            {
                GL.DeleteFramebuffer(_copyFramebufferHandle);

                _copyFramebufferHandle = 0;
            }

            _antiAliasing?.Dispose();
            _scalingFilter?.Dispose();
            _upscaledTexture?.Dispose();
        }

        public void SetAntiAliasing(AntiAliasing effect)
        {
            if (_currentAntiAliasing == effect && _antiAliasing != null)
            {
                return;
            }

            _currentAntiAliasing = effect;

            _updateEffect = true;
        }

        public void SetScalingFilter(ScalingFilter type)
        {
            if (_currentScalingFilter == type && _antiAliasing != null)
            {
                return;
            }

            _currentScalingFilter = type;

            _updateScalingFilter = true;
        }

        public void SetColorSpacePassthrough(bool colorSpacePassthroughEnabled) { }

        private void UpdateEffect()
        {
            if (_updateEffect)
            {
                _updateEffect = false;

                switch (_currentAntiAliasing)
                {
                    case AntiAliasing.Fxaa:
                        _antiAliasing?.Dispose();
                        _antiAliasing = new FxaaPostProcessingEffect(_renderer);
                        break;
                    case AntiAliasing.None:
                        _antiAliasing?.Dispose();
                        _antiAliasing = null;
                        break;
                    case AntiAliasing.SmaaLow:
                    case AntiAliasing.SmaaMedium:
                    case AntiAliasing.SmaaHigh:
                    case AntiAliasing.SmaaUltra:
                        var quality = _currentAntiAliasing - AntiAliasing.SmaaLow;
                        if (_antiAliasing is SmaaPostProcessingEffect smaa)
                        {
                            smaa.Quality = quality;
                        }
                        else
                        {
                            _antiAliasing?.Dispose();
                            _antiAliasing = new SmaaPostProcessingEffect(_renderer, quality);
                        }
                        break;
                }
            }

            if (_updateSize && !_updateScalingFilter)
            {
                RecreateUpscalingTexture();
            }

            _updateSize = false;

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
                        _upscaledTexture?.Dispose();
                        _upscaledTexture = null;
                        break;
                    case ScalingFilter.Fsr:
                        _scalingFilter ??= new FsrScalingFilter(_renderer);
                        _isLinear = false;
                        _scalingFilter.Level = _scalingFilterLevel;

                        RecreateUpscalingTexture();
                        break;
                }
            }
        }

        private void RecreateUpscalingTexture(bool forceBgra = false)
        {
            _upscaledTexture?.Dispose();

            var info = new TextureCreateInfo(
                _width,
                _height,
                1,
                1,
                1,
                1,
                1,
                1,
                Format.R8G8B8A8Unorm,
                DepthStencilMode.Depth,
                Target.Texture2D,
                forceBgra ? SwizzleComponent.Blue : SwizzleComponent.Red,
                SwizzleComponent.Green,
                forceBgra ? SwizzleComponent.Red : SwizzleComponent.Blue,
                SwizzleComponent.Alpha);

            _isBgra = forceBgra;
            _upscaledTexture = _renderer.CreateTexture(info) as TextureView;
        }

        public void SetScalingFilterLevel(float level)
        {
            _scalingFilterLevel = level;
            _updateScalingFilter = true;
        }
    }
}
