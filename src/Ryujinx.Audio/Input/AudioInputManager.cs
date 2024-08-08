using Ryujinx.Audio.Common;
using Ryujinx.Audio.Integration;
using Ryujinx.Common.Logging;
using Ryujinx.Memory;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Ryujinx.Audio.Input
{
    /// <summary>
    /// The audio input manager.
    /// </summary>
    public class AudioInputManager : IDisposable
    {
        private readonly Lock _lock = new();

        /// <summary>
        /// Lock used for session allocation.
        /// </summary>
        private readonly Lock _sessionLock = new();

        /// <summary>
        /// The session ids allocation table.
        /// </summary>
        private readonly int[] _sessionIds;

        /// <summary>
        /// The device driver.
        /// </summary>
        private IHardwareDeviceDriver _deviceDriver;

        /// <summary>
        /// The events linked to each session.
        /// </summary>
        private IWritableEvent[] _sessionsBufferEvents;

        /// <summary>
        /// The <see cref="AudioInputSystem"/> session instances.
        /// </summary>
        private readonly AudioInputSystem[] _sessions;

        /// <summary>
        /// The count of active sessions.
        /// </summary>
        private int _activeSessionCount;

        /// <summary>
        /// The dispose state.
        /// </summary>
        private int _disposeState;

        /// <summary>
        /// Create a new <see cref="AudioInputManager"/>.
        /// </summary>
        public AudioInputManager()
        {
            _sessionIds = new int[Constants.AudioInSessionCountMax];
            _sessions = new AudioInputSystem[Constants.AudioInSessionCountMax];
            _activeSessionCount = 0;

            for (int i = 0; i < _sessionIds.Length; i++)
            {
                _sessionIds[i] = i;
            }
        }

        /// <summary>
        /// Initialize the <see cref="AudioInputManager"/>.
        /// </summary>
        /// <param name="deviceDriver">The device driver.</param>
        /// <param name="sessionRegisterEvents">The events associated to each session.</param>
        public void Initialize(IHardwareDeviceDriver deviceDriver, IWritableEvent[] sessionRegisterEvents)
        {
            _deviceDriver = deviceDriver;
            _sessionsBufferEvents = sessionRegisterEvents;
        }

        /// <summary>
        /// Acquire a new session id.
        /// </summary>
        /// <returns>A new session id.</returns>
        private int AcquireSessionId()
        {
            lock (_sessionLock)
            {
                int index = _activeSessionCount;

                Debug.Assert(index < _sessionIds.Length);

                int sessionId = _sessionIds[index];

                _sessionIds[index] = -1;

                _activeSessionCount++;

                Logger.Info?.Print(LogClass.AudioRenderer, $"Registered new input ({sessionId})");

                return sessionId;
            }
        }

        /// <summary>
        /// Release a given <paramref name="sessionId"/>.
        /// </summary>
        /// <param name="sessionId">The session id to release.</param>
        private void ReleaseSessionId(int sessionId)
        {
            lock (_sessionLock)
            {
                Debug.Assert(_activeSessionCount > 0);

                int newIndex = --_activeSessionCount;

                _sessionIds[newIndex] = sessionId;
            }

            Logger.Info?.Print(LogClass.AudioRenderer, $"Unregistered input ({sessionId})");
        }

        /// <summary>
        /// Used to update audio input system.
        /// </summary>
        public void Update()
        {
            lock (_sessionLock)
            {
                foreach (AudioInputSystem input in _sessions)
                {
                    input?.Update();
                }
            }
        }

        /// <summary>
        /// Register a new <see cref="AudioInputSystem"/>.
        /// </summary>
        /// <param name="input">The <see cref="AudioInputSystem"/> to register.</param>
        private void Register(AudioInputSystem input)
        {
            lock (_sessionLock)
            {
                _sessions[input.GetSessionId()] = input;
            }
        }

        /// <summary>
        /// Unregister a new <see cref="AudioInputSystem"/>.
        /// </summary>
        /// <param name="input">The <see cref="AudioInputSystem"/> to unregister.</param>
        internal void Unregister(AudioInputSystem input)
        {
            lock (_sessionLock)
            {
                int sessionId = input.GetSessionId();

                _sessions[input.GetSessionId()] = null;

                ReleaseSessionId(sessionId);
            }
        }

        /// <summary>
        /// Get the list of all audio inputs names.
        /// </summary>
        /// <param name="filtered">If true, filter disconnected devices</param>
        /// <returns>The list of all audio inputs name</returns>
        public string[] ListAudioIns(bool filtered)
        {
            if (filtered)
            {
                // TODO: Detect if the driver supports audio input
            }

            return new[] { Constants.DefaultDeviceInputName };
        }

        /// <summary>
        /// Open a new <see cref="AudioInputSystem"/>.
        /// </summary>
        /// <param name="outputDeviceName">The output device name selected by the <see cref="AudioInputSystem"/></param>
        /// <param name="outputConfiguration">The output audio configuration selected by the <see cref="AudioInputSystem"/></param>
        /// <param name="obj">The new <see cref="AudioInputSystem"/></param>
        /// <param name="memoryManager">The memory manager that will be used for all guest memory operations</param>
        /// <param name="inputDeviceName">The input device name wanted by the user</param>
        /// <param name="sampleFormat">The sample format to use</param>
        /// <param name="parameter">The user configuration</param>
        /// <returns>A <see cref="ResultCode"/> reporting an error or a success</returns>
        public ResultCode OpenAudioIn(out string outputDeviceName,
                                      out AudioOutputConfiguration outputConfiguration,
                                      out AudioInputSystem obj,
                                      IVirtualMemoryManager memoryManager,
                                      string inputDeviceName,
                                      SampleFormat sampleFormat,
                                      ref AudioInputConfiguration parameter)
        {
            int sessionId = AcquireSessionId();

            _sessionsBufferEvents[sessionId].Clear();

            IHardwareDeviceSession deviceSession = _deviceDriver.OpenDeviceSession(IHardwareDeviceDriver.Direction.Input, memoryManager, sampleFormat, parameter.SampleRate, parameter.ChannelCount);

            AudioInputSystem audioIn = new(this, _lock, deviceSession, _sessionsBufferEvents[sessionId]);

            ResultCode result = audioIn.Initialize(inputDeviceName, sampleFormat, ref parameter, sessionId);

            if (result == ResultCode.Success)
            {
                outputDeviceName = audioIn.DeviceName;
                outputConfiguration = new AudioOutputConfiguration
                {
                    ChannelCount = audioIn.ChannelCount,
                    SampleFormat = audioIn.SampleFormat,
                    SampleRate = audioIn.SampleRate,
                    AudioOutState = audioIn.GetState(),
                };

                obj = audioIn;

                Register(audioIn);
            }
            else
            {
                ReleaseSessionId(sessionId);

                obj = null;
                outputDeviceName = null;
                outputConfiguration = default;
            }

            return result;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (Interlocked.CompareExchange(ref _disposeState, 1, 0) == 0)
            {
                Dispose(true);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Clone the sessions array to dispose them outside the lock.
                AudioInputSystem[] sessions;

                lock (_sessionLock)
                {
                    sessions = _sessions.ToArray();
                }

                foreach (AudioInputSystem input in sessions)
                {
                    input?.Dispose();
                }
            }
        }
    }
}
