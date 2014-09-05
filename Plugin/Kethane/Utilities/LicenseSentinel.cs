using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Kethane.Utilities
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class LicenseSentinel : MonoBehaviour
    {
        protected void Start()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var path = assembly.Location;
            if (!Path.GetFileName(path).Equals("Kethane.dll", StringComparison.OrdinalIgnoreCase))
            {
                path += "-Kethane";
            }
            else
            {
                path = Path.ChangeExtension(path, null);
            }
            var text = new StreamReader(assembly.GetManifestResourceStream("Kethane.LICENSE.txt")).ReadToEnd();
            File.WriteAllText(path + "-LICENSE.txt", text);
        }
    }
}
