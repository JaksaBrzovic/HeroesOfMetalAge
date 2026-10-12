using HeroesOfMetalAge.Infrastructure.Content;
using UnityEditor;
using UnityEngine;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>Inspector of an adventure map: shows the resulting grid for the image and cell size, and the Rebuild button.</summary>
    [CustomEditor(typeof(AdventureMapAuthoring))]
    internal sealed class AdventureMapAuthoringEditor : UnityEditor.Editor
    {
        private const string PAINTING_HELP = "Paint the walkable parts of the image on the Terrain Logic layer with the Tile Palette "
            + "(Window > 2D > Tile Palette, palette 'Terrain Logic'). Unpainted cells are impassable. Hover the map in the Scene view "
            + "to read a cell's coordinates for the mission's start and encounters (counted from the top-left corner).";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            AdventureMapAuthoring map = (AdventureMapAuthoring)target;
            EditorGUILayout.Space();

            if (map.MapImage != null && map.PixelsPerCell > 0)
            {
                Vector2Int size = AdventureMapBuilder.ImageSize(map.MapImage);
                int width = size.x / map.PixelsPerCell;
                int height = size.y / map.PixelsPerCell;
                string grid = $"Image {size.x} x {size.y} px at {map.PixelsPerCell} px per cell = {width} x {height} cells.";
                bool isCut = size.x % map.PixelsPerCell != 0 || size.y % map.PixelsPerCell != 0;
                EditorGUILayout.HelpBox(isCut ? grid + " The image does not divide evenly; the last partial row and column are not playable." : grid,
                    isCut ? MessageType.Warning : MessageType.Info);
            }

            if (!map.IsBuilt)
            {
                EditorGUILayout.HelpBox("The image or cell size changed since the last rebuild. Press Rebuild.", MessageType.Warning);
            }

            EditorGUILayout.HelpBox(PAINTING_HELP, MessageType.None);
            if (GUILayout.Button("Rebuild"))
            {
                AdventureMapBuilder.Rebuild(map);
            }
        }
    }
}
