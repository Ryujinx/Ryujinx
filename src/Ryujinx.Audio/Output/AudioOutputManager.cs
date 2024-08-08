using Ryujinx.Audio.Common;
using Ryujinx.Audio.Integration;
using Ryujinx.Common.Logging;
using Ryujinx.Memory;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Ryujinx.Audio.Output
{
    /// <summary>
    /// The audio output manager.
    /// </summary>
    public class AudioOutputManager : IDisposable
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
        /// The <see cref="AudioOutputSystem"/> session instances.
        /// </summary>
        private readonly AudioOutputSystem[] _sessions;

        /// <summary>
        /// The count of active sessions.
        /// </summary>
        private int _activeSessionCount;

        /// <summary>
        /// The dispose state.
        /// </summary>
        private int _disposeState;

        /// <summary>
        /// Create a new <see cref="AudioOutputManager"/>.
        /// </summary>
        public AudioOutputManager()
        {
            _sessionIds = new int[Constants.AudioOutSessionCountMax];
            _sessions = new AudioOutputSystem[Constants.AudioOutSessionCountMax];
            _activeSessionCount = 0;

            for (int i = 0; i < _sessionIds.Length; i++)
            {
                _sessionIds[i] = i;
            }
        }

        /// <summary>
        /// Initialize the <see cref="AudioOutputManager"/>.
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

                Logger.Info?.Print(LogClass.AudioRenderer, $"Registered new output ({sessionId})");

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

            Logger.Info?.Print(LogClass.AudioRenderer, $"Unregistered output ({sessionId})");
        }

        /// <summary>
        /// Used to update audio output system.
        /// </summary>
        public void Update()
        {
            lock (_sessionLock)
            {
                foreach (AudioOutputSystem output in _sessions)
                {
                    output?.Update();
                }
            }
        }

        /// <summary>
        /// Register a new <see cref="AudioOutputSystem"/>.
        /// </summary>
        /// <param name="output">The <see cref="AudioOutputSystem"/> to register.</param>
        private void Register(AudioOutputSystem output)
        {
            lock (_sessionLock)
            {
                _sessions[output.GetSessionId()] = output;
            }
        }

        /// <summary>
        /// Unregister a new <see cref="AudioOutputSystem"/>.
        /// </summary>
        /// <param name="output">The <see cref="AudioOutputSystem"/> to unregister.</param>
        internal void Unregister(AudioOutputSystem output)
        {
            lock (_sessionLock)
            {
                int sessionId = output.GetSessionId();

                _sessions[output.GetSessionId()] = null;

                ReleaseSessionId(sessionId);
            }
        }

        /// <summary>
        /// Get the list of all audio outputs name.
        /// </summary>
        /// <returns>The list of all audio outputs name</returns>
        public string[] ListAudioOuts()
        {
            return new[] { Constants.DefaultDeviceOutputName };
        }

        /// <summary>
        /// Open a new <see cref="AudioOutputSystem"/>.
        /// </summary>
        /// <param name="outputDeviceName">The output device name selected by the <see cref="AudioOutputSystem"/></param>
        /// <param name="outputConfiguration">The output audio configuration selected by the <see cref="AudioOutputSystem"/></param>
        /// <param name="obj">The new <see cref="AudioOutputSystem"/></param>
        /// <param name="memoryManager">The memory manager that will be used for all guest memory operations</param>
        /// <param name="inputDeviceName">The input device name wanted by the user</param>
        /// <param name="sampleFormat">The sample format to use</param>
        /// <param name="parameter">The user configuration</param>
        /// <returns>A <see cref="ResultCode"/> reporting an error or a success</returns>
        public ResultCode OpenAudioOut(out string outputDeviceName,
                                       out AudioOutputConfiguration outputConfiguration,
                                       out AudioOutputSystem obj,
                                       IVirtualMemoryManager memoryManager,
                                       string inputDeviceName,
                                       SampleFormat sampleFormat,
                                       ref AudioInputConfiguration parameter)
        {
            int sessionId = AcquireSessionId();

            _sessionsBufferEvents[sessionId].Clear();

            IHardwareDeviceSession deviceSession = _deviceDriver.OpenDeviceSession(IHardwareDeviceDriver.Direction.Output, memoryManager, sampleFormat, parameter.SampleRate, parameter.ChannelCount);

            AudioOutputSystem audioOut = new(this, _lock, deviceSession, _sessionsBufferEvents[sessionId]);

            ResultCode result = audioOut.Initialize(inputDeviceName, sampleFormat, ref parameter, sessionId);

            if (result == ResultCode.Success)
            {
                outputDeviceName = audioOut.DeviceName;
                outputConfiguration = new AudioOutputConfiguration
                {
                    ChannelCount = audioOut.ChannelCount,
                    SampleFormat = audioOut.SampleFormat,
                    SampleRate = audioOut.SampleRate,
                    AudioOutState = audioOut.GetState(),
                };

                obj = audioOut;

                Register(audioOut);
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
                AudioOutputSystem[] sessions;

                lock (_sessionLock)
                {
                    sessions = _sessions.ToArray();
                }

                foreach (AudioOutputSystem output in sessions)
                {
                    output?.Dispose();
                }
            }
        }
    }
}
