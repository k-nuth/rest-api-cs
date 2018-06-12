using System.Collections;
using System.Collections.Generic;

namespace bitprim.insight
{

    public class OrderedSet<T> : ICollection<T>
    {
        private readonly IDictionary<T, LinkedListNode<T>> dictionary_;
        private readonly LinkedList<T> linkedList_;

        public OrderedSet()
            : this(EqualityComparer<T>.Default)
        {
        }

        public OrderedSet(IEqualityComparer<T> comparer)
        {
            dictionary_ = new Dictionary<T, LinkedListNode<T>>(comparer);
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
            return true;
        }

        public List<T> ToList()
        {
            return new List<T>(linkedList_);
        }
    }

}