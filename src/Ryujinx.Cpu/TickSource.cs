using System;
using System.Diagnostics;

namespace Ryujinx.Cpu
{
    public class TickSource : ITickSource
    {
        private static Stopwatch _tickCounter;

        private static double _hostTickFreq;

        /// <inheritdoc/>
        public ulong Frequency { get; }

        /// <inheritdoc/>
        public ulong Counter => (ulong)(ElapsedSeconds * Frequency);

        public long TickMultiplier { get; set; } = 100;
        private static long AcumElapsedTicks = 0;
        private static long LastElapsedTicks = 0;
        private long Elapsedticks
        {
            get
            {
                long elapsedTicks = _tickCounter.ElapsedTicks;
                AcumElapsedTicks += (elapsedTicks - LastElapsedTicks) * TickMultiplier / 100;
                LastElapsedTicks = elapsedTicks;
                return AcumElapsedTicks;
            }
        }

        /// <inheritdoc/>
        public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(0, Elapsedticks);

        /// <inheritdoc/>
        public double ElapsedSeconds => Elapsedticks * _hostTickFreq;

        public TickSource(ulong frequency)
        {
            Frequency = frequency;
            _hostTickFreq = 1.0 / Stopwatch.Frequency;

            _tickCounter = new Stopwatch();
            _tickCounter.Start();
        }

        /// <inheritdoc/>
        public void Suspend()
        {
            _tickCounter.Stop();
        }

        /// <inheritdoc/>
        public void Resume()
        {
            _tickCounter.Start();
        }
    }
}
