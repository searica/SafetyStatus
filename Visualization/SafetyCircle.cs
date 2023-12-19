using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace SafetyStatus.Visualization {

    internal class SafetyCircle : MonoBehaviour {

        private static Material mudMarker;
        private static Mesh quadMesh;
        private GameObject visualization;
        private float lastRadius = float.NegativeInfinity;

        private static Material GetMaterial() {
            if (mudMarker == null) {
                var path_v2 = PrefabManager.Instance.GetPrefab("path_v2");
                if (!path_v2) {
                    Log.LogError("Could not find material!");
                }
                mudMarker = path_v2.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
            }

            return mudMarker;
        }
        private static Mesh GetMesh() {
            if (quadMesh == null) {
                var path_v2 = PrefabManager.Instance.GetPrefab("path_v2");
                if (!path_v2) {
                    Log.LogError("Could not find mesh!");
                }
                quadMesh = path_v2.GetComponentInChildren<MeshFilter>(true).sharedMesh;
            }

            return quadMesh;
        }



        private void Awake() {

            visualization = new GameObject("visualization");
            visualization.transform.parent = base.transform;
            visualization.transform.localPosition = Vector3.zero;

            // set up static bounds
            var bounds = new GameObject("bounds");
            bounds.transform.parent = visualization.transform;
            bounds.transform.localPosition = Vector3.zero;
            bounds.transform.localEulerAngles = new Vector3(90f, 0f, 0f);

            var meshFilter = bounds.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetMesh();

            var meshRenderer = bounds.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetMaterial();

            bounds.SetActive(true);

            // Set up paticle system
            var particleSystem = visualization.AddComponent<ParticleSystem>();
            particleSystem.useAutoRandomSeed = true;

            var psMain = particleSystem.main;
            psMain.duration = 10f;
            psMain.loop = true;
            psMain.prewarm = true;
            psMain.startLifetime = 8f;
            psMain.startSpeed = 0f;
            psMain.startSize3D = false;
            psMain.startSize = 1f;
            psMain.startRotation3D = false;
            psMain.startRotation = 0f;
            psMain.flipRotation = 0f;
            psMain.startColor = Color.white;
            psMain.gravityModifier = 0f;
            psMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            psMain.simulationSpeed = 1f;
            psMain.useUnscaledTime = false;
            psMain.scalingMode = ParticleSystemScalingMode.Local;
            psMain.playOnAwake = true;
            //psMain.emitterVelocity = 
            psMain.maxParticles = 10;
            psMain.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
            //psMain.cullingMode = ParticleSystemCullingMode.Automatic;
            psMain.stopAction = ParticleSystemStopAction.None;
            //psMain.stopAction = ParticleSystemStopAction.Destroy;
            psMain.ringBufferMode = ParticleSystemRingBufferMode.Disabled;

            var emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 1f;
            emission.rateOverDistance = 0f;

            var shape = particleSystem.shape;
            shape.enabled = true;
            shape.radius = 0.01f;
            shape.radiusThickness = 0.2f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            shape.arcSpread = 0f;
            shape.texture = null;
            shape.scale = Vector3.one;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            // A simple 2 color gradient with a fixed alpha of 1.0f.
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(Color.white, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.6f, 0.0f),
                    new GradientAlphaKey(0.1f, 0.75f),
                    new GradientAlphaKey(0.05f, 1.0f) }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.0f);
            sizeCurve.AddKey(0.75f, 1.0f);
            var sizeMinMaxCurve = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);
            sizeOverLifetime.size = sizeMinMaxCurve;

            var psRenderer = visualization.GetComponent<ParticleSystemRenderer>();
            psRenderer.enabled = true;
            psRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            psRenderer.normalDirection = 1f;
            psRenderer.sharedMaterial = GetMaterial();
            psRenderer.sortMode = ParticleSystemSortMode.None;
            psRenderer.sortingFudge = 0f;
            psRenderer.minParticleSize = 0f;
            psRenderer.maxParticleSize = 10f;
            psRenderer.flip = Vector3.zero;
            psRenderer.pivot = Vector3.zero;
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;
            psRenderer.shadowBias = 0f;
            psRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            psRenderer.sortingLayerID = 0;
            psRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            psRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            //psRenderer.EnableVertexStreams(ParticleSystemVertexStreams.)
            // send custom data to the shader
            psRenderer.SetActiveVertexStreams(
                new List<ParticleSystemVertexStream>(
                    new ParticleSystemVertexStream[] {
                        ParticleSystemVertexStream.Position,
                        ParticleSystemVertexStream.Color,
                        ParticleSystemVertexStream.UV
                    }
                )
            );

            if (SafetyStatus.IsSafetyCircleActive) {
                visualization.SetActive(SafetyStatus.IsSafetyCircleActive);
            }
        }

        private bool TryGetPlayerBaseRadius(out float radius) {
            radius = float.NegativeInfinity;

            if (TryGetComponent(out EffectArea effectArea) &&
                effectArea.m_type == EffectArea.Type.PlayerBase &&
                effectArea.m_collider &&
                effectArea.m_collider.GetType() == typeof(SphereCollider)
            ) {
                var collider = effectArea.m_collider as SphereCollider;
                radius = collider.radius;
            }

            return radius != float.NegativeInfinity;
        }

        private void Update() {
            visualization.SetActive(SafetyStatus.IsSafetyCircleActive);
            if (visualization.activeSelf &&
                TryGetPlayerBaseRadius(out float radius) &&
                radius != lastRadius
            ) {
                lastRadius = radius;
                visualization.transform.localScale = Vector3.one * lastRadius * 2;
            }
        }
    }
}