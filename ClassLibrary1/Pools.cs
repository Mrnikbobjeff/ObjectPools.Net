using System;
using System.Runtime.CompilerServices;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            var nullVector = Vector256<long>.Zero;
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
                        for (int x = i; x <= i + Vector256<long>.Count; x++)
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

        public unsafe int FreeFasterSimplifiedAsm(T obj)
        {
            var items = _items;
            var length = items.Length;
            var nullVector = Vector256<long>.Zero;
            fixed (long* addrPtr = itemsRef)
            {
                int i = 0;
                for (; i + Vector256<long>.Count <= length; i += Vector256<long>.Count)
                {
                    var v2 = Avx2.LoadVector256(addrPtr + i);
                    var temp = Avx2.CompareEqual(v2, nullVector);
                    var maskResult = Avx.MoveMask(temp.AsDouble());
                    if (maskResult != 0) // Null found
                    {
                        goto nullOrFound;
                    }
                }
            nullOrFound:
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe int IsNull(long* source, long length)
        {
            for (int i = 0; i < length; ++i)
            {
                if (source[i] == 0)
                    return i;
            }
            return -1;
        }

        public unsafe int FreeFasterSimplifiedAsmAligned(T obj)
        {
            var items = _items;
            var length = items.Length;
            var nullVector = Vector256<long>.Zero;
            fixed (long* addrPtr = itemsRef)
            {
                var aligned = (long*)(((ulong)addrPtr + 31UL) & ~31UL);
                var pos = (int)(aligned - addrPtr);
                for (int w = 0; w < pos; w++)
                    if (addrPtr[w] == 0)
                        return w;

                int i = 0;
                for (; i + Vector256<long>.Count <= length; i += Vector256<long>.Count)
                {
                    var v2 = Avx2.LoadAlignedVector256(aligned + i);
                    var temp = Avx2.CompareEqual(v2, nullVector);
                    var maskResult = Avx.MoveMask(temp.AsDouble());
                    if (maskResult != 0) // Null found
                    {
                        goto nullOrFound;
                    }
                }
            nullOrFound:
                for (int x = i + pos; x < length; x++)
                {
                    if (addrPtr[x] == 0)
                    {
                        return x;
                    }
                }
            }
            return -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public unsafe int FreeFasterSimplifiedAsmAlignedNonTemporal(T obj)
        {
            var items = _items;
            var length = items.Length;
            var nullVector = Vector256<long>.Zero;
            fixed (long* addrPtr = itemsRef)
            {
                var aligned = (long*)(((ulong)addrPtr + 31UL) & ~31UL);
                var pos = (aligned - addrPtr);
                for (int w = 0; w < pos; w++)
                    if (addrPtr[w] == 0)
                    {
                        items[w].Value = obj;
                        return w;
                    }

                int i = 0;
                int vSizeLong = 4;
                int bSizeLong = 16;
                for (; i <= length - Vector256<long>.Count; i += Vector256<long>.Count)
                {
                    var v2 = Avx2.LoadAlignedVector256NonTemporal(aligned + i);
                    var temp = Avx2.CompareEqual(v2, nullVector);
                    var maskResult = Avx.MoveMask(temp.AsDouble());
                    if (maskResult != 0) // Null found
                    {
                        goto nullOrFound;
                    }
                }
            nullOrFound:
                for (int x = i; x < length; x++)
                {
                    if (items[x].Value == null)
                    {
                        return x;
                    }
                }
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public unsafe int FreeFasterSimplifiedAsmAlignedNonTemporalUnrolled(T obj)
        {
            var items = _items;
            var length = items.Length;
            var nullVector = Vector256<long>.Zero;
            fixed (long* addrPtr = itemsRef)
            {
                var aligned = (long*)(((ulong)addrPtr + 31UL) & ~31UL);
                var pos = (int)(aligned - addrPtr);
                for (int w = 0; w < pos; w++)
                    if (addrPtr[w] == 0)
                    {
                        items[w].Value = obj;
                        return w;
                    }

                int i = 0;
                int vSizeLong = 4;
                int bSizeLong = 16;
                var lengthWithoutBlockSize = length - bSizeLong;
                for (; i <= lengthWithoutBlockSize; i += bSizeLong)
                {
                    var v2i = i + vSizeLong;
                    var v3i = i + 2 * vSizeLong;
                    var v4i = i + 3 * vSizeLong;
                    var v21 = Avx2.LoadAlignedVector256NonTemporal(aligned + i);
                    var v22 = Avx2.LoadAlignedVector256NonTemporal(aligned + v2i);
                    var v23 = Avx2.LoadAlignedVector256NonTemporal(aligned + v3i);
                    var v24 = Avx2.LoadAlignedVector256NonTemporal(aligned + v4i);
                    var temp1 = Avx2.CompareEqual(v21, nullVector).AsDouble();
                    var m1 = Avx.MoveMask(temp1);
                    var temp2 = Avx2.CompareEqual(v22, nullVector).AsDouble();
                    var m2 = Avx.MoveMask(temp2);
                    var temp3 = Avx2.CompareEqual(v23, nullVector).AsDouble();
                    var m3 = Avx.MoveMask(temp3);
                    var temp4 = Avx2.CompareEqual(v24, nullVector).AsDouble();
                    var m4 = Avx.MoveMask(temp4);
                    Vector128<int> maskVector = Vector128.Create(m1, m2, m3, m4);
                    if (maskVector.Equals(Vector128<int>.Zero))
                        continue;

                    {
                        if (maskVector.GetLower().Equals(Vector64<int>.Zero)) //Not in first 8 refs
                        {
                            if (maskVector.GetUpper().ToScalar() == 0) // 8-12 refs also set
                                i += 12;
                            else
                                i += 8;

                        }
                        else
                            if (maskVector.GetLower().ToScalar() != 0) //2nd 4 references 4-8
                            i += 4;
                        goto nullOrFound;
                    }
                }
                var unvectorLength = length - Vector256<long>.Count;
                for (; i <= unvectorLength; i += Vector256<long>.Count)
                {
                    var v2 = Avx2.LoadAlignedVector256NonTemporal(aligned + i);
                    var temp = Avx2.CompareEqual(v2, nullVector);
                    var maskResult = Avx.MoveMask(temp.AsDouble());
                    if (maskResult != 0) // Null found
                    {
                        goto nullOrFound;
                    }
                }
            nullOrFound:
                for (int x = i + pos; x < length; x++)
                {
                    if (addrPtr[x] == 0)
                    {
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
                for (; i + Vector256<long>.Count <= length; i += Vector256<long>.Count)
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