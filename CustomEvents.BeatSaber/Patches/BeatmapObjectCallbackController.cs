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
    internal class SendBeatmapEventDidTriggetEvent
    {
        public static bool Prefix(BeatmapObjectCallbackController __instance, BeatmapEventData beatmapEventData)
        {
            CEPlugin.Instance.Log.Debug($"In {nameof(BeatmapObjectCallbackController.SendBeatmapEventDidTriggerEvent)}");
            Events.Source.SendEvent(Events.BeatmapEvent, beatmapEventData);
            return false;
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
