using System;
using System.Collections.Generic;

namespace Ryujinx.Graphics.Metal
{
    class IdList<T> where T : class
    {
        private readonly List<T> _list;
        private int _freeMin;

        public IdList()
        {
            _list = new List<T>();
            _freeMin = 0;
        }

        public int Add(T value)
        {
            int id;
            int count = _list.Count;
            id = _list.IndexOf(null, _freeMin);

            if ((uint)id < (uint)count)
            {
                _list[id] = value;
            }
            else
            {
                id = count;
                _freeMin = id + 1;

                _list.Add(value);
            }

            return id + 1;
        }

        public void Remove(int id)
        {
            id--;

            int count = _list.Count;

            if ((uint)id >= (uint)count)
            {
                return;
            }

            if (id + 1 == count)
            {
                // Trim unused items.
                int removeIndex = id;

                while (removeIndex > 0 && _list[removeIndex - 1] == null)
                {
                    removeIndex--;
                }

                _list.RemoveRange(removeIndex, count - removeIndex);

                if (_freeMin > removeIndex)
                {
                    _freeMin = removeIndex;
                }
            }
            else
            {
                _list[id] = null;

                if (_freeMin > id)
                {
                    _freeMin = id;
                }
            }
        }

        public bool TryGetValue(int id, out T value)
        {
            id--;

            try
            {
                if ((uint)id < (uint)_list.Count)
                {
                    value = _list[id];
                    return value != null;
                }

                value = null;
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                value = null;
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                value = null;
                return false;
            }
        }

        public void Clear()
        {
            _list.Clear();
            _freeMin = 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i] != null)
                {
                    yield return _list[i];
                }
            }
        }
    }
}
