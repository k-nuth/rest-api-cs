using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace bitprim.insight
{

    public class OrderedSet<T> : ICollection<T>
    {
        private readonly IDictionary<T, LinkedListNode<T>> dictionary_;
        private readonly IDictionary<int, LinkedListNode<T>> index_;
        private readonly LinkedList<T> linkedList_;

        public OrderedSet()
            : this(EqualityComparer<T>.Default)
        {
        }

        public OrderedSet(IEqualityComparer<T> comparer)
        {
            dictionary_ = new Dictionary<T, LinkedListNode<T>>(comparer);
            index_ = new Dictionary<int, LinkedListNode<T>>();
            linkedList_ = new LinkedList<T>();
        }

        public int Count
        {
            get { return dictionary_.Count; }
        }

        public virtual bool IsReadOnly
        {
            get { return dictionary_.IsReadOnly; }
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        public void Clear()
        {
            linkedList_.Clear();
            dictionary_.Clear();
            index_.Clear();
        }

        public bool Remove(T item)
        {
            LinkedListNode<T> node;
            bool found = dictionary_.TryGetValue(item, out node);
            if (!found)
            {
                return false;
            }
            dictionary_.Remove(item);
            linkedList_.Remove(node);
            var indexEntryToRemove = index_.First(kvp => kvp.Value.Value.Equals(node.Value)); //TODO Avoid this search by saving index in node
            index_.Remove(indexEntryToRemove.Key);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return linkedList_.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(T item)
        {
            return dictionary_.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            linkedList_.CopyTo(array, arrayIndex);
        }

        public bool Add(T item)
        {
            if (dictionary_.ContainsKey(item))
            {
                return false;
            }
            LinkedListNode<T> node = linkedList_.AddLast(item);
            dictionary_.Add(item, node);
            index_.Add(linkedList_.Count-1, node);
            return true;
        }

        public List<T> ToList()
        {
            return new List<T>(linkedList_);
        }

        public List<T> GetRange(int index, int count)
        {
            if(index < 0 || count < 0)
            {
                throw new ArgumentException("Index and count must be positive");
            }

            if (linkedList_.Count - index < count) 
            { 
                throw new ArgumentException("Trying to get range outside collection bounds");
            } 

            var range = new List<T>(count);
            for(int i=0; i<count; i++)
            {
                range.Add(index_[index+i].Value);
            }
            return range;
        }
    }

}