// Ignore Spelling: SafetyStatus Jotunn

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using SafetyStatus.Visualization;
using System.Reflection;
using UnityEngine;

namespace SafetyStatus {
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, Jotunn.Main.Version)]
    internal sealed class SafetyStatus : BaseUnityPlugin {
        internal const string Author = "Searica";
        public const string PluginName = "SafetyStatus";
        public const string PluginGUID = $"{Author}.Valheim.{PluginName}";
        public const string PluginVersion = "1.0.0";

        internal static CustomStatusEffect SafeEffect;
        internal const string SafeEffectName = "SafeStatusEffect";
        internal static int SafeEffectHash;

        internal static bool IsSafetyCircleActive = false;

        public void Awake() {
            Log.Init(Logger);

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

            Game.isModded = true;

            PrefabManager.OnVanillaPrefabsAvailable += AddStatusEffect;
        }

        /// <summary>
        ///     Create and add the Safe status effect
        /// </summary>
        private void AddStatusEffect() {
            try {
                StatusEffect statusEffect = ScriptableObject.CreateInstance<StatusEffect>();
                statusEffect.name = SafeEffectName;
                statusEffect.m_name = "Safe";
                statusEffect.m_icon = PrefabManager.Cache.GetPrefab<CraftingStation>("piece_workbench").m_icon;
                statusEffect.m_startMessageType = MessageHud.MessageType.TopLeft;
                statusEffect.m_startMessage = "You feel safer";
                statusEffect.m_stopMessageType = MessageHud.MessageType.TopLeft;
                statusEffect.m_stopMessage = "You feel less safe";
                SafeEffect = new CustomStatusEffect(statusEffect, false);
                ItemManager.Instance.AddStatusEffect(SafeEffect);
                SafeEffectHash = SafeEffect.StatusEffect.NameHash();
            }
            finally {
                PrefabManager.OnVanillaPrefabsAvailable -= AddStatusEffect;
            }
        }


        private void Update() {
            if (Input.GetKeyDown(KeyCode.G)) {
                IsSafetyCircleActive = !IsSafetyCircleActive;
            }
        }

        [HarmonyPatch(typeof(EffectArea))]
        internal static class EffectAreaPatch {
            /// <summary>
            ///     Catch things that are not pieces but have a PlayerBase effect
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPatch(nameof(EffectArea.Awake))]
            private static void AwakePostfix(EffectArea __instance) {
                AddSafetyEffects(__instance);
            }
        }

        [HarmonyPatch(typeof(Piece))]
        internal static class PiecePatch {
            /// <summary>
            ///     Catch pieces that mods like MVBP add a PlayerBase effect to
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            [HarmonyPatch(nameof(Piece.Awake))]
            private static void AwakePostfix(Piece __instance) {
                AddSafteyEffectsToPiece(__instance.gameObject);
            }

            /// <summary>
            ///     Catch pieces that mods like MVBP add a PlayerBase effect to
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            [HarmonyPatch(nameof(Piece.SetCreator))]
            private static void SetCreatorPostfix(Piece __instance) {
                AddSafteyEffectsToPiece(__instance.gameObject);
            }

            /// <summary>
            ///     Scans components in children and add SafeEffect if PlayerBase effect area is found.
            /// </summary>
            /// <param name="gameObject"></param>
            private static void AddSafteyEffectsToPiece(GameObject gameObject) {
                if (!gameObject) { return; }

                foreach (var effectArea in gameObject.GetComponentsInChildren<EffectArea>()) {
                    AddSafetyEffects(effectArea);
                }
            }
        }

        private static void AddSafetyEffects(EffectArea effectArea) {
            if (effectArea.m_type == EffectArea.Type.PlayerBase) {
                effectArea.m_statusEffect = SafeEffectName;
                effectArea.m_statusEffectHash = SafeEffectHash;

                //add visible rings
                if (!effectArea.GetComponent<SafetyCircle>()) {
                    effectArea.gameObject.AddComponent<SafetyCircle>();
                }
            }
        }

        [HarmonyPatch(typeof(Player))]
        internal static class PlayerPatch {
            /// <summary>
            ///     Patch to check if the SafeStatusEffect should be removed.
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPatch(nameof(Player.UpdateEnvStatusEffects))]
            private static void UpdateEnvStatusEffectsPostFix(Player __instance) {
                var inPlayerBase = EffectArea.IsPointInsideArea(__instance.transform.position, EffectArea.Type.PlayerBase, 1f);
                var hasSafeEffect = __instance.m_seman.HaveStatusEffect(SafeEffectName);

                if (hasSafeEffect && !inPlayerBase) {
                    __instance.m_seman.RemoveStatusEffect(SafeEffectHash);
                }
            }
        }
    }

    /// <summary>
    /// Helper class for properly logging from static contexts.
    /// </summary>
    internal static class Log {
        internal static ManualLogSource _logSource;

        internal static void Init(ManualLogSource logSource) {
            _logSource = logSource;
        }

        internal static void LogDebug(object data) => _logSource.LogDebug(data);

        internal static void LogError(object data) => _logSource.LogError(data);

        internal static void LogFatal(object data) => _logSource.LogFatal(data);

        internal static void LogInfo(object data) => _logSource.LogInfo(data);

        internal static void LogMessage(object data) => _logSource.LogMessage(data);

        internal static void LogWarning(object data) => _logSource.LogWarning(data);
    }
}