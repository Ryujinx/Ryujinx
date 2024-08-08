using Ryujinx.HLE.UI;
using Ryujinx.Memory;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Applets.SoftwareKeyboard
{
    /// <summary>
    /// Base class that generates the graphics for the software keyboard applet during inline mode.
    /// </summary>
    internal class SoftwareKeyboardRendererBase
    {
        public const int TextBoxBlinkThreshold = 8;

        const string MessageText = "Please use the keyboard to input text";
        const string AcceptText = "Accept";
        const string CancelText = "Cancel";
        const string ControllerToggleText = "Toggle input";

        private readonly object _bufferLock = new();

        private RenderingSurfaceInfo _surfaceInfo = null;
        private SKImageInfo _imageInfo;
        private SKSurface _surface = null;
        private byte[] _bufferData = null;

        private readonly SKBitmap _ryujinxLogo = null;
        private readonly SKBitmap _padAcceptIcon = null;
        private readonly SKBitmap _padCancelIcon = null;
        private readonly SKBitmap _keyModeIcon = null;

        private readonly float _textBoxOutlineWidth;
        private readonly float _padPressedPenWidth;

        private readonly SKColor _textNormalColor;
        private readonly SKColor _textSelectedColor;
        private readonly SKColor _textOverCursorColor;

        private readonly SKPaint _panelBrush;
        private readonly SKPaint _disabledBrush;
        private readonly SKPaint _cursorBrush;
        private readonly SKPaint _selectionBoxBrush;

        private readonly SKPaint _textBoxOutlinePen;
        private readonly SKPaint _cursorPen;
        private readonly SKPaint _selectionBoxPen;
        private readonly SKPaint _padPressedPen;

        private readonly int _inputTextFontSize;
        private SKFont _messageFont;
        private SKFont _inputTextFont;
        private SKFont _labelsTextFont;

        private SKRect _panelRectangle;
        private SKPoint _logoPosition;
        private float _messagePositionY;

        public SoftwareKeyboardRendererBase(IHostUITheme uiTheme)
        {
            int ryujinxLogoSize = 32;

            string ryujinxIconPath = "Ryujinx.HLE.HOS.Applets.SoftwareKeyboard.Resources.Logo_Ryujinx.png";
            _ryujinxLogo = LoadResource(typeof(SoftwareKeyboardRendererBase).Assembly, ryujinxIconPath, ryujinxLogoSize, ryujinxLogoSize);

            string padAcceptIconPath = "Ryujinx.HLE.HOS.Applets.SoftwareKeyboard.Resources.Icon_BtnA.png";
            string padCancelIconPath = "Ryujinx.HLE.HOS.Applets.SoftwareKeyboard.Resources.Icon_BtnB.png";
            string keyModeIconPath = "Ryujinx.HLE.HOS.Applets.SoftwareKeyboard.Resources.Icon_KeyF6.png";

            _padAcceptIcon = LoadResource(typeof(SoftwareKeyboardRendererBase).Assembly, padAcceptIconPath, 0, 0);
            _padCancelIcon = LoadResource(typeof(SoftwareKeyboardRendererBase).Assembly, padCancelIconPath, 0, 0);
            _keyModeIcon = LoadResource(typeof(SoftwareKeyboardRendererBase).Assembly, keyModeIconPath, 0, 0);

            var panelColor = ToColor(uiTheme.DefaultBackgroundColor, 255);
            var panelTransparentColor = ToColor(uiTheme.DefaultBackgroundColor, 150);
            var borderColor = ToColor(uiTheme.DefaultBorderColor);
            var selectionBackgroundColor = ToColor(uiTheme.SelectionBackgroundColor);

            _textNormalColor = ToColor(uiTheme.DefaultForegroundColor);
            _textSelectedColor = ToColor(uiTheme.SelectionForegroundColor);
            _textOverCursorColor = ToColor(uiTheme.DefaultForegroundColor, null, true);

            float cursorWidth = 2;

            _textBoxOutlineWidth = 2;
            _padPressedPenWidth = 2;

            _panelBrush = new SKPaint()
            {
                Color = panelColor,
                IsAntialias = true
            };
            _disabledBrush = new SKPaint()
            {
                Color = panelTransparentColor,
                IsAntialias = true
            };
            _cursorBrush = new SKPaint() { Color = _textNormalColor, IsAntialias = true };
            _selectionBoxBrush = new SKPaint() { Color = selectionBackgroundColor, IsAntialias = true };

            _textBoxOutlinePen = new SKPaint()
            {
                Color = borderColor,
                StrokeWidth = _textBoxOutlineWidth,
                IsStroke = true,
                IsAntialias = true
            };
            _cursorPen = new SKPaint() { Color = _textNormalColor, StrokeWidth = cursorWidth, IsStroke = true, IsAntialias = true };
            _selectionBoxPen = new SKPaint() { Color = selectionBackgroundColor, StrokeWidth = cursorWidth, IsStroke = true, IsAntialias = true };
            _padPressedPen = new SKPaint() { Color = borderColor, StrokeWidth = _padPressedPenWidth, IsStroke = true, IsAntialias = true };

            _inputTextFontSize = 20;

            CreateFonts(uiTheme.FontFamily);
        }

        private void CreateFonts(string uiThemeFontFamily)
        {
            // Try a list of fonts in case any of them is not available in the system.

            string[] availableFonts = {
                uiThemeFontFamily,
                "Liberation Sans",
                "FreeSans",
                "DejaVu Sans",
                "Lucida Grande",
            };

            foreach (string fontFamily in availableFonts)
            {
                try
                {
                    using var typeface = SKTypeface.FromFamilyName(fontFamily, SKFontStyle.Normal);
                    _messageFont = new SKFont(typeface, 26);
                    _inputTextFont = new SKFont(typeface, _inputTextFontSize);
                    _labelsTextFont = new SKFont(typeface, 24);

                    return;
                }
                catch
                {
                }
            }

            throw new Exception($"None of these fonts were found in the system: {String.Join(", ", availableFonts)}!");
        }

        private static SKColor ToColor(ThemeColor color, byte? overrideAlpha = null, bool flipRgb = false)
        {
            var a = (byte)(color.A * 255);
            var r = (byte)(color.R * 255);
            var g = (byte)(color.G * 255);
            var b = (byte)(color.B * 255);

            if (flipRgb)
            {
                r = (byte)(255 - r);
                g = (byte)(255 - g);
                b = (byte)(255 - b);
            }

            return new SKColor(r, g, b, overrideAlpha.GetValueOrDefault(a));
        }

        private static SKBitmap LoadResource(Assembly assembly, string resourcePath, int newWidth, int newHeight)
        {
            Stream resourceStream = assembly.GetManifestResourceStream(resourcePath);

            return LoadResource(resourceStream, newWidth, newHeight);
        }

        private static SKBitmap LoadResource(Stream resourceStream, int newWidth, int newHeight)
        {
            Debug.Assert(resourceStream != null);

            var bitmap = SKBitmap.Decode(resourceStream);

            if (newHeight != 0 && newWidth != 0)
            {
                var resized = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
                if (resized != null)
                {
                    bitmap.Dispose();
                    bitmap = resized;
                }
            }

            return bitmap;
        }

        private void DrawImmutableElements()
        {
            if (_surface == null)
            {
                return;
            }
            var canvas = _surface.Canvas;

            canvas.Clear(SKColors.Transparent);
            canvas.DrawRect(_panelRectangle, _panelBrush);
            canvas.DrawBitmap(_ryujinxLogo, _logoPosition);

            float halfWidth = _panelRectangle.Width / 2;
            float buttonsY = _panelRectangle.Top + 185;

            SKPoint disableButtonPosition = new(halfWidth + 180, buttonsY);

            DrawControllerToggle(canvas, disableButtonPosition);
        }

        public void DrawMutableElements(SoftwareKeyboardUIState state)
        {
            if (_surface == null)
            {
                return;
            }

            using var paint = new SKPaint(_messageFont)
            {
                Color = _textNormalColor,
                IsAntialias = true
            };

            var canvas = _surface.Canvas;
            var messageRectangle = MeasureString(MessageText, paint);
            float messagePositionX = (_panelRectangle.Width - messageRectangle.Width) / 2 - messageRectangle.Left;
            float messagePositionY = _messagePositionY - messageRectangle.Top;
            var messagePosition = new SKPoint(messagePositionX, messagePositionY);
            var messageBoundRectangle = SKRect.Create(messagePositionX, messagePositionY, messageRectangle.Width, messageRectangle.Height);

            canvas.DrawRect(messageBoundRectangle, _panelBrush);

            canvas.DrawText(MessageText, messagePosition.X, messagePosition.Y + _messageFont.Metrics.XHeight + _messageFont.Metrics.Descent, paint);

            if (!state.TypingEnabled)
            {
                // Just draw a semi-transparent rectangle on top to fade the component with the background.
                // TODO (caian): This will not work if one decides to add make background semi-transparent as well.

                canvas.DrawRect(messageBoundRectangle, _disabledBrush);
            }

            DrawTextBox(canvas, state);

            float halfWidth = _panelRectangle.Width / 2;
            float buttonsY = _panelRectangle.Top + 185;

            SKPoint acceptButtonPosition = new(halfWidth - 180, buttonsY);
            SKPoint cancelButtonPosition = new(halfWidth, buttonsY);
            SKPoint disableButtonPosition = new(halfWidth + 180, buttonsY);

            DrawPadButton(canvas, acceptButtonPosition, _padAcceptIcon, AcceptText, state.AcceptPressed, state.ControllerEnabled);
            DrawPadButton(canvas, cancelButtonPosition, _padCancelIcon, CancelText, state.CancelPressed, state.ControllerEnabled);

        }

        public void CreateSurface(RenderingSurfaceInfo surfaceInfo)
        {
            if (_surfaceInfo != null)
            {
                return;
            }

            _surfaceInfo = surfaceInfo;

            Debug.Assert(_surfaceInfo.ColorFormat == Services.SurfaceFlinger.ColorFormat.A8B8G8R8);

            // Use the whole area of the image to draw, even the alignment, otherwise it may shear the final
            // image if the pitch is different.
            uint totalWidth = _surfaceInfo.Pitch / 4;
            uint totalHeight = _surfaceInfo.Size / _surfaceInfo.Pitch;

            Debug.Assert(_surfaceInfo.Width <= totalWidth);
            Debug.Assert(_surfaceInfo.Height <= totalHeight);
            Debug.Assert(_surfaceInfo.Pitch * _surfaceInfo.Height <= _surfaceInfo.Size);

            _imageInfo = new SKImageInfo((int)totalWidth, (int)totalHeight, SKColorType.Rgba8888);
            _surface = SKSurface.Create(_imageInfo);

            ComputeConstants();
            DrawImmutableElements();
        }

        private void ComputeConstants()
        {
            int totalWidth = (int)_surfaceInfo.Width;
            int totalHeight = (int)_surfaceInfo.Height;

            int panelHeight = 240;
            int panelPositionY = totalHeight - panelHeight;

            _panelRectangle = SKRect.Create(0, panelPositionY, totalWidth, panelHeight);

            _messagePositionY = panelPositionY + 60;

            int logoPositionX = (totalWidth - _ryujinxLogo.Width) / 2;
            int logoPositionY = panelPositionY + 18;

            _logoPosition = new SKPoint(logoPositionX, logoPositionY);
        }
        private static SKRect MeasureString(string text, SKPaint paint)
        {
            SKRect bounds = SKRect.Empty;

            if (text == "")
            {
                paint.MeasureText(" ", ref bounds);
            }
            else
            {
                paint.MeasureText(text, ref bounds);
            }

            return bounds;
        }

        private static SKRect MeasureString(ReadOnlySpan<char> text, SKPaint paint)
        {
            SKRect bounds = SKRect.Empty;

            if (text == "")
            {
                paint.MeasureText(" ", ref bounds);
            }
            else
            {
                paint.MeasureText(text, ref bounds);
            }

            return bounds;
        }

        private void DrawTextBox(SKCanvas canvas, SoftwareKeyboardUIState state)
        {
            using var textPaint = new SKPaint(_labelsTextFont)
            {
                IsAntialias = true,
                Color = _textNormalColor
            };
            var inputTextRectangle = MeasureString(state.InputText, textPaint);

            float boxWidth = (int)(Math.Max(300, inputTextRectangle.Width + inputTextRectangle.Left + 8));
            float boxHeight = 32;
            float boxY = _panelRectangle.Top + 110;
            float boxX = (int)((_panelRectangle.Width - boxWidth) / 2);

            SKRect boxRectangle = SKRect.Create(boxX, boxY, boxWidth, boxHeight);

            SKRect boundRectangle = SKRect.Create(_panelRectangle.Left, boxY - _textBoxOutlineWidth,
                    _panelRectangle.Width, boxHeight + 2 * _textBoxOutlineWidth);

            canvas.DrawRect(boundRectangle, _panelBrush);

            canvas.DrawRect(boxRectangle, _textBoxOutlinePen);

            float inputTextX = (_panelRectangle.Width - inputTextRectangle.Width) / 2 - inputTextRectangle.Left;
            float inputTextY = boxY + 5;

            var inputTextPosition = new SKPoint(inputTextX, inputTextY);
            canvas.DrawText(state.InputText, inputTextPosition.X, inputTextPosition.Y + (_labelsTextFont.Metrics.XHeight + _labelsTextFont.Metrics.Descent), textPaint);

            // Draw the cursor on top of the text and redraw the text with a different color if necessary.

            SKColor cursorTextColor;
            SKPaint cursorBrush;
            SKPaint cursorPen;

            float cursorPositionYTop = inputTextY + 1;
            float cursorPositionYBottom = cursorPositionYTop + _inputTextFontSize + 1;
            float cursorPositionXLeft;
            float cursorPositionXRight;

            bool cursorVisible = false;

            if (state.CursorBegin != state.CursorEnd)
            {
                Debug.Assert(state.InputText.Length > 0);

                cursorTextColor = _textSelectedColor;
                cursorBrush = _selectionBoxBrush;
                cursorPen = _selectionBoxPen;

                ReadOnlySpan<char> textUntilBegin = state.InputText.AsSpan(0, state.CursorBegin);
                ReadOnlySpan<char> textUntilEnd = state.InputText.AsSpan(0, state.CursorEnd);

                var selectionBeginRectangle = MeasureString(textUntilBegin, textPaint);
                var selectionEndRectangle = MeasureString(textUntilEnd, textPaint);

                cursorVisible = true;
                cursorPositionXLeft = inputTextX + selectionBeginRectangle.Width + selectionBeginRectangle.Left;
                cursorPositionXRight = inputTextX + selectionEndRectangle.Width + selectionEndRectangle.Left;
            }
            else
            {
                cursorTextColor = _textOverCursorColor;
                cursorBrush = _cursorBrush;
                cursorPen = _cursorPen;

                if (state.TextBoxBlinkCounter < TextBoxBlinkThreshold)
                {
                    // Show the blinking cursor.

                    int cursorBegin = Math.Min(state.InputText.Length, state.CursorBegin);
                    ReadOnlySpan<char> textUntilCursor = state.InputText.AsSpan(0, cursorBegin);
                    var cursorTextRectangle = MeasureString(textUntilCursor, textPaint);

                    cursorVisible = true;
                    cursorPositionXLeft = inputTextX + cursorTextRectangle.Width + cursorTextRectangle.Left;

                    if (state.OverwriteMode)
                    {
                        // The blinking cursor is in overwrite mode so it takes the size of a character.

                        if (state.CursorBegin < state.InputText.Length)
                        {
                            textUntilCursor = state.InputText.AsSpan(0, cursorBegin + 1);
                            cursorTextRectangle = MeasureString(textUntilCursor, textPaint);
                            cursorPositionXRight = inputTextX + cursorTextRectangle.Width + cursorTextRectangle.Left;
                        }
                        else
                        {
                            cursorPositionXRight = cursorPositionXLeft + _inputTextFontSize / 2;
                        }
                    }
                    else
                    {
                        // The blinking cursor is in insert mode so it is only a line.
                        cursorPositionXRight = cursorPositionXLeft;
                    }
                }
                else
                {
                    cursorPositionXLeft = inputTextX;
                    cursorPositionXRight = inputTextX;
                }
            }

            if (state.TypingEnabled && cursorVisible)
            {
                float cursorWidth = cursorPositionXRight - cursorPositionXLeft;
                float cursorHeight = cursorPositionYBottom - cursorPositionYTop;

                if (cursorWidth == 0)
                {
                    canvas.DrawLine(new SKPoint(cursorPositionXLeft, cursorPositionYTop),
                        new SKPoint(cursorPositionXLeft, cursorPositionYBottom),
                        cursorPen);
                }
                else
                {
                    var cursorRectangle = SKRect.Create(cursorPositionXLeft, cursorPositionYTop, cursorWidth, cursorHeight);

                    canvas.DrawRect(cursorRectangle, cursorPen);
                    canvas.DrawRect(cursorRectangle, cursorBrush);

                    using var textOverCursor = SKSurface.Create(new SKImageInfo((int)cursorRectangle.Width, (int)cursorRectangle.Height, SKColorType.Rgba8888));
                    var textOverCanvas = textOverCursor.Canvas;
                    var textRelativePosition = new SKPoint(inputTextPosition.X - cursorRectangle.Left, inputTextPosition.Y - cursorRectangle.Top);

                    using var cursorPaint = new SKPaint(_inputTextFont)
                    {
                        Color = cursorTextColor,
                        IsAntialias = true
                    };

                    textOverCanvas.DrawText(state.InputText, textRelativePosition.X, textRelativePosition.Y + _inputTextFont.Metrics.XHeight + _inputTextFont.Metrics.Descent, cursorPaint);

                    var cursorPosition = new SKPoint((int)cursorRectangle.Left, (int)cursorRectangle.Top);
                    textOverCursor.Flush();
                    canvas.DrawSurface(textOverCursor, cursorPosition);
                }
            }
            else if (!state.TypingEnabled)
            {
                // Just draw a semi-transparent rectangle on top to fade the component with the background.
                // TODO (caian): This will not work if one decides to add make background semi-transparent as well.

                canvas.DrawRect(boundRectangle, _disabledBrush);
            }
        }

        private void DrawPadButton(SKCanvas canvas, SKPoint point, SKBitmap icon, string label, bool pressed, bool enabled)
        {
            // Use relative positions so we can center the entire drawing later.

            float iconX = 0;
            float iconY = 0;
            float iconWidth = icon.Width;
            float iconHeight = icon.Height;

            using var paint = new SKPaint(_labelsTextFont)
            {
                Color = _textNormalColor,
                IsAntialias = true
            };

            var labelRectangle = MeasureString(label, paint);

            float labelPositionX = iconWidth + 8 - labelRectangle.Left;
            float labelPositionY = 3;

            float fullWidth = labelPositionX + labelRectangle.Width + labelRectangle.Left;
            float fullHeight = iconHeight;

            // Convert all relative positions into absolute.

            float originX = (int)(point.X - fullWidth / 2);
            float originY = (int)(point.Y - fullHeight / 2);

            iconX += originX;
            iconY += originY;

            var iconPosition = new SKPoint((int)iconX, (int)iconY);
            var labelPosition = new SKPoint(labelPositionX + originX, labelPositionY + originY);

            var selectedRectangle = SKRect.Create(originX - 2 * _padPressedPenWidth, originY - 2 * _padPressedPenWidth,
                fullWidth + 4 * _padPressedPenWidth, fullHeight + 4 * _padPressedPenWidth);

            var boundRectangle = SKRect.Create(originX, originY, fullWidth, fullHeight);
            boundRectangle.Inflate(4 * _padPressedPenWidth, 4 * _padPressedPenWidth);

            canvas.DrawRect(boundRectangle, _panelBrush);
            canvas.DrawBitmap(icon, iconPosition);
            canvas.DrawText(label, labelPosition.X, labelPosition.Y + _labelsTextFont.Metrics.XHeight + _labelsTextFont.Metrics.Descent, paint);

            if (enabled)
            {
                if (pressed)
                {
                    canvas.DrawRect(selectedRectangle, _padPressedPen);
                }
            }
            else
            {
                // Just draw a semi-transparent rectangle on top to fade the component with the background.
                // TODO (caian): This will not work if one decides to add make background semi-transparent as well.

                canvas.DrawRect(boundRectangle, _disabledBrush);
            }
        }

        private void DrawControllerToggle(SKCanvas canvas, SKPoint point)
        {
            using var paint = new SKPaint(_labelsTextFont)
            {
                IsAntialias = true,
                Color = _textNormalColor
            };
            var labelRectangle = MeasureString(ControllerToggleText, paint);

            // Use relative positions so we can center the entire drawing later.

            float keyWidth = _keyModeIcon.Width;
            float keyHeight = _keyModeIcon.Height;

            float labelPositionX = keyWidth + 8 - labelRectangle.Left;
            float labelPositionY = -labelRectangle.Top - 1;

            float keyX = 0;
            float keyY = (int)((labelPositionY + labelRectangle.Height - keyHeight) / 2);

            float fullWidth = labelPositionX + labelRectangle.Width;
            float fullHeight = Math.Max(labelPositionY + labelRectangle.Height, keyHeight);

            // Convert all relative positions into absolute.

            float originX = (int)(point.X - fullWidth / 2);
            float originY = (int)(point.Y - fullHeight / 2);

            keyX += originX;
            keyY += originY;

            var labelPosition = new SKPoint(labelPositionX + originX, labelPositionY + originY);
            var overlayPosition = new SKPoint((int)keyX, (int)keyY);

            canvas.DrawBitmap(_keyModeIcon, overlayPosition);
            canvas.DrawText(ControllerToggleText, labelPosition.X, labelPosition.Y + _labelsTextFont.Metrics.XHeight, paint);
        }

        public unsafe void CopyImageToBuffer()
        {
            lock (_bufferLock)
            {
                if (_surface == null)
                {
                    return;
                }

                // Convert the pixel format used in the image to the one used in the Switch surface.
                _surface.Flush();

                var buffer = new byte[_imageInfo.BytesSize];
                fixed (byte* bufferPtr = buffer)
                {
                    if (!_surface.ReadPixels(_imageInfo, (nint)bufferPtr, _imageInfo.RowBytes, 0, 0))
                    {
                        return;
                    }
                }

                _bufferData = buffer;

                Debug.Assert(buffer.Length == _surfaceInfo.Size);
            }
        }

        public bool WriteBufferToMemory(IVirtualMemoryManager destination, ulong position)
        {
            lock (_bufferLock)
            {
                if (_bufferData == null)
                {
                    return false;
                }

                try
                {
                    destination.Write(position, _bufferData);
                }
                catch
                {
                    return false;
                }

                return true;
            }
        }
    }
}
