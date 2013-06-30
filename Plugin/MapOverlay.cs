using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Kethane
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    internal class MapOverlay : MonoBehaviour
    {
        public static MapOverlay Instance { get; private set; }

        private static GeodesicGrid grid;

        private CelestialBody body;
        private Dictionary<CelestialBody, double> bodyRadii = new Dictionary<CelestialBody, double>();
        private Mesh mesh;
        private GUISkin skin;
        private GeodesicGrid.Cell? hoverCell;
        private ResourceDefinition resource;
        private Rect controlWindowPos = new Rect(Screen.width * 0.20f, 250, 160, 0);

        private static GUIStyle centeredStyle = null;
        private static GUISkin defaultSkin = null;

        private static readonly Color32 colorEmpty = new Color32(128, 128, 128, 192);
        private static readonly Color32 colorUnknown = new Color32(0, 0, 0, 128);

        public static GeodesicGrid.Cell GetCellUnder(CelestialBody body, Vector3 worldPosition)
        {
            return grid.NearestCell(body.transform.InverseTransformPoint(worldPosition));
        }

        public void Awake()
        {
            if (grid == null) { grid = new GeodesicGrid(5); }

            var scene = HighLogic.LoadedScene;
            if (scene != GameScenes.FLIGHT && scene != GameScenes.TRACKSTATION)
            {
                enabled = false;
            }
        }

        public void Start()
        {
            Instance = this;

            KethaneController.LoadKethaneDeposits();
            resource = KethaneController.ResourceDefinitions.Where(d => d.Resource == "Kethane").Single();

            setUpMesh();
            gameObject.layer = 10;
            ScaledSpace.AddScaledSpaceTransform(gameObject.transform);

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

        public void Update()
        {
            if (!MapView.MapIsEnabled)
            {
                gameObject.renderer.enabled = false;
                return;
            }

            gameObject.renderer.enabled = true;

            var target = MapView.fetch.mapCamera.target;

            var newBody = getTargetBody(target);
            var bodyChanged = newBody != body;
            if (bodyChanged)
            {
                body = newBody;
                var radius = bodyRadii.ContainsKey(body) ? bodyRadii[body] : 1.025;
                gameObject.transform.localScale = Vector3.one * (float)(radius * body.Radius / ScaledSpace.ScaleFactor);
            }

            if (bodyChanged || resource.Resource != KethaneController.SelectedResource)
            {
                resource = KethaneController.ResourceDefinitions.Where(r => r.Resource == KethaneController.SelectedResource).Single().ForBody(body);
                refreshCellColors();
            }

            var ray = MapView.MapCamera.camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitInfo;
            if (Physics.Raycast(ray, out hitInfo, (ray.origin - gameObject.transform.position).magnitude * 2, 1 << 10) && (hitInfo.transform == gameObject.transform))
            {
                var hitVertex = triangleToVertexBase(hitInfo.triangleIndex);
                var sum = Enumerable.Range(hitVertex, 6).Select(c => mesh.vertices[c]).Aggregate((a, b) => a + b);
                hoverCell = grid.NearestCell(sum);
            }
            else
            {
                hoverCell = null;
            }

            gameObject.transform.position = ScaledSpace.LocalToScaledSpace(body.position);
            gameObject.transform.rotation = body.rotation;
        }

        public void RefreshCellColor(GeodesicGrid.Cell cell, CelestialBody body)
        {
            if (body != this.body) { return; }
            var colors = mesh.colors32;
            refreshCellColor(cell, body, colors);
            mesh.colors32 = colors;
        }

        private void refreshCellColors()
        {
            var colors = new Color32[mesh.vertexCount];
            foreach (var cell in grid)
            {
                refreshCellColor(cell, body, colors);
            }
            mesh.colors32 = colors;
        }

        private void refreshCellColor(GeodesicGrid.Cell cell, CelestialBody body, Color32[] colors)
        {
            setCellColor(cell, KethaneController.Scans[resource.Resource][body.name][cell] ? getDepositColor(resource, KethaneController.GetCellDeposit(resource.Resource, body, cell)) : colorUnknown, colors);
        }

        private static void setCellColor(GeodesicGrid.Cell cell, Color32 color, Color32[] colors)
        {
            var idx = cell.GetHashCode() * 6;
            for (int i = idx; i < idx + 6; i++)
            {
                colors[i] = color;
            }
        }

        private static Color32 getDepositColor(ResourceDefinition definition, Deposit deposit)
        {
            Color32 color;
            if (deposit != null)
            {
                var ratio = (deposit.Quantity / definition.MaxQuantity);
                color = (Color32)(definition.ColorFull * ratio + definition.ColorEmpty * (1 - ratio));
            }
            else
            {
                color = colorEmpty;
            }
            return color;
        }

        public void OnGUI()
        {
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

            if (hoverCell != null)
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

            GUI.skin = defaultSkin;
            var oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = XKCDColors.Green;

            if (MapView.MapIsEnabled)
            {
                controlWindowPos = GUILayout.Window(12358, controlWindowPos, controlWindow, "Kethane Scan Map");
            }

            GUI.backgroundColor = oldBackground;
        }

        private void mouseWindow(int windowId)
        {
            var cell = hoverCell.Value;
            var pos = cell.GetPosition();
            var lat = (float)(Math.Atan2(pos.y, Math.Sqrt(pos.x * pos.x + pos.z * pos.z)) * 180 / Math.PI);
            var lon = (float)(Math.Atan2(pos.z, pos.x) * 180 / Math.PI);

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Center:");
            GUILayout.FlexibleSpace();
            GUILayout.Label(String.Format("{0:F1} {1}, {2:F1} {3}", Math.Abs(lat), lat < 0 ? "S" : "N", Math.Abs(lon), lon < 0 ? "W" : "E"));
            GUILayout.EndHorizontal();

            foreach (var definition in KethaneController.ResourceDefinitions.Select(d => d.ForBody(body)))
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label(String.Format("{0}:", definition.Resource));
                GUILayout.FlexibleSpace();
                if (KethaneController.Scans[definition.Resource][body.name][cell])
                {
                    var deposit = KethaneController.GetCellDeposit(definition.Resource, body, cell);
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
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();

            GUI.enabled = KethaneController.ResourceDefinitions.First().Resource != KethaneController.SelectedResource;
            if (GUILayout.Button("◀", GUILayout.ExpandWidth(false)))
            {
                KethaneController.SelectedResource = KethaneController.ResourceDefinitions.Select(d => d.Resource).Last(s => s.CompareTo(KethaneController.SelectedResource) < 0);
            }
            GUI.enabled = true;

            GUILayout.Label(KethaneController.SelectedResource, centeredStyle, GUILayout.ExpandWidth(true));

            GUI.enabled = KethaneController.ResourceDefinitions.Last().Resource != KethaneController.SelectedResource;
            if (GUILayout.Button("▶", GUILayout.ExpandWidth(false)))
            {
                KethaneController.SelectedResource = KethaneController.ResourceDefinitions.Select(d => d.Resource).First(s => s.CompareTo(KethaneController.SelectedResource) > 0);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUI.DragWindow(new Rect(0, 0, 300, 60));
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

        private int triangleToVertexBase(int triangle)
        {
            var i = mesh.triangles[triangle * 3];
            return i - i % 6;
        }

        private void setUpMesh()
        {
            var vertices = new List<UnityEngine.Vector3>();
            var triangles = new List<int>();

            foreach (var cell in grid)
            {
                var neighbors = cell.Neighbors.ToArray();

                for (var i = 0; i < neighbors.Length; i++)
                {
                    var a = neighbors[i];
                    var b = neighbors[i == neighbors.Length - 1 ? 0 : (i + 1)];

                    var center = (a.GetPosition() + b.GetPosition() + cell.GetPosition()).normalized;

                    var blend = 0.08f;
                    vertices.Add(cell.GetPosition() * blend + center * (1 - blend));
                }

                if (cell.IsPentagon)
                {
                    vertices.Add(cell.GetPosition());
                    var t = vertices.Count - 6;
                    for (var i = 0; i < 5; i++)
                    {
                        triangles.AddRange(new int[] { t + 5, t + (i + 1) % 5, t + i });
                    }
                }
                else
                {
                    var t = vertices.Count - 6;
                    triangles.AddRange(new int[] { t + 2, t + 1, t, t + 4, t + 3, t + 2, t, t + 5, t + 4, t + 4, t + 2, t });
                }
            }

            mesh = gameObject.AddComponent<MeshFilter>().mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();

            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.normals = mesh.vertices;
            mesh.colors32 = Enumerable.Repeat(colorUnknown, vertices.Count).ToArray();
            mesh.Optimize();

            renderer.enabled = false;
            renderer.castShadows = false;
            renderer.receiveShadows = false;

            var material = new Material(KSP.IO.File.ReadAllText<MapOverlay>("AlphaUnlitVertexColored.txt"));

            var color = Color.white;
            color.a = 0.4f;
            material.color = color;

            renderer.material = material;

            var collider = gameObject.AddComponent<MeshCollider>();
            collider.isTrigger = true;
        }
    }
}
