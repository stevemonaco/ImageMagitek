using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ImageMagitek.Codec
{
    /// <summary>
    /// A list that implements index-wrapping for repeating/incrementing patterns
    /// </summary>
    public class RepeatList : IEnumerable<int>
    {
        private readonly List<int> _list;

        public int Capacity => _list.Capacity;
        public int Count => _list.Count;

        /// <summary>
        /// Increment to add for each repeat past the end of the list
        /// </summary>
        public int RepeatIncrement { get; set; }

        public RepeatList() : this(4) { }
        public RepeatList(int capacity)
        {
            _list = new List<int>(capacity);
        }
        public RepeatList(IEnumerable<int> items)
        {
            _list = new List<int>(items);
            RepeatIncrement = _list.Count;
        }

        public RepeatList(IEnumerable<int> items, int repeatIncrement)
        {
            _list = new List<int>(items);
            RepeatIncrement = repeatIncrement;
        }

        public void Add(int item)
        {
            _list.Add(item);
            RepeatIncrement++;
        }

        public IEnumerator<int> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        public int this[int index]
        {
            get
            {
                if (index < 0)
                    throw new ArgumentOutOfRangeException($"{nameof(index)} ({index}) cannot be negative");

                return _list[index % Count] + (index / Count) * RepeatIncrement;
            }
        }
    }
}