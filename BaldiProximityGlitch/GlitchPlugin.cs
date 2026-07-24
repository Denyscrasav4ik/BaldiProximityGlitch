using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using BaldiProximityGlitch.KinoGlitch;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BaldiProximityGlitch
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class GlitchPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "denyscrasav4ik.thedumbfactory.baldiproximityglitch";
        public const string ModName = "Baldi Proximity Glitch";
        public const string ModVersion = "1.1.0";

        public static GlitchPlugin? Instance { get; private set; }

        private Harmony? _harmony;
        private Shader? _glitchShader;
        private AnalogGlitchRenderPass? _renderPass;
        private AnalogGlitchVolume? _glitchVolume;
        private static readonly HashSet<Camera> TargetCameras = new HashSet<Camera>();

        private ConfigEntry<float>? _configMinDistance;
        private ConfigEntry<float>? _configMaxDistance;
        private ConfigEntry<float>? _configMaxIntensity;

        private PlayerManager? _cachedPlayer;
        private Baldi? _cachedBaldi;

        private void Awake()
        {
            Instance = this;

            _configMinDistance = Config.Bind(
                "Settings",
                "MinDistance",
                5f,
                "Distance at which glitch intensity reaches its maximum."
            );

            _configMaxDistance = Config.Bind(
                "Settings",
                "MaxDistance",
                70f,
                "Distance at which glitch effects start becoming visible."
            );

            _configMaxIntensity = Config.Bind(
                "Settings",
                "MaxIntensity",
                1.0f,
                "Maximum intensity cap for the glitch effect (Range: 0.0 to 1.0)."
            );

            LoadEmbeddedShader();
            SetupGlitchVolume();
            _renderPass = new AnalogGlitchRenderPass(_glitchShader!);
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _harmony = new Harmony(ModGuid);
            _harmony.PatchAll(typeof(GlitchPlugin));
            _harmony.PatchAll(typeof(GameCameraPatch));

            Logger.LogInfo("Baldi Proximity Glitch plugin successfully initialized.");
        }

        private void Update()
        {
            if (_glitchVolume == null) return;

            if (_cachedPlayer == null)
                _cachedPlayer = FindObjectOfType<PlayerManager>();

            if (_cachedBaldi == null)
                _cachedBaldi = FindObjectOfType<Baldi>();

            if (_cachedPlayer != null && _cachedBaldi != null)
            {
                float minDist = _configMinDistance?.Value ?? 5f;
                float maxDist = _configMaxDistance?.Value ?? 70f;
                float maxIntensity = Mathf.Clamp01(_configMaxIntensity?.Value ?? 1.0f);

                float distance = Vector3.Distance(_cachedPlayer.transform.position, _cachedBaldi.transform.position);

                float range = Mathf.Max(0.0001f, maxDist - minDist);
                float t = Mathf.Clamp01(1f - ((distance - minDist) / range));

                float intensity = Mathf.Pow(t, 2f) * maxIntensity;

                _glitchVolume.scanLineJitter.value = intensity;
                _glitchVolume.verticalJump.value = intensity;
                _glitchVolume.horizontalShake.value = intensity;
                _glitchVolume.colorDrift.value = intensity;
            }
            else
            {
                _glitchVolume.scanLineJitter.value = 0f;
                _glitchVolume.verticalJump.value = 0f;
                _glitchVolume.horizontalShake.value = 0f;
                _glitchVolume.colorDrift.value = 0f;
            }
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            _renderPass?.Dispose();
            _harmony?.UnpatchSelf();
        }

        private void LoadEmbeddedShader()
        {
            string bundleFileName = true switch
            {
                _ when RuntimeInformation.IsOSPlatform(OSPlatform.Windows) => "assets-win.bundle",
                _ when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "assets-mac.bundle",
                _ when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "assets-linux.bundle",
                _ => throw new PlatformNotSupportedException("Unsupported Operating System platform.")
            };

            Assembly executingAssembly = Assembly.GetExecutingAssembly();

            string resourceName = executingAssembly.GetManifestResourceNames()
                .FirstOrDefault(str => str.EndsWith(bundleFileName, StringComparison.OrdinalIgnoreCase));

            using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
            {
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);

                AssetBundle bundle = AssetBundle.LoadFromMemory(buffer);
                _glitchShader = bundle.LoadAsset<Shader>("AnalogGlitch");
                bundle.Unload(false);
            }
        }

        private void SetupGlitchVolume()
        {
            GameObject volumeObject = new GameObject("AnalogGlitchVolumeObject");
            DontDestroyOnLoad(volumeObject);

            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _glitchVolume = profile.Add<AnalogGlitchVolume>(true);

            _glitchVolume.scanLineJitter.overrideState = true;
            _glitchVolume.verticalJump.overrideState = true;
            _glitchVolume.horizontalShake.overrideState = true;
            _glitchVolume.colorDrift.overrideState = true;

            volume.profile = profile;
        }

        public static void RegisterCamera(Camera cam)
        {
            if (cam == null) return;

            var camData = cam.gameObject.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null)
            {
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            camData.renderPostProcessing = true;

            TargetCameras.Add(cam);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_renderPass != null && TargetCameras.Contains(camera))
            {
                var camData = camera.GetUniversalAdditionalCameraData();
                if (camData != null && camData.scriptableRenderer != null)
                {
                    camData.scriptableRenderer.EnqueuePass(_renderPass);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GameCamera))]
    public static class GameCameraPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        public static void Awake_Postfix(GameCamera __instance)
        {
            FieldInfo[] fields = typeof(GameCamera).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == typeof(Camera))
                {
                    Camera cam = (field.GetValue(__instance) as Camera)!;
                    if (cam != null)
                    {
                        GlitchPlugin.RegisterCamera(cam);
                    }
                }
            }
        }
    }
}
