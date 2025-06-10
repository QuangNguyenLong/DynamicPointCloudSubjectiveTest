using System;
public class MyMath
{
    static public int[] ShuffleArray(int min, int max)
    {
        int count = max - min + 1;
        int[] result = new int[count];

        // Fill the array with consecutive integers from min to max
        for (int i = 0; i < count; i++)
        {
            result[i] = min + i;
        }

        System.Random rng = new System.Random();

        // Fisher-Yates shuffle
        for (int i = count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (result[j], result[i]) = (result[i], result[j]);
        }

        return result;
    }
    public class Queue<T>
    {
        private T[] _queue;
        private int _head;
        private int _tail;
        // private static int _size; // This was the bug
        private int _size;         // Corrected: _size should be an instance field

        public Queue(int capacity)
        {
            if (capacity <= 0)
            {
                UnityEngine.Debug.LogError("Queue capacity must be positive.");
                // Handle error appropriately, e.g., throw ArgumentOutOfRangeException
                // or default to a sensible minimum capacity. For now, let it proceed
                // which might lead to issues if capacity is truly zero or negative.
                // Consider: capacity = Math.Max(1, capacity);
            }
            _queue = new T[capacity];
            _head = 0;
            _tail = -1; // Initialize tail correctly for the first Enqueue
            _size = 0;
        }

        public bool IsFull() { return _size == _queue.Length; }
        public bool IsEmpty() { return _size == 0; }
        public int Count => _size;

        public void Enqueue(T item)
        {
            if (IsFull())
            {
                UnityEngine.Debug.LogError("Queue is full. Cannot enqueue."); // Changed from Log to LogError for more visibility
                return; // Or throw an exception
            }

            _tail = (_tail + 1) % _queue.Length;
            _queue[_tail] = item;
            _size++;
        }

        public void Dequeue()
        {
            if (IsEmpty())
            {
                UnityEngine.Debug.LogError("Queue is empty. Cannot dequeue."); // Changed from Log to LogError
                return; // Or throw an exception
            }

            // Optional: Clear the dequeued slot to free up references if T is a reference type
            // _queue[_head] = default(T); 

            _head = (_head + 1) % _queue.Length;
            _size--;
        }

        public T Peek()
        {
            if (IsEmpty())
            {
                UnityEngine.Debug.LogError("Queue is empty. Cannot peek.");
                return default(T); // Or throw an exception
            }
            return _queue[_head];
        }
    }

}