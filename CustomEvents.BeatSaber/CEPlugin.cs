using DNEE;
using HarmonyLib;
using IPA;
using IPA.Loader;
using IPA.Logging;
using SiraUtil.Zenject;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace CustomEvents
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", 
        Justification = "This class is instantiated by the mod loader.")]
    internal class CEPlugin
    {
        private static CEPlugin? instance;
        public static CEPlugin Instance => instance ?? throw new InvalidOperationException();

        public Logger Log { get; }
        public Harmony Harmony { get; }
        public PluginMetadata Metadata { get; }

        [Init]
        public CEPlugin(Logger log, PluginMetadata meta)
        {
            instance = this;
            Metadata = meta;
            Log = log;
            Harmony = new Harmony("com.cirr.danike.CustomEvents");
        }

        [OnEnable]
        public void OnEnable()
        {
            Log.Debug($"Enabling...");
            Harmony.PatchAll(Assembly.GetExecutingAssembly());
            Installer.RegisterAppInstaller<PluginInstaller>();
            Log.Debug($"Enabled {Metadata.Name} version {Metadata.Version}");

            var source = new EventSource("TestSource");
            source.SubscribeTo<BeatmapEventData>(Events.BeatmapEvent, (@event, data) =>
            {
                Log.Debug($"Before invoke default BeatmapEvent: {data}");
                @event.NextAndTryTransform(data, _ => _);
                Log.Debug("After invoke default BeatmapEvent");
            }, 0);
            source.SubscribeTo<ICallaheadData<BeatmapEventData>>(Events.BeatmapEvent, (@event, data) =>
            {
                if (data.HasValue)
                {
                    var cah = data.Value;
                    Log.Debug($"Callahead Event: {cah.Data} (ahead {cah.EventCallaheadAmount})");
                }
            }, (HandlerPriority)(-1));
            source.SubscribeTo<BeatmapObjectData>(Events.BeatmapObject, (@event, data) =>
            {
                Log.Debug($"Before invoke default BeatmapObject: {data}");
                @event.NextAndTryTransform(data, _ => _);
                Log.Debug("After invoke default BeatmapObject");
            }, 0);
            source.SubscribeTo<ICallaheadData<BeatmapObjectData>>(Events.BeatmapObject, (@event, data) =>
            {
                if (data.HasValue)
                {
                    var cah = data.Value;
                    Log.Debug($"Callahead Object: {cah.Data} (ahead {cah.EventCallaheadAmount})");
                }
            }, (HandlerPriority)(-1));
        }

        [OnDisable]
        public void OnDisable()
        {
            Installer.UnregisterAppInstaller<PluginInstaller>();
            Harmony.UnpatchAll(Harmony.Id);
            Log.Debug($"Disabled {Metadata.Name} version {Metadata.Version}");
        }
    }
}
