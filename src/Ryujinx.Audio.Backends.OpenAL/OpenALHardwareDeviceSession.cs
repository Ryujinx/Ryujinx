using OpenTK.Audio.OpenAL;
using Ryujinx.Audio.Backends.Common;
using Ryujinx.Audio.Common;
using Ryujinx.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Ryujinx.Audio.Backends.OpenAL
{
    class OpenALHardwareDeviceSession : HardwareDeviceSessionOutputBase
    {
        private readonly OpenALHardwareDeviceDriver _driver;
        private readonly int _sourceId;
        private readonly ALFormat _targetFormat;
        private bool _isActive;
        private readonly Queue<OpenALAudioBuffer> _queuedBuffers;
        private ulong _playedSampleCount;
        private float _volume;

        private readonly Lock _lock = new();

        public OpenALHardwareDeviceSession(OpenALHardwareDeviceDriver driver, IVirtualMemoryManager memoryManager, SampleFormat requestedSampleFormat, uint requestedSampleRate, uint requestedChannelCount) : base(memoryManager, requestedSampleFormat, requestedSampleRate, requestedChannelCount)
        {
            _driver = driver;
            _queuedBuffers = new Queue<OpenALAudioBuffer>();
            _sourceId = AL.GenSource();
            _targetFormat = GetALFormat();
            _isActive = false;
            _playedSampleCount = 0;
            SetVolume(1f);
        }

        private ALFormat GetALFormat()
        {
            return RequestedSampleFormat switch
            {
                SampleFormat.PcmInt16 => RequestedChannelCount switch
                {
                    1 => ALFormat.Mono16,
                    2 => ALFormat.Stereo16,
                    6 => ALFormat.Multi51Chn16Ext,
                    _ => throw new NotImplementedException($"Unsupported channel config {RequestedChannelCount}"),
                },
                _ => throw new NotImplementedException($"Unsupported sample format {RequestedSampleFormat}"),
            };
        }

        public override void PrepareToClose() { }

        private void StartIfNotPlaying()
        {
            AL.GetSource(_sourceId, ALGetSourcei.SourceState, out int stateInt);

            ALSourceState State = (ALSourceState)stateInt;

            if (State != ALSourceState.Playing)
            {
                AL.SourcePlay(_sourceId);
            }
        }

        public override void QueueBuffer(AudioBuffer buffer)
        {
            lock (_lock)
            {
                OpenALAudioBuffer driverBuffer = new()
                {
                    DriverIdentifier = buffer.DataPointer,
                    BufferId = AL.GenBuffer(),
                    SampleCount = GetSampleCount(buffer),
                };

                AL.BufferData(driverBuffer.BufferId, _targetFormat, buffer.Data, (int)RequestedSampleRate);

                _queuedBuffers.Enqueue(driverBuffer);

                AL.SourceQueueBuffer(_sourceId, driverBuffer.BufferId);

                if (_isActive)
                {
                    StartIfNotPlaying();
                }
            }
        }

        public override void SetVolume(float volume)
        {
            _volume = volume;

            UpdateMasterVolume(_driver.Volume);
        }

        public override float GetVolume()
        {
            return _volume;
        }

        public void UpdateMasterVolume(float newVolume)
        {
            lock (_lock)
            {
                AL.Source(_sourceId, ALSourcef.Gain, newVolume * _volume);
            }
        }

        public override void Start()
        {
            lock (_lock)
            {
                _isActive = true;

                StartIfNotPlaying();
            }
        }

        public override void Stop()
        {
            lock (_lock)
            {
                SetVolume(0.0f);

                AL.SourceStop(_sourceId);

                _isActive = false;
            }
        }

        public override void UnregisterBuffer(AudioBuffer buffer) { }

        public override bool WasBufferFullyConsumed(AudioBuffer buffer)
        {
            lock (_lock)
            {
                if (!_queuedBuffers.TryPeek(out OpenALAudioBuffer driverBuffer))
                {
                    return true;
                }

                return driverBuffer.DriverIdentifier != buffer.DataPointer;
            }
        }

        public override ulong GetPlayedSampleCount()
        {
            lock (_lock)
            {
                return _playedSampleCount;
            }
        }

        public bool Update()
        {
            lock (_lock)
            {
                if (_isActive)
                {
                    AL.GetSource(_sourceId, ALGetSourcei.BuffersProcessed, out int releasedCount);

                    if (releasedCount > 0)
                    {
                        int[] bufferIds = new int[releasedCount];

                        AL.SourceUnqueueBuffers(_sourceId, releasedCount, bufferIds);

                        int i = 0;

                        while (_queuedBuffers.TryPeek(out OpenALAudioBuffer buffer) && i < bufferIds.Length)
                        {
                            if (buffer.BufferId == bufferIds[i])
                            {
                                _playedSampleCount += buffer.SampleCount;

                                _queuedBuffers.TryDequeue(out _);

                                i++;
                            }
                        }

                        Debug.Assert(i == bufferIds.Length, "Unknown buffer ids found!");

                        AL.DeleteBuffers(bufferIds);
                    }

                    return releasedCount > 0;
                }

                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _driver.Unregister(this))
            {
                lock (_lock)
                {
                    PrepareToClose();
                    Stop();

                    AL.DeleteSource(_sourceId);
                }
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
