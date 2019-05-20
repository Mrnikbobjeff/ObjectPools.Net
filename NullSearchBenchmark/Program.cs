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
    [DisassemblyDiagnoser]
    public class Program
    {
        public Program()
        {

        }

        static int poolSize = 100;
        static int fillSize = 80;
        static ObjectPool<Program> op = new ObjectPool<Program>(() => new Program(), poolSize);
        static ObjectPoolFast<Program> op2 = new ObjectPoolFast<Program>(() => new Program(), poolSize);

        static Program()
        {
            for (int i = 0; i < fillSize; i++)
                op._items[i].Value = new Program();

            for (int i = 0; i < fillSize; i++)
                op2._items[i].Value = new Program();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearch()
        {
            return op.Free(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFast()
        {
            return op2.FreeFast(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchFaster()
        {
            return op2.FreeFaster(null);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [Benchmark]
        public int NullSearchLazy()
        {
            return op2.FreeLazyCast(null);
        }

        static void Main(string[] args)
        {
            BenchmarkRunner.Run<Program>();
        }
    }
}