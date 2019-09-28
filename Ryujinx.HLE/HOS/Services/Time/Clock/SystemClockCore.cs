﻿using Ryujinx.HLE.HOS.Kernel.Threading;

namespace Ryujinx.HLE.HOS.Services.Time.Clock
{
    abstract class SystemClockCore
    {
        private SteadyClockCore    _steadyClockCore;
        private SystemClockContext _context;
        private bool               _isInitialized;

        public SystemClockCore(SteadyClockCore steadyClockCore)
        {
            _steadyClockCore = steadyClockCore;
            _context         = new SystemClockContext();
            _isInitialized   = false;

            _context.SteadyTimePoint.ClockSourceId = steadyClockCore.GetClockSourceId();
        }

        public virtual SteadyClockCore GetSteadyClockCore()
        {
            return _steadyClockCore;
        }

        public ResultCode GetCurrentTime(KThread thread, out long posixTime)
        {
            posixTime = 0;

            SteadyClockTimePoint currentTimePoint = _steadyClockCore.GetCurrentTimePoint(thread);

            ResultCode result = GetClockContext(thread, out SystemClockContext clockContext);

            if (result == ResultCode.Success)
            {
                result = ResultCode.TimeMismatch;

                if (currentTimePoint.ClockSourceId == clockContext.SteadyTimePoint.ClockSourceId)
                {
                    posixTime = clockContext.Offset + currentTimePoint.TimePoint;

                    result = 0;
                }
            }

            return result;
        }

        public ResultCode SetCurrentTime(KThread thread, long posixTime)
        {
            SteadyClockTimePoint currentTimePoint = _steadyClockCore.GetCurrentTimePoint(thread);

            SystemClockContext clockContext = new SystemClockContext()
            {
                Offset = posixTime - currentTimePoint.TimePoint,
                SteadyTimePoint = currentTimePoint
            };

            ResultCode result = SetClockContext(clockContext);

            if (result == ResultCode.Success)
            {
                result = Flush(clockContext);
            }

            return result;
        }

        public virtual ResultCode GetClockContext(KThread thread, out SystemClockContext context)
        {
            context = _context;

            return ResultCode.Success;
        }

        public virtual ResultCode SetClockContext(SystemClockContext context)
        {
            _context = context;

            return ResultCode.Success;
        }

        protected abstract ResultCode Flush(SystemClockContext context);

        public ResultCode SetSystemClockContext(SystemClockContext context)
        {
            ResultCode result = SetClockContext(context);

            if (result == ResultCode.Success)
            {
                result = Flush(context);
            }

            return result;
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        public void MarkInitialized()
        {
            _isInitialized = true;
        }

        public bool IsClockSetup(KThread thread)
        {
            ResultCode result = GetClockContext(thread, out SystemClockContext context);

            if (result == ResultCode.Success)
            {
                SteadyClockTimePoint steadyClockTimePoint = _steadyClockCore.GetCurrentTimePoint(thread);

                return steadyClockTimePoint.ClockSourceId == context.SteadyTimePoint.ClockSourceId;
            }

            return false;
        }
    }
}
