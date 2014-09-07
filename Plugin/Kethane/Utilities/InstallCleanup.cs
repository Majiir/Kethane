using System.IO;
using UnityEngine;

namespace Kethane.Utilities
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class InstallCleanup: MonoBehaviour
    {
        public void Start()
        {
            File.Delete(KSPUtil.ApplicationRootPath + "GameData" + Path.DirectorySeparatorChar + "Kethane" + Path.DirectorySeparatorChar + "Plugins" + Path.DirectorySeparatorChar + "MMI_Kethane.dll");
            File.Delete(KSPUtil.ApplicationRootPath + "GameData" + Path.DirectorySeparatorChar + "Kethane" + Path.DirectorySeparatorChar + "Plugins" + Path.DirectorySeparatorChar + "KethaneToolbar.dll");
        }
    }
}
