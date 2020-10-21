using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace CustomEvents
{
    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
        Justification = "This class is instantiated by SiraUtil/Zenject.")]
    internal class PluginInstaller : InstallerBase
    {
        public override void InstallBindings()
        {
            Container.BindInstance(CEPlugin.Instance);
        }
    }
}
