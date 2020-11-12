using DNEE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomEvents
{
    public static class Events
    {
        internal static readonly EventSource Source = new ("CustomEventsSource");

        public static DataOrigin DataOrigin { get; } = Source.Origin;

        public static EventName BeatmapObject { get; } = Source.Event(nameof(BeatmapObject));
        public static EventName BeatmapEvent { get; } = Source.Event(nameof(BeatmapEvent));
    }
}
