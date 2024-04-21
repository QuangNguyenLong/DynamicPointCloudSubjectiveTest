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
        private static int _size;

        public Queue(int capacity)
        {
            _queue = new T[capacity];
            _head = 0;
            _tail = -1;
            _size = 0;
        }

        public bool IsFull() { return _size == _queue.Length; }
        public bool IsEmpty() { return _size == 0; }
        public int Count => _size;
        public void Enqueue(T item)
        {
            if (IsFull())
            {
                // Queue is full
                UnityEngine.Debug.Log("Queue is full");
            }

            _tail = (_tail + 1) % _queue.Length;
            _queue[_tail] = item; // Reference to same object
            _size++;
        }

        public void Dequeue()
        {
            if (IsEmpty())
            {
                // Queue is empty
                UnityEngine.Debug.Log("Queue is empty");
            }

            _head = (_head + 1) % _queue.Length;
            _size--;

        }

        public T Peek()
        {
            if (IsEmpty())
            {
                // Queue is empty
                UnityEngine.Debug.LogError("Queue is empty");
            }
            return _queue[_head];
        }

    }

}