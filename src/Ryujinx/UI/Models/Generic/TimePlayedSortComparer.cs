using Ryujinx.UI.App.Common;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.UI.Models.Generic
{
    internal class TimePlayedSortComparer : IComparer<ApplicationData>
    {
        public TimePlayedSortComparer() { }
        public TimePlayedSortComparer(bool isAscending) { IsAscending = isAscending; }

        public bool IsAscending { get; }

        public int Compare(ApplicationData x, ApplicationData y)
        {
            TimeSpan aValue = TimeSpan.Zero, bValue = TimeSpan.Zero;

            if (x?.TimePlayed != null)
            {
                aValue = x.TimePlayed;
            }

            if (y?.TimePlayed != null)
            {
                bValue = y.TimePlayed;
            }

            return (IsAscending ? 1 : -1) * TimeSpan.Compare(aValue, bValue);
        }
    }
}
