using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace bitprim.insight
{
    /// <summary>
    /// A collection meant to prevent duplicates while keeping insertion order.
    /// (New elements are added at the end).
    /// </summary>
    public class OrderedSet<T> : ICollection<T>
    {
        private readonly IDictionary<T, LinkedListNode<T>> dictionary_;
        private readonly IDictionary<int, LinkedListNode<T>> index_;
        private readonly LinkedList<T> linkedList_;

        /// <summary>
        /// Use default equality comparator from the generic type.  
        /// </summary>
        public OrderedSet()
            : this(EqualityComparer<T>.Default)
        {
        }

        /// <summary>
        /// Use a custom equality comparison. 
        /// </summary>
        /// <param name="comparer"> Custom comparison for the T generic type. </param>
        public OrderedSet(IEqualityComparer<T> comparer)
        {
            dictionary_ = new Dictionary<T, LinkedListNode<T>>(comparer);
            index_ = new Dictionary<int, LinkedListNode<T>>();
            linkedList_ = new LinkedList<T>();
        }

        /// <summary>
        /// Current amount of elements in the collection.
        /// </summary>
        public int Count
        {
            get { return dictionary_.Count; }
        }

        /// <summary>
        /// True iif the collection is read-only.
        /// </summary>
        public virtual bool IsReadOnly
        {
            get { return dictionary_.IsReadOnly; }
        }

        /// <summary>
        /// Add an element to the set. If it's a duplicate, it will be ignored.
        /// </summary>
        /// <param name="item"> </param>
        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        /// <summary>
        /// Remove all elements from the collection.
        /// </summary>
        public void Clear()
        {
            linkedList_.Clear();
            dictionary_.Clear();
            index_.Clear();
        }

        /// <summary>
        /// Remove a specific item from the set. Returns 
        /// </summary>
        /// <param name="item"> Item to remove. </param>
        /// <returns> True if and only if the element was found and removed. </returns>
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

        /// <summary>
        /// For iterating the collection in insertion order.
        /// </summary>
        /// <returns> Generic enumerator. </returns>
        public IEnumerator<T> GetEnumerator()
        {
            return linkedList_.GetEnumerator();
        }

        /// <summary>
        /// For iterating the collection in insertion order.
        /// </summary>
        /// <returns> Enumerator interface. </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Checks if an item is already in the set.
        /// </summary>
        /// <param name="item"> Item to check. </param>
        /// <returns> True if and only if an equivalent item exists in the set. </returns>
        public bool Contains(T item)
        {
            return dictionary_.ContainsKey(item);
        }

        /// <summary>
        /// Copy all elements to an array, respecting their order.
        /// </summary>
        /// <param name="array"> Destination array. </param>
        /// <param name="arrayIndex"> Index on destination array where copy will begin. </param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            linkedList_.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Add an item to the set, at the end. If it already exists in the set, nothing is done.
        /// </summary>
        /// <param name="item"> Item to add. </param>
        /// <returns> True if and only if the element did not exist in the set, and was added. </returns>
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

        /// <summary>
        /// Copy all set elements to a list.
        /// </summary>
        /// <returns> A newly instantiated list with a copy of all set elements. </returns>
        public List<T> ToList()
        {
            return new List<T>(linkedList_);
        }

        /// <summary>
        /// Copy a range of the set ordered elements to a list.
        /// </summary>
        /// <param name="index"> Range starting index, zero-based. </param>
        /// <param name="count"> Range starting index, zero-based. </param>
        /// <returns> A newly instantiated list with a copy of the set range, respecting order. </returns>
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