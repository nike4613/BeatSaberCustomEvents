using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace CustomEvents
{
    internal class PluginInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindInstance(CEPlugin.Instance);
        }
    }
}
