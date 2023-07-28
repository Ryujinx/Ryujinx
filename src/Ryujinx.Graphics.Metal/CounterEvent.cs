using Ryujinx.Graphics.GAL;

namespace Ryujinx.Graphics.Metal
{
    public class CounterEvent : ICounterEvent
    {

        public CounterEvent()
        {
            Invalid = false;
        }

        public bool Invalid { get; set; }
        public bool ReserveForHostAccess()
        {
            return true;
        }

        public void Flush() { }

        public void Dispose() { }
    }
}
