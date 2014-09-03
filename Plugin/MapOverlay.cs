using Kethane.GeodesicGrid;
using Kethane.Toolbar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Kethane
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class MapOverlay : MonoBehaviour
    {
        public const int GridLevel = 5;

        public static MapOverlay Instance { get; private set; }
        public static string SelectedResource { get; set; }
        public static bool ShowOverlay { get; set; }

        private CelestialBody body;
        private Dictionary<CelestialBody, double> bodyRadii = new Dictionary<CelestialBody, double>();
        private Mesh mesh;
        private GUISkin skin;
        private Cell? hoverCell;
        private ResourceDefinition resource;
        private Func<Cell, float> heightAt;
        private BoundsMap bounds;

        private static RenderingManager renderingManager;
        private static GUIStyle centeredStyle = null;
        private static GUIStyle minMaxStyle = null;
        private static GUISkin defaultSkin = null;
        private static Rect controlWindowPos = new Rect(0, 0, 160, 0);
        private static bool revealAll = false;
        private static bool expandWindow = true;
        private static WindowToggle toolbar = findToolbar();

        private static readonly Color32 colorEmpty = Misc.Parse(SettingsManager.GetValue("ColorEmpty"), new Color32(128, 128, 128, 192));
        private static readonly Color32 colorUnknown = Misc.Parse(SettingsManager.GetValue("ColorUnknown"), new Color32(0, 0, 0, 128));
        private static readonly bool debugEnabled = Misc.Parse(SettingsManager.GetValue("Debug"), false);

        static MapOverlay()
        {
            controlWindowPos.x = Misc.Parse(SettingsManager.GetValue("WindowLeft"), 200f);
            controlWindowPos.y = Misc.Parse(SettingsManager.GetValue("WindowTop"), 200f);
            ShowOverlay = Misc.Parse(SettingsManager.GetValue("ShowOverlay"), true);
            SelectedResource = "Kethane";
        }

        public static Cell GetCellUnder(CelestialBody body, Vector3 worldPosition)
        {
            return Cell.Containing(body.transform.InverseTransformPoint(worldPosition), MapOverlay.GridLevel);
        }

        public void Awake()
        {
            var scene = HighLogic.LoadedScene;
            if (scene != GameScenes.FLIGHT && scene != GameScenes.TRACKSTATION && scene != GameScenes.MAINMENU)
            {
                enabled = false;
            }
        }

        public void Start()
        {
            if (Instance != null)
            {
                Destroy(Instance.gameObject);
            }

            Instance = this;

            if (HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                var success = startMenuOverlay();
                if (gameObject.renderer != null)
                {
                    gameObject.renderer.enabled = success;
                }
            }
            else if (HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.TRACKSTATION)
            {
                startMapOverlay();
            }
        }

        private void startMapOverlay()
        {
            setUpMesh();
            gameObject.layer = 10;

            var node = ConfigNode.Load(KSPUtil.ApplicationRootPath + "GameData/Kethane/Grid.cfg");
            if (node == null) { return; }
            foreach (var body in FlightGlobals.Bodies)
            {
                double result;
                if (double.TryParse(node.GetValue(body.name), out result))
                {
                    bodyRadii[body] = result;
                }
            }
        }

        private bool startMenuOverlay()
        {
            if (!Misc.Parse(SettingsManager.GetValue("ShowInMenu"), true)) { return false; }

            setUpMesh();

            var objects = GameObject.FindObjectsOfType(typeof(GameObject));
            if (objects.Any(o => o.name == "LoadingBuffer")) { return false; }
            var kerbin = objects.OfType<GameObject>().Where(b => b.name == "Kerbin").LastOrDefault();

            if (kerbin == null)
            {
                Debug.LogWarning("[Kethane] Couldn't find Kerbin!");
                return false;
            }

            gameObject.layer = kerbin.layer;
            gameObject.transform.parent = kerbin.transform;
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = Vector3.one * 1020;

            gameObject.renderer.enabled = true;

            var random = new System.Random();
            var colors = new Color32[mesh.vertexCount];

            foreach (var cell in Cell.AtLevel(MapOverlay.GridLevel))
            {
                var rand = random.Next(100);
                Color32 color;
                if (rand < 16)
                {
                    color = rand < 4 ? new Color32(21, 176, 26, 255) : colorEmpty;
                    foreach (var neighbor in cell.GetNeighbors(MapOverlay.GridLevel))
                    {
                        if (random.Next(2) < 1)
                        {
                            setCellColor(neighbor, color, colors);
                        }
                    }
                }
                else
                {
                    color = colorUnknown;
                }

                setCellColor(cell, color, colors);
            }

            mesh.colors32 = colors;

            return true;
        }

        public void OnDestroy()
        {
            SettingsManager.SetValue("ShowOverlay", ShowOverlay);
            SettingsManager.SetValue("WindowLeft", MapOverlay.controlWindowPos.x);
            SettingsManager.SetValue("WindowTop", MapOverlay.controlWindowPos.y);
            SettingsManager.Save();
        }

        public void Update()
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT && HighLogic.LoadedScene != GameScenes.TRACKSTATION)
            {
                if (HighLogic.LoadedScene != GameScenes.MAINMENU)
                {
                    gameObject.renderer.enabled = false;
                }
                return;
            }

            updateMapView();
        }

        private void updateMapView()
        {
            if (!MapView.MapIsEnabled || !ShowOverlay || MapView.MapCamera == null || KethaneData.Current == null)
            {
                gameObject.renderer.enabled = false;
                return;
            }

            gameObject.renderer.enabled = true;

            var target = MapView.MapCamera.target;

            var newBody = getTargetBody(target);
            var bodyChanged = (newBody != null) && (newBody != body);
            if (bodyChanged)
            {
                body = newBody;

                refreshMesh();
                refreshCollider();

                var radius = bodyRadii.ContainsKey(body) ? bodyRadii[body] : 1.025;
                var parent = ScaledSpace.Instance.scaledSpaceTransforms.FirstOrDefault(t => t.name == body.name);
                if (parent != null)
                {
                    gameObject.transform.parent = parent;
                }
                gameObject.transform.localScale = Vector3.one * 1000f * (float)radius;
                gameObject.transform.localPosition = Vector3.zero;
                gameObject.transform.localRotation = Quaternion.identity;
            }

            if (bodyChanged || resource == null || resource.Resource != SelectedResource)
            {
                resource = KethaneController.ResourceDefinitions.Where(r => r.Resource == SelectedResource).Single();
                refreshCellColors();
            }

            var ray = MapView.MapCamera.camera.ScreenPointToRay(Input.mousePosition);
            hoverCell = Cell.Raycast(ray, MapOverlay.GridLevel, bounds, heightAt, gameObject.transform);
        }

        public void RefreshCellColor(Cell cell, CelestialBody body)
        {
            if (body != this.body) { return; }
            var colors = mesh.colors32;
            refreshCellColor(cell, body, colors, KethaneData.Current);
            mesh.colors32 = colors;
        }

        private void refreshCellColors()
        {
            var colors = new Color32[mesh.vertexCount];
            var data = KethaneData.Current;
            foreach (var cell in Cell.AtLevel(MapOverlay.GridLevel))
            {
                refreshCellColor(cell, body, colors, data);
            }
            mesh.colors32 = colors;
        }

        private void refreshCellColor(Cell cell, CelestialBody body, Color32[] colors, KethaneData data)
        {
            var deposit = data.GetCellDeposit(resource.Resource, body, cell);
            var scanned = data[resource.Resource][body].IsCellScanned(cell);
            var bodyResources = data[resource.Resource][body];
            var color = (revealAll ? deposit != null : scanned) ? getDepositColor(resource, bodyResources, deposit) : colorUnknown;
            setCellColor(cell, color, colors);
        }

        private static void setCellColor(Cell cell, Color32 color, Color32[] colors)
        {
            var idx = cell.Index * 6;
            for (var i = idx; i < idx + 6; i++)
            {
                colors[i] = color;
            }
        }

        private static Color32 getDepositColor(ResourceDefinition definition, BodyResourceData bodyResources, ICellResource deposit)
        {
            Color32 color;
            if (deposit != null)
            {
                var ratio = (float)(deposit.Quantity / bodyResources.MaxDepositQuantity);
                color = (Color32)(definition.ColorFull * ratio + definition.ColorEmpty * (1 - ratio));
            }
            else
            {
                color = colorEmpty;
            }
            return color;
        }

        private static WindowToggle findToolbar()
        {
            if (ToolbarManager.ToolbarAvailable)
            {
                Debug.Log("[Kethane] Toolbar instance found. Using new interface.");
                return new WindowToggle();
            }
            else
            {
                Debug.LogWarning("[Kethane] Could not find Toolbar instance. Reverting to legacy interface.");
                return null;
            }
        }

        public void OnGUI()
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT && HighLogic.LoadedScene != GameScenes.TRACKSTATION) { return; }

            if (renderingManager == null)
            {
                renderingManager = (RenderingManager)GameObject.FindObjectOfType(typeof(RenderingManager));
            }

            if (renderingManager != null)
            {
                var obj = renderingManager.uiElementsToDisable.FirstOrDefault();
                if (obj != null && !obj.activeSelf) { return; }
            }

            if (!MapView.MapIsEnabled || MapView.MapCamera == null) { return; }

            if (skin == null)
            {
                GUI.skin = null;
                skin = (GUISkin)GameObject.Instantiate(GUI.skin);

                var window = skin.window;
                window.padding = new RectOffset(6, 6, 20, 6);
                window.fontSize = 10;
                skin.window = window;

                var label = skin.label;
                label.margin = new RectOffset(1, 1, 1, 1);
                label.padding = new RectOffset(1, 1, 1, 1);
                label.fontSize = 10;
                skin.label = label;
            }

            GUI.skin = skin;

            if (hoverCell != null && ShowOverlay)
            {
                var mouse = Event.current.mousePosition;
                var position = new Rect(mouse.x + 16, mouse.y + 4, 160, 32);
                GUILayout.Window(12359, position, mouseWindow, "Resource Info");
            }

            if (defaultSkin == null)
            {
                GUI.skin = null;
                defaultSkin = (GUISkin)GameObject.Instantiate(GUI.skin);
            }

            if (centeredStyle == null)
            {
                centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
                centeredStyle.alignment = TextAnchor.MiddleCenter;
            }

            if (minMaxStyle == null)
            {
                minMaxStyle = new GUIStyle(GUI.skin.button);
                minMaxStyle.contentOffset = new Vector2(-1, 0);
            }

            if (toolbar != null && !toolbar.IsVisible) { return; }

            GUI.skin = defaultSkin;
            var oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = XKCDColors.Green;

            controlWindowPos = GUILayout.Window(12358, controlWindowPos, controlWindow, expandWindow ? "Kethane Scan Map" : String.Empty);

            GUI.backgroundColor = oldBackground;
        }

        private void mouseWindow(int windowId)
        {
            var cell = hoverCell.Value;
            var pos = cell.Position;
            var lat = (float)(Math.Atan2(pos.y, Math.Sqrt(pos.x * pos.x + pos.z * pos.z)) * 180 / Math.PI);
            var lon = (float)(Math.Atan2(pos.z, pos.x) * 180 / Math.PI);

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Center:");
            GUILayout.FlexibleSpace();
            GUILayout.Label(String.Format("{0:F1} {1}, {2:F1} {3}", Math.Abs(lat), lat < 0 ? "S" : "N", Math.Abs(lon), lon < 0 ? "W" : "E"));
            GUILayout.EndHorizontal();

            foreach (var definition in KethaneController.ResourceDefinitions)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label(String.Format("{0}:", definition.Resource));
                GUILayout.FlexibleSpace();
                if (revealAll || KethaneData.Current[definition.Resource][body].IsCellScanned(cell))
                {
                    var deposit = KethaneData.Current.GetCellDeposit(definition.Resource, body, cell);
                    GUILayout.Label(deposit != null ? String.Format("{0:N1}", deposit.Quantity) : "(none)");
                }
                else
                {
                    GUILayout.Label("(no data)");
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void controlWindow(int windowID)
        {
            if ((toolbar == null) && GUI.Button(new Rect(2, 2, 20, 15), expandWindow ? "\u25B4" : "\u25BE", minMaxStyle))
            {
                expandWindow = !expandWindow;
                controlWindowPos = new Rect(controlWindowPos.xMin, controlWindowPos.yMin, expandWindow ? 160 : 24, 36);
            }

            GUILayout.BeginVertical();

            if (!expandWindow)
            {
                GUI.Label(new Rect(8, 15, 15, 20), "K");
            }
            else
            {
                GUILayout.BeginHorizontal();

                var defs = KethaneController.ResourceDefinitions.Select(d => d.Resource).ToList();

                GUI.enabled = defs.Count > 1;
                if (GUILayout.Button("◀", GUILayout.ExpandWidth(false)))
                {
                    SelectedResource = defs.LastOrDefault(s => s.CompareTo(SelectedResource) < 0) ?? defs.Last();
                }
                GUI.enabled = true;

                GUILayout.Label(SelectedResource, centeredStyle, GUILayout.ExpandWidth(true));

                GUI.enabled = defs.Count > 1;
                if (GUILayout.Button("▶", GUILayout.ExpandWidth(false)))
                {
                    SelectedResource = defs.FirstOrDefault(s => s.CompareTo(SelectedResource) > 0) ?? defs.First();
                }
                GUI.enabled = true;

                GUILayout.EndHorizontal();

                ShowOverlay = GUILayout.Toggle(ShowOverlay, "Show Grid Overlay");

                if (debugEnabled)
                {
                    var vessel = FlightGlobals.ActiveVessel;
                    if (vessel != null && vessel.mainBody != body) { vessel = null; }
                    var deposit = vessel == null ? null : KethaneData.Current.GetCellDeposit(resource.Resource, body, GetCellUnder(body, vessel.transform.position));

                    GUILayout.BeginVertical(GUI.skin.box);

                    if (revealAll != GUILayout.Toggle(revealAll, "Reveal Unscanned Cells"))
                    {
                        revealAll = !revealAll;
                        refreshCellColors();
                    }

                    if (GUILayout.Button("Reset " + (body ? body.name : "[null]") + " Data"))
                    {
                        KethaneData.Current[resource.Resource].ResetBodyData(body);
                        refreshCellColors();
                    }

                    if (GUILayout.Button("Reset Generator Config"))
                    {
                        KethaneData.Current.ResetGeneratorConfig(resource);
                        refreshCellColors();
                    }

                    if (GUILayout.Button("Export Data (" + (revealAll ? "All" : "Scanned") + ")"))
                    {
                        export();
                    }

                    GUILayout.EndVertical();
                }
            }

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, controlWindowPos.width, controlWindowPos.height));
        }

        private static void export()
        {
            var sb = new StringBuilder();

            var cells = new CellMap<string>(MapOverlay.GridLevel);
            foreach (var cell in Cell.AtLevel(MapOverlay.GridLevel))
            {
                var pos = cell.Position;
                var lat = (float)(Math.Atan2(pos.y, Math.Sqrt(pos.x * pos.x + pos.z * pos.z)) * 180 / Math.PI);
                var lon = (float)(Math.Atan2(pos.z, pos.x) * 180 / Math.PI);
                cells[cell] = String.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},", cell.Index, lat, lon, pos.x, pos.y, pos.z);
            }

            sb.AppendLine("body,resource,cellId,lat,lon,x,y,z,scanned,quantity");

            foreach (var body in FlightGlobals.Bodies)
            {
                foreach (var resource in KethaneController.ResourceDefinitions)
                {
                    foreach (var cell in Cell.AtLevel(MapOverlay.GridLevel))
                    {
                        var scanned = KethaneData.Current[resource.Resource][body].IsCellScanned(cell);
                        var deposit = KethaneData.Current.GetCellDeposit(resource.Resource, body, cell);

                        sb.Append(String.Format("{0},{1},", body.name, resource.Resource));
                        sb.Append(cells[cell]);
                        sb.Append(scanned ? "true" : "false");
                        if ((revealAll || scanned) && deposit != null)
                        {
                            sb.Append(String.Format(CultureInfo.InvariantCulture, ",{0}", deposit.Quantity));
                        }
                        else
                        {
                            sb.Append(",");
                        }
                        sb.AppendLine();
                    }
                }
            }

            KSP.IO.File.WriteAllText<KethaneController>(sb.ToString(), "kethane_export.csv");
        }

        private static CelestialBody getTargetBody(MapObject target)
        {
            if (target.type == MapObject.MapObjectType.CELESTIALBODY)
            {
                return target.celestialBody;
            }
            else if (target.type == MapObject.MapObjectType.MANEUVERNODE)
            {
                return target.maneuverNode.patch.referenceBody;
            }
            else if (target.type == MapObject.MapObjectType.VESSEL)
            {
                return target.vessel.mainBody;
            }

            return null;
        }

        private void setUpMesh()
        {
            var triangles = new List<int>();

            foreach (var cell in Cell.AtLevel(MapOverlay.GridLevel))
            {
                var t = (int)cell.Index * 6;
                if (cell.IsPentagon)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        triangles.Add(t + 1 + i, t + 1 + (i + 1) % 5, t);
                    }
                }
                else
                {
                    triangles.Add(t + 0, t + 1, t + 2, t + 2, t + 3, t + 4, t + 4, t + 5, t + 0, t + 0, t + 2, t + 4);
                }
            }

            mesh = gameObject.AddComponent<MeshFilter>().mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();

            mesh.vertices = new Vector3[Cell.CountAtLevel(MapOverlay.GridLevel) * 6];
            mesh.triangles = triangles.ToArray();
            mesh.colors32 = Enumerable.Repeat(colorUnknown, mesh.vertexCount).ToArray();
            mesh.Optimize();

            renderer.enabled = false;
            renderer.castShadows = false;
            renderer.receiveShadows = false;

            var material = new Material(new System.IO.StreamReader(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Kethane.Resources.AlphaUnlitVertexColored.txt")).ReadToEnd());

            var color = Color.white;
            color.a = 0.4f;
            material.color = color;

            renderer.material = material;

            refreshMesh();
        }

        private Func<Cell, float> getHeightRatioMap()
        {
            Func<Cell, float> heightRatioAt;

            try
            {
                var bodyTerrain = TerrainData.ForBody(body);
                heightRatioAt = c => Math.Max(1, bodyTerrain.GetHeightRatio(c));
            }
            catch (ArgumentException)
            {
                heightRatioAt = c => 1;
            }

            return heightRatioAt;
        }

        private void refreshMesh()
        {
            var vertices = new List<UnityEngine.Vector3>();

            heightAt = getHeightRatioMap();

            foreach (var cell in Cell.AtLevel(MapOverlay.GridLevel))
            {
                var center = cell.Position * heightAt(cell);

                if (cell.IsPentagon)
                {
                    vertices.Add(center);
                }

                var blend = 0.08f;
                center *= blend;

                foreach (var vertex in cell.GetVertices(MapOverlay.GridLevel, heightAt))
                {
                    vertices.Add(center + vertex * (1 - blend));
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.RecalculateBounds();
        }

        private void refreshCollider()
        {
            bounds = new BoundsMap(heightAt, MapOverlay.GridLevel);
        }
    }
}
