using CustomEvents.Internal;
using DNEE;
using DNEE.Utility;
using HarmonyLib;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomEvents.Patches._BeatmapObjectCallbackController
{
    using static __Type;

    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "The __ is so that it doesn't conflict with System.Type and other similar types.")]
    internal static class __Type
    {
        internal static readonly FieldAccessor<BeatmapObjectCallbackController, IReadonlyBeatmapData>.Accessor _beatmapData
            = FieldAccessor<BeatmapObjectCallbackController, IReadonlyBeatmapData>.GetAccessor(nameof(_beatmapData));
        internal static readonly FieldAccessor<BeatmapObjectCallbackController, float>.Accessor _spawningStartTime
            = FieldAccessor<BeatmapObjectCallbackController, float>.GetAccessor(nameof(_spawningStartTime));
        internal static readonly FieldAccessor<BeatmapObjectCallbackController, int>.Accessor _nextEventIndex
            = FieldAccessor<BeatmapObjectCallbackController, int>.GetAccessor(nameof(_nextEventIndex));
        internal static readonly FieldAccessor<BeatmapObjectCallbackController, IAudioTimeSource>.Accessor _audioTimeSource
            = FieldAccessor<BeatmapObjectCallbackController, IAudioTimeSource>.GetAccessor(nameof(_audioTimeSource));

        internal static readonly FieldAccessor<BeatmapObjectCallbackController, Action?>.Accessor callbacksWereProcessed
            = FieldAccessor<BeatmapObjectCallbackController, Action?>.GetAccessor(nameof(BeatmapObjectCallbackController.callbacksForThisFrameWereProcessedEvent));
        internal static readonly FieldAccessor<BeatmapObjectCallbackController, Action<BeatmapEventData>?>.Accessor beatmapEventDidTriggerEvent
            = FieldAccessor<BeatmapObjectCallbackController, Action<BeatmapEventData>?>.GetAccessor(nameof(BeatmapObjectCallbackController.beatmapEventDidTriggerEvent));

        internal static readonly ConditionalWeakTable<BeatmapObjectCallbackController, MoreFields> moreFieldsHolder = new();
    }

    internal sealed class MoreFields
    {
        public sealed class EventCell
        {
            public int NextEventIdx;
        }

        private readonly Dictionary<float, int[]> ObjectCells = new();
        private readonly Dictionary<float, EventCell> EventCells = new();

        public void Clear()
        {
            ObjectCells.Clear();
            EventCells.Clear();
        }

        public int[] GetObjCell(IReadonlyBeatmapData data, float callahead)
        {
            if (!ObjectCells.TryGetValue(callahead, out var cell))
                ObjectCells.Add(callahead, cell = new int[data.beatmapLinesData.Count]);
            return cell;
        }
        public EventCell GetEvtCell(float callahead)
        {
            if (!EventCells.TryGetValue(callahead, out var cell))
                EventCells.Add(callahead, cell = new());
            return cell;
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectCallbackController))]
    [HarmonyPatch(nameof(BeatmapObjectCallbackController.SendBeatmapEventDidTriggerEvent))]
    [HarmonyPatch(MethodType.Normal)]
    internal class SendBeatmapEventDidTriggerEvent
    {
        // This method is called *at* event execution time.
        // TODO: Should this then provide a BeatmapEventData impl with ICallaheadData.EventCallaheadAmount = 0?
        public static bool Prefix(BeatmapEventData beatmapEventData)
        {
            //CEPlugin.Instance.Log.Debug($"In {nameof(BeatmapObjectCallbackController.SendBeatmapEventDidTriggerEvent)}");
            Events.Source.SendEvent(Events.BeatmapEvent, beatmapEventData);
            return false;
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectCallbackController))]
    [HarmonyPatch(nameof(BeatmapObjectCallbackController.SetNewBeatmapData))]
    [HarmonyPatch(MethodType.Normal)]
    internal class SetNewBeatmapData
    {
        public static void Postfix(BeatmapObjectCallbackController __instance)
        {
            var moreFields = moreFieldsHolder.GetValue(__instance, oc => new());

            moreFields.Clear();
        }
    }

    internal class CustomEventCallbackData : BeatmapObjectCallbackController.BeatmapEventCallbackData, IDisposable
    {
        private readonly EventHandle handle;

        public CustomEventCallbackData(BeatmapObjectCallbackController.BeatmapEventCallback callback, float aheadTime, EventHandle handle)
            : base(callback, aheadTime)
        {
            this.handle = handle;
        }

        public void Dispose()
        {
            handle.Dispose();
            GC.SuppressFinalize(this);
        }

        ~CustomEventCallbackData()
        {
            handle.Dispose();
        }
    }

    internal class CustomObjectCallbackData : BeatmapObjectCallbackController.BeatmapObjectCallbackData, IDisposable
    {
        private readonly EventHandle handle;

        public CustomObjectCallbackData(BeatmapObjectCallbackController.BeatmapObjectCallback callback, float aheadTime, EventHandle handle)
            : base(callback, aheadTime, 0)
        {
            this.handle = handle;
        }

        public void Dispose()
        {
            handle.Dispose();
            GC.SuppressFinalize(this);
        }

        ~CustomObjectCallbackData()
        {
            handle.Dispose();
        }
    }

    // TODO: somehow handle dynamic changes in the callahead time???
    [HarmonyPatch(typeof(BeatmapObjectCallbackController))]
    [HarmonyPatch(MethodType.Normal)]
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "The leading underscore indicates that it is not an actual member, but rather several.")]
    internal class _CallbackFunctions
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(BeatmapObjectCallbackController.AddBeatmapEventCallback))]
        public static bool AddBeatmapEventCallbackPre(//BeatmapObjectCallbackController __instance,
            out BeatmapObjectCallbackController.BeatmapEventCallbackData __result,
            BeatmapObjectCallbackController.BeatmapEventCallback callback,
            float aheadTime)
        {
            var handle = Events.Source.SubscribeToCallahead<BeatmapEventData>(Events.BeatmapEvent, (ev, data) =>
                {
                    if (!data.HasValue) return;

                    var cad = data.Value;

                    callback(cad.Data);
                }, 
                (HandlerPriority)int.MinValue,
                callahead: aheadTime);

            __result = new CustomEventCallbackData(callback, aheadTime, handle);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(BeatmapObjectCallbackController.RemoveBeatmapEventCallback))]
        public static bool RemoveBeatmapEventCallbackPre(BeatmapObjectCallbackController.BeatmapEventCallbackData callbackData)
        {
            if (callbackData is not CustomEventCallbackData ced)
            {
                CEPlugin.Instance.Log.Warn($"RemoveBeatmapEventCallback given unknown event data type {callbackData.GetType()}");
                return false;
            }

            ced.Dispose();

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(BeatmapObjectCallbackController.AddBeatmapObjectCallback))]
        public static bool AddBeatmapObjectCallbackPre(//BeatmapObjectCallbackController __instance,
            out BeatmapObjectCallbackController.BeatmapObjectCallbackData __result,
            BeatmapObjectCallbackController.BeatmapObjectCallback callback,
            float aheadTime)
        {
            var handle = Events.Source.SubscribeToCallahead<BeatmapObjectData>(Events.BeatmapObject, (ev, data) =>
                {
                    if (!data.HasValue) return;

                    var cad = data.Value;

                    callback(cad.Data);
                },
                (HandlerPriority)int.MinValue,
                callahead: aheadTime);

            __result = new CustomObjectCallbackData(callback, aheadTime, handle);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(BeatmapObjectCallbackController.RemoveBeatmapObjectCallback))]
        public static bool RemoveBeatmapObjectCallbackPre(BeatmapObjectCallbackController.BeatmapObjectCallbackData callbackData)
        {
            if (callbackData is not CustomObjectCallbackData ced)
            {
                CEPlugin.Instance.Log.Warn($"RemoveBeatmapObjectCallback given unknown event data type {callbackData.GetType()}");
                return false;
            }

            ced.Dispose();

            return false;
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectCallbackController))]
    [HarmonyPatch(nameof(BeatmapObjectCallbackController.LateUpdate))]
    [HarmonyPatch(MethodType.Normal)]
    internal class LateUpdate
    {

        public static bool Prefix(BeatmapObjectCallbackController __instance)
        {
            Impl(__instance);
            return false;
        }

        // This implementation is partially copied from the game, hoping to preserve behaviour while using a completely
        //   different event dispatcher
        private static void Impl(BeatmapObjectCallbackController self)
        {
            var moreFields = moreFieldsHolder.GetValue(self, oc => new());

            var beatmapData = _beatmapData(ref self);
            if (beatmapData == null) return;

            var timeSource = _audioTimeSource(ref self);
            var spawningStartTime = _spawningStartTime(ref self);

            // Handle objects
            var objCallaheads = CallaheadManager.ObjectCallaheads;
            foreach (var callahead in objCallaheads)
            {
                // our cell is the array that stores the nextInLine data for all the lines (it is a single array)
                var cell = moreFields.GetObjCell(beatmapData, callahead);
                for (int i = 0; i < beatmapData.beatmapLinesData.Count; i++)
                {
                    var objs = beatmapData.beatmapLinesData[i].beatmapObjectsData;
                    ref var nextInLine = ref cell[i];
                    while (nextInLine < objs.Count)
                    {
                        var data = objs[nextInLine];
                        if (data.time - callahead >= timeSource.songTime)
                            break;

                        if (data.time >= spawningStartTime)
                        {
                            // TODO: actually preserve exact time ordering, even between lines
                            // TODO: change out the actual object data for a derivative that implements ICallaheadData (with internal setters ofc)
                            Events.Source.SendEvent<ICallaheadData<BeatmapObjectData>>(Events.BeatmapObject, new WrapperCallaheadData<BeatmapObjectData>(data, callahead));
                        }

                        nextInLine++;
                    }
                }
            }

            // Handle event callaheads
            var evtCallaheads = CallaheadManager.EventCallaheads;
            foreach (var callahead in evtCallaheads)
            {
                // our cell is a reference type that we can modify in place as we work through everything
                var cell = moreFields.GetEvtCell(callahead);
                var eventDatas = beatmapData.beatmapEventsData;
                while (cell.NextEventIdx < eventDatas.Count)
                {
                    var data = eventDatas[cell.NextEventIdx];
                    if (data.time - callahead >= timeSource.songTime)
                        break;

                    // TODO: change out the actual event data for a derivative that implements ICallaheadData
                    Events.Source.SendEvent<ICallaheadData<BeatmapEventData>>(Events.BeatmapEvent, new WrapperCallaheadData<BeatmapEventData>(data, callahead));

                    cell.NextEventIdx++;
                }
            }

            {
                var eventDatas = beatmapData.beatmapEventsData;

                // handle on time events
                ref var nextIdx = ref _nextEventIndex(ref self);
                while (nextIdx < eventDatas.Count)
                {
                    var data = eventDatas[nextIdx];
                    if (data.time >= timeSource.songTime)
                        break;

                    // TODO: re: should this be a callahead with time 0?
                    self.SendBeatmapEventDidTriggerEvent(data);

                    nextIdx++;
                }
            }

            callbacksWereProcessed(ref self)?.Invoke();
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectCallbackController))]
    [HarmonyPatch(nameof(BeatmapObjectCallbackController.Start))]
    [HarmonyPatch(MethodType.Normal)]
    internal class Start
    {

        [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
            Justification = "This class is instantiated by Unity.")]
        private class EventHandleManager : MonoBehaviour
        {
            public EventHandle Handle { get; set; }
            public void OnDestroy()
            {
                if (Handle.IsValid)
                {
                    CEPlugin.Instance.Log.Debug($"Unsubscribing event handler");
                    Handle.Dispose();
                    Handle = default;
                }
            }
        }

        public static void Prefix(BeatmapObjectCallbackController __instance)
        {
            CEPlugin.Instance.Log.Debug($"In {nameof(BeatmapObjectCallbackController.Start)}");

            var handle = Events.Source.SubscribeTo<BeatmapEventData>(Events.BeatmapEvent, (@event, data) =>
            {
                if (!__instance.isActiveAndEnabled)
                    return;

                DataOrigin origin = @event.DataOrigin;
                BeatmapEventData eventData;

                if (data.HasValue)
                {
                    eventData = data.Value;
                }
                else if(@event.DynamicData is ICallaheadData<BeatmapEventData> cah)
                {
                    if (cah.EventCallaheadAmount == 0f)
                    {
                        eventData = cah.Data;
                    }
                    else
                    {
                        return; // ignore other callaheads
                    }
                }
                else
                {
                    var edata = @event.DataHistory.Where(d => d.IsTyped).Select(d => Maybe.Some(d)).FirstOrDefault();
                    if (!edata.HasValue)
                    {
                        CEPlugin.Instance.Log.Warn($"Default handler for {Events.BeatmapEvent} got unknown data type");
                        return;
                    }
                    origin = edata.Value.Origin;
                    eventData = edata.Value.TypedData;
                }

                //CEPlugin.Instance.Log.Debug($"In {Events.BeatmapEvent} default handler with {eventData} (from {origin})");

                var callbacks = beatmapEventDidTriggerEvent(ref __instance);
                callbacks?.Invoke(eventData);

            }, (HandlerPriority)int.MinValue);

            __instance.gameObject.AddComponent<EventHandleManager>().Handle = handle;
        }
    }
}
