using Ryujinx.UI.App.Common;
using System;
using System.Collections.Generic;

namespace Ryujinx.Ava.UI.Models.Generic
{
    internal class LastPlayedSortComparer : IComparer<ApplicationData>
    {
        public LastPlayedSortComparer() { }
        public LastPlayedSortComparer(bool isAscending) { IsAscending = isAscending; }

        public bool IsAscending { get; }

        public int Compare(ApplicationData x, ApplicationData y)
        {
            DateTime aValue = DateTime.UnixEpoch, bValue = DateTime.UnixEpoch;

            if (x?.LastPlayed != null)
            {
                aValue = x.LastPlayed.Value;
            }

            if (y?.LastPlayed != null)
            {
                bValue = y.LastPlayed.Value;
            }

            return (IsAscending ? 1 : -1) * DateTime.Compare(aValue, bValue);
        }
    }
}
