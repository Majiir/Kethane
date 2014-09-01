using System;
using System.Collections;
using System.Collections.Generic;

namespace Kethane.GeodesicGrid
{
    public class CellSet : IEnumerable<Cell>
    {
        private readonly BitArray set;

        public CellSet(int level)
        {
            this.set = new BitArray((int)Cell.CountAtLevel(level));
        }

        public CellSet(int level, byte[] array)
        {
            this.set = new BitArray(array);
            this.set.Length = (int)Cell.CountAtLevel(level);
        }

        public bool this[Cell cell]
        {
            get
            {
                if (cell.Index >= set.Count) { throw new ArgumentException(); }
                return set[(int)cell.Index];
            }
            set
            {
                if (cell.Index >= set.Count) { throw new ArgumentException(); }
                set[(int)cell.Index] = value;
            }
        }

        public IEnumerator<Cell> GetEnumerator()
        {
            for (var i = 0; i < set.Count; i++)
            {
                if (set[i])
                {
                    yield return new Cell((uint)i);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public byte[] ToByteArray()
        {
            return set.ToByteArray();
        }
    }
}
