using System;
using System.IO;
using UnityEngine;

namespace Kethane.Utilities
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    internal class TutorialInstaller : MonoBehaviour
    {
        public void Start()
        {
            var sourcePath = Path.GetFullPath(KSPUtil.ApplicationRootPath) + "GameData/Kethane/Tutorials/";
            var sourceUri = new Uri(sourcePath);
            var destinationUri = new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath) + "saves/training/");

            foreach (var file in new DirectoryInfo(sourcePath).GetFiles("*", SearchOption.AllDirectories))
            {
                file.CopyTo(new Uri(destinationUri, sourceUri.MakeRelativeUri(new Uri(file.FullName))).LocalPath, true);
            }
        }
    }
}
