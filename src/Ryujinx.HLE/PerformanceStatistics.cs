using Ryujinx.Common;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Ryujinx.HLE
{
    public class PerformanceStatistics
    {
        private const int FrameTypeGame = 0;
        private const int PercentTypeFifo = 0;

        private readonly double[] _frameRate;
        private readonly double[] _accumulatedFrameTime;
        private readonly double[] _previousFrameTime;

        private readonly double[] _averagePercent;
        private readonly double[] _accumulatedActiveTime;
        private readonly double[] _percentLastEndTime;
        private readonly double[] _percentStartTime;

        private readonly long[] _framesRendered;
        private readonly double[] _percentTime;

        private readonly Lock[] _frameLock;
        private readonly Lock[] _percentLock;

        private readonly double _ticksToSeconds;

        private readonly Timer _resetTimer;

        public PerformanceStatistics()
        {
            _frameRate = new double[1];
            _accumulatedFrameTime = new double[1];
            _previousFrameTime = new double[1];

            _averagePercent = new double[1];
            _accumulatedActiveTime = new double[1];
            _percentLastEndTime = new double[1];
            _percentStartTime = new double[1];

            _framesRendered = new long[1];
            _percentTime = new double[1];

            _frameLock = new[] { new Lock() };
            _percentLock = new[] { new Lock() };

            _resetTimer = new Timer(750);

            _resetTimer.Elapsed += ResetTimerElapsed;
            _resetTimer.AutoReset = true;

            _resetTimer.Start();

            _ticksToSeconds = 1.0 / PerformanceCounter.TicksPerSecond;
        }

        private void ResetTimerElapsed(object sender, ElapsedEventArgs e)
        {
            CalculateFrameRate(FrameTypeGame);
            CalculateAveragePercent(PercentTypeFifo);
        }

        private void CalculateFrameRate(int frameType)
        {
            double frameRate = 0;

            lock (_frameLock[frameType])
            {
                if (_accumulatedFrameTime[frameType] > 0)
                {
                    frameRate = _framesRendered[frameType] / _accumulatedFrameTime[frameType];
                }

                _frameRate[frameType] = frameRate;
                _framesRendered[frameType] = 0;
                _accumulatedFrameTime[frameType] = 0;
            }
        }

        private void CalculateAveragePercent(int percentType)
        {
            // If start time is non-zero, a percent reading is still being measured.
            // If there aren't any readings, the default should be 100% if still being measured, or 0% if not.
            double percent = (_percentStartTime[percentType] == 0) ? 0 : 100;

            lock (_percentLock[percentType])
            {
                if (_percentTime[percentType] > 0)
                {
                    percent = (_accumulatedActiveTime[percentType] / _percentTime[percentType]) * 100;
                }

                _averagePercent[percentType] = percent;
                _percentTime[percentType] = 0;
                _accumulatedActiveTime[percentType] = 0;
            }
        }

        public void RecordGameFrameTime()
        {
            RecordFrameTime(FrameTypeGame);
        }

        public void RecordFifoStart()
        {
            StartPercentTime(PercentTypeFifo);
        }

        public void RecordFifoEnd()
        {
            EndPercentTime(PercentTypeFifo);
        }

        private void StartPercentTime(int percentType)
        {
            double currentTime = PerformanceCounter.ElapsedTicks * _ticksToSeconds;

            _percentStartTime[percentType] = currentTime;
        }

        private void EndPercentTime(int percentType)
        {
            double currentTime = PerformanceCounter.ElapsedTicks * _ticksToSeconds;
            double elapsedTime = currentTime - _percentLastEndTime[percentType];
            double elapsedActiveTime = currentTime - _percentStartTime[percentType];

            lock (_percentLock[percentType])
            {
                _accumulatedActiveTime[percentType] += elapsedActiveTime;
                _percentTime[percentType] += elapsedTime;
            }

            _percentLastEndTime[percentType] = currentTime;
            _percentStartTime[percentType] = 0;
        }

        private void RecordFrameTime(int frameType)
        {
            double currentFrameTime = PerformanceCounter.ElapsedTicks * _ticksToSeconds;
            double elapsedFrameTime = currentFrameTime - _previousFrameTime[frameType];

            _previousFrameTime[frameType] = currentFrameTime;

            lock (_frameLock[frameType])
            {
                _accumulatedFrameTime[frameType] += elapsedFrameTime;

                _framesRendered[frameType]++;
            }
        }

        public double GetGameFrameRate()
        {
            return _frameRate[FrameTypeGame];
        }

        public double GetFifoPercent()
        {
            return _averagePercent[PercentTypeFifo];
        }

        public double GetGameFrameTime()
        {
            return 1000 / _frameRate[FrameTypeGame];
        }
    }
}
