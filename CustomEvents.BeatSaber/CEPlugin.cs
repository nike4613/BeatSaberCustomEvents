using HarmonyLib;
using IPA;
using IPA.Loader;
using IPA.Logging;
using SiraUtil.Zenject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CustomEvents
{
    [Plugin(RuntimeOptions.SingleStartInit)]
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
            Harmony.PatchAll(Assembly.GetCallingAssembly());
            Installer.RegisterAppInstaller<PluginInstaller>();
            Log.Debug($"Enabled {Metadata.Name} version {Metadata.Version}");
        }

        [OnDisable]
        public void OnDisable()
        {
            Installer.UnregisterAppInstaller<PluginInstaller>();
            Harmony.UnpatchAll(Harmony.Id);
        }
    }
}
