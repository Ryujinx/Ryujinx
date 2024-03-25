using Ryujinx.HLE.UI;
using Ryujinx.Memory;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
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
        private Image<Argb32> _surface = null;
        private byte[] _bufferData = null;

        private readonly Image _ryujinxLogo = null;
        private readonly Image _padAcceptIcon = null;
        private readonly Image _padCancelIcon = null;
        private readonly Image _keyModeIcon = null;

        private readonly float _textBoxOutlineWidth;
        private readonly float _padPressedPenWidth;

        private readonly Color _textNormalColor;
        private readonly Color _textSelectedColor;
        private readonly Color _textOverCursorColor;

        private readonly Brush _panelBrush;
        private readonly Brush _disabledBrush;
        private readonly Brush _cursorBrush;
        private readonly Brush _selectionBoxBrush;

        private readonly Pen _textBoxOutlinePen;
        private readonly Pen _cursorPen;
        private readonly Pen _selectionBoxPen;
        private readonly Pen _padPressedPen;

        private readonly int _inputTextFontSize;
        private Font _messageFont;
        private Font _inputTextFont;
        private Font _labelsTextFont;

        private RectangleF _panelRectangle;
        private Point _logoPosition;
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

            Color panelColor = ToColor(uiTheme.DefaultBackgroundColor, 255);
            Color panelTransparentColor = ToColor(uiTheme.DefaultBackgroundColor, 150);
            Color borderColor = ToColor(uiTheme.DefaultBorderColor);
            Color selectionBackgroundColor = ToColor(uiTheme.SelectionBackgroundColor);

            _textNormalColor = ToColor(uiTheme.DefaultForegroundColor);
            _textSelectedColor = ToColor(uiTheme.SelectionForegroundColor);
            _textOverCursorColor = ToColor(uiTheme.DefaultForegroundColor, null, true);

            float cursorWidth = 2;

            _textBoxOutlineWidth = 2;
            _padPressedPenWidth = 2;

            _panelBrush = new SolidBrush(panelColor);
            _disabledBrush = new SolidBrush(panelTransparentColor);
            _cursorBrush = new SolidBrush(_textNormalColor);
            _selectionBoxBrush = new SolidBrush(selectionBackgroundColor);

            _textBoxOutlinePen = Pens.Solid(borderColor, _textBoxOutlineWidth);
            _cursorPen = Pens.Solid(_textNormalColor, cursorWidth);
            _selectionBoxPen = Pens.Solid(selectionBackgroundColor, cursorWidth);
            _padPressedPen = Pens.Solid(borderColor, _padPressedPenWidth);

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
                    _messageFont = SystemFonts.CreateFont(fontFamily, 26, FontStyle.Regular);
                    _inputTextFont = SystemFonts.CreateFont(fontFamily, _inputTextFontSize, FontStyle.Regular);
                    _labelsTextFont = SystemFonts.CreateFont(fontFamily, 24, FontStyle.Regular);

                    return;
                }
                catch
                {
                }
            }

            throw new Exception($"None of these fonts were found in the system: {String.Join(", ", availableFonts)}!");
        }

        private static Color ToColor(ThemeColor color, byte? overrideAlpha = null, bool flipRgb = false)
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

            return Color.FromRgba(r, g, b, overrideAlpha.GetValueOrDefault(a));
        }

        private static Image LoadResource(Assembly assembly, string resourcePath, int newWidth, int newHeight)
        {
            Stream resourceStream = assembly.GetManifestResourceStream(resourcePath);

            return LoadResource(resourceStream, newWidth, newHeight);
        }

        private static Image LoadResource(Stream resourceStream, int newWidth, int newHeight)
        {
            Debug.Assert(resourceStream != null);

            var image = Image.Load(resourceStream);

            if (newHeight != 0 && newWidth != 0)
            {
                image.Mutate(x => x.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));
            }

            return image;
        }

        private static void SetGraphicsOptions(IImageProcessingContext context)
        {
            context.GetGraphicsOptions().Antialias = true;
            context.GetDrawingOptions().GraphicsOptions.Antialias = true;
        }

        private void DrawImmutableElements()
        {
            if (_surface == null)
            {
                return;
            }

            _surface.Mutate(context =>
            {
                SetGraphicsOptions(context);

                context.Clear(Color.Transparent);
                context.Fill(_panelBrush, _panelRectangle);
                context.DrawImage(_ryujinxLogo, _logoPosition, 1);

                float halfWidth = _panelRectangle.Width / 2;
                float buttonsY = _panelRectangle.Y + 185;

                PointF disableButtonPosition = new(halfWidth + 180, buttonsY);

                DrawControllerToggle(context, disableButtonPosition);
            });
        }

        public void DrawMutableElements(SoftwareKeyboardUIState state)
        {
            if (_surface == null)
            {
                return;
            }

            _surface.Mutate(context =>
            {
                var messageRectangle = MeasureString(MessageText, _messageFont);
                float messagePositionX = (_panelRectangle.Width - messageRectangle.Width) / 2 - messageRectangle.X;
                float messagePositionY = _messagePositionY - messageRectangle.Y;
                var messagePosition = new PointF(messagePositionX, messagePositionY);
                var messageBoundRectangle = new RectangleF(messagePositionX, messagePositionY, messageRectangle.Width, messageRectangle.Height);

                SetGraphicsOptions(context);

                context.Fill(_panelBrush, messageBoundRectangle);

                context.DrawText(MessageText, _messageFont, _textNormalColor, messagePosition);

                if (!state.TypingEnabled)
                {
                    // Just draw a semi-transparent rectangle on top to fade the component with the background.
                    // TODO (caian): This will not work if one decides to add make background semi-transparent as well.

                    context.Fill(_disabledBrush, messageBoundRectangle);
                }

                DrawTextBox(context, state);

                float halfWidth = _panelRectangle.Width / 2;
                float buttonsY = _panelRectangle.Y + 185;

                PointF acceptButtonPosition = new(halfWidth - 180, buttonsY);
                PointF cancelButtonPosition = new(halfWidth, buttonsY);
                PointF disableButtonPosition = new(halfWidth + 180, buttonsY);

                DrawPadButton(context, acceptButtonPosition, _padAcceptIcon, AcceptText, state.AcceptPressed, state.ControllerEnabled);
                DrawPadButton(context, cancelButtonPosition, _padCancelIcon, CancelText, state.CancelPressed, state.ControllerEnabled);
            });
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

            _surface = new Image<Argb32>((int)totalWidth, (int)totalHeight);

            ComputeConstants();
            DrawImmutableElements();
        }

        private void ComputeConstants()
        {
            int totalWidth = (int)_surfaceInfo.Width;
            int totalHeight = (int)_surfaceInfo.Height;

            int panelHeight = 240;
            int panelPositionY = totalHeight - panelHeight;

            _panelRectangle = new RectangleF(0, panelPositionY, totalWidth, panelHeight);

            _messagePositionY = panelPositionY + 60;

            int logoPositionX = (totalWidth - _ryujinxLogo.Width) / 2;
            int logoPositionY = panelPositionY + 18;

            _logoPosition = new Point(logoPositionX, logoPositionY);
        }
        private static RectangleF MeasureString(string text, Font font)
        {
            TextOptions options = new(font);

            if (text == "")
            {
                FontRectangle emptyRectangle = TextMeasurer.MeasureSize(" ", options);

                return new RectangleF(0, emptyRectangle.Y, 0, emptyRectangle.Height);
            }

            FontRectangle rectangle = TextMeasurer.MeasureSize(text, options);

            return new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        private static RectangleF MeasureString(ReadOnlySpan<char> text, Font font)
        {
            TextOptions options = new(font);

            if (text == "")
            {
                FontRectangle emptyRectangle = TextMeasurer.MeasureSize(" ", options);
                return new RectangleF(0, emptyRectangle.Y, 0, emptyRectangle.Height);
            }

            FontRectangle rectangle = TextMeasurer.MeasureSize(text, options);

            return new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        private void DrawTextBox(IImageProcessingContext context, SoftwareKeyboardUIState state)
        {
            var inputTextRectangle = MeasureString(state.InputText, _inputTextFont);

            float boxWidth = (int)(Math.Max(300, inputTextRectangle.Width + inputTextRectangle.X + 8));
            float boxHeight = 32;
            float boxY = _panelRectangle.Y + 110;
            float boxX = (int)((_panelRectangle.Width - boxWidth) / 2);

            RectangleF boxRectangle = new(boxX, boxY, boxWidth, boxHeight);

            RectangleF boundRectangle = new(_panelRectangle.X, boxY - _textBoxOutlineWidth,
                    _panelRectangle.Width, boxHeight + 2 * _textBoxOutlineWidth);

            context.Fill(_panelBrush, boundRectangle);

            context.Draw(_textBoxOutlinePen, boxRectangle);

            float inputTextX = (_panelRectangle.Width - inputTextRectangle.Width) / 2 - inputTextRectangle.X;
            float inputTextY = boxY + 5;

            var inputTextPosition = new PointF(inputTextX, inputTextY);

            context.DrawText(state.InputText, _inputTextFont, _textNormalColor, inputTextPosition);

            // Draw the cursor on top of the text and redraw the text with a different color if necessary.

            Color cursorTextColor;
            Brush cursorBrush;
            Pen cursorPen;

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

                var selectionBeginRectangle = MeasureString(textUntilBegin, _inputTextFont);
                var selectionEndRectangle = MeasureString(textUntilEnd, _inputTextFont);

                cursorVisible = true;
                cursorPositionXLeft = inputTextX + selectionBeginRectangle.Width + selectionBeginRectangle.X;
                cursorPositionXRight = inputTextX + selectionEndRectangle.Width + selectionEndRectangle.X;
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
                    var cursorTextRectangle = MeasureString(textUntilCursor, _inputTextFont);

                    cursorVisible = true;
                    cursorPositionXLeft = inputTextX + cursorTextRectangle.Width + cursorTextRectangle.X;

                    if (state.OverwriteMode)
                    {
                        // The blinking cursor is in overwrite mode so it takes the size of a character.

                        if (state.CursorBegin < state.InputText.Length)
                        {
                            textUntilCursor = state.InputText.AsSpan(0, cursorBegin + 1);
                            cursorTextRectangle = MeasureString(textUntilCursor, _inputTextFont);
                            cursorPositionXRight = inputTextX + cursorTextRectangle.Width + cursorTextRectangle.X;
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
                    PointF[] points = {
                        new PointF(cursorPositionXLeft, cursorPositionYTop),
                        new PointF(cursorPositionXLeft, cursorPositionYBottom),
                    };

                    context.DrawLine(cursorPen, points);
                }
                else
                {
                    var cursorRectangle = new RectangleF(cursorPositionXLeft, cursorPositionYTop, cursorWidth, cursorHeight);

                    context.Draw(cursorPen, cursorRectangle);
                    context.Fill(cursorBrush, cursorRectangle);

                    Image<Argb32> textOverCursor = new((int)cursorRectangle.Width, (int)cursorRectangle.Height);
                    textOverCursor.Mutate(context =>
                    {
                        var textRelativePosition = new PointF(inputTextPosition.X - cursorRectangle.X, inputTextPosition.Y - cursorRectangle.Y);
                        context.DrawText(state.InputText, _inputTextFont, cursorTextColor, textRelativePosition);
                    });

                    var cursorPosition = new Point((int)cursorRectangle.X, (int)cursorRectangle.Y);
                    context.DrawImage(textOverCursor, cursorPosition, 1);
                }
            }
            else if (!state.TypingEnabled)
            {
                // Just draw a semi-transparent rectangle on top to fade the component with the background.
                // TODO (caian): This will not work if one decides to add make background semi-transparent as well.

                context.Fill(_disabledBrush, boundRectangle);
            }
        }

        private void DrawPadButton(IImageProcessingContext context, PointF point, Image icon, string label, bool pressed, bool enabled)
        {
            // Use relative positions so we can center the entire drawing later.

            float iconX = 0;
            float iconY = 0;
            float iconWidth = icon.Width;
            float iconHeight = icon.Height;

            var labelRectangle = MeasureString(label, _labelsTextFont);

            float labelPositionX = iconWidth + 8 - labelRectangle.X;
            float labelPositionY = 3;

            float fullWidth = labelPositionX + labelRectangle.Width + labelRectangle.X;
            float fullHeight = iconHeight;

            // Convert all relative positions into absolute.

            float originX = (int)(point.X - fullWidth / 2);
            float originY = (int)(point.Y - fullHeight / 2);

            iconX += originX;
            iconY += originY;

            var iconPosition = new Point((int)iconX, (int)iconY);
            var labelPosition = new PointF(labelPositionX + originX, labelPositionY + originY);

            var selectedRectangle = new RectangleF(originX - 2 * _padPressedPenWidth, originY - 2 * _padPressedPenWidth,
                fullWidth + 4 * _padPressedPenWidth, fullHeight + 4 * _padPressedPenWidth);

            var boundRectangle = new RectangleF(originX, originY, fullWidth, fullHeight);
            boundRectangle.Inflate(4 * _padPressedPenWidth, 4 * _padPressedPenWidth);

            context.Fill(_panelBrush, boundRectangle);
            context.DrawImage(icon, iconPosition, 1);
            context.DrawText(label, _labelsTextFont, _textNormalColor, labelPosition);

            if (enabled)
            {
                if (pressed)
                {
                    context.Draw(_padPressedPen, selectedRectangle);
                }
            }
            else
            {
                // Just draw a semi-transparent rectangle on top to fade the component with the background.
                // TODO (caian): This will not work if one decides to add make background semi-transparent as well.

                context.Fill(_disabledBrush, boundRectangle);
            }
        }

        private void DrawControllerToggle(IImageProcessingContext context, PointF point)
        {
            var labelRectangle = MeasureString(ControllerToggleText, _labelsTextFont);

            // Use relative positions so we can center the entire drawing later.

            float keyWidth = _keyModeIcon.Width;
            float keyHeight = _keyModeIcon.Height;

            float labelPositionX = keyWidth + 8 - labelRectangle.X;
            float labelPositionY = -labelRectangle.Y - 1;

            float keyX = 0;
            float keyY = (int)((labelPositionY + labelRectangle.Height - keyHeight) / 2);

            float fullWidth = labelPositionX + labelRectangle.Width;
            float fullHeight = Math.Max(labelPositionY + labelRectangle.Height, keyHeight);

            // Convert all relative positions into absolute.

            float originX = (int)(point.X - fullWidth / 2);
            float originY = (int)(point.Y - fullHeight / 2);

            keyX += originX;
            keyY += originY;

            var labelPosition = new PointF(labelPositionX + originX, labelPositionY + originY);
            var overlayPosition = new Point((int)keyX, (int)keyY);

            context.DrawImage(_keyModeIcon, overlayPosition, 1);
            context.DrawText(ControllerToggleText, _labelsTextFont, _textNormalColor, labelPosition);
        }

        public void CopyImageToBuffer()
        {
            lock (_bufferLock)
            {
                if (_surface == null)
                {
                    return;
                }

                // Convert the pixel format used in the image to the one used in the Switch surface.

                if (!_surface.DangerousTryGetSinglePixelMemory(out Memory<Argb32> pixels))
                {
                    return;
                }

                _bufferData = MemoryMarshal.AsBytes(pixels.Span).ToArray();
                Span<uint> dataConvert = MemoryMarshal.Cast<byte, uint>(_bufferData);

                Debug.Assert(_bufferData.Length == _surfaceInfo.Size);

                for (int i = 0; i < dataConvert.Length; i++)
                {
                    dataConvert[i] = BitOperations.RotateRight(dataConvert[i], 8);
                }
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
