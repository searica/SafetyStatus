// Ignore Spelling: SafetyStatus Jotunn
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;


namespace SafetyStatus
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, Jotunn.Main.Version)]
    internal sealed class SafetyStatus : BaseUnityPlugin
    {
        internal const string Author = "Searica";
        public const string PluginName = "SafetyStatus";
        public const string PluginGUID = $"{Author}.Valheim.{PluginName}";
        public const string PluginVersion = "1.2.2";

        private ConfigEntry<string> tileColor;
        private ConfigEntry<int> tileAlpha;
        private ConfigEntry<KeyboardShortcut> keyBind;

        internal static CustomStatusEffect SafeEffect;
        internal const string SafeEffectName = "SafeStatusEffect";
        internal static int SafeEffectHash;

        internal static SafetyStatus Instance { get; private set; }
        internal static Dictionary<EffectArea, List<GameObject>> Visuals = new();
        internal static bool VisualsOn = false;

        static readonly List<Vector3> vertices = new();
        static readonly List<int> triangles = new();
        static readonly List<Vector2> uvs = new();

        public void Awake()
        {
            Instance = this;

            Log.Init(Logger);

            tileColor = Config.Bind("Appearance", "TileColor", "Green", "Set the color of the terrain mesh (color name or hex)");
            tileAlpha = Config.Bind("Appearance", "TileAlpha", 50, new ConfigDescription("Set the transparency of the terrain mesh (0 = transparent, 100 = opaque).", new AcceptableValueRange<int>(0, 100)));
            keyBind = Config.Bind("Keybind", "ToggleVisuals", new KeyboardShortcut(KeyCode.F7), "Keybind to toggle the visibility of the visual effect areas.");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), harmonyInstanceId: PluginGUID);

            Game.isModded = true;

            PrefabManager.OnVanillaPrefabsAvailable += AddCustomStatusEffect;
        }

        public void Update()
        {
            if (keyBind.Value.IsDown())
            {
                VisualsOn = !VisualsOn;

                foreach (var ea in Visuals.Keys)
                {
                    if (Visuals[ea].Count == 0)
                    {
                        VisualiseEffectArea(ea);
                    }

                    foreach (var go in Visuals[ea])
                    {
                        go.SetActive(VisualsOn);
                    }
                }

            }
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
                SafeEffect = new CustomStatusEffect(statusEffect, false);
                SafeEffectHash = SafeEffect.StatusEffect.NameHash();
                ItemManager.Instance.AddStatusEffect(SafeEffect);
            }
            finally
            {
                PrefabManager.OnVanillaPrefabsAvailable -= AddCustomStatusEffect;
            }
        }

        /// <summary>
        ///     Generate the intial visual vertices
        /// </summary>
        void GenerateShape()
        {
            if (vertices.Count > 0) return;

            int ringCount = 20;
            int segments = 60;

            vertices.Clear();
            triangles.Clear();
            uvs.Clear();

            // Add centre vertex
            vertices.Add(Vector3.zero);
            uvs.Add(Vector2.zero);

            for (int r = 1; r <= ringCount; r++)
            {
                float currentRadius = r / (float)ringCount;
                for (int s = 0; s < segments; s++)
                {
                    float angle = (s / (float)segments) * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * currentRadius;
                    float z = Mathf.Sin(angle) * currentRadius;
                    vertices.Add(new Vector3(x, 0f, z));
                    uvs.Add(new Vector2(x, z));
                }
            }

            // Triangles
            for (int r = 0; r < ringCount - 1; r++)
            {
                int start = 1 + r * segments;
                int next = start + segments;

                for (int s = 0; s < segments; s++)
                {
                    int curr = start + s;
                    int nextSeg = start + (s + 1) % segments;
                    int currNextRing = next + s;
                    int nextNextRing = next + (s + 1) % segments;

                    if (r == 0)
                    {
                        // Fan from centre
                        triangles.Add(0);
                        triangles.Add(nextSeg);
                        triangles.Add(curr);
                    }

                    // Quads between rings (split into two triangles)
                    triangles.Add(currNextRing);
                    triangles.Add(curr);
                    triangles.Add(nextSeg);

                    triangles.Add(nextNextRing);
                    triangles.Add(currNextRing);
                    triangles.Add(nextSeg);
                }
            }
        }

        /// <summary>
        ///     Apply the generated shape to each PlayerBase EffectArea
        /// </summary>
        internal void VisualiseEffectArea(EffectArea area)
        {
            GenerateShape();

            float radius = area.GetRadius();
            Vector3 centre = area.transform.position;

            List<Vector3> verts = new(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 offset = vertices[i] * radius;
                Vector3 pos = centre + offset;
                float terrainY = ZoneSystem.instance.GetGroundHeight(pos);
                verts.Add(new Vector3(pos.x, terrainY + 0.25f, pos.z));
            }

            GenerateMesh(verts, triangles, uvs, area);
        }

        internal void GenerateMesh(List<Vector3> verts, List<int> tris, List<Vector2> uvs, EffectArea area)
        {
            Mesh mesh = new Mesh
            {
                vertices = verts.ToArray(),
                triangles = tris.ToArray(),
                uv = uvs.ToArray()
            };
            mesh.RecalculateNormals();

            GameObject tile = new GameObject("SafetyStatus_TerrainMesh");
            tile.transform.SetParent(transform);
            MeshFilter mf = tile.AddComponent<MeshFilter>();
            mf.mesh = mesh;
            MeshRenderer mr = tile.AddComponent<MeshRenderer>();

            string colorInput = tileColor.Value.ToLower();
            Color parsedColor = Color.green; // Default fallback

            var colorProperty = typeof(Color).GetProperty(colorInput, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static); //interpret color from config
            if (colorProperty != null)
            {
                parsedColor = (Color)colorProperty.GetValue(null);
            }
            else if (ColorUtility.TryParseHtmlString(colorInput.StartsWith("#") ? colorInput : "#" + colorInput, out Color hexColor))
            {
                parsedColor = hexColor;
            }

            mr.material = new Material(Resources.FindObjectsOfTypeAll<Shader>().First(s => s.name == "UI/Unlit/Transparent")) { 
                color = new Color(parsedColor.r, parsedColor.g, parsedColor.b, Mathf.Clamp(tileAlpha.Value / 100f, 0f, 1f)) 
            };

            Visuals[area].Add(tile);

        }

        /// <summary>
        ///     Clear effectArea list and visuals when switching worlds
        /// </summary>

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        internal class ZoneSystemStartPatch
        {
            [HarmonyPostfix]
            private static void ZoneSystemStartPostfix()
            {

                foreach (var effectArea in Visuals)
                {
                    foreach (var tile in effectArea.Value)
                    {
                        if (tile != null)
                            GameObject.Destroy(tile);
                    }
                }

                Visuals.Clear();

                VisualsOn = false;
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

                    if (!Visuals.ContainsKey(__instance))
                    {
                        Visuals[__instance] = new List<GameObject>();

                        if (Player.m_localPlayer != null && VisualsOn == true)
                        {
                            SafetyStatus.Instance?.VisualiseEffectArea(__instance);
                        }
                    }
                }
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(EffectArea.OnDestroy))]
            private static void OnDestroyPostfix(EffectArea __instance)
            {
                if (__instance.m_type == EffectArea.Type.PlayerBase)
                {
                    if (Visuals.ContainsKey(__instance))
                    {
                        foreach (var tile in Visuals[__instance])
                        {
                            GameObject.Destroy(tile);
                        }
                        Visuals.Remove(__instance);
                    }
                }
            }

            /// <summary>
            ///     SafetyStatus is also applied to non player creatures so when they die it tries to update 
            ///     the status effect for them since they didn't leave, but they are not there any more.
            ///     So remove any invalid items from list of items to update the status effect of the EffectArea for.
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
                __instance.m_collidedWithCharacter = __instance.m_collidedWithCharacter.Where(x => IsValidCollidedWithCharacter(x)).ToList();
            }

            private static bool IsValidCollidedWithCharacter(Character item)
            {
                if (!item || item.GetSEMan() == null || !item.GetSEMan().m_nview || !item.GetSEMan().m_nview.IsValid())
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
            [HarmonyPatch(nameof(Piece.OnPlaced))]
            private static void OnPlacedPostfix(Piece __instance)
            {
                if (!__instance)
                {
                    return;
                }

                AddSafeEffect(__instance.gameObject);
                AddVisual(__instance.gameObject);
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

            private static void AddVisual(GameObject gameObject)
            {
                if (!gameObject) { return; }

                foreach (var effectArea in gameObject.GetComponentsInChildren<EffectArea>())
                {
                    if (effectArea.m_type == EffectArea.Type.PlayerBase && Player.m_localPlayer != null)
                    {
                        if (!Visuals.ContainsKey(effectArea))
                        {
                            Visuals[effectArea] = new List<GameObject>();
                        }

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
