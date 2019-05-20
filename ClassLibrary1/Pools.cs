using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;

namespace ObjectPools
{
    public class ObjectPool<T> where T : class
    {
        public struct Element
        {
            public T Value;
        }

        public readonly Element[] _items;
        private readonly Func<T> _factory;

        public ObjectPool(Func<T> factory)
            : this(factory, Environment.ProcessorCount * 2)
        { }

        public ObjectPool(Func<T> factory, int size)
        {
            _factory = factory;
            _items = new Element[size];
        }

        private T CreateInstance()
        {
            var inst = _factory();
            return inst;
        }
        internal T Allocate()
        {
            var items = _items;
            T inst;

            for (int i = 0; i < items.Length; i++)
            {
                inst = items[i].Value;
                if (inst != null)
                {
                    if (inst == Interlocked.CompareExchange(ref items[i].Value, null, inst))
                    {
                        goto gotInstance;
                    }
                }
            }

            inst = CreateInstance();
        gotInstance:

            return inst;
        }
        public int Free(T obj)
        {
            var items = _items;
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].Value == null)
                {
                    items[i].Value = obj;
                    return i;
                }
            }
            return -1;
        }

    }
    public class ObjectPoolFast<T> where T : class
    {
        public struct Element
        {
            public T Value;
        }

        public readonly Element[] _items;
        private readonly long[] itemsRef;
        private readonly Func<T> _factory;

        static readonly long[] nullRepeater = new long[] { 0, 0, 0, 0 };

        public ObjectPoolFast(Func<T> factory)
            : this(factory, Environment.ProcessorCount * 2)
        { }

        public ObjectPoolFast(Func<T> factory, int size)
        {
            _factory = factory;
            _items = new Element[size];
            itemsRef = ReinterpretCast<Element[], long[]>(_items);
        }

        private T CreateInstance()
        {
            var inst = _factory();
            return inst;
        }
        internal T Allocate()
        {
            var items = _items;
            T inst;

            for (int i = 0; i < items.Length; i++)
            {
                inst = items[i].Value;
                if (inst != null)
                {
                    if (inst == Interlocked.CompareExchange(ref items[i].Value, null, inst))
                    {
                        goto gotInstance;
                    }
                }
            }

            inst = CreateInstance();
        gotInstance:

            return inst;
        }

        static unsafe TDest ReinterpretCast<TSource, TDest>(TSource source)
        {
            var sourceRef = __makeref(source);
            var dest = default(TDest);
            var destRef = __makeref(dest);
            *(IntPtr*)&destRef = *(IntPtr*)&sourceRef;
            return __refvalue(destRef, TDest);
        }

        public unsafe int FreeFast(T obj)
        {
            var items = _items;
            var length = items.Length;
            fixed (long* nullVector = nullRepeater)
            fixed (long* addrPtr = itemsRef)
            {
                var v1 = Avx2.LoadVector256(nullVector);
                int i = 0;
                for (i = 0; i + 4 < length; i += Vector256<long>.Count)
                {
                    var v2 = Avx2.LoadVector256(addrPtr + i);
                    var temp = Avx2.CompareEqual(v2, v1);
                    var maskResult = Avx.MoveMask(temp.AsDouble());
                    if (maskResult != 0) // Null found
                    {
                        for (int x = i; x < i + Vector256<long>.Count; x++)
                        {
                            if (items[x].Value == null)
                            {
                                items[x].Value = obj;
                                return x;
                            }
                        }
                    }
                }
                for (int x = i; x < length; x++)
                {
                    if (items[x].Value == null)
                    {
                        items[x].Value = obj;
                        return x;
                    }
                }
            }
            return -1;
        }
        public unsafe int FreeFaster(T obj)
        {
            var items = _items;
            var length = items.Length;
            var nullVector = new Vector256<long>();
            fixed (long* addrPtr = itemsRef)
            {
                int i = 0;
                for (i = 0; i + Vector256<long>.Count < length; i += Vector256<long>.Count)
                {
                    var v2 = Avx2.LoadVector256(addrPtr + i);
                    var temp = Avx2.CompareEqual(v2, nullVector);
                    var maskResult = Avx.MoveMask(temp.AsDouble());
                    if (maskResult != 0) // Null found
                    {
                        for (int x = i; x < i + Vector256<long>.Count; x++)
                        {
                            if (items[x].Value == null)
                            {
                                items[x].Value = obj;
                                return x;
                            }
                        }
                    }
                }
                for (int x = i; x < length; x++)
                {
                    if (items[x].Value == null)
                    {
                        items[x].Value = obj;
                        return x;
                    }
                }
            }
            return -1;
        }
        public unsafe int FreeLazyCast(T obj)
        {
            var items = _items;
            var length = items.Length;
            var nullVector = new Vector256<long>();
            var pinnable = ReinterpretCast<Element[], long[]>(_items);
            fixed (long* addrPtr = pinnable)
            {
                int i = 0;
                for (i = 0; i + Vector256<long>.Count < length; i += Vector256<long>.Count)
                {
                    var v2 = Avx2.LoadVector256(addrPtr + i);
                    var temp = Avx2.CompareEqual(v2, nullVector);
                    var maskResult = Avx.MoveMask(temp.AsDouble());
                    if (maskResult != 0) // Null found
                    {
                        for (int x = i; x < i + Vector256<long>.Count; x++)
                        {
                            if (items[x].Value == null)
                            {
                                items[x].Value = obj;
                                return x;
                            }
                        }
                    }
                }
                for (int x = i; x < length; x++)
                {
                    if (items[x].Value == null)
                    {
                        items[x].Value = obj;
                        return x;
                    }
                }
            }
            return -1;
        }
    }
}