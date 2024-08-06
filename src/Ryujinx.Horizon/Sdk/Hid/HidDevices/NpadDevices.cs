using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using Ryujinx.Horizon.Sdk.Hid.SixAxis;
using Ryujinx.Horizon.Sdk.Hid.Vibration;
using Ryujinx.Horizon.Sdk.OsTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Ryujinx.Horizon.Sdk.Hid.HidDevices
{
    public class NpadDevices : BaseDevice
    {
        private const int NoMatchNotifyFrequencyMs = 2000;
        private int _activeCount;
        private long _lastNotifyTimestamp;

        public const int MaxControllers = 9; // Players 1-8 and Handheld
        private NpadStyleTag[] _configuredTypes;
        private readonly Event[] _styleSetUpdateEvents;
        private readonly bool[] _supportedPlayers;
        private VibrationValue _neutralVibrationValue = new()
        {
            AmplitudeLow = 0f,
            FrequencyLow = 160f,
            AmplitudeHigh = 0f,
            FrequencyHigh = 320f,
        };

        internal NpadJoyHoldType JoyHold { get; set; }
        internal bool SixAxisActive = false; // TODO: link to hidserver when implemented
        internal NpadStyleTag SupportedStyleSets { get; set; }

        public Dictionary<NpadIdType, ConcurrentQueue<(VibrationValue, VibrationValue)>> RumbleQueues = new();
        public Dictionary<NpadIdType, (VibrationValue, VibrationValue)> LastVibrationValues = new();

        public NpadDevices(bool active = true) : base(active)
        {
            _configuredTypes = new NpadStyleTag[MaxControllers];

            SupportedStyleSets = NpadStyleTag.Handheld | NpadStyleTag.JoyDual |
                                 NpadStyleTag.JoyLeft | NpadStyleTag.JoyRight |
                                 NpadStyleTag.FullKey;

            _supportedPlayers = new bool[MaxControllers];
            _supportedPlayers.AsSpan().Fill(true);

            _styleSetUpdateEvents = new Event[MaxControllers];
            for (int i = 0; i < _styleSetUpdateEvents.Length; ++i)
            {
                _styleSetUpdateEvents[i] = new Event(_device.System.KernelContext);
            }

            _activeCount = 0;

            JoyHold = NpadJoyHoldType.Vertical;
        }

        internal ref Event GetStyleSetUpdateEvent(PlayerIndex player)
        {
            return ref _styleSetUpdateEvents[(int)player];
        }

        internal void ClearSupportedPlayers()
        {
            _supportedPlayers.AsSpan().Clear();
        }

        internal void SetSupportedPlayer(PlayerIndex player, bool supported = true)
        {
            if ((uint)player >= _supportedPlayers.Length)
            {
                return;
            }

            _supportedPlayers[(int)player] = supported;
        }

        internal IEnumerable<PlayerIndex> GetSupportedPlayers()
        {
            for (int i = 0; i < _supportedPlayers.Length; ++i)
            {
                if (_supportedPlayers[i])
                {
                    yield return (PlayerIndex)i;
                }
            }
        }

        public bool Validate(int playerMin, int playerMax, NpadStyleTag acceptedTypes, out int configuredCount, out PlayerIndex primaryIndex)
        {
            primaryIndex = PlayerIndex.Unknown;
            configuredCount = 0;

            for (int i = 0; i < MaxControllers; ++i)
            {
                NpadStyleTag npad = _configuredTypes[i];

                if (npad == NpadStyleTag.Handheld && _device.System.State.DockedMode)
                {
                    continue;
                }

                NpadStyleTag currentType = (NpadStyleTag)_device.Hid.SharedMemory.Npads[i].InternalState.StyleSet;

                if (currentType != NpadStyleTag.None && (npad & acceptedTypes) != 0 && _supportedPlayers[i])
                {
                    configuredCount++;
                    if (primaryIndex == PlayerIndex.Unknown)
                    {
                        primaryIndex = (PlayerIndex)i;
                    }
                }
            }

            if (configuredCount < playerMin || configuredCount > playerMax || primaryIndex == PlayerIndex.Unknown)
            {
                return false;
            }

            return true;
        }

        public void Configure(params ControllerConfig[] configs)
        {
            _configuredTypes = new NpadStyleTag[MaxControllers];

            for (int i = 0; i < configs.Length; ++i)
            {
                PlayerIndex player = configs[i].Player;
                NpadStyleTag controllerType = configs[i].Type;

                if (player > PlayerIndex.Handheld)
                {
                    throw new InvalidOperationException("Player must be Player1-8 or Handheld");
                }

                if (controllerType == NpadStyleTag.Handheld)
                {
                    player = PlayerIndex.Handheld;
                }

                _configuredTypes[(int)player] = controllerType;

                Logger.Info?.Print(LogClass.Hid, $"Configured Controller {controllerType} to {player}");
            }
        }

        public void Update(IList<GamepadInput> states)
        {
            Remap();

            Span<bool> updated = stackalloc bool[10];

            // Update configured inputs
            for (int i = 0; i < states.Count; ++i)
            {
                GamepadInput state = states[i];

                updated[(int)state.PlayerId] = true;

                UpdateInput(state);
            }

            for (int i = 0; i < updated.Length; i++)
            {
                if (!updated[i])
                {
                    UpdateDisconnectedInput((PlayerIndex)i);
                }
            }
        }

        private void Remap()
        {
            // Remap/Init if necessary
            for (int i = 0; i < MaxControllers; ++i)
            {
                NpadStyleTag config = _configuredTypes[i];

                // Remove Handheld config when Docked
                if (config == NpadStyleTag.Handheld && _device.System.State.DockedMode)
                {
                    config = NpadStyleTag.None;
                }

                // Auto-remap ProController and JoyconPair
                if (config == NpadStyleTag.JoyDual && (SupportedStyleSets & NpadStyleTag.JoyDual) == 0 && (SupportedStyleSets & NpadStyleTag.FullKey) != 0)
                {
                    config = NpadStyleTag.FullKey;
                }
                else if (config == NpadStyleTag.FullKey && (SupportedStyleSets & NpadStyleTag.FullKey) == 0 && (SupportedStyleSets & NpadStyleTag.JoyDual) != 0)
                {
                    config = NpadStyleTag.JoyDual;
                }

                // Check StyleSet and PlayerSet
                if ((config & SupportedStyleSets) == 0 || !_supportedPlayers[i])
                {
                    config = NpadStyleTag.None;
                }

                SetupNpad((PlayerIndex)i, config);
            }

            if (_activeCount == 0 && PerformanceCounter.ElapsedMilliseconds > _lastNotifyTimestamp + NoMatchNotifyFrequencyMs)
            {
                Logger.Warning?.Print(LogClass.Hid, $"No matching controllers found. Application requests '{SupportedStyleSets}' on '{string.Join(", ", GetSupportedPlayers())}'");
                _lastNotifyTimestamp = PerformanceCounter.ElapsedMilliseconds;
            }
        }

        private void SetupNpad(PlayerIndex player, NpadStyleTag type)
        {
            ref NpadInternalState controller = ref _device.Hid.SharedMemory.Npads[(int)player].InternalState;

            NpadStyleTag oldType = controller.StyleSet;

            if (oldType == type)
            {
                return; // Already configured
            }

            controller = NpadInternalState.Create(); // Reset it

            if (type == NpadStyleTag.None)
            {
                _styleSetUpdateEvents[(int)player].ReadableEvent.Signal(); // Signal disconnect
                _activeCount--;

                Logger.Info?.Print(LogClass.Hid, $"Disconnected Controller {oldType} from {player}");

                return;
            }

            // TODO: Allow customizing colors at config
            controller.JoyAssignmentMode = NpadJoyAssignmentMode.Dual;
            controller.FullKeyColor.FullKeyBody = (uint)NpadColor.BodyGray;
            controller.FullKeyColor.FullKeyButtons = (uint)NpadColor.ButtonGray;
            controller.JoyColor.LeftBody = (uint)NpadColor.BodyNeonBlue;
            controller.JoyColor.LeftButtons = (uint)NpadColor.ButtonGray;
            controller.JoyColor.RightBody = (uint)NpadColor.BodyNeonRed;
            controller.JoyColor.RightButtons = (uint)NpadColor.ButtonGray;

            controller.SystemProperties = NpadSystemProperties.IsPoweredJoyDual |
                                          NpadSystemProperties.IsPoweredJoyLeft |
                                          NpadSystemProperties.IsPoweredJoyRight;

            controller.BatteryLevelJoyDual = NpadBatteryLevel.Percent100;
            controller.BatteryLevelJoyLeft = NpadBatteryLevel.Percent100;
            controller.BatteryLevelJoyRight = NpadBatteryLevel.Percent100;

            switch (type)
            {
#pragma warning disable IDE0055 // Disable formatting
                case NpadStyleTag.FullKey:
                    controller.StyleSet           = NpadStyleTag.FullKey;
                    controller.DeviceType         = DeviceType.FullKey;
                    controller.SystemProperties  |= NpadSystemProperties.IsAbxyButtonOriented |
                                                    NpadSystemProperties.IsPlusAvailable      |
                                                    NpadSystemProperties.IsMinusAvailable;
                    controller.AppletFooterUiType = AppletFooterUiType.SwitchProController;
                    break;
                case NpadStyleTag.Handheld:
                    controller.StyleSet           = NpadStyleTag.Handheld;
                    controller.DeviceType         = DeviceType.HandheldLeft |
                                                    DeviceType.HandheldRight;
                    controller.SystemProperties  |= NpadSystemProperties.IsAbxyButtonOriented |
                                                    NpadSystemProperties.IsPlusAvailable      |
                                                    NpadSystemProperties.IsMinusAvailable;
                    controller.AppletFooterUiType = AppletFooterUiType.HandheldJoyConLeftJoyConRight;
                    break;
                case NpadStyleTag.JoyDual:
                    controller.StyleSet           = NpadStyleTag.JoyDual;
                    controller.DeviceType         = DeviceType.JoyLeft |
                                                    DeviceType.JoyRight;
                    controller.SystemProperties  |= NpadSystemProperties.IsAbxyButtonOriented |
                                                    NpadSystemProperties.IsPlusAvailable      |
                                                    NpadSystemProperties.IsMinusAvailable;
                    controller.AppletFooterUiType = _device.System.State.DockedMode ? AppletFooterUiType.JoyDual : AppletFooterUiType.HandheldJoyConLeftJoyConRight;
                    break;
                case NpadStyleTag.JoyLeft:
                    controller.StyleSet           = NpadStyleTag.JoyLeft;
                    controller.JoyAssignmentMode  = NpadJoyAssignmentMode.Single;
                    controller.DeviceType         = DeviceType.JoyLeft;
                    controller.SystemProperties  |= NpadSystemProperties.IsSlSrButtonOriented |
                                                    NpadSystemProperties.IsMinusAvailable;
                    controller.AppletFooterUiType = _device.System.State.DockedMode ? AppletFooterUiType.JoyDualLeftOnly : AppletFooterUiType.HandheldJoyConLeftOnly;
                    break;
                case NpadStyleTag.JoyRight:
                    controller.StyleSet           = NpadStyleTag.JoyRight;
                    controller.JoyAssignmentMode  = NpadJoyAssignmentMode.Single;
                    controller.DeviceType         = DeviceType.JoyRight;
                    controller.SystemProperties  |= NpadSystemProperties.IsSlSrButtonOriented |
                                                    NpadSystemProperties.IsPlusAvailable;
                    controller.AppletFooterUiType = _device.System.State.DockedMode ? AppletFooterUiType.JoyDualRightOnly : AppletFooterUiType.HandheldJoyConRightOnly;
                    break;
                case NpadStyleTag.Palma:
                    controller.StyleSet           = NpadStyleTag.Palma;
                    controller.DeviceType         = DeviceType.Palma;
                    controller.AppletFooterUiType = AppletFooterUiType.None;
                    break;
#pragma warning restore IDE0055
            }

            _styleSetUpdateEvents[(int)player].ReadableEvent.Signal();
            _activeCount++;

            Logger.Info?.Print(LogClass.Hid, $"Connected Controller {type} to {player}");
        }

        private ref RingLifo<NpadCommonState> GetCommonStateLifo(ref NpadInternalState npad)
        {
            switch (npad.StyleSet)
            {
                case NpadStyleTag.FullKey:
                    return ref npad.FullKey;
                case NpadStyleTag.Handheld:
                    return ref npad.Handheld;
                case NpadStyleTag.JoyDual:
                    return ref npad.JoyDual;
                case NpadStyleTag.JoyLeft:
                    return ref npad.JoyLeft;
                case NpadStyleTag.JoyRight:
                    return ref npad.JoyRight;
                case NpadStyleTag.Palma:
                    return ref npad.Palma;
                default:
                    return ref npad.SystemExt;
            }
        }

        private void UpdateUnusedInputIfNotEqual(ref RingLifo<NpadCommonState> currentlyUsed, ref RingLifo<NpadCommonState> possiblyUnused)
        {
            if (!Unsafe.AreSame(ref currentlyUsed, ref possiblyUnused))
            {
                NpadCommonState newState = new();

                WriteNewInputEntry(ref possiblyUnused, ref newState);
            }
        }

        private void WriteNewInputEntry(ref RingLifo<NpadCommonState> lifo, ref NpadCommonState state)
        {
            ref NpadCommonState previousEntry = ref lifo.GetCurrentEntryRef();

            state.SamplingNumber = previousEntry.SamplingNumber + 1;

            lifo.Write(ref state);
        }

        private void UpdateUnusedSixInputIfNotEqual(ref RingLifo<SixAxisSensorState> currentlyUsed, ref RingLifo<SixAxisSensorState> possiblyUnused)
        {
            if (!Unsafe.AreSame(ref currentlyUsed, ref possiblyUnused))
            {
                SixAxisSensorState newState = new();

                WriteNewSixInputEntry(ref possiblyUnused, ref newState);
            }
        }

        private void WriteNewSixInputEntry(ref RingLifo<SixAxisSensorState> lifo, ref SixAxisSensorState state)
        {
            ref SixAxisSensorState previousEntry = ref lifo.GetCurrentEntryRef();

            state.SamplingNumber = previousEntry.SamplingNumber + 1;

            lifo.Write(ref state);
        }

        private void UpdateInput(GamepadInput state)
        {
            if (state.PlayerId == PlayerIndex.Unknown)
            {
                return;
            }

            ref NpadInternalState currentNpad = ref _device.Hid.SharedMemory.Npads[(int)state.PlayerId].InternalState;

            if (currentNpad.StyleSet == NpadStyleTag.None)
            {
                return;
            }

            ref RingLifo<NpadCommonState> lifo = ref GetCommonStateLifo(ref currentNpad);

            NpadCommonState newState = new()
            {
                Buttons = (NpadButton)state.Buttons,
                AnalogStickL = new AnalogStickState
                {
                    X = state.LStick.Dx,
                    Y = state.LStick.Dy,
                },
                AnalogStickR = new AnalogStickState
                {
                    X = state.RStick.Dx,
                    Y = state.RStick.Dy,
                },
                Attributes = NpadAttribute.IsConnected,
            };

            switch (currentNpad.StyleSet)
            {
                case NpadStyleTag.Handheld:
                case NpadStyleTag.FullKey:
                    newState.Attributes |= NpadAttribute.IsWired;
                    break;
                case NpadStyleTag.JoyDual:
                    newState.Attributes |= NpadAttribute.IsLeftConnected |
                                           NpadAttribute.IsRightConnected;
                    break;
                case NpadStyleTag.JoyLeft:
                    newState.Attributes |= NpadAttribute.IsLeftConnected;
                    break;
                case NpadStyleTag.JoyRight:
                    newState.Attributes |= NpadAttribute.IsRightConnected;
                    break;
            }

            WriteNewInputEntry(ref lifo, ref newState);

            // Mirror data to Default layout just in case
            if (!currentNpad.StyleSet.HasFlag(NpadStyleTag.SystemExt))
            {
                WriteNewInputEntry(ref currentNpad.SystemExt, ref newState);
            }

            UpdateUnusedInputIfNotEqual(ref lifo, ref currentNpad.FullKey);
            UpdateUnusedInputIfNotEqual(ref lifo, ref currentNpad.Handheld);
            UpdateUnusedInputIfNotEqual(ref lifo, ref currentNpad.JoyDual);
            UpdateUnusedInputIfNotEqual(ref lifo, ref currentNpad.JoyLeft);
            UpdateUnusedInputIfNotEqual(ref lifo, ref currentNpad.JoyRight);
            UpdateUnusedInputIfNotEqual(ref lifo, ref currentNpad.Palma);
        }

        private void UpdateDisconnectedInput(PlayerIndex index)
        {
            ref NpadInternalState currentNpad = ref _device.Hid.SharedMemory.Npads[(int)index].InternalState;

            NpadCommonState newState = new();

            WriteNewInputEntry(ref currentNpad.FullKey, ref newState);
            WriteNewInputEntry(ref currentNpad.Handheld, ref newState);
            WriteNewInputEntry(ref currentNpad.JoyDual, ref newState);
            WriteNewInputEntry(ref currentNpad.JoyLeft, ref newState);
            WriteNewInputEntry(ref currentNpad.JoyRight, ref newState);
            WriteNewInputEntry(ref currentNpad.Palma, ref newState);
        }

        public void UpdateSixAxis(IList<SixAxisInput> states)
        {
            Span<bool> updated = stackalloc bool[10];

            for (int i = 0; i < states.Count; ++i)
            {
                updated[(int)states[i].PlayerId] = true;

                if (SetSixAxisState(states[i]))
                {
                    i++;

                    if (i >= states.Count)
                    {
                        return;
                    }

                    SetSixAxisState(states[i], true);
                }
            }

            for (int i = 0; i < updated.Length; i++)
            {
                if (!updated[i])
                {
                    UpdateDisconnectedInputSixAxis((PlayerIndex)i);
                }
            }
        }

        private ref RingLifo<SixAxisSensorState> GetSixAxisSensorLifo(ref NpadInternalState npad, bool isRightPair)
        {
            switch (npad.StyleSet)
            {
                case NpadStyleTag.FullKey:
                    return ref npad.FullKeySixAxisSensor;
                case NpadStyleTag.Handheld:
                    return ref npad.HandheldSixAxisSensor;
                case NpadStyleTag.JoyDual:
                    if (isRightPair)
                    {
                        return ref npad.JoyDualRightSixAxisSensor;
                    }
                    else
                    {
                        return ref npad.JoyDualSixAxisSensor;
                    }
                case NpadStyleTag.JoyLeft:
                    return ref npad.JoyLeftSixAxisSensor;
                case NpadStyleTag.JoyRight:
                    return ref npad.JoyRightSixAxisSensor;
                default:
                    throw new NotImplementedException($"{npad.StyleSet}");
            }
        }

        private bool SetSixAxisState(SixAxisInput state, bool isRightPair = false)
        {
            if (state.PlayerId == PlayerIndex.Unknown)
            {
                return false;
            }

            ref NpadInternalState currentNpad = ref _device.Hid.SharedMemory.Npads[(int)state.PlayerId].InternalState;

            if (currentNpad.StyleSet == NpadStyleTag.None)
            {
                return false;
            }

            HidVector accel = new()
            {
                X = state.Accelerometer.X,
                Y = state.Accelerometer.Y,
                Z = state.Accelerometer.Z,
            };

            HidVector gyro = new()
            {
                X = state.Gyroscope.X,
                Y = state.Gyroscope.Y,
                Z = state.Gyroscope.Z,
            };

            HidVector rotation = new()
            {
                X = state.Rotation.X,
                Y = state.Rotation.Y,
                Z = state.Rotation.Z,
            };

            SixAxisSensorState newState = new()
            {
                Acceleration = accel,
                AngularVelocity = gyro,
                Angle = rotation,
                Attributes = SixAxisSensorAttribute.IsConnected,
            };

            state.Orientation.AsSpan().CopyTo(newState.Direction.AsSpan());

            ref RingLifo<SixAxisSensorState> lifo = ref GetSixAxisSensorLifo(ref currentNpad, isRightPair);

            WriteNewSixInputEntry(ref lifo, ref newState);

            bool needUpdateRight = currentNpad.StyleSet == NpadStyleTag.JoyDual && !isRightPair;

            if (!isRightPair)
            {
                UpdateUnusedSixInputIfNotEqual(ref lifo, ref currentNpad.FullKeySixAxisSensor);
                UpdateUnusedSixInputIfNotEqual(ref lifo, ref currentNpad.HandheldSixAxisSensor);
                UpdateUnusedSixInputIfNotEqual(ref lifo, ref currentNpad.JoyDualSixAxisSensor);
                UpdateUnusedSixInputIfNotEqual(ref lifo, ref currentNpad.JoyLeftSixAxisSensor);
                UpdateUnusedSixInputIfNotEqual(ref lifo, ref currentNpad.JoyRightSixAxisSensor);
            }

            if (!needUpdateRight && !isRightPair)
            {
                SixAxisSensorState emptyState = new()
                {
                    Attributes = SixAxisSensorAttribute.IsConnected,
                };

                WriteNewSixInputEntry(ref currentNpad.JoyDualRightSixAxisSensor, ref emptyState);
            }

            return needUpdateRight;
        }

        private void UpdateDisconnectedInputSixAxis(PlayerIndex index)
        {
            ref NpadInternalState currentNpad = ref _device.Hid.SharedMemory.Npads[(int)index].InternalState;

            SixAxisSensorState newState = new()
            {
                Attributes = SixAxisSensorAttribute.IsConnected,
            };

            WriteNewSixInputEntry(ref currentNpad.FullKeySixAxisSensor, ref newState);
            WriteNewSixInputEntry(ref currentNpad.HandheldSixAxisSensor, ref newState);
            WriteNewSixInputEntry(ref currentNpad.JoyDualSixAxisSensor, ref newState);
            WriteNewSixInputEntry(ref currentNpad.JoyDualRightSixAxisSensor, ref newState);
            WriteNewSixInputEntry(ref currentNpad.JoyLeftSixAxisSensor, ref newState);
            WriteNewSixInputEntry(ref currentNpad.JoyRightSixAxisSensor, ref newState);
        }

        public void UpdateRumbleQueue(NpadIdType index, Dictionary<byte, VibrationValue> dualVibrationValues)
        {
            if (RumbleQueues.TryGetValue(index, out ConcurrentQueue<(VibrationValue, VibrationValue)> currentQueue))
            {
                if (!dualVibrationValues.TryGetValue(0, out VibrationValue leftVibrationValue))
                {
                    leftVibrationValue = _neutralVibrationValue;
                }

                if (!dualVibrationValues.TryGetValue(1, out VibrationValue rightVibrationValue))
                {
                    rightVibrationValue = _neutralVibrationValue;
                }

                if (!LastVibrationValues.TryGetValue(index, out (VibrationValue, VibrationValue) dualVibrationValue) || !leftVibrationValue.Equals(dualVibrationValue.Item1) || !rightVibrationValue.Equals(dualVibrationValue.Item2))
                {
                    currentQueue.Enqueue((leftVibrationValue, rightVibrationValue));

                    LastVibrationValues[index] = (leftVibrationValue, rightVibrationValue);
                }
            }
        }

        public VibrationValue GetLastVibrationValue(NpadIdType index, byte position)
        {
            if (!LastVibrationValues.TryGetValue(index, out (VibrationValue, VibrationValue) dualVibrationValue))
            {
                return _neutralVibrationValue;
            }

            return (position == 0) ? dualVibrationValue.Item1 : dualVibrationValue.Item2;
        }

        public ConcurrentQueue<(VibrationValue, VibrationValue)> GetRumbleQueue(NpadIdType index)
        {
            if (!RumbleQueues.TryGetValue(index, out ConcurrentQueue<(VibrationValue, VibrationValue)> rumbleQueue))
            {
                rumbleQueue = new ConcurrentQueue<(VibrationValue, VibrationValue)>();
                _device.Hid.Npads.RumbleQueues[index] = rumbleQueue;
            }

            return rumbleQueue;
        }
    }
}
