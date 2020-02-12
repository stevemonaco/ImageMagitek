using System.Collections;
using System.Collections.Generic;

namespace ImageMagitek.Codec
{
    /// <summary>
    /// A list that implements index-wrapping
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class BroadcastList<T> : IEnumerable<T>
    {
        private List<T> _list;

        public int Capacity => _list.Capacity;
        public int Count => _list.Count;

        public BroadcastList() : this(4) { }
        public BroadcastList(int capacity)
        {
            _list = new List<T>(capacity);
        }
        public BroadcastList(IEnumerable<T> items)
        {
            _list = new List<T>(items);
        }

        public void Add(T item) => _list.Add(item);
        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        public T this[int index]
        {
            get
            {
                if (index < 0)
                    return _list[((index % Count) + Count) % Count];

                return _list[index % Count];
            }

            set
            {
                if (index < 0)
                    _list[((index % Count) + Count) % Count] = value;
                else
                    _list[index % Count] = value;
            }
        }
    }
}
