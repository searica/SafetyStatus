// Ignore Spelling: SafetyStatus Jotunn
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace SafetyStatus
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, Jotunn.Main.Version)]
    internal sealed class SafetyStatus : BaseUnityPlugin
    {
        internal const string Author = "Searica";
        public const string PluginName = "SafetyStatus";
        public const string PluginGUID = $"{Author}.Valheim.{PluginName}";
        public const string PluginVersion = "1.3.3";

        internal static CustomStatusEffect SafeEffect;
        internal const string SafeEffectName = "SafeStatusEffect";
        internal static int SafeEffectHash;

        public void Awake()
        {
            Log.Init(Logger);

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

            Game.isModded = true;

            PrefabManager.OnVanillaPrefabsAvailable += AddCustomStatusEffect;
        }

        /// <summary>
        ///     Create and add the Safe status effect
        /// </summary>
        private void AddCustomStatusEffect()
        {
            try
            {
                StatusEffect statusEffect = ScriptableObject.CreateInstance<StatusEffect>();
                statusEffect.name = SafeEffectName;
                statusEffect.m_name = "Safe";
                statusEffect.m_icon = PrefabManager.Cache.GetPrefab<CraftingStation>("piece_workbench").m_icon;
                statusEffect.m_startMessageType = MessageHud.MessageType.TopLeft;
                statusEffect.m_startMessage = "You feel safer";
                statusEffect.m_stopMessageType = MessageHud.MessageType.TopLeft;
                statusEffect.m_stopMessage = "You feel less safe";
                statusEffect.m_effectsOnlyOnPlayer = true;
                SafeEffect = new CustomStatusEffect(statusEffect, false);
                SafeEffectHash = SafeEffect.StatusEffect.NameHash();
                ItemManager.Instance.AddStatusEffect(SafeEffect);
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= AddCustomStatusEffect;
            }
        }

        [HarmonyPatch(typeof(EffectArea))]
        internal static class EffectAreaPatch
        {
            /// <summary>
            ///     Catch things that are not pieces but have a PlayerBase effect
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPatch(nameof(EffectArea.Awake))]
            private static void AwakePostfix(EffectArea __instance)
            {
                if (__instance.m_type == EffectArea.Type.PlayerBase)
                {
                    __instance.m_statusEffect = SafeEffectName;
                    __instance.m_statusEffectHash = SafeEffectHash;
                }
            }

            /// <summary>
            ///     SafetyStatus should only be applied to players but it appears to be applied to hens in some cases? Add a check
            ///     before updating status effects on targets to remove any invalid targets that are not actually there anymore, such 
            ///     as a dead player who never exited the collider and is not removed from the list of targets. This patch runs for all
            ///     status effects though, which can be applied to targets other than players, so make sure to check and respect whether
            ///     the status effect should only affect players in the `IsValidColliedWithCharacter` function.
            /// </summary>
            /// <param name="__instance"></param>
            /// <param name="deltaTime"></param>
            [HarmonyPrefix]
            [HarmonyPatch(nameof(EffectArea.CustomFixedUpdate))]
            private static void CustomFixedUpdatePrefex(EffectArea __instance, float deltaTime)
            {
                if (!__instance)
                {
                    return;
                }
                __instance.m_collidedWithCharacter = __instance.m_collidedWithCharacter.Where(x => IsValidCollidedWithCharacter(x, __instance.m_playerOnly, __instance.m_statusEffectHash == SafeEffectHash)).ToList();
            }

            /// <summary>
            ///     Check if the collided character is a valid target for the status effect.
            /// </summary>
            /// <param name="character">Character to check.</param>
            /// <param name="playerOnly">Whether status effect should only be applied to player.</param>
            /// <param name="localPlayerOnly">Whether status effect should only be applied to a local player.</param>
            /// <returns></returns>
            private static bool IsValidCollidedWithCharacter(Character character, bool playerOnly, bool localPlayerOnly)
            {
                if (
                    !character 
                    || (playerOnly && !character.IsPlayer()) 
                    || (localPlayerOnly && character != Player.m_localPlayer) 
                    || character.GetSEMan() == null 
                    || !character.GetSEMan().m_nview 
                    || !character.GetSEMan().m_nview.IsValid()
                )
                {
                    return false;
                }

                return true;
            }

        }

        [HarmonyPatch(typeof(Piece))]
        internal static class PiecePatch
        {
            /// <summary>
            ///     Catch pieces that mods like MVBP add a PlayerBase effect to
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            [HarmonyPatch(nameof(Piece.Awake))]
            private static void AwakePostfix(Piece __instance)
            {
                if (!__instance)
                {
                    return;
                }

                AddSafeEffect(__instance.gameObject);
            }

            /// <summary>
            ///     Catch pieces that mods like MVBP add a PlayerBase effect to
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPriority(Priority.Low)]
            [HarmonyPatch(nameof(Piece.SetCreator))]
            private static void SetCreatorPostfix(Piece __instance)
            {
                if (!__instance)
                {
                    return;
                }

                AddSafeEffect(__instance.gameObject);
            }

            /// <summary>
            ///     Scans components in children and add SafeEffect if PlayerBase effect area is found.
            /// </summary>
            /// <param name="gameObject"></param>
            private static void AddSafeEffect(GameObject gameObject)
            {
                if (!gameObject) { return; }

                foreach (var effectArea in gameObject.GetComponentsInChildren<EffectArea>())
                {
                    if (effectArea.m_type == EffectArea.Type.PlayerBase)
                    {
                        effectArea.m_statusEffect = SafeEffectName;
                        effectArea.m_statusEffectHash = SafeEffectHash;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Player))]
        internal static class PlayerPatch
        {
            /// <summary>
            ///     Patch to check if the SafeStatusEffect should be removed.
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPostfix]
            [HarmonyPatch(nameof(Player.UpdateEnvStatusEffects))]
            private static void UpdateEnvStatusEffectsPostFix(Player __instance)
            {
                if (__instance != Player.m_localPlayer)
                {
                    return;
                }

                var inPlayerBase = EffectArea.IsPointInsideArea(__instance.transform.position, EffectArea.Type.PlayerBase, 1f);
                var hasSafeEffect = __instance.m_seman.HaveStatusEffect(SafeEffectHash);

                if (hasSafeEffect && !inPlayerBase)
                {
                    __instance.m_seman.RemoveStatusEffect(SafeEffectHash);
                }
            }
        }
    }

    /// <summary>
    /// Helper class for properly logging from static contexts.
    /// </summary>
    internal static class Log
    {
        internal static ManualLogSource _logSource;

        internal static void Init(ManualLogSource logSource)
        {
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