using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ryujinx.Input.Assigner
{
    /// <summary>
    /// <see cref="IButtonAssigner"/> implementation for regular <see cref="IGamepad"/>.
    /// </summary>
    public class GamepadButtonAssigner : IButtonAssigner
    {
        private readonly IGamepad _gamepad;

        private GamepadStateSnapshot _currState;

        private GamepadStateSnapshot _prevState;

        private readonly JoystickButtonDetector _detector;

        private readonly bool _forStick;

        public GamepadButtonAssigner(IGamepad gamepad, float triggerThreshold, bool forStick)
        {
            _gamepad = gamepad;
            _detector = new JoystickButtonDetector();
            _forStick = forStick;

            _gamepad?.SetTriggerThreshold(triggerThreshold);
        }

        public void Initialize()
        {
            if (_gamepad != null)
            {
                _currState = _gamepad.GetStateSnapshot();
                _prevState = _currState;
            }
        }

        public void ReadInput()
        {
            if (_gamepad != null)
            {
                _prevState = _currState;
                _currState = _gamepad.GetStateSnapshot();
            }

            CollectButtonStats();
        }

        public bool HasAnyButtonPressed()
        {
            return _detector.HasAnyButtonPressed();
        }

        public bool ShouldCancel()
        {
            return _gamepad == null || !_gamepad.IsConnected;
        }

        public ButtonValue? GetPressedButton()
        {
            IEnumerable<GamepadButtonInputId> pressedButtons = _detector.GetPressedButtons();

            if (pressedButtons.Any())
            {
                return !_forStick ? new(pressedButtons.First()) : new(((StickInputId)pressedButtons.First()));
            }

            return null;
        }

        private void CollectButtonStats()
        {
            if (_forStick)
            {
                for (StickInputId inputId = StickInputId.Left; inputId < StickInputId.Count; inputId++)
                {
                    (float x, float y) = _currState.GetStick(inputId);

                    float value;

                    if (x != 0.0f)
                    {
                        value = x;
                    }
                    else if (y != 0.0f)
                    {
                        value = y;
                    }
                    else
                    {
                        continue;
                    }

                    _detector.AddInput((GamepadButtonInputId)inputId, value);
                }
            }
            else
            {
                for (GamepadButtonInputId inputId = GamepadButtonInputId.A; inputId < GamepadButtonInputId.Count; inputId++)
                {
                    if (_currState.IsPressed(inputId) && !_prevState.IsPressed(inputId))
                    {
                        _detector.AddInput(inputId, 1);
                    }

                    if (!_currState.IsPressed(inputId) && _prevState.IsPressed(inputId))
                    {
                        _detector.AddInput(inputId, -1);
                    }
                }
            }
        }

        private class JoystickButtonDetector
        {
            private readonly Dictionary<GamepadButtonInputId, InputSummary> _stats;

            public JoystickButtonDetector()
            {
                _stats = new Dictionary<GamepadButtonInputId, InputSummary>();
            }

            public bool HasAnyButtonPressed()
            {
                return _stats.Values.Any(CheckButtonPressed);
            }

            public IEnumerable<GamepadButtonInputId> GetPressedButtons()
            {
                return _stats.Where(kvp => CheckButtonPressed(kvp.Value)).Select(kvp => kvp.Key);
            }

            public void AddInput(GamepadButtonInputId button, float value)
            {

                if (!_stats.TryGetValue(button, out InputSummary inputSummary))
                {
                    inputSummary = new InputSummary();
                    _stats.Add(button, inputSummary);
                }

                inputSummary.AddInput(value);
            }

            public override string ToString()
            {
                StringWriter writer = new();

                foreach (var kvp in _stats)
                {
                    writer.WriteLine($"Button {kvp.Key} -> {kvp.Value}");
                }

                return writer.ToString();
            }

            private bool CheckButtonPressed(InputSummary sequence)
            {
                float distance = Math.Abs(sequence.Min - sequence.Avg) + Math.Abs(sequence.Max - sequence.Avg);
                return distance > 1.5; // distance range [0, 2]
            }
        }

        private class InputSummary
        {
            public float Min, Max, Sum, Avg;

            public int NumSamples;

            public InputSummary()
            {
                Min = float.MaxValue;
                Max = float.MinValue;
                Sum = 0;
                NumSamples = 0;
                Avg = 0;
            }

            public void AddInput(float value)
            {
                Min = Math.Min(Min, value);
                Max = Math.Max(Max, value);
                Sum += value;
                NumSamples += 1;
                Avg = Sum / NumSamples;
            }

            public override string ToString()
            {
                return $"Avg: {Avg} Min: {Min} Max: {Max} Sum: {Sum} NumSamples: {NumSamples}";
            }
        }
    }
}
