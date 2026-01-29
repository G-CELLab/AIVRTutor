
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AI.Performance
{
    /// <summary>
    /// Generic object pool that minimizes GC allocations by reusing objects.
    /// Thread-safe for multi-threaded access.
    /// </summary>
    /// <typeparam name="T">Type of objects to pool</typeparam>
    public class ObjectPool<T> where T : class
    {
        private readonly ConcurrentBag<T> _pool = new ConcurrentBag<T>();
        private readonly Func<T> _factory;
        private readonly Action<T> _reset;
        private readonly int _maxSize;
        private int _count;
        
        /// <summary>
        /// Create an object pool
        /// </summary>
        /// <param name="factory">Factory function to create new instances</param>
        /// <param name="reset">Optional reset action when returning to pool</param>
        /// <param name="maxSize">Maximum pool size (prevents memory bloat)</param>
        /// <param name="prewarm">Number of objects to create initially</param>
        public ObjectPool(Func<T> factory, Action<T> reset = null, int maxSize = 64, int prewarm = 0)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _reset = reset;
            _maxSize = maxSize;
            _count = 0;
            
            // Prewarm pool
            for (int i = 0; i < prewarm && i < maxSize; i++)
            {
                _pool.Add(_factory());
                _count++;
            }
        }
        
        /// <summary>
        /// Get an object from the pool or create a new one
        /// </summary>
        public T Get()
        {
            if (_pool.TryTake(out var item))
            {
                return item;
            }
            return _factory();
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;
            
            _reset?.Invoke(item);
            
            // Only add if under max size
            if (_count < _maxSize)
            {
                _pool.Add(item);
                System.Threading.Interlocked.Increment(ref _count);
            }
        }
        
        /// <summary>
        /// Clear the pool
        /// </summary>
        public void Clear()
        {
            while (_pool.TryTake(out _)) { }
            _count = 0;
        }
        
        public int Count => _count;
    }
    
    /// <summary>
    /// Specialized pool for byte arrays to minimize audio buffer allocations.
    /// Uses size buckets for efficient reuse.
    /// </summary>
    public class ByteArrayPool
    {
        private static ByteArrayPool _instance;
        public static ByteArrayPool Instance => _instance ??= new ByteArrayPool();
        
        // Size buckets: 1KB, 4KB, 16KB, 64KB, 256KB, 1MB
        private readonly int[] _bucketSizes = { 1024, 4096, 16384, 65536, 262144, 1048576 };
        private readonly ConcurrentBag<byte[]>[] _buckets;
        private readonly int[] _bucketCounts;
        private const int MAX_ITEMS_PER_BUCKET = 16;
        
        private ByteArrayPool()
        {
            _buckets = new ConcurrentBag<byte[]>[_bucketSizes.Length];
            _bucketCounts = new int[_bucketSizes.Length];
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i] = new ConcurrentBag<byte[]>();
            }
        }
        
        private int GetBucketIndex(int size)
        {
            for (int i = 0; i < _bucketSizes.Length; i++)
            {
                if (size <= _bucketSizes[i]) return i;
            }
            return -1; // Too large for pooling
        }
        
        /// <summary>
        /// Rent a byte array of at least the specified size.
        /// Returned array may be larger than requested.
        /// </summary>
        public byte[] Rent(int minimumSize)
        {
            if (minimumSize <= 0) return Array.Empty<byte>();
            
            int bucketIndex = GetBucketIndex(minimumSize);
            
            if (bucketIndex >= 0 && _buckets[bucketIndex].TryTake(out var pooled))
            {
                System.Threading.Interlocked.Decrement(ref _bucketCounts[bucketIndex]);
                return pooled;
            }
            
            // Create new array (use bucket size if available, otherwise exact size)
            int size = bucketIndex >= 0 ? _bucketSizes[bucketIndex] : minimumSize;
            return new byte[size];
        }
        
        /// <summary>
        /// Return a byte array to the pool.
        /// The array should not be used after returning.
        /// </summary>
        public void Return(byte[] array)
        {
            if (array == null || array.Length == 0) return;
            
            int bucketIndex = GetBucketIndex(array.Length);
            if (bucketIndex < 0) return; // Too large, let GC handle it
            
            // Only if array matches bucket size exactly
            if (array.Length == _bucketSizes[bucketIndex])
            {
                if (_bucketCounts[bucketIndex] < MAX_ITEMS_PER_BUCKET)
                {
                    // Clear sensitive data (optional, can skip for performance)
                    // Array.Clear(array, 0, array.Length);
                    
                    _buckets[bucketIndex].Add(array);
                    System.Threading.Interlocked.Increment(ref _bucketCounts[bucketIndex]);
                }
            }
        }
        
        /// <summary>
        /// Clear all pooled arrays
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _buckets.Length; i++)
            {
                while (_buckets[i].TryTake(out _)) { }
                _bucketCounts[i] = 0;
            }
        }
    }
    
    /// <summary>
    /// Specialized pool for float arrays (audio samples)
    /// </summary>
    public class FloatArrayPool
    {
        private static FloatArrayPool _instance;
        public static FloatArrayPool Instance => _instance ??= new FloatArrayPool();
        
        // Common audio buffer sizes: 480, 960, 2048, 4096, 8192, 16384, 32768
        private readonly int[] _bucketSizes = { 480, 960, 2048, 4096, 8192, 16384, 32768 };
        private readonly ConcurrentBag<float[]>[] _buckets;
        private readonly int[] _bucketCounts;
        private const int MAX_ITEMS_PER_BUCKET = 16;
        
        private FloatArrayPool()
        {
            _buckets = new ConcurrentBag<float[]>[_bucketSizes.Length];
            _bucketCounts = new int[_bucketSizes.Length];
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i] = new ConcurrentBag<float[]>();
            }
        }
        
        private int GetBucketIndex(int size)
        {
            for (int i = 0; i < _bucketSizes.Length; i++)
            {
                if (size <= _bucketSizes[i]) return i;
            }
            return -1;
        }
        
        /// <summary>
        /// Rent a float array of at least the specified size.
        /// </summary>
        public float[] Rent(int minimumSize)
        {
            if (minimumSize <= 0) return Array.Empty<float>();
            
            int bucketIndex = GetBucketIndex(minimumSize);
            
            if (bucketIndex >= 0 && _buckets[bucketIndex].TryTake(out var pooled))
            {
                System.Threading.Interlocked.Decrement(ref _bucketCounts[bucketIndex]);
                return pooled;
            }
            
            int size = bucketIndex >= 0 ? _bucketSizes[bucketIndex] : minimumSize;
            return new float[size];
        }
        
        /// <summary>
        /// Return a float array to the pool.
        /// </summary>
        public void Return(float[] array)
        {
            if (array == null || array.Length == 0) return;
            
            int bucketIndex = GetBucketIndex(array.Length);
            if (bucketIndex < 0) return;
            
            if (array.Length == _bucketSizes[bucketIndex])
            {
                if (_bucketCounts[bucketIndex] < MAX_ITEMS_PER_BUCKET)
                {
                    _buckets[bucketIndex].Add(array);
                    System.Threading.Interlocked.Increment(ref _bucketCounts[bucketIndex]);
                }
            }
        }
        
        public void Clear()
        {
            for (int i = 0; i < _buckets.Length; i++)
            {
                while (_buckets[i].TryTake(out _)) { }
                _bucketCounts[i] = 0;
            }
        }
    }
    
    /// <summary>
    /// Pool for StringBuilder instances to avoid string allocations
    /// </summary>
    public class StringBuilderPool
    {
        private static StringBuilderPool _instance;
        public static StringBuilderPool Instance => _instance ??= new StringBuilderPool();
        
        private readonly ConcurrentBag<StringBuilder> _pool = new ConcurrentBag<StringBuilder>();
        private int _count;
        private const int MAX_POOL_SIZE = 16;
        private const int DEFAULT_CAPACITY = 1024;
        private const int MAX_CAPACITY = 16384; // Don't pool huge StringBuilders
        
        public StringBuilder Get(int capacity = DEFAULT_CAPACITY)
        {
            if (_pool.TryTake(out var sb))
            {
                System.Threading.Interlocked.Decrement(ref _count);
                if (sb.Capacity < capacity)
                {
                    sb.Capacity = capacity;
                }
                return sb;
            }
            return new StringBuilder(Math.Max(capacity, DEFAULT_CAPACITY));
        }
        
        public void Return(StringBuilder sb)
        {
            if (sb == null) return;
            
            // Don't pool if it grew too large
            if (sb.Capacity > MAX_CAPACITY) return;
            
            sb.Clear();
            
            if (_count < MAX_POOL_SIZE)
            {
                _pool.Add(sb);
                System.Threading.Interlocked.Increment(ref _count);
            }
        }
        
        /// <summary>
        /// Get a StringBuilder, use it, and return the resulting string
        /// </summary>
        public string UseAndReturn(Action<StringBuilder> action)
        {
            var sb = Get();
            try
            {
                action(sb);
                return sb.ToString();
            }
            finally
            {
                Return(sb);
            }
        }
        
        public void Clear()
        {
            while (_pool.TryTake(out _)) { }
            _count = 0;
        }
    }
    
    /// <summary>
    /// Pool for List&lt;float&gt; used in audio capture
    /// </summary>
    public class FloatListPool
    {
        private static FloatListPool _instance;
        public static FloatListPool Instance => _instance ??= new FloatListPool();
        
        private readonly ConcurrentBag<List<float>> _pool = new ConcurrentBag<List<float>>();
        private int _count;
        private const int MAX_POOL_SIZE = 8;
        private const int DEFAULT_CAPACITY = 16384;
        private const int MAX_CAPACITY = 262144; // ~5 seconds at 48kHz
        
        public List<float> Get(int capacity = DEFAULT_CAPACITY)
        {
            if (_pool.TryTake(out var list))
            {
                System.Threading.Interlocked.Decrement(ref _count);
                list.Clear();
                if (list.Capacity < capacity)
                {
                    list.Capacity = capacity;
                }
                return list;
            }
            return new List<float>(Math.Max(capacity, DEFAULT_CAPACITY));
        }
        
        public void Return(List<float> list)
        {
            if (list == null) return;
            
            // Don't pool if it grew too large
            if (list.Capacity > MAX_CAPACITY)
            {
                list.Clear();
                list.TrimExcess();
                return;
            }
            
            list.Clear();
            
            if (_count < MAX_POOL_SIZE)
            {
                _pool.Add(list);
                System.Threading.Interlocked.Increment(ref _count);
            }
        }
        
        public void Clear()
        {
            while (_pool.TryTake(out _)) { }
            _count = 0;
        }
    }
    
    /// <summary>
    /// Reusable memory stream to avoid MemoryStream allocations
    /// </summary>
    public class MemoryStreamPool
    {
        private static MemoryStreamPool _instance;
        public static MemoryStreamPool Instance => _instance ??= new MemoryStreamPool();
        
        private readonly ConcurrentBag<System.IO.MemoryStream> _pool = new ConcurrentBag<System.IO.MemoryStream>();
        private int _count;
        private const int MAX_POOL_SIZE = 8;
        private const int DEFAULT_CAPACITY = 65536;
        private const int MAX_CAPACITY = 1048576; // 1MB
        
        public System.IO.MemoryStream Get(int capacity = DEFAULT_CAPACITY)
        {
            if (_pool.TryTake(out var ms))
            {
                System.Threading.Interlocked.Decrement(ref _count);
                ms.SetLength(0);
                ms.Position = 0;
                return ms;
            }
            return new System.IO.MemoryStream(Math.Max(capacity, DEFAULT_CAPACITY));
        }
        
        public void Return(System.IO.MemoryStream ms)
        {
            if (ms == null) return;
            
            // Don't pool if too large
            if (ms.Capacity > MAX_CAPACITY)
            {
                ms.Dispose();
                return;
            }
            
            ms.SetLength(0);
            ms.Position = 0;
            
            if (_count < MAX_POOL_SIZE)
            {
                _pool.Add(ms);
                System.Threading.Interlocked.Increment(ref _count);
            }
            else
            {
                ms.Dispose();
            }
        }
        
        public void Clear()
        {
            while (_pool.TryTake(out var ms))
            {
                ms?.Dispose();
            }
            _count = 0;
        }
    }
}
