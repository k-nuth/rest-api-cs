using System;
using System.Collections.Generic;
using Xunit;

namespace bitprim.insight.tests
{
    public class OrderedSetTest
    {
        private class Person
        {
            public int IdNumber { get; set; }
            public string Name { get; set; }
        }

        private class IdNumberComparer : IEqualityComparer<Person>
        {
            public bool Equals(Person lv, Person rv)
            {
                return lv.IdNumber == rv.IdNumber;
            }

            public int GetHashCode(Person p)
            {
                return p.IdNumber.GetHashCode();
            }
        }

        [Fact]
        public void ClearAllItems()
        {
            var set = new OrderedSet<int>{1, 2, 3};
            set.Clear();
            Assert.Equal(set.Count, 0);
            Assert.False(set.Contains(1));
            Assert.False(set.Contains(2));
            Assert.False(set.Contains(3));
        }

        [Fact]
        public void CopyWholeSetToArray()
        {
            var set = new OrderedSet<int>{3, 1, 2};
            var array = new int[3];
            set.CopyTo(array, 0);
            Assert.Equal(array.Length, 3);
            Assert.Equal(array[0], 3);
            Assert.Equal(array[1], 1);
            Assert.Equal(array[2], 2);
            //Change set, ensure array does not change
            Assert.True(set.Add(4));
            Assert.Equal(array.Length, 3);
            Assert.Equal(array[0], 3);
            Assert.Equal(array[1], 1);
            Assert.Equal(array[2], 2);
        }

        [Fact]
        public void DuplicatesNotInserted()
        {
            var set = new OrderedSet<int>{1};
            Assert.False(set.Add(1));
        }

        [Fact]
        public void EnumerateAllItems()
        {
            var set = new OrderedSet<int>{3, 1, 2};
            int j = 0;
            List<int> setItems = set.ToList();
            foreach(int setItem in set)
            {
                Assert.Equal(setItem, setItems[j]);
                j++;
            }
        }

        [Fact]
        public void GetRangeNegativeArgsThrow()
        {
            var set = new OrderedSet<int>{0, 1, 2, 3};
            var exception = Assert.Throws<ArgumentException>( () => set.GetRange(-1, 3) );
            Assert.Equal("Index and count must be positive", exception.Message);
            var exception2 = Assert.Throws<ArgumentException>( () => set.GetRange(0, -3) );
            Assert.Equal("Index and count must be positive", exception2.Message);
        }

        [Fact]
        public void GetRangeOutOfBoundsThrows()
        {
            var set = new OrderedSet<int>{0, 1, 2, 3};
            var exception = Assert.Throws<ArgumentException>( () => set.GetRange(2, 3) );
            Assert.Equal("Trying to get range outside collection bounds", exception.Message);
        }

        [Fact]
        public void GetRangeSuccessfully()
        {
            var set = new OrderedSet<int>{0, 1, 2, 3, 4, 5};
            var subSet = set.GetRange(3, 2);
            Assert.Equal(subSet.Count, 2);
            Assert.Equal(subSet[0], 3);
            Assert.Equal(subSet[1], 4);
        }

        [Fact]
        public void InsertionOrderIsPreserved()
        {
            var set = new OrderedSet<int>();
            Assert.True(set.Add(3));
            Assert.True(set.Add(1));
            Assert.True(set.Add(2));

            List<int> setItems = set.ToList();
            Assert.Equal(setItems.Count, 3);
            Assert.Equal(setItems[0], 3);
            Assert.Equal(setItems[1], 1);
            Assert.Equal(setItems[2], 2);
        }

        [Fact]
        public void RemovalWorks()
        {
            var set = new OrderedSet<int>{3, 1, 2};
            Assert.True(set.Contains(1));
            Assert.Equal(set.Count, 3);
            Assert.True(set.Remove(1));
            Assert.False(set.Contains(1));
            Assert.Equal(set.Count, 2);
            List<int> setItems = set.ToList();
            Assert.Equal(setItems[0], 3);
            Assert.Equal(setItems[1], 2);
        }

        [Fact]
        public void UseCustomEquality()
        {
            //Two persons are considered the same if they have the same id number
            var set = new OrderedSet<Person>(new IdNumberComparer());
            Assert.True(set.Add(new Person(){ IdNumber = 1, Name = "John Doe" }));
            Assert.False(set.Add(new Person(){ IdNumber = 1, Name = "Richard Bauer" }));
        }
        
    }
}