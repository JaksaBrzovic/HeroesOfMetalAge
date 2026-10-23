using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// Keeps the prefabs' texts in line with <c>Content/UI/Typography.asset</c>: <see cref="ApplyTypography"/> re-applies
    /// every text's style after the asset was edited, and <see cref="StyleFor"/> picks a style for a text that has none
    /// yet from its prefab and object name.
    /// </summary>
    public static class TypographyTools
    {
        private const string PREFAB_ROOT = "Assets/HeroesOfMetalAge/Prefabs";

        // "Prefab/Object" for texts whose name alone says too little
        private static readonly Dictionary<string, TextStyle> KnownTexts = new Dictionary<string, TextStyle>(StringComparer.OrdinalIgnoreCase)
        {
            { "FrontEnd/TitleText", TextStyle.Display },
            { "FrontEnd/CreditsText", TextStyle.Scroll },
            { "FrontEnd/HintText", TextStyle.Label },
            { "MissionBriefing/PageText", TextStyle.Scroll },
            { "Defeat/title", TextStyle.Display },
            { "Victory/title", TextStyle.Display },
            { "Rewards/title", TextStyle.Display },
            { "Rewards/gold", TextStyle.Display },
            { "Header/gold", TextStyle.Number },
            { "Header/day", TextStyle.Number },
            { "Combat/actor", TextStyle.Title },
            { "Notifications/hit", TextStyle.Title },
            { "Town/quantity", TextStyle.Number },
            { "WorldMarker/Count", TextStyle.Number },
            { "DamageText/DamageText", TextStyle.Floating },
            { "ArmySlot/Count", TextStyle.Number },
            { "ArmySlot/Name", TextStyle.Label },
            { "ArtefactCard/Title", TextStyle.Heading },
            { "RecruitmentCard/Title", TextStyle.Heading },
            { "RecruitmentCard/Recruit", TextStyle.Button },
            { "Button/Title", TextStyle.Button }
        };

        public static TypographySettings Settings => AssetDatabase.LoadAssetAtPath<TypographySettings>(ContentAssets.TYPOGRAPHY_PATH);

        /// <summary>Re-applies the typography asset to every text in every prefab.</summary>
        [MenuItem("Tools/Heroes of Metal Age/Apply typography")]
        public static void ApplyTypography()
        {
            int changed = ForEachPrefab((root, prefabName) =>
            {
                bool isChanged = false;
                foreach (TypographyText text in root.GetComponentsInChildren<TypographyText>(true))
                {
                    isChanged |= text.Apply();
                }

                return isChanged;
            });
            Debug.Log("Typography applied; prefabs changed: " + changed);
        }

        /// <summary>Gives every text without a style one picked from its name, then applies the styles.</summary>
        public static int AddMissingStyles(TypographySettings settings)
        {
            return ForEachPrefab((root, prefabName) =>
            {
                bool isChanged = false;
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.GetComponent<TypographyText>() != null)
                    {
                        continue;
                    }

                    // a nested prefab's texts belong to that prefab and are styled there
                    if (PrefabUtility.IsPartOfPrefabInstance(text.gameObject) && PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject) != root)
                    {
                        continue;
                    }

                    TextStyle style = StyleFor(prefabName, text.name);
                    // decorative fonts bring their own weight, so faux bold is dropped
                    text.fontStyle &= ~FontStyles.Bold;
                    text.gameObject.AddComponent<TypographyText>().Configure(settings, style);
                    isChanged = true;
                }

                return isChanged;
            });
        }

        /// <summary>The style for a text from its prefab and object name.</summary>
        public static TextStyle StyleFor(string prefabName, string objectName)
        {
            if (KnownTexts.TryGetValue(prefabName + "/" + objectName, out TextStyle known))
            {
                return known;
            }

            string name = objectName.ToLowerInvariant();
            if (name.StartsWith("line", StringComparison.Ordinal) && prefabName == "FrontEnd")
            {
                return TextStyle.Verse;
            }

            if (name.EndsWith("title", StringComparison.Ordinal) && name != "title" && name != "titletext")
            {
                return TextStyle.Heading;
            }

            if (name.Contains("title") || name.Contains("speaker") || name == "hero" || name.Contains("name"))
            {
                return TextStyle.Title;
            }

            if (name.Contains("button") || name.Contains("caption"))
            {
                return TextStyle.Button;
            }

            if (name.Contains("count") || name.Contains("amount") || name.Contains("gold") || name.Contains("number"))
            {
                return TextStyle.Number;
            }

            bool isLabel = name.Contains("label") || name.Contains("continue") || name.Contains("help") || name.Contains("audio")
                || name.Contains("level") || name.Contains("movement") || name.Contains("turn") || name.Contains("progression")
                || name.Contains("home") || name.Contains("visiting") || name.Contains("owner") || name == "mission";
            return isLabel ? TextStyle.Label : TextStyle.Body;
        }

        /// <returns>How many prefabs were changed and saved.</returns>
        private static int ForEachPrefab(Func<GameObject, string, bool> edit)
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_ROOT }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (edit(root, root.name))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changed++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return changed;
        }
    }
}
