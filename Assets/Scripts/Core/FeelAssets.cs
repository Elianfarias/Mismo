using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Core
{
    /// <summary>Feel defaults: stable editor GUIDs, explicit catalog references in players.</summary>
    public static class FeelAssets
    {
#if UNITY_EDITOR
        static readonly Dictionary<string, string> Guids = new Dictionary<string, string>
        {
            { "MMF_PlayerConfiguration", "c14ab46a507c8324c8b54e6a3fdc2b0e" },
            { "MMFeedbacksConfiguration", "59a4add72d602ce4a9693b14a97d2a83" },
            { "SequencerButtonBackground", "e802b29e86b4ef74abe728250ecaf37c" },
            { "SequencerDotBackground", "82c9d5c8726c57249b51c39e76eee072" },
            { "MM_6D_Shake", "bcf6524ce6451f34cb7106d0c00da9a5" },
            { "MMDefaultHDRPProfile", "d6c55732adbfa0d4593bd219357d1a00" },
            { "MMDefaultHDRPVolume", "041a480513ea4f947a590bb1195e91a0" },
            { "MMDefaultPostProcessingProfile", "c1bea42a5942a4c09a3ecd393f6054aa" },
            { "MMDefaultPostProcessingVolume", "948e90df3bdde44068685e4ebecfcc25" },
            { "MMDefaultURPProfile", "57d94797548149846b6fb7aec61488f2" },
            { "MMDefaultURPVolume", "f41da011e26b4194cbae36ef66842b50" },
            { "MMDebugOnScreenConsole", "dbdbfb7cee2876a42980b6bcd9bc441a" },
            { "AchievementDisplay", "445360f52aabb4446be71f24e6576a72" },
            { "nv-constant-template", "28f1ec0b82fdf434b8efa0ef0f9a9d37" },
            { "nv-emphasis-template", "5f7cb0ced88db4af08a2dd3945067cd7" },
            { "nv-pattern-template", "db8a2f512b50d437b8268f17384f58c8" },
        };
#endif
        public static Object Load(string key) => Load<Object>(key);
        public static T Load<T>(string key) where T : Object
        {
#if UNITY_EDITOR
            if (Guids.TryGetValue(key, out string guid))
                return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            return null;
#else
            return ProjectAssets.Load<T>("Feel/" + key);
#endif
        }
    }
}