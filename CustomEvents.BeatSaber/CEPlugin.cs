using HarmonyLib;
using IPA;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [Init]
        public CEPlugin(Logger log)
        {
            instance = this;
            Log = log;
            Harmony = new Harmony("com.cirr.danike.CustomEvents");
        }

        [OnEnable]
        public void OnEnable()
        {

        }

        [OnDisable]
        public void OnDisable()
        {

        }
    }
}
