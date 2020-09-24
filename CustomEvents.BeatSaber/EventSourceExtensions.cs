using CustomEvents.Internal;
using DNEE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomEvents
{
    public static class EventSourceExtensions
    {
        public static EventHandle SubscribeToCallahead(this EventSource source, 
            in EventName @event, 
            DynamicEventHandler handler, 
            HandlerPriority priority,
            int callahead = 0, 
            bool callIfNoCallaheadInfo = false)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            RegisterCallaheadFor(@event, callahead);

            var handle = source.SubscribeTo<ICallaheadData<dynamic?>>(@event, (ev, data) =>
            {
                if (data.HasValue)
                {
                    var cai = data.Value;
                    if (cai.EventCallaheadAmount == callahead)
                    {
                        handler(new DynamicCallaheadEventProxy(ev, cai), cai);
                    }
                }
                else
                {
                    if (callIfNoCallaheadInfo)
                    {
                        handler(ev, ev.DynamicData);
                    }
                }
            }, priority);

            return RegisterUnsubscribeNotif(handle, callahead);
        }

        public static EventHandle SubscribeToCallahead<T>(this EventSource source, 
            in EventName @event, 
            NoReturnEventHandler<ICallaheadData<T>> handler, 
            HandlerPriority prioriy,
            int callahead = 0,
            bool callIfDynamicOnly = false)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            RegisterCallaheadFor(@event, callahead);

            var handle = source.SubscribeTo<ICallaheadData<T>>(@event, (ev, data) =>
            {
                if (data.HasValue)
                {
                    var cai = data.Value;
                    if (cai.EventCallaheadAmount == callahead)
                    {
                        handler(ev, data);
                    }
                }
                else
                {
                    if (callIfDynamicOnly)
                    {
                        handler(ev, data);
                    }
                }
            }, prioriy);

            return RegisterUnsubscribeNotif(handle, callahead);
        }

        public static EventHandle SubscribeToCallahead<T, TRet>(this EventSource source,
            in EventName @event,
            ReturnEventHandler<ICallaheadData<T>, TRet> handler,
            HandlerPriority prioriy,
            int callahead = 0,
            bool callIfDynamicOnly = false)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            RegisterCallaheadFor(@event, callahead);

            var handle = source.SubscribeTo<ICallaheadData<T>, TRet>(@event, (ev, data) =>
            {
                if (data.HasValue)
                {
                    var cai = data.Value;
                    if (cai.EventCallaheadAmount == callahead)
                    {
                        handler(ev, data);
                    }
                }
                else
                {
                    if (callIfDynamicOnly)
                    {
                        handler(ev, data);
                    }
                }
            }, prioriy);

            return RegisterUnsubscribeNotif(handle, callahead);
        }

        private static void RegisterCallaheadFor(in EventName @event, int callahead)
        {
            // TODO: somehow actually register a callahead for the event
        }

        private static EventHandle RegisterUnsubscribeNotif(EventHandle handle, int callahead)
            => handle.OnUnsubscribe(() => { /* TODO: unregister the callahead if there are no others */ });

        private sealed class DynamicCallaheadEventProxy : IEvent
        {
            private readonly IEvent @event;
            private readonly ICallaheadData<dynamic?> prevData;

            public DynamicCallaheadEventProxy(IEvent ev, ICallaheadData<dynamic?> data)
                => (@event, prevData) = (ev, data);

            public DataOrigin DataOrigin => @event.DataOrigin;

            public EventName EventName => @event.EventName;

            public dynamic? Result { get => @event.Result; set => @event.Result = value; }
            public bool AlwaysInvokeNext { get => @event.AlwaysInvokeNext; set => @event.AlwaysInvokeNext = value; }

            public IEnumerable<DataWithOrigin> DataHistory => @event.DataHistory;

            public EventResult Next(dynamic? data)
            {
                var obj = (object?)data;
                if (obj is ICallaheadData<object?> callahead && callahead.EventCallaheadAmount == prevData.EventCallaheadAmount)
                    return @event.Next(obj);
                else
                    return @event.Next(new WrapperCallaheadData<dynamic?>(obj, prevData.EventCallaheadAmount));
            }
        }
    }
}
