using CustomEvents.Internal;
using DNEE;
using DNEE.Utility;
using HarmonyLib;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CustomEvents.Patches._BeatmapObjectCallbackController
{
    [HarmonyPatch(typeof(BeatmapObjectCallbackController))]
    [HarmonyPatch(nameof(BeatmapObjectCallbackController.SendBeatmapEventDidTriggerEvent))]
    [HarmonyPatch(MethodType.Normal)]
    internal class SendBeatmapEventDidTriggerEvent
    {
        // This method is called *at* event execution time.
        // TODO: Should this then provide a BeatmapEventData impl with ICallaheadData.EventCallaheadAmount = 0?
        public static bool Prefix(BeatmapEventData beatmapEventData)
        {
            CEPlugin.Instance.Log.Debug($"In {nameof(BeatmapObjectCallbackController.SendBeatmapEventDidTriggerEvent)}");
            Events.Source.SendEvent(Events.BeatmapEvent, beatmapEventData);
            return false;
        }
    }

    [HarmonyPatch(typeof(BeatmapObjectCallbackController))]
    [HarmonyPatch(nameof(BeatmapObjectCallbackController.LateUpdate))]
    [HarmonyPatch(MethodType.Normal)]
    internal class LateUpdate
    {
        private static readonly FieldAccessor<BeatmapObjectCallbackController, BeatmapData>.Accessor _beatmapData
            = FieldAccessor<BeatmapObjectCallbackController, BeatmapData>.GetAccessor(nameof(_beatmapData));
        private static readonly FieldAccessor<BeatmapObjectCallbackController, float>.Accessor _spawningStartTime
            = FieldAccessor<BeatmapObjectCallbackController, float>.GetAccessor(nameof(_spawningStartTime));
        private static readonly FieldAccessor<BeatmapObjectCallbackController, int>.Accessor _nextEventIndex
            = FieldAccessor<BeatmapObjectCallbackController, int>.GetAccessor(nameof(_nextEventIndex));
        private static readonly FieldAccessor<BeatmapObjectCallbackController, IAudioTimeSource>.Accessor _audioTimeSource
            = FieldAccessor<BeatmapObjectCallbackController, IAudioTimeSource>.GetAccessor(nameof(_audioTimeSource));

        private static readonly FieldAccessor<BeatmapObjectCallbackController, Action?>.Accessor callbacksWereProcessed
            = FieldAccessor<BeatmapObjectCallbackController, Action?>.GetAccessor(nameof(BeatmapObjectCallbackController.callbacksForThisFrameWereProcessedEvent));

        private sealed class MoreFields
        {
            public sealed class EventCell
            {
                public int NextEventIdx;
            }

            private readonly Dictionary<float, int[]> ObjectCells = new();
            private readonly Dictionary<float, EventCell> EventCells = new();

            public int[] GetObjCell(BeatmapData data, float callahead)
            {
                if (!ObjectCells.TryGetValue(callahead, out var cell))
                    ObjectCells.Add(callahead, cell = new int[data.beatmapLinesData.Length]);
                return cell;
            }
            public EventCell GetEvtCell(float callahead)
            {
                if (!EventCells.TryGetValue(callahead, out var cell))
                    EventCells.Add(callahead, cell = new());
                return cell;
            }
        }

        private static readonly ConditionalWeakTable<BeatmapObjectCallbackController, MoreFields> moreFieldsHolder = new();

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
                for (int i = 0; i < beatmapData.beatmapLinesData.Length; i++)
                {
                    var objs = beatmapData.beatmapLinesData[i].beatmapObjectsData;
                    ref var nextInLine = ref cell[i];
                    while (nextInLine < objs.Length)
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
                    }
                    nextInLine++;
                }
            }

            // Handle event callaheads
            var evtCallaheads = CallaheadManager.EventCallaheads;
            foreach (var callahead in evtCallaheads)
            {
                // our cell is a reference type that we can modify in place as we work through everything
                var cell = moreFields.GetEvtCell(callahead);
                var eventDatas = beatmapData.beatmapEventData;
                while (cell.NextEventIdx < eventDatas.Length)
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
                var eventDatas = beatmapData.beatmapEventData;

                // handle on time events
                ref var nextIdx = ref _nextEventIndex(ref self);
                while (nextIdx < eventDatas.Length)
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
        private static readonly FieldAccessor<BeatmapObjectCallbackController, Action<BeatmapEventData>?>.Accessor beatmapEventDidTriggerEvent
            = FieldAccessor<BeatmapObjectCallbackController, Action<BeatmapEventData>?>.GetAccessor(nameof(BeatmapObjectCallbackController.beatmapEventDidTriggerEvent));

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

                CEPlugin.Instance.Log.Debug($"In {Events.BeatmapEvent} default handler with {eventData} (from {origin})");

                var callbacks = beatmapEventDidTriggerEvent(ref __instance);
                callbacks?.Invoke(eventData);

            }, (HandlerPriority)int.MinValue);

            __instance.gameObject.AddComponent<EventHandleManager>().Handle = handle;
        }
    }
}
