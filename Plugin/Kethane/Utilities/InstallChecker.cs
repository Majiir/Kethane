using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Kethane.Utilities
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class InstallChecker : MonoBehaviour
    {
        protected void Start()
        {
            var assemblies = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name).Where(a => a.url != "Kethane/Plugins");
            if (assemblies.Any())
            {
                var badPaths = assemblies.Select(a => a.path).Select(p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar)));
                PopupDialog.SpawnPopupDialog("Incorrect Kethane Installation", "Kethane has been installed incorrectly and will not function properly. All Kethane files should be located in KSP/GameData/Kethane. Do not move any files from inside the Kethane folder.\n\nIncorrect path(s):\n" + String.Join("\n", badPaths.ToArray()), "OK", false, HighLogic.Skin);
            }
        }
    }
}
