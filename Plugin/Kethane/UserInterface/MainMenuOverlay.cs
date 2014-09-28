using GeodesicGrid;
using System.Linq;
using UnityEngine;

namespace Kethane.UserInterface
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    internal class MainMenuOverlay : MonoBehaviour
    {
        private OverlayRenderer overlayRenderer;

        protected void Start()
        {
            overlayRenderer = gameObject.AddComponent<OverlayRenderer>();
            overlayRenderer.SetGridLevel(KethaneData.GridLevel);
            overlayRenderer.IsVisible = startMenuOverlay();
        }

        private bool startMenuOverlay()
        {
            if (!Misc.Parse(SettingsManager.GetValue("ShowInMenu"), true)) { return false; }

            var objects = GameObject.FindObjectsOfType(typeof(GameObject));
            if (objects.Any(o => o.name == "LoadingBuffer")) { return false; }
            var kerbin = objects.OfType<GameObject>().Where(b => b.name == "Kerbin").LastOrDefault();

            if (kerbin == null)
            {
                Debug.LogWarning("[Kethane] Couldn't find Kerbin!");
                return false;
            }

            overlayRenderer.SetTarget(kerbin.transform);
            overlayRenderer.SetRadiusMultiplier(1.02f);

            var random = new System.Random();
            var colors = new CellMap<Color32>(KethaneData.GridLevel);

            foreach (var cell in Cell.AtLevel(KethaneData.GridLevel))
            {
                var rand = random.Next(100);
                Color32 color;
                if (rand < 16)
                {
                    color = rand < 4 ? new Color32(21, 176, 26, 255) : new Color32(128, 128, 128, 192);
                    foreach (var neighbor in cell.GetNeighbors(KethaneData.GridLevel))
                    {
                        if (random.Next(2) < 1)
                        {
                            colors[neighbor] = color;
                        }
                    }
                }
                else
                {
                    color = new Color32(0, 0, 0, 128);
                }

                colors[cell] = color;
            }

            overlayRenderer.SetCellColors(colors);

            return true;
        }
    }
}
