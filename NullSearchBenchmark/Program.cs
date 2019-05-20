using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ObjectPools;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;

namespace NullSearchBenchmark
{
    [MemoryDiagnoser]
    [DisassemblyDiagnoser()]
    public class Program
    {
        string field;
        public Program()
        {
            field = this.GetHashCode().ToString();
        }

        static int poolSize = 100;
        static int fillSize = 80;
        static ObjectPool<Program> op_small = new ObjectPool<Program>(() => new Program(), poolSize / 10);
        static ObjectPoolFast<Program> op2_small = new ObjectPoolFast<Program>(() => new Program(), poolSize / 10);


        static ObjectPool<Program> op = new ObjectPool<Program>(() => new Program(), poolSize);
        static ObjectPoolFast<Program> op2 = new ObjectPoolFast<Program>(() => new Program(), poolSize);


        static ObjectPool<Program> op_big = new ObjectPool<Program>(() => new Program(), poolSize * 10);
        static ObjectPoolFast<Program> op2_big = new ObjectPoolFast<Program>(() => new Program(), poolSize * 10);


        static ObjectPool<Program> op_huge = new ObjectPool<Program>(() => new Program(), poolSize * 50);
        static ObjectPoolFast<Program> op2_huge = new ObjectPoolFast<Program>(() => new Program(), poolSize * 50);
        #region Setup
        static Program()
        {
            for(int i = 0; i < poolSize * 50; i++)
            {
                var p = new Program();
                if( i < fillSize / 10)
                {
                    op_small._items[i].Value = p;
                    op2_small._items[i].Value = p;
                }
                if (i < fillSize)
                {
                    op._items[i].Value = p;
                    op2._items[i].Value = p;
                }
                if (i < fillSize * 10)
                {
                    op_big._items[i].Value = p;
                    op2_big._items[i].Value = p;
                }
                if (i < fillSize * 50)
                {
                    op_huge._items[i].Value = p;
                    op2_huge._items[i].Value = p;
                }
            }
        }
        #endregion
        #region Benchmarks

        [MethodImpl(MethodImplOptions.NoInlining)]
        //[Benchmark]
        public int NullSearch()
        {
            return op.Free(null);
        }
        /*

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchOriginalSmall()
        {
            return op_small.Free(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchOriginal()
        {
            return op.Free(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchOriginalBig()
        {
            return op_big.Free(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchOriginalHuge()
        {
            return op_huge.Free(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplifiedSmall()
        {
            return op2_small.FreeFasterSimplifiedAsm(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplified()
        {
            return op2.FreeFasterSimplifiedAsm(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplifiedBig()
        {
            return op2_big.FreeFasterSimplifiedAsm(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplifiedHuge()
        {
            return op2_huge.FreeFasterSimplifiedAsm(null);
        }
*/

        [MethodImpl(MethodImplOptions.NoInlining)]
        //[Benchmark]
        public int NullSearchFasterSimplifiedAlignedSmall()
        {
            return op2_small.FreeFasterSimplifiedAsmAlignedNonTemporal(null);
        }


        [BenchmarkCategory("Temporal")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplifiedAligned()
        {
            return op2.FreeFasterSimplifiedAsmAlignedNonTemporal(null);
        }


        [BenchmarkCategory("Temporal")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplifiedAlignedBig()
        {
            return op2_big.FreeFasterSimplifiedAsmAlignedNonTemporal(null);
        }


        [BenchmarkCategory("Temporal")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplifiedAlignedHuge()
        {
            return op2_huge.FreeFasterSimplifiedAsmAlignedNonTemporal(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        //[Benchmark]
        public int NullSearchFasterSimplifiedAlignedUnrolledSmall()
        {
            return op2_small.FreeFasterSimplifiedAsmAlignedNonTemporalUnrolled(null);
        }

        [BenchmarkCategory("Unrolled")]

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplifiedAlignedUnrolled()
        {
            return op2.FreeFasterSimplifiedAsmAlignedNonTemporalUnrolled(null);
        }

        [BenchmarkCategory("Unrolled"), Benchmark(Baseline = true)]

        [MethodImpl(MethodImplOptions.NoInlining)]
        public int NullSearchFasterSimplifiedAlignedUnrolledBig()
        {
            return op2_big.FreeFasterSimplifiedAsmAlignedNonTemporalUnrolled(null);
        }

        [BenchmarkCategory("Unrolled")]

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFasterSimplifiedAlignedUnrolledHuge()
        {
            return op2_huge.FreeFasterSimplifiedAsmAlignedNonTemporalUnrolled(null);
        }


        #endregion
        #region Allocation
        [MethodImpl(MethodImplOptions.NoInlining)]
        //[Benchmark]
        public void ObjectPoolAllocation()
        {
            new ObjectPool<Program>(GetProgram);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        Program GetProgram()
        {
            return new Program();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //[Benchmark]
        public void ObjectPoolFastAllocation()
        {
            new ObjectPoolFast<Program>(GetProgram);
        }
        #endregion

        static void Main(string[] args)
        {
            //new Program().NullSearchFasterSimplifiedAlignedUnrolled();
            BenchmarkRunner.Run<Program>();
        }
    }
}