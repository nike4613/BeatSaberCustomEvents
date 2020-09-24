using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomEvents.Internal
{
    internal class WrapperCallaheadData<T> : ICallaheadData<T>
    {
        public T Data { get; }

        public float EventCallaheadAmount { get; }

        public WrapperCallaheadData(T data, float callahead)
            => (Data, EventCallaheadAmount) = (data, callahead);
    }
}
