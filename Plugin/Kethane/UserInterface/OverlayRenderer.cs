using GeodesicGrid;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kethane.UserInterface
{
    internal class OverlayRenderer : MonoBehaviour
    {
        private Mesh mesh;

        private Func<Cell, float> heightMap = c => 1;
        private float radiusMultiplier = 1;
        private Transform target = null;

        public int GridLevel { get; private set; }

        public bool IsVisible
        {
            get
            {
                var renderer = gameObject.GetComponent<MeshRenderer>();
                if (renderer == null) { return false; }
                return renderer.enabled;
            }
            set
            {
                var renderer = gameObject.GetComponent<MeshRenderer>();
                if (renderer == null) { throw new InvalidOperationException("OverlayRenderer has not started"); }
                renderer.enabled = value;
            }
        }

        protected void Awake()
        {
            setUpComponents();
            updateTriangles();
            updateVertices();
            updateTarget();
        }

        #region Configuration setters

        public void SetGridLevel(int gridLevel)
        {
            SetGridLevelAndHeightMap(gridLevel, heightMap);
        }

        public void SetHeightMap(Func<Cell, float> heightMap)
        {
            SetGridLevelAndHeightMap(GridLevel, heightMap);
        }

        public void SetGridLevelAndHeightMap(int gridLevel, Func<Cell, float> heightMap)
        {
            if (gridLevel < 0) { throw new ArgumentOutOfRangeException("gridLevel"); }
            if (heightMap == null) { throw new ArgumentNullException("heightMap"); }

            if (gridLevel != GridLevel)
            {
                GridLevel = gridLevel;
                updateTriangles();
            }
            else
            {
                if (heightMap == this.heightMap) { return; }
            }

            this.heightMap = heightMap;
            updateVertices();
        }

        public void SetRadiusMultiplier(float radiusMultiplier)
        {
            if (radiusMultiplier < 0) { throw new ArgumentOutOfRangeException("radiusMultiplier"); }
            if (radiusMultiplier != this.radiusMultiplier)
            {
                this.radiusMultiplier = radiusMultiplier;
                updateScale();
            }
        }

        public void SetTarget(Transform target)
        {
            if (target != this.target)
            {
                this.target = target;
                updateTarget();
            }
        }

        #endregion

        #region Cell color setters

        public void SetCellColor(Cell cell, Color32 color)
        {
            var colors = mesh.colors32;
            setCellColor(cell, color, colors);
            mesh.colors32 = colors;
        }

        public void SetCellColors(IDictionary<Cell, Color32> assignments)
        {
            setCellColors(assignments);
        }

        public void SetCellColors(CellMap<Color32> assignments)
        {
            setCellColors(assignments);
        }

        private void setCellColors(IEnumerable<KeyValuePair<Cell, Color32>> assignments)
        {
            var colors = mesh.colors32;

            foreach (var assignment in assignments)
            {
                setCellColor(assignment.Key, assignment.Value, colors);
            }

            mesh.colors32 = colors;
        }

        private static void setCellColor(Cell cell, Color32 color, Color32[] colors)
        {
            var idx = cell.Index * 6;
            for (var i = idx; i < idx + 6; i++)
            {
                colors[i] = color;
            }
        }

        #endregion

        private void setUpComponents()
        {
            mesh = gameObject.AddComponent<MeshFilter>().mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();

            renderer.enabled = false;
            renderer.castShadows = false;
            renderer.receiveShadows = false;

            var material = new Material(new System.IO.StreamReader(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Kethane.Resources.AlphaUnlitVertexColored.txt")).ReadToEnd());

            var color = Color.white;
            color.a = 0.4f;
            material.color = color;

            renderer.material = material;
        }

        private void updateTriangles()
        {
            mesh.Clear();
            mesh.vertices = new Vector3[Cell.CountAtLevel(GridLevel) * 6];
            mesh.colors32 = new Color32[mesh.vertexCount];

            var triangles = new List<int>();

            foreach (var cell in Cell.AtLevel(GridLevel))
            {
                var t = (int)cell.Index * 6;
                if (cell.IsPentagon)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        triangles.AddRange(new[] { t + 1 + i, t + 1 + (i + 1) % 5, t });
                    }
                }
                else
                {
                    triangles.AddRange(new[] { t + 0, t + 1, t + 2, t + 2, t + 3, t + 4, t + 4, t + 5, t + 0, t + 0, t + 2, t + 4 });
                }
            }

            mesh.triangles = triangles.ToArray();
            mesh.Optimize();
        }

        private void updateVertices()
        {
            var vertices = new List<UnityEngine.Vector3>();

            foreach (var cell in Cell.AtLevel(GridLevel))
            {
                var center = cell.Position * heightMap(cell);

                if (cell.IsPentagon)
                {
                    vertices.Add(center);
                }

                var blend = 0.08f;
                center *= blend;

                foreach (var vertex in cell.GetVertices(GridLevel, heightMap))
                {
                    vertices.Add(center + vertex * (1 - blend));
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.RecalculateBounds();
        }

        private void updateTarget()
        {
            if (target != null)
            {
                gameObject.layer = target.gameObject.layer;
            }
            gameObject.transform.parent = target;
            gameObject.transform.localPosition = Vector3.zero;
            gameObject.transform.localRotation = Quaternion.identity;
            updateScale();
        }

        private void updateScale()
        {
            gameObject.transform.localScale = Vector3.one * 1000 * radiusMultiplier;
        }
    }
}
