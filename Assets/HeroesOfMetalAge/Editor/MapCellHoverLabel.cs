using HeroesOfMetalAge.Infrastructure.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// While an adventure map is open in Prefab Mode, the Scene view outlines the cell under the pointer and labels it with
    /// the coordinates the mission settings use (from the top-left corner) and its painted terrain.
    /// </summary>
    [InitializeOnLoad]
    internal static class MapCellHoverLabel
    {
        private static readonly Color OUTLINE = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color FILL = new Color(1f, 0.85f, 0.3f, 0.12f);

        static MapCellHoverLabel()
        {
            SceneView.duringSceneGui += DrawHoveredCell;
        }

        private static void DrawHoveredCell(SceneView view)
        {
            Event current = Event.current;
            if (current.type != EventType.Repaint && current.type != EventType.MouseMove)
            {
                return;
            }

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return;
            }

            AdventureMapAuthoring map = stage.prefabContentsRoot.GetComponent<AdventureMapAuthoring>();
            if (map == null || map.Width <= 0 || map.TerrainLogic == null)
            {
                return;
            }

            if (current.type == EventType.MouseMove)
            {
                view.Repaint();
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (Mathf.Approximately(ray.direction.z, 0f))
            {
                return;
            }

            Vector3 point = ray.origin + ray.direction * (-ray.origin.z / ray.direction.z);
            Vector3 local = map.transform.InverseTransformPoint(point);
            int x = Mathf.FloorToInt(local.x);
            int y = Mathf.FloorToInt(local.y);
            if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            {
                return;
            }

            TerrainTile tile = map.TerrainLogic.GetTile<TerrainTile>(new Vector3Int(x, y, 0));
            string terrain = tile != null ? tile.Terrain.ToString() : "Blocked (unpainted)";
            Vector3 corner = map.transform.TransformPoint(new Vector3(x, y, 0f));
            Vector3[] outline =
            {
                corner,
                map.transform.TransformPoint(new Vector3(x + 1, y, 0f)),
                map.transform.TransformPoint(new Vector3(x + 1, y + 1, 0f)),
                map.transform.TransformPoint(new Vector3(x, y + 1, 0f))
            };
            Handles.DrawSolidRectangleWithOutline(outline, FILL, OUTLINE);
            Handles.Label(outline[3], $"cell {x}, {map.Height - 1 - y}  {terrain}", EditorStyles.whiteBoldLabel);
        }
    }
}
