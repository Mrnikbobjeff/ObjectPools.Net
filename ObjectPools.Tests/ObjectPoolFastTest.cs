using System;
using Xunit;

namespace ObjectPools.Tests
{
    public class ObjectPoolFastTest
    {
        public class Sample { }
        [Theory]
        [InlineData(new int[] { 20,21,22,23,24,25,26,27 })]
        public void ObjectPool_AreSame(int[] values)
        {
            foreach(var value in values)
            {
                var op = new ObjectPool<Sample>(() => new Sample(), value);
                var op2 = new ObjectPoolFast<Sample>(() => new Sample(), value);
                for(int i = 0; i < value; i++)
                {
                    var sample = new Sample();
                    var index1 = op.Free(null);
                    var index2 = op2.FreeFast(null);
                    Assert.Equal(index1, index2);
                    op._items[i].Value = sample;
                    op2._items[i].Value = sample;

                    index1 = op.Free(null);
                    index2 = op2.FreeFasterSimplifiedAsmAligned(null);
                    Assert.Equal(index1, index2);
                }
            }
        }
    }
}
