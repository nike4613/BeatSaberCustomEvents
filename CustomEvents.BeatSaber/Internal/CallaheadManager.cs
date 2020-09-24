using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CustomEvents.Internal
{
    internal static class CallaheadManager
    {
        private sealed class CallaheadRefcountCell
        {
            public int Refcount;
        }

        //                                      callahead -> refcount
        private static readonly ConcurrentDictionary<float, CallaheadRefcountCell> objectCallaheads = new();
        private static readonly ConcurrentDictionary<float, CallaheadRefcountCell> eventCallaheads = new();

        public static IEnumerable<float> ObjectCallaheads => GetCallaheads(objectCallaheads);
        public static IEnumerable<float> EventCallaheads => GetCallaheads(eventCallaheads);

        public static void AddObjectCallahead(float callahead) => AddCallahead(objectCallaheads, callahead);
        public static void RemoveObjectCallahead(float callahead) => RemoveCallahead(objectCallaheads, callahead);
        public static void AddEventCallahead(float callahead) => AddCallahead(eventCallaheads, callahead);
        public static void RemoveEventCallahead(float callahead) => RemoveCallahead(eventCallaheads, callahead);


        private static IEnumerable<float> GetCallaheads(ConcurrentDictionary<float, CallaheadRefcountCell> dict)
            => dict.ToArray().Select(kvp => kvp.Key);

        private static void AddCallahead(ConcurrentDictionary<float, CallaheadRefcountCell> dict, float callahead)
        {
            CEPlugin.Instance.Log.Debug($"Registered callahead {callahead}");

            var cell = dict.GetOrAdd(callahead, f => new());
            Interlocked.Increment(ref cell.Refcount);
        }

        private static void RemoveCallahead(ConcurrentDictionary<float, CallaheadRefcountCell> dict, float callahead)
        {
            CEPlugin.Instance.Log.Debug($"Removed callahead ref {callahead}");

            if (dict.TryGetValue(callahead, out var cell))
            {
                if (Interlocked.Decrement(ref cell.Refcount) <= 0)
                {
                    if (dict.TryRemove(callahead, out cell) && cell.Refcount > 0)
                    {
                        var newCell = dict.GetOrAdd(callahead, cell);
                        if (newCell != cell)
                            Interlocked.Add(ref newCell.Refcount, cell.Refcount);
                    }
                }
            }
        }
    }
}
