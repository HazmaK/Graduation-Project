using RetroFPS.Settings;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Maps each difficulty level to its preset; loaded from Resources/Difficulty.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Combat/Difficulty Preset Table", fileName = "DifficultyPresetTable")]
    public sealed class DifficultyPresetTable : ScriptableObject
    {
        /// <summary>Resources path used by <see cref="GameDifficulty"/>.</summary>
        public const string ResourcesPath = "Difficulty/DifficultyPresetTable";

        [SerializeField] private DifficultyPreset easy;
        [SerializeField] private DifficultyPreset normal;
        [SerializeField] private DifficultyPreset hard;

        /// <summary>Returns the level's modifiers; a missing preset falls back to identity.</summary>
        public DifficultyModifiers Get(DifficultySetting difficulty)
        {
            DifficultyPreset preset = difficulty == DifficultySetting.Easy ? easy
                : difficulty == DifficultySetting.Hard ? hard : normal;
            return preset != null ? preset.Modifiers : DifficultyModifiers.Identity;
        }
    }
}
