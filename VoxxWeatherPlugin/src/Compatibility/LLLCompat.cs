using UnityEngine;
using System.Runtime.CompilerServices;
using LethalLevelLoader;
using VoxxWeatherPlugin.Weathers;
using VoxxWeatherPlugin.Behaviours;
using System.Collections.Generic;
using HarmonyLib;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace VoxxWeatherPlugin.Compatibility
{
    public static class LLLCompat
    {
        public static bool isActive { get; private set; } = false;
        private static readonly string snowColorTag = "voxxSnowColor";
        private static readonly string snowOverlayColorTag = "voxxSnowOverlayColor";
        private static readonly string blizzardFogColorTag = "voxxBlizzardFogColor";
        private static readonly string blizzardCrystalsColorTag = "voxxBlizzardCrystalsColor";
        private static readonly string toxicFumesColorTag = "voxxToxicFumesColor";
        private static readonly string toxicFogColorTag = "voxxToxicFogColor";

        public static void Init()
        {
            isActive = true;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TagRecolorSnow()
        {
            if (LevelManipulator.Instance == null)
                return;

            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel == null)
                return;

            if (!ContentTagManager.TryGetContentTagColour(currentLevel, snowColorTag, out Color snowColor))
            {
                snowColor = LevelManipulator.Instance.snowColor;
            }

            if (!ContentTagManager.TryGetContentTagColour(currentLevel, snowOverlayColorTag, out Color overlayColor))
            {
                overlayColor = LevelManipulator.Instance.snowOverlayColor;
            }

            if (!ContentTagManager.TryGetContentTagColour(currentLevel, blizzardFogColorTag, out Color fogColor))
            {
                fogColor = LevelManipulator.Instance.blizzardFogColor;
            }

            if (!ContentTagManager.TryGetContentTagColour(currentLevel, blizzardCrystalsColorTag, out Color crystalsColor))
            {
                crystalsColor = LevelManipulator.Instance.blizzardCrystalsColor;
            }

            LevelManipulator.Instance.SetSnowColor(snowColor, overlayColor, fogColor, crystalsColor);
            
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void TagRecolorToxic()
        {
            if (ToxicSmogWeather.Instance == null)
                return;
            
            ExtendedLevel currentLevel = LevelManager.CurrentExtendedLevel;
            if (currentLevel == null)
                return;

            if (!ContentTagManager.TryGetContentTagColour(currentLevel, toxicFogColorTag, out Color fogColor))
            {
                fogColor = ToxicSmogWeather.Instance.VFXManager?.toxicFogColor ?? Color.green;
            }

            if (!ContentTagManager.TryGetContentTagColour(currentLevel, toxicFumesColorTag, out Color fumesColor))
            {
                fumesColor = ToxicSmogWeather.Instance.VFXManager?.toxicFumesColor ?? Color.green;
            }

            ToxicSmogWeather.Instance.VFXManager?.SetToxicFumesColor(fogColor, fumesColor);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static string StoreLevelData(string fileName = "levelData.csv") 
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = Path.GetDirectoryName(assemblyPath);

            string filePath = Path.Combine(assemblyDirectory, fileName);
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Level Name,Level Description,Tags");

            foreach (ExtendedLevel level in PatchedContent.ExtendedLevels)
            {
                string levelName = level.NumberlessPlanetName;
                SelectableLevel selectableLevel = level.SelectableLevel;
                string levelDescription = selectableLevel.LevelDescription;

                // Escape quotes in description
                levelDescription = levelDescription.Replace("\"", "\"\""); // Replace " with ""
                if (levelDescription.Contains("\""))
                {
                    levelDescription = $"\"{levelDescription}\""; // Enclose in quotes if it contains commas or quotes
                }
                // Remove new line characters, replace commas with semicolons
                levelDescription = levelDescription.Replace("\n", " ").Replace(",", ";");

                // Handle tags (join them into a single string, or add them as separate columns)
                string levelTags = string.Join("|", level.ContentTags.Select(tag => tag.contentTagName));

                sb.AppendLine($"{levelName},{levelDescription},{levelTags}");
            }

            File.WriteAllText(filePath, sb.ToString());

            return $"Level data stored to {filePath}";
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static string StoreInteriorData(string fileName = "interiorData.csv") 
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string assemblyDirectory = Path.GetDirectoryName(assemblyPath);

            string filePath = Path.Combine(assemblyDirectory, fileName);
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Dungeon Name");

            foreach (ExtendedDungeonFlow dungeon in PatchedContent.ExtendedDungeonFlows)
            {
                string dungeonName = dungeon.DungeonName;

                sb.AppendLine($"{dungeonName}");
            }

            File.WriteAllText(filePath, sb.ToString());

            return $"Level data stored to {filePath}";
        }
    }
}