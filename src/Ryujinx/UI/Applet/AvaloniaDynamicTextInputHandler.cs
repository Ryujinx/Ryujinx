using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Ryujinx.Ava.Input;
using Ryujinx.Ava.UI.Helpers;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.HLE.UI;
using System;
using System.Threading;
using HidKey = Ryujinx.Common.Configuration.Hid.Key;

namespace Ryujinx.Ava.UI.Applet
{
    class AvaloniaDynamicTextInputHandler : IDynamicTextInputHandler
    {
        private MainWindow _parent;
        private readonly OffscreenTextBox _hiddenTextBox;
        private bool _canProcessInput;
        private IDisposable _textChangedSubscription;
        private IDisposable _selectionStartChangedSubscription;
        private IDisposable _selectionEndtextChangedSubscription;

        public AvaloniaDynamicTextInputHandler(MainWindow parent)
        {
            _parent = parent;

            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyPressed += AvaloniaDynamicTextInputHandler_KeyPressed;
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyRelease += AvaloniaDynamicTextInputHandler_KeyRelease;
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).TextInput += AvaloniaDynamicTextInputHandler_TextInput;

            _hiddenTextBox = _parent.HiddenTextBox;

            Dispatcher.UIThread.Post(() =>
            {
                _textChangedSubscription = _hiddenTextBox.GetObservable(TextBox.TextProperty).Subscribe(TextChanged);
                _selectionStartChangedSubscription = _hiddenTextBox.GetObservable(TextBox.SelectionStartProperty).Subscribe(SelectionChanged);
                _selectionEndtextChangedSubscription = _hiddenTextBox.GetObservable(TextBox.SelectionEndProperty).Subscribe(SelectionChanged);
            });
        }

        private void TextChanged(string text)
        {
            TextChangedEvent?.Invoke(text ?? string.Empty, _hiddenTextBox.SelectionStart, _hiddenTextBox.SelectionEnd, false);
        }

        private void SelectionChanged(int selection)
        {
            TextChangedEvent?.Invoke(_hiddenTextBox.Text ?? string.Empty, _hiddenTextBox.SelectionStart, _hiddenTextBox.SelectionEnd, false);
        }

        private void AvaloniaDynamicTextInputHandler_TextInput(object sender, string text)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_canProcessInput)
                {
                    _hiddenTextBox.SendText(text);
                }
            });
        }

        private void AvaloniaDynamicTextInputHandler_KeyRelease(object sender, KeyEventArgs e)
        {
            var key = (HidKey)AvaloniaKeyboardMappingHelper.ToInputKey(e.Key);

            if (!(KeyReleasedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            e.RoutedEvent = OffscreenTextBox.GetKeyUpRoutedEvent();

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_canProcessInput)
                {
                    _hiddenTextBox.SendKeyUpEvent(e);
                }
            });
        }

        private void AvaloniaDynamicTextInputHandler_KeyPressed(object sender, KeyEventArgs e)
        {
            var key = (HidKey)AvaloniaKeyboardMappingHelper.ToInputKey(e.Key);

            if (!(KeyPressedEvent?.Invoke(key)).GetValueOrDefault(true))
            {
                return;
            }

            e.RoutedEvent = OffscreenTextBox.GetKeyUpRoutedEvent();

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_canProcessInput)
                {
                    _hiddenTextBox.SendKeyDownEvent(e);
                }
            });
        }

        public bool TextProcessingEnabled
        {
            get
            {
                return Volatile.Read(ref _canProcessInput);
            }
            set
            {
                Volatile.Write(ref _canProcessInput, value);
            }
        }

        public event DynamicTextChangedHandler TextChangedEvent;
        public event KeyPressedHandler KeyPressedEvent;
        public event KeyReleasedHandler KeyReleasedEvent;

        public void Dispose()
        {
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyPressed -= AvaloniaDynamicTextInputHandler_KeyPressed;
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).KeyRelease -= AvaloniaDynamicTextInputHandler_KeyRelease;
            (_parent.InputManager.KeyboardDriver as AvaloniaKeyboardDriver).TextInput -= AvaloniaDynamicTextInputHandler_TextInput;

            _textChangedSubscription?.Dispose();
            _selectionStartChangedSubscription?.Dispose();
            _selectionEndtextChangedSubscription?.Dispose();

            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Clear();
                _parent.ViewModel.RendererHostControl.Focus();

                _parent = null;
            });
        }

        public void SetText(string text, int cursorBegin)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Text = text;
                _hiddenTextBox.CaretIndex = cursorBegin;
            });
        }

        public void SetText(string text, int cursorBegin, int cursorEnd)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _hiddenTextBox.Text = text;
                _hiddenTextBox.SelectionStart = cursorBegin;
                _hiddenTextBox.SelectionEnd = cursorEnd;
            });
        }
    }
}
