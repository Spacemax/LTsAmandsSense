using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using Comfort.Common;
using System.IO;
using UnityEngine.Networking;
using EFT.Interactive;
using System.Linq;
using SPT.Common.Utils;
using SPT.Common.Http;
using SPT.Reflection.Utils;
using Sirenix.Utilities;
using UnityEngine.UI;
using TMPro;
using static EFT.Player;

namespace AmandsSense
{
    public class AmandsSenseClass : MonoBehaviour
    {
        public static Player Player;
        public static PlayerInventoryController inventoryControllerClass;

        public static RaycastHit hit;
        public static LayerMask LowLayerMask;
        public static LayerMask HighLayerMask;
        public static LayerMask FoliageLayerMask;

        public static float CooldownTime = 0f;
        public static float AlwaysOnTime = 0f;
        public static float Radius = 0f;

        public static PrismEffects prismEffects;

        public static ItemsJsonClass itemsJsonClass;

        public static float lastDoubleClickTime = 0.0f;

        public static AudioSource SenseAudioSource;

        public static Dictionary<string, Sprite> LoadedSprites = new Dictionary<string, Sprite>();
        public static Dictionary<string, AudioClip> LoadedAudioClips = new Dictionary<string, AudioClip>();

        // ============================================================
        // Price Cache - Real flea market prices for accurate tier detection
        // ============================================================
        /// <summary>Cached flea market prices from Session.RagfairGetPrices()</summary>
        private static Dictionary<string, float> _fleaPriceCache = null;
        /// <summary>Cached trader prices by item template ID</summary>
        private static Dictionary<string, int> _traderPriceCache = new Dictionary<string, int>();
        /// <summary>Whether the price cache has been initialized</summary>
        public static bool PriceCacheInitialized = false;
        /// <summary>Session reference for price lookups</summary>
        private static ISession _session = null;

        /// <summary>
        /// Resolved path to the Sense assets folder. Checked once at startup.
        /// </summary>
        private static string _senseBasePath = null;

        /// <summary>
        /// Gets the base path to the Sense assets folder, checking multiple possible locations.
        /// </summary>
        public static string SenseBasePath
        {
            get
            {
                if (_senseBasePath == null)
                {
                    _senseBasePath = ResolveSenseBasePath();
                }
                return _senseBasePath;
            }
        }

        /// <summary>
        /// Resolves the Sense assets folder path by checking multiple locations.
        /// </summary>
        private static string ResolveSenseBasePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] possiblePaths = new[]
            {
                Path.Combine(baseDir, "BepInEx", "plugins", "AmandsSense", "Sense"),
                Path.Combine(baseDir, "BepInEx", "plugins", "Sense"),
                Path.Combine(baseDir, "BepInEx", "plugins", "AmandsSense")
            };

            foreach (string path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Found Sense assets at: {path}");
                    return path;
                }
            }

            // Default to first option even if it doesn't exist
            string defaultPath = possiblePaths[0];
            AmandsSensePlugin.Log.LogWarning($"[AmandsSense] Sense assets folder not found! Expected at: {defaultPath}");
            AmandsSensePlugin.Log.LogWarning($"[AmandsSense] Please create the folder structure: {defaultPath}/images/ and {defaultPath}/sounds/");
            return defaultPath;
        }

        #region Camera Access (SPT4 Compatible)

        private static Camera _cachedCamera;
        private static float _cameraRefreshTime;
        private const float CameraRefreshInterval = 1f;

        private static float _screenScale = 1f;
        private static float _nextScaleCheckTime;

        /// <summary>
        /// Gets the main game camera using EFT's CameraClass with fallback to GameCamera.
        /// Caches the result for performance.
        /// </summary>
        public static Camera GameCamera
        {
            get
            {
                if (Time.time > _cameraRefreshTime || _cachedCamera == null)
                {
                    _cachedCamera = GetCamera();
                    _cameraRefreshTime = Time.time + CameraRefreshInterval;
                }
                return _cachedCamera;
            }
        }

        /// <summary>
        /// Gets the correct camera reference for EFT/SPT4.
        /// Prefers CameraClass.Instance.Camera, falls back to GameCamera.
        /// </summary>
        private static Camera GetCamera()
        {
            try
            {
                var cameraClass = CameraClass.Instance;
                if (cameraClass?.Camera != null)
                    return cameraClass.Camera;
            }
            catch { }

            return GameCamera;
        }

        /// <summary>
        /// Gets SSAA screen scale factor for correct WorldToScreenPoint coordinate conversion.
        /// When SSAA (Super-sampling Anti-Aliasing) is enabled, render buffer differs from display resolution.
        /// This correction is CRITICAL for proper UI positioning with different graphics settings.
        /// </summary>
        public static float GetScreenScale()
        {
            if (Time.time > _nextScaleCheckTime)
            {
                _nextScaleCheckTime = Time.time + 5f; // Check every 5 seconds
                try
                {
                    var ssaa = CameraClass.Instance?.SSAA;
                    if (ssaa != null && ssaa.isActiveAndEnabled)
                    {
                        float outputWidth = ssaa.GetOutputWidth();
                        float inputWidth = ssaa.GetInputWidth();
                        if (inputWidth > 0)
                        {
                            _screenScale = outputWidth / inputWidth;
                        }
                    }
                    else
                    {
                        _screenScale = 1f;
                    }
                }
                catch
                {
                    _screenScale = 1f;
                }
            }
            return _screenScale;
        }

        #endregion

        public static Vector3[] SenseOverlapLocations = new Vector3[9] { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.forward + Vector3.left, Vector3.forward + Vector3.right, Vector3.back + Vector3.left, Vector3.back + Vector3.right };
        public static int CurrentOverlapLocation = 9;

        public static LayerMask BoxInteractiveLayerMask;
        public static LayerMask BoxDeadbodyLayerMask;
        public static int[] CurrentOverlapCount = new int[9];
        public static Collider[] CurrentOverlapLoctionColliders = new Collider[100];

        public static Dictionary<int, AmandsSenseWorld> SenseWorlds = new Dictionary<int, AmandsSenseWorld>();

        public static List<SenseDeadPlayerStruct> DeadPlayers = new List<SenseDeadPlayerStruct>();

        public static List<AmandsSenseExfil> SenseExfils = new List<AmandsSenseExfil>();
        public static AmandsSenseExfil ClosestAmandsSenseExfil = null;

        public static List<Item> SenseItems = new List<Item>();

        public static Transform parent;

        public static string scene;
        public void OnGUI()
        {
            /*GUILayout.BeginArea(new Rect(20, 10, 1280, 720));
            GUILayout.Label("SenseWorlds " + SenseWorlds.Count().ToString());
            GUILayout.EndArea();*/
        }
        private void Awake()
        {
            LowLayerMask = LayerMask.GetMask("Terrain", "LowPolyCollider", "HitCollider");
            HighLayerMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "HitCollider");
            FoliageLayerMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "HitCollider", "Foliage");

            BoxInteractiveLayerMask = LayerMask.GetMask("Interactive");
            BoxDeadbodyLayerMask = LayerMask.GetMask("Deadbody");
        }
        public void Start()
        {
            // Try to load Items.json from the resolved Sense path
            string itemsJsonPath = Path.Combine(SenseBasePath, "Items.json");
            if (File.Exists(itemsJsonPath))
            {
                try
                {
                    itemsJsonClass = ReadFromJsonFile<ItemsJsonClass>(itemsJsonPath);
                    AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Loaded Items.json from: {itemsJsonPath}");
                }
                catch (Exception ex)
                {
                    AmandsSensePlugin.Log.LogError($"[AmandsSense] Failed to parse Items.json: {ex.Message}");
                    itemsJsonClass = new ItemsJsonClass();
                }
            }
            else
            {
                AmandsSensePlugin.Log.LogWarning($"[AmandsSense] Items.json not found at: {itemsJsonPath}");
                AmandsSensePlugin.Log.LogWarning("[AmandsSense] Rare/Kappa item highlighting will not work without Items.json");
                itemsJsonClass = new ItemsJsonClass();
            }

            ReloadFiles(this, false);

            // Initialize price cache for accurate loot tier detection
            StartCoroutine(InitializePriceCache());

            AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Start() complete, enabled={enabled}, gameObject.activeSelf={gameObject?.activeSelf}");
        }

        /// <summary>
        /// Initializes the price cache by fetching flea market prices from the session.
        /// This provides accurate item values for tier detection instead of hardcoded fallbacks.
        /// </summary>
        private IEnumerator InitializePriceCache()
        {
            AmandsSensePlugin.Log.LogInfo("[AmandsSense] Initializing price cache...");

            // Wait for session to be available
            int attempts = 0;
            while (_session == null && attempts < 30)
            {
                try
                {
                    _session = ClientAppUtils.GetMainApp().GetClientBackEndSession();
                }
                catch { }

                if (_session == null)
                {
                    attempts++;
                    yield return new WaitForSeconds(1f);
                }
            }

            if (_session == null)
            {
                AmandsSensePlugin.Log.LogWarning("[AmandsSense] Could not get session for price cache - using fallback pricing");
                PriceCacheInitialized = false;
                yield break;
            }

            // Fetch flea market prices
            bool pricesLoaded = false;
            _session.RagfairGetPrices(result =>
            {
                if (result.Succeed && result.Value != null)
                {
                    _fleaPriceCache = result.Value;
                    AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Loaded {_fleaPriceCache.Count} flea market prices");
                }
                else
                {
                    AmandsSensePlugin.Log.LogWarning("[AmandsSense] Failed to get flea market prices");
                    _fleaPriceCache = new Dictionary<string, float>();
                }
                pricesLoaded = true;
            });

            // Wait for prices to load
            float timeout = 10f;
            while (!pricesLoaded && timeout > 0)
            {
                timeout -= 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            PriceCacheInitialized = true;
            AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Price cache initialized: {_fleaPriceCache?.Count ?? 0} items");
        }

        /// <summary>
        /// Gets the flea market price for an item from the cache.
        /// </summary>
        public static int GetFleaPrice(string templateId)
        {
            if (_fleaPriceCache != null && _fleaPriceCache.TryGetValue(templateId, out float price))
            {
                return (int)price;
            }
            return -1;
        }

        /// <summary>
        /// Gets the best trader price for an item.
        /// </summary>
        public static int GetBestTraderPrice(Item item)
        {
            if (item == null || _session == null)
                return 0;

            // Check cache first
            if (_traderPriceCache.TryGetValue(item.TemplateId, out int cachedPrice))
                return cachedPrice;

            try
            {
                int bestPrice = 0;
                foreach (var trader in _session.Traders)
                {
                    if (trader.SupplyData_0 != null)
                    {
                        var result = trader.GetUserItemPrice(item);
                        if (result != null && result.Value.Amount > bestPrice)
                        {
                            bestPrice = result.Value.Amount;
                        }
                    }
                }

                // Cache the result
                _traderPriceCache[item.TemplateId] = bestPrice;
                return bestPrice;
            }
            catch
            {
                return 0;
            }
        }

        private void OnEnable()
        {
            AmandsSensePlugin.Log.LogInfo("[AmandsSense] OnEnable called");
        }

        private void OnDisable()
        {
            AmandsSensePlugin.Log.LogInfo("[AmandsSense] OnDisable called");
        }

        private bool _loggedUpdateRunning = false;
        private bool _loggedPlayerFallback = false;
        private bool _loggedUpdateStarted = false;
        private int _updateCallCount = 0;

        public void Update()
        {
            _updateCallCount++;

            // Log first 3 Update calls to prove it's running
            if (_updateCallCount <= 3)
            {
                AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Update() call #{_updateCallCount}");
            }

            // Fallback: if Player is null, try to get from GameWorld (like TFS does)
            if (Player == null)
            {
                try
                {
                    var gameWorld = Singleton<GameWorld>.Instance;
                    var mainPlayer = gameWorld?.MainPlayer;
                    if (mainPlayer != null)
                    {
                        Player = mainPlayer;
                        AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Player set from GameWorld.MainPlayer: {mainPlayer.Profile?.Nickname ?? "unknown"}");
                    }
                }
                catch (System.Exception ex)
                {
                    AmandsSensePlugin.Log.LogError($"[AmandsSense] Fallback error: {ex.Message}");
                }
            }

            if (gameObject != null && Player != null && AmandsSensePlugin.EnableSense.Value != EEnableSense.Off)
            {
                // One-time log to confirm Update is running
                if (!_loggedUpdateRunning)
                {
                    _loggedUpdateRunning = true;
                    AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Update loop active - EnableSense={AmandsSensePlugin.EnableSense.Value}, AlwaysOn={AmandsSensePlugin.SenseAlwaysOn.Value}");
                }

                if (CurrentOverlapLocation <= 8)
                {
                    int CurrentOverlapCountTest = Physics.OverlapBoxNonAlloc(Player.Position + (Vector3)(SenseOverlapLocations[CurrentOverlapLocation] * ((AmandsSensePlugin.Radius.Value * 2f) / 3f)), (Vector3.one * ((AmandsSensePlugin.Radius.Value * 2f) / 3f)), CurrentOverlapLoctionColliders, Quaternion.Euler(0f, 0f, 0f), BoxInteractiveLayerMask, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < CurrentOverlapCountTest; i++)
                    {
                        if (!SenseWorlds.ContainsKey(CurrentOverlapLoctionColliders[i].GetInstanceID()))
                        {
                            GameObject SenseWorldGameObject = new GameObject("SenseWorld");
                            AmandsSenseWorld amandsSenseWorld = SenseWorldGameObject.AddComponent<AmandsSenseWorld>();
                            amandsSenseWorld.OwnerCollider = CurrentOverlapLoctionColliders[i];
                            amandsSenseWorld.OwnerGameObject = amandsSenseWorld.OwnerCollider.gameObject;
                            amandsSenseWorld.Id = amandsSenseWorld.OwnerCollider.GetInstanceID();
                            amandsSenseWorld.Delay = Vector3.Distance(Player.Position, amandsSenseWorld.OwnerCollider.transform.position) / AmandsSensePlugin.Speed.Value;
                            SenseWorlds.Add(amandsSenseWorld.Id, amandsSenseWorld);
                        }
                        else
                        {
                            SenseWorlds[CurrentOverlapLoctionColliders[i].GetInstanceID()].RestartSense();
                        }
                    }
                    CurrentOverlapLocation++;
                }
                else if (AmandsSensePlugin.SenseAlwaysOn.Value)
                {
                    AlwaysOnTime += Time.deltaTime;
                    if (AlwaysOnTime > AmandsSensePlugin.AlwaysOnFrequency.Value)
                    {
                        AlwaysOnTime = 0f;
                        CurrentOverlapLocation = 0;
                        AmandsSensePlugin.Log.LogInfo($"[AmandsSense] AlwaysOn triggered, calling SenseDeadBodies");
                        SenseDeadBodies();
                    }
                }
                if (CooldownTime < AmandsSensePlugin.Cooldown.Value)
                {
                    CooldownTime += Time.deltaTime;
                }
                if (Input.GetKeyDown(AmandsSensePlugin.SenseKey.Value.MainKey))
                {
                    if (AmandsSensePlugin.DoubleClick.Value)
                    {
                        float timeSinceLastClick = Time.time - lastDoubleClickTime;
                        lastDoubleClickTime = Time.time;

                        // Check if this is a valid double-click within the configured window
                        if (timeSinceLastClick <= AmandsSensePlugin.DoubleClickWindow.Value)
                        {
                            if (CooldownTime >= AmandsSensePlugin.Cooldown.Value)
                            {
                                ActivateSense();
                            }
                            else
                            {
                                // Cooldown feedback - play quieter sound to indicate not ready
                                PlayCooldownFeedback();
                            }
                        }
                    }
                    else
                    {
                        if (CooldownTime >= AmandsSensePlugin.Cooldown.Value)
                        {
                            ActivateSense();
                        }
                        else
                        {
                            // Cooldown feedback - play quieter sound to indicate not ready
                            PlayCooldownFeedback();
                        }
                    }
                }
                if (Radius < Mathf.Max(AmandsSensePlugin.Radius.Value, AmandsSensePlugin.DeadPlayerRadius.Value))
                {
                    Radius += AmandsSensePlugin.Speed.Value * Time.deltaTime;
                    if (prismEffects != null)
                    {
                        prismEffects.dofFocusPoint = Radius - prismEffects.dofFocusDistance;
                        if (prismEffects.dofRadius < 0.5f)
                        {
                            prismEffects.dofRadius += 2f * Time.deltaTime;
                        }
                    }
                }
                else if (prismEffects != null && prismEffects.dofRadius > 0.001f)
                {
                    prismEffects.dofRadius -= 0.5f * Time.deltaTime;
                    if (prismEffects.dofRadius < 0.001f)
                    {
                        prismEffects.useDof = false;
                    }
                }
            }
        }

        /// <summary>
        /// Activates the sense ability - scans for items, corpses, and shows exfils.
        /// </summary>
        private void ActivateSense()
        {
            CooldownTime = 0f;
            CurrentOverlapLocation = 0;
            SenseDeadBodies();
            ShowSenseExfils();

            // Activate DOF effect if enabled
            if (prismEffects != null)
            {
                Radius = 0;
                prismEffects.useDof = AmandsSensePlugin.useDof.Value;
            }

            // Play activation sound
            if (LoadedAudioClips.ContainsKey("Sense.wav"))
            {
                SenseAudioSource.PlayOneShot(LoadedAudioClips["Sense.wav"], AmandsSensePlugin.ActivateSenseVolume.Value);
            }
        }

        /// <summary>
        /// Plays audio feedback when sense is activated during cooldown.
        /// Uses a quieter version of the activation sound to indicate "not ready".
        /// </summary>
        private void PlayCooldownFeedback()
        {
            if (!AmandsSensePlugin.ShowCooldownFeedback.Value)
                return;

            // Play a quieter/muffled version of the sense sound to indicate cooldown
            if (LoadedAudioClips.ContainsKey("Sense.wav"))
            {
                // Use 20% volume to indicate "blocked" state
                SenseAudioSource.PlayOneShot(LoadedAudioClips["Sense.wav"], AmandsSensePlugin.ActivateSenseVolume.Value * 0.2f);
            }
        }

        public void SenseDeadBodies()
        {
            // Log every call to verify the method is being invoked
            AmandsSensePlugin.Log.LogInfo($"[AmandsSense] SenseDeadBodies called, {DeadPlayers.Count} dead players tracked");

            if (Player == null)
            {
                AmandsSensePlugin.Log.LogWarning("[AmandsSense] SenseDeadBodies: Player is null");
                return;
            }

            foreach (SenseDeadPlayerStruct deadPlayer in DeadPlayers)
            {
                if (deadPlayer.victim == null)
                {
                    AmandsSensePlugin.Log.LogWarning("[AmandsSense] Dead player victim is null, skipping");
                    continue;
                }

                if (deadPlayer.victim.gameObject == null)
                {
                    AmandsSensePlugin.Log.LogWarning($"[AmandsSense] Dead player {deadPlayer.victim.Profile?.Nickname ?? "unknown"} has null gameObject");
                    continue;
                }

                float dist = Vector3.Distance(Player.Position, deadPlayer.victim.Position);
                if (dist < AmandsSensePlugin.DeadPlayerRadius.Value)
                {
                    AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Dead player in range: {deadPlayer.victim.Profile?.Nickname ?? "unknown"}, dist={dist:F1}m, creating marker...");

                    int instanceId = deadPlayer.victim.GetInstanceID();
                    if (!SenseWorlds.ContainsKey(instanceId))
                    {
                        try
                        {
                            GameObject SenseWorldGameObject = new GameObject("SenseWorld");
                            AmandsSenseWorld amandsSenseWorld = SenseWorldGameObject.AddComponent<AmandsSenseWorld>();
                            amandsSenseWorld.OwnerGameObject = deadPlayer.victim.gameObject;
                            amandsSenseWorld.Id = instanceId;
                            amandsSenseWorld.Delay = dist / AmandsSensePlugin.Speed.Value;
                            amandsSenseWorld.Lazy = false;
                            amandsSenseWorld.eSenseWorldType = ESenseWorldType.Deadbody;
                            amandsSenseWorld.SenseDeadPlayer = deadPlayer.victim;
                            SenseWorlds.Add(amandsSenseWorld.Id, amandsSenseWorld);
                            AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Created SenseWorld for dead player: {deadPlayer.victim.Profile?.Nickname ?? "unknown"}, id={instanceId}");
                        }
                        catch (System.Exception ex)
                        {
                            AmandsSensePlugin.Log.LogError($"[AmandsSense] Error creating SenseWorld: {ex.Message}");
                        }
                    }
                    else
                    {
                        SenseWorlds[instanceId].RestartSense();
                        AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Restarted SenseWorld for dead player: {deadPlayer.victim.Profile?.Nickname ?? "unknown"}");
                    }
                }
            }
        }
        public void ShowSenseExfils()
        {
            if (!AmandsSensePlugin.EnableExfilSense.Value) return;

            if (scene == "Factory_Day" || scene == "Factory_Night" || scene == "Laboratory_Scripts") return;

            float ClosestDistance = 10000000000f;
            if (ClosestAmandsSenseExfil != null && ClosestAmandsSenseExfil.light != null) ClosestAmandsSenseExfil.light.shadows = LightShadows.None;
            foreach (AmandsSenseExfil senseExfil in SenseExfils)
            {
                if (Player != null && Vector3.Distance(senseExfil.transform.position,Player.gameObject.transform.position) < ClosestDistance)
                {
                    ClosestAmandsSenseExfil = senseExfil;
                    ClosestDistance = Vector3.Distance(senseExfil.transform.position, Player.gameObject.transform.position);
                }

                if (senseExfil.Intensity > 0.5f)
                {
                    senseExfil.LifeSpan = 0f;
                    senseExfil.UpdateSense();
                }
                else
                {
                    senseExfil.ShowSense();
                }
            }
            if (AmandsSensePlugin.ExfilLightShadows.Value && ClosestAmandsSenseExfil != null && ClosestAmandsSenseExfil.light != null) ClosestAmandsSenseExfil.light.shadows = LightShadows.Hard;
        }
        public static void Clear()
        {
            foreach (KeyValuePair<int,AmandsSenseWorld> keyValuePair in SenseWorlds)
            {
                if (keyValuePair.Value != null) keyValuePair.Value.RemoveSense();
            }
            SenseWorlds.Clear();

            ClosestAmandsSenseExfil = null;
            SenseExfils = SenseExfils.Where(x => x != null).ToList();

            DeadPlayers.Clear();
        }
        public static ESenseItemType SenseItemType(Type itemType)
        {
            if (TemplateIdToObjectMappingsClass.TypeTable["57864ada245977548638de91"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.BuildingMaterials;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["57864a66245977548f04a81f"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Electronics;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["57864ee62459775490116fc1"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.EnergyElements;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["57864e4c24597754843f8723"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.FlammableMaterials;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["57864c322459775490116fbf"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.HouseholdMaterials;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["57864c8c245977548867e7f1"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.MedicalSupplies;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["57864bb7245977548b3b66c2"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Tools;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["57864a3d24597754843f8721"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Valuables;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["590c745b86f7743cc433c5f2"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Others;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448e53e4bdc2d60728b4567"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Backpacks;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448e54d4bdc2dcc718b4568"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.BodyArmor;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448e5724bdc2ddf718b4568"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Eyewear;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5a341c4686f77469e155819e"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Facecovers;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5a341c4086f77401f2541505"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Headgear;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["57bef4c42459772e8d35a53b"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.GearComponents;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5b3f15d486f77432d0509248"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.GearComponents;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5645bcb74bdc2ded0b8b4578"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Headsets;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448bf274bdc2dfc2f8b456a"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.SecureContainers;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5795f317245977243854e041"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.StorageContainers;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448e5284bdc2dcb718b4567"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.TacticalRigs;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["550aa4154bdc2dd8348b456b"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.FunctionalMods;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["55802f3e4bdc2de7118b4584"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.GearMods;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5a74651486f7744e73386dd1"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.GearMods;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["55802f4a4bdc2ddb688b4569"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.VitalParts;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5447e1d04bdc2dff2f8b4567"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.MeleeWeapons;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["543be6564bdc2df4348b4568"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Throwables;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["543be5cb4bdc2deb348b4568"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.AmmoPacks;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5485a8684bdc2da71d8b4567"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Rounds;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448e8d64bdc2dce718b4568"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Drinks;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448e8d04bdc2ddf718b4569"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Food;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448f3a64bdc2d60728b456a"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Injectors;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448f3ac4bdc2dce718b4569"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.InjuryTreatment;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448f39d4bdc2d0a728b4568"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Medkits;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448f3a14bdc2d27728b4569"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Pills;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5c164d2286f774194c5e69fa"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.ElectronicKeys;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5c99f98d86f7745c314214b3"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.MechanicalKeys;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5448ecbe4bdc2d60728b4568"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.InfoItems;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5447e0e74bdc2d3c308b4567"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.SpecialEquipment;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["616eb7aea207f41933308f46"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.SpecialEquipment;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["61605ddea09d851a0a0c1bbc"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.SpecialEquipment;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["5f4fbaaca5573a5ac31db429"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.SpecialEquipment;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["567849dd4bdc2d150f8b456e"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Maps;
            }
            if (TemplateIdToObjectMappingsClass.TypeTable["543be5dd4bdc2deb348b4569"].IsAssignableFrom(itemType))
            {
                return ESenseItemType.Money;
            }
            return ESenseItemType.All;
        }
        public static void WriteToJsonFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var contentsToWriteToFile = Json.Serialize(objectToWrite);
                writer = new StreamWriter(filePath, append);
                writer.Write(contentsToWriteToFile);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }
        public static T ReadFromJsonFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                reader = new StreamReader(filePath);
                var fileContents = reader.ReadToEnd();
                return Json.Deserialize<T>(fileContents);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
        /// <summary>
        /// Loads sprite and audio assets using Unity coroutines.
        /// Uses coroutines instead of async/await to avoid Task state transition errors during scene changes.
        /// </summary>
        /// <param name="runner">MonoBehaviour instance to run coroutines on</param>
        /// <param name="onlySounds">If true, only loads audio files</param>
        public static void ReloadFiles(MonoBehaviour runner, bool onlySounds)
        {
            if (runner == null) return;

            if (!onlySounds)
            {
                string imagesPath = Path.Combine(SenseBasePath, "images");
                if (Directory.Exists(imagesPath))
                {
                    string[] files = Directory.GetFiles(imagesPath, "*.png");
                    AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Loading {files.Length} sprite(s) from: {imagesPath}");
                    foreach (string file in files)
                    {
                        runner.StartCoroutine(LoadSpriteCoroutine(file));
                    }
                }
                else
                {
                    AmandsSensePlugin.Log.LogWarning($"[AmandsSense] Images folder not found: {imagesPath}");
                    AmandsSensePlugin.Log.LogWarning("[AmandsSense] Item category icons will not be displayed.");
                }
            }

            string soundsPath = Path.Combine(SenseBasePath, "sounds");
            if (Directory.Exists(soundsPath))
            {
                string[] audioFiles = Directory.GetFiles(soundsPath);
                AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Loading {audioFiles.Length} audio file(s) from: {soundsPath}");
                foreach (string file in audioFiles)
                {
                    runner.StartCoroutine(LoadAudioClipCoroutine(file));
                }
            }
            else
            {
                AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Sounds folder not found (optional): {soundsPath}");
            }
        }

        /// <summary>
        /// Coroutine to load a sprite from disk.
        /// </summary>
        private static IEnumerator LoadSpriteCoroutine(string path)
        {
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(path))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(www);
                    if (texture != null)
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        LoadedSprites[Path.GetFileName(path)] = sprite;
                    }
                }
            }
        }

        /// <summary>
        /// Coroutine to load an audio clip from disk.
        /// </summary>
        private static IEnumerator LoadAudioClipCoroutine(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            AudioType audioType = AudioType.WAV;
            switch (extension)
            {
                case ".wav":
                    audioType = AudioType.WAV;
                    break;
                case ".ogg":
                    audioType = AudioType.OGGVORBIS;
                    break;
                default:
                    yield break; // Skip unsupported formats
            }

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(path, audioType))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                    if (audioClip != null)
                    {
                        LoadedAudioClips[Path.GetFileName(path)] = audioClip;
                    }
                }
            }
        }
    }
    public class AmandsSenseWorld : MonoBehaviour
    {
        public bool Lazy = true;
        public ESenseWorldType eSenseWorldType = ESenseWorldType.Item;
        public GameObject OwnerGameObject;
        public Collider OwnerCollider;

        public Player SenseDeadPlayer;

        public int Id;

        public float Delay;
        public float LifeSpan;

        public bool Waiting = false;
        public bool WaitingRemoveSense = false;
        public bool UpdateIntensity = false;
        public bool Starting = true;
        public float Intensity = 0f;

        public GameObject amandsSenseConstructorGameObject;
        public AmandsSenseConstructor amandsSenseConstructor;

        public void Start()
        {
            enabled = false;
            StartCoroutine(WaitAndStartCoroutine());
        }

        /// <summary>
        /// Coroutine that waits for the delay period before initializing sense indicators.
        /// Uses WaitForSeconds instead of Task.Delay to avoid Task state transition errors.
        /// </summary>
        private IEnumerator WaitAndStartCoroutine()
        {
            Waiting = true;
            yield return new WaitForSeconds(Delay);

            // Check if we were cancelled during the wait
            if (WaitingRemoveSense)
            {
                RemoveSense();
                yield break;
            }

            // Check if owner is still valid after the delay
            if (OwnerGameObject == null || (OwnerGameObject != null && !OwnerGameObject.activeSelf))
            {
                RemoveSense();
                yield break;
            }
            if (Starting)
            {
                if (OwnerGameObject != null)
                {
                    transform.position = OwnerGameObject.transform.position;
                }
                if (HeightCheck())
                {
                    RemoveSense();
                    yield break;
                }

                enabled = true;
                UpdateIntensity = true;

                amandsSenseConstructorGameObject = new GameObject("Constructor");
                amandsSenseConstructorGameObject.transform.SetParent(gameObject.transform, false);
                amandsSenseConstructorGameObject.transform.localScale = Vector3.one * AmandsSensePlugin.Size.Value;

                if (Lazy)
                {
                    ObservedLootItem observedLootItem = OwnerGameObject.GetComponent<ObservedLootItem>();
                    if (observedLootItem != null)
                    {
                        eSenseWorldType = ESenseWorldType.Item;
                        amandsSenseConstructor = amandsSenseConstructorGameObject.AddComponent<AmandsSenseItem>();
                        amandsSenseConstructor.amandsSenseWorld = this;
                        amandsSenseConstructor.Construct();
                        amandsSenseConstructor.SetSense(observedLootItem);
                    }
                    else
                    {
                        LootableContainer lootableContainer = OwnerGameObject.GetComponent<LootableContainer>();
                        if (lootableContainer != null)
                        {
                            if (lootableContainer.Template == "578f87b7245977356274f2cd")
                            {
                                eSenseWorldType = ESenseWorldType.Drawer;
                                amandsSenseConstructorGameObject.transform.localPosition = new Vector3(-0.08f, 0.05f, 0);
                                amandsSenseConstructorGameObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
                            }
                            else
                            {
                                eSenseWorldType = ESenseWorldType.Container;
                            }

                            amandsSenseConstructor = amandsSenseConstructorGameObject.AddComponent<AmandsSenseContainer>();
                            amandsSenseConstructor.amandsSenseWorld = this;
                            amandsSenseConstructor.Construct();
                            amandsSenseConstructor.SetSense(lootableContainer);
                        }
                        else
                        {
                            RemoveSense();
                            yield break;
                        }
                    }
                }
                else
                {
                    switch (eSenseWorldType)
                    {
                        case ESenseWorldType.Item:
                            break;
                        case ESenseWorldType.Container:
                            break;
                        case ESenseWorldType.Drawer:
                            break;
                        case ESenseWorldType.Deadbody:
                            amandsSenseConstructor = amandsSenseConstructorGameObject.AddComponent<AmandsSenseDeadPlayer>();
                            amandsSenseConstructor.amandsSenseWorld = this;
                            amandsSenseConstructor.Construct();
                            amandsSenseConstructor.SetSense(SenseDeadPlayer);
                            break;
                    }
                }

                // SenseWorld Starting Posittion
                switch (eSenseWorldType)
                {
                    case ESenseWorldType.Item:
                    case ESenseWorldType.Container:
                        gameObject.transform.position = new Vector3(OwnerCollider.bounds.center.x, OwnerCollider.ClosestPoint(OwnerCollider.bounds.center + (Vector3.up * 10f)).y + AmandsSensePlugin.VerticalOffset.Value, OwnerCollider.bounds.center.z);
                        break;
                    case ESenseWorldType.Drawer:
                        if (OwnerCollider != null)
                        {
                            BoxCollider boxCollider = OwnerCollider as BoxCollider;
                            if (boxCollider != null)
                            {
                                Vector3 position = OwnerCollider.transform.TransformPoint(boxCollider.center);
                                gameObject.transform.position = position;
                                gameObject.transform.rotation = OwnerCollider.transform.rotation;
                            }
                        }
                        break;
                    case ESenseWorldType.Deadbody:
                        if (amandsSenseConstructor != null)
                        {
                            amandsSenseConstructor.UpdateSenseLocation();
                        }
                        break;
                }
            }
            else
            {
                LifeSpan = 0f;

                if (HeightCheck())
                {
                    RemoveSense();
                    yield break;
                }


                if (amandsSenseConstructor != null) amandsSenseConstructor.UpdateSense();

                // SenseWorld Position
                switch (eSenseWorldType)
                {
                    case ESenseWorldType.Item:
                        gameObject.transform.position = new Vector3(OwnerCollider.bounds.center.x, OwnerCollider.ClosestPoint(OwnerCollider.bounds.center + (Vector3.up * 10f)).y + AmandsSensePlugin.VerticalOffset.Value, OwnerCollider.bounds.center.z);
                        break;
                    case ESenseWorldType.Container:
                        break;
                    case ESenseWorldType.Deadbody:
                        if (amandsSenseConstructor != null) amandsSenseConstructor.UpdateSenseLocation();
                        break;
                    case ESenseWorldType.Drawer:
                        break;
                }
            }

            Waiting = false;
        }
        public void RestartSense()
        {
            if (Waiting || UpdateIntensity) return;

            LifeSpan = 0f;
            Delay = Vector3.Distance(AmandsSenseClass.Player.Position, gameObject.transform.position) / AmandsSensePlugin.Speed.Value;
            StartCoroutine(WaitAndStartCoroutine());
        }
        public bool HeightCheck()
        {
            switch (eSenseWorldType)
            {
                case ESenseWorldType.Item:
                case ESenseWorldType.Container:
                case ESenseWorldType.Drawer:
                case ESenseWorldType.Deadbody:
                    return AmandsSenseClass.Player != null && (transform.position.y < AmandsSenseClass.Player.Position.y + AmandsSensePlugin.MinHeight.Value || transform.position.y > AmandsSenseClass.Player.Position.y + AmandsSensePlugin.MaxHeight.Value);
            }
            return false;
        }
        public void RemoveSense()
        {
            if (amandsSenseConstructor != null) amandsSenseConstructor.RemoveSense();
            AmandsSenseClass.SenseWorlds.Remove(Id);
            if (gameObject != null) Destroy(gameObject);
        }
        public void CancelSense()
        {
            UpdateIntensity = true;
            Starting = false;
        }
        public void Update()
        {
            if (UpdateIntensity)
            {
                if (Starting)
                {
                    Intensity += AmandsSensePlugin.IntensitySpeed.Value * Time.deltaTime;
                    if (Intensity >= 1f)
                    {
                        UpdateIntensity = false;
                        Starting = false;
                    }
                }
                else
                {
                    Intensity -= AmandsSensePlugin.IntensitySpeed.Value * Time.deltaTime;
                    if (Intensity <= 0f)
                    {
                        if (Waiting)
                        {
                            WaitingRemoveSense = true;
                        }
                        else
                        {
                            RemoveSense();
                        }
                        return;
                    }
                }

                if (amandsSenseConstructor != null) amandsSenseConstructor.UpdateIntensity(Intensity);

            }
            else if (!Starting && !Waiting)
            {
                LifeSpan += Time.deltaTime;
                if (LifeSpan > AmandsSensePlugin.Duration.Value)
                {
                    UpdateIntensity = true;
                }
            }

            // Per-frame position update for dead bodies (ragdoll settles after spawn)
            if (eSenseWorldType == ESenseWorldType.Deadbody && amandsSenseConstructor != null)
            {
                amandsSenseConstructor.UpdateSenseLocation();
            }

            var cam = AmandsSenseClass.GameCamera;
            if (cam != null)
            {
                switch (eSenseWorldType)
                {
                    case ESenseWorldType.Item:
                    case ESenseWorldType.Container:
                    case ESenseWorldType.Deadbody:
                        transform.rotation = cam.transform.rotation;
                        transform.localScale = Vector3.one * Mathf.Min(AmandsSensePlugin.SizeClamp.Value, Vector3.Distance(cam.transform.position, transform.position));
                        break;
                    case ESenseWorldType.Drawer:
                        break;
                }
            }
        }
    }
    public class AmandsSenseConstructor : MonoBehaviour
    {
        public AmandsSenseWorld amandsSenseWorld;

        public Color color = AmandsSensePlugin.ObservedLootItemColor.Value;
        public Color textColor = AmandsSensePlugin.TextColor.Value;

        public SpriteRenderer spriteRenderer;
        public Sprite sprite;

        public Light light;

        public GameObject textGameObject;
        public GameObject backgroundPlateObject;
        public SpriteRenderer backgroundPlateRenderer;

        public TextMeshPro typeText;
        public TextMeshPro nameText;
        public TextMeshPro descriptionText;

        /// <summary>
        /// Apply styling to a TextMeshPro component with tier color on fill and dark outline.
        /// Tier color fills the text for visible loot quality; dark outline provides contrast.
        /// </summary>
        public static void ApplyTextStyle(TextMeshPro tmp, Color tierColor, float fontSize)
        {
            if (tmp == null) return;

            float uiScale = AmandsSensePlugin.UIScale.Value;
            tmp.fontSize = fontSize * uiScale;

            // Text overflow: ellipsis, no word wrap for clean labels
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.enableWordWrapping = false;

            // Dark outline for contrast against any background
            float outlineWidth = AmandsSensePlugin.TextOutlineWidth.Value;
            if (outlineWidth > 0f)
            {
                tmp.outlineWidth = outlineWidth * 1.1f;
                // Dark outline for contrast (tier color on fill)
                tmp.outlineColor = new Color32(20, 22, 28, 255);
            }

            // Face color = tier color for visible loot quality
            tmp.faceColor = new Color32(
                (byte)(tierColor.r * 255),
                (byte)(tierColor.g * 255),
                (byte)(tierColor.b * 255),
                255
            );

            // Font style - bold for readability at small sizes
            tmp.fontStyle = FontStyles.Bold;

            // Character spacing for better readability
            tmp.characterSpacing = 0.5f;
        }

        /// <summary>
        /// Apply polish styling to a TextMeshPro component (instance wrapper).
        /// </summary>
        protected void StyleTextMeshPro(TextMeshPro tmp, Color outlineColor, float fontSize)
        {
            ApplyTextStyle(tmp, outlineColor, fontSize);
        }

        /// <summary>
        /// Apply styling for dead body labels - uses tier color for text fill (not white).
        /// This makes loot quality color visible on corpse markers per Polish.md design.
        /// Dark outline provides contrast, tier color fills the text.
        /// </summary>
        public static void ApplyDeadBodyTextStyle(TextMeshPro tmp, Color tierColor, float fontSize)
        {
            if (tmp == null) return;

            float uiScale = AmandsSensePlugin.UIScale.Value;
            tmp.fontSize = fontSize * uiScale;

            // Text overflow: ellipsis, no word wrap for clean labels
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.enableWordWrapping = false;

            // Dark outline for contrast against any background
            float outlineWidth = AmandsSensePlugin.TextOutlineWidth.Value;
            if (outlineWidth > 0f)
            {
                tmp.outlineWidth = outlineWidth * 1.2f; // Slightly thicker for dead bodies
                // Dark outline for contrast (tier color on fill)
                tmp.outlineColor = new Color32(20, 22, 28, 255);
            }

            // Face color = tier color (NOT white) for visible loot quality
            tmp.faceColor = new Color32(
                (byte)(tierColor.r * 255),
                (byte)(tierColor.g * 255),
                (byte)(tierColor.b * 255),
                255
            );

            // Font style - bold for readability
            tmp.fontStyle = FontStyles.Bold;

            // Character spacing for better readability
            tmp.characterSpacing = 0.5f;
        }

        /// <summary>
        /// Create a semi-transparent background plate behind text for readability on bright scenes.
        /// </summary>
        protected void CreateBackgroundPlate(Transform parent)
        {
            if (!AmandsSensePlugin.EnableBackgroundPlate.Value) return;

            backgroundPlateObject = new GameObject("BackgroundPlate");
            backgroundPlateObject.transform.SetParent(parent, false);
            backgroundPlateObject.transform.localPosition = new Vector3(0.05f, 0, 0.001f); // Slightly behind text

            backgroundPlateRenderer = backgroundPlateObject.AddComponent<SpriteRenderer>();
            // Use a simple white sprite that will be tinted dark
            backgroundPlateRenderer.sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 4, 4),
                new Vector2(0f, 0.5f), // Left-center pivot
                100f
            );
            float opacity = AmandsSensePlugin.BackgroundPlateOpacity.Value;
            backgroundPlateRenderer.color = new Color(0.05f, 0.06f, 0.08f, 0f); // Start transparent, fade in with text
            backgroundPlateRenderer.sortingOrder = -1; // Behind text

            // Scale to approximate text bounds
            float uiScale = AmandsSensePlugin.UIScale.Value;
            backgroundPlateObject.transform.localScale = new Vector3(0.4f * uiScale, 0.15f * uiScale, 1f);
        }

        /// <summary>
        /// Update background plate opacity to match text fade.
        /// </summary>
        protected void UpdateBackgroundPlateOpacity(float intensity)
        {
            if (backgroundPlateRenderer != null)
            {
                float opacity = AmandsSensePlugin.BackgroundPlateOpacity.Value * intensity;
                backgroundPlateRenderer.color = new Color(0.05f, 0.06f, 0.08f, opacity);
            }
        }

        virtual public void Construct()
        {
            // SenseConstructor Sprite GameObject
            GameObject spriteGameObject = new GameObject("Sprite");
            spriteGameObject.transform.SetParent(gameObject.transform, false);
            RectTransform spriteRectTransform = spriteGameObject.AddComponent<RectTransform>();
            spriteRectTransform.localScale = Vector3.one * AmandsSensePlugin.IconSize.Value;

            // SenseConstructor Sprite
            spriteRenderer = spriteGameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = new Color(color.r, color.g, color.b, 0f);

            // SenseConstructor Sprite Light
            light = spriteGameObject.AddComponent<Light>();
            light.color = new Color(color.r, color.g, color.b, 1f);
            light.shadows = AmandsSensePlugin.LightShadows.Value ? LightShadows.Hard : LightShadows.None;
            light.intensity = 0f;
            light.range = AmandsSensePlugin.LightRange.Value;

            if (AmandsSensePlugin.EnableSense.Value != EEnableSense.OnText) return;

            float uiScale = AmandsSensePlugin.UIScale.Value;

            // SenseConstructor Text
            textGameObject = new GameObject("Text");
            textGameObject.transform.SetParent(gameObject.transform, false);
            RectTransform textRectTransform = textGameObject.AddComponent<RectTransform>();
            textRectTransform.localPosition = new Vector3(AmandsSensePlugin.TextOffset.Value * uiScale, 0, 0);
            textRectTransform.pivot = new Vector2(0, 0.5f);

            // Background plate for readability on bright scenes
            CreateBackgroundPlate(gameObject.transform);

            // SenseConstructor VerticalLayoutGroup
            VerticalLayoutGroup verticalLayoutGroup = textGameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.spacing = -0.02f * uiScale;
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.childForceExpandWidth = false;
            verticalLayoutGroup.childControlHeight = true;
            verticalLayoutGroup.childControlWidth = true;
            ContentSizeFitter contentSizeFitter = textGameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // SenseConstructor Type (category/tier label)
            GameObject typeTextGameObject = new GameObject("Type");
            typeTextGameObject.transform.SetParent(textGameObject.transform, false);
            typeText = typeTextGameObject.AddComponent<TextMeshPro>();
            typeText.autoSizeTextContainer = true;
            typeText.text = "Type";
            typeText.color = new Color(color.r, color.g, color.b, 0f);
            StyleTextMeshPro(typeText, color, 0.5f);

            // SenseConstructor Name (main item/player label)
            // Use tier color so fade animation reveals rarity correctly
            GameObject nameTextGameObject = new GameObject("Name");
            nameTextGameObject.transform.SetParent(textGameObject.transform, false);
            nameText = nameTextGameObject.AddComponent<TextMeshPro>();
            nameText.autoSizeTextContainer = true;
            nameText.text = "Name";
            nameText.color = new Color(color.r, color.g, color.b, 0f);
            StyleTextMeshPro(nameText, color, 1f);

            // SenseConstructor Description (details/count)
            // Use tier color so fade animation reveals rarity correctly
            GameObject descriptionTextGameObject = new GameObject("Description");
            descriptionTextGameObject.transform.SetParent(textGameObject.transform, false);
            descriptionText = descriptionTextGameObject.AddComponent<TextMeshPro>();
            descriptionText.autoSizeTextContainer = true;
            descriptionText.text = "";
            descriptionText.color = new Color(color.r, color.g, color.b, 0f);
            StyleTextMeshPro(descriptionText, color, 0.75f);
        }
        virtual public void SetSense(ObservedLootItem observedLootItem)
        {

        }
        virtual public void SetSense(LootableContainer lootableContainer)
        {

        }
        virtual public void SetSense(Player DeadPlayer)
        {

        }
        virtual public void SetSense(ExfiltrationPoint ExfiltrationPoint)
        {

        }
        virtual public void UpdateSense()
        {

        }
        virtual public void UpdateSenseLocation()
        {

        }
        virtual public void UpdateIntensity(float Intensity)
        {

        }
        virtual public void RemoveSense()
        {

        }
    }
    public class AmandsSenseItem : AmandsSenseConstructor
    {
        public ObservedLootItem observedLootItem;
        public string ItemId;
        public string type;

        public ESenseItemType eSenseItemType = ESenseItemType.All;

        public override void SetSense(ObservedLootItem ObservedLootItem)
        {
            eSenseItemType = ESenseItemType.All;
            color = AmandsSensePlugin.ObservedLootItemColor.Value;

            observedLootItem = ObservedLootItem;
            if (observedLootItem != null && observedLootItem.gameObject.activeSelf && observedLootItem.Item != null)
            {
                AmandsSenseClass.SenseItems.Add(observedLootItem.Item);

                ItemId = observedLootItem.ItemId;

                // Weapon SenseItem Color, Sprite and Type
                Weapon weapon = observedLootItem.Item as Weapon;
                if (weapon != null)
                {
                    switch (weapon.WeapClass)
                    {
                        case "assaultCarbine":
                            eSenseItemType = ESenseItemType.AssaultCarbines;
                            color = AmandsSensePlugin.AssaultCarbinesColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_carbines.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_carbines.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f78e986f77447ed5636b1", EStringCase.None);
                            break;
                        case "assaultRifle":
                            eSenseItemType = ESenseItemType.AssaultRifles;
                            color = AmandsSensePlugin.AssaultRiflesColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_assaultrifles.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_assaultrifles.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f78fc86f77409407a7f90", EStringCase.None);
                            break;
                        case "sniperRifle":
                            eSenseItemType = ESenseItemType.BoltActionRifles;
                            color = AmandsSensePlugin.BoltActionRiflesColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_botaction.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_botaction.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f798886f77447ed5636b5", EStringCase.None);
                            break;
                        case "grenadeLauncher":
                            eSenseItemType = ESenseItemType.GrenadeLaunchers;
                            color = AmandsSensePlugin.GrenadeLaunchersColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_gl.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_gl.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f79d186f774093f2ed3c2", EStringCase.None);
                            break;
                        case "machinegun":
                            eSenseItemType = ESenseItemType.MachineGuns;
                            color = AmandsSensePlugin.MachineGunsColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_mg.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_mg.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f79a486f77409407a7f94", EStringCase.None);
                            break;
                        case "marksmanRifle":
                            eSenseItemType = ESenseItemType.MarksmanRifles;
                            color = AmandsSensePlugin.MarksmanRiflesColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_dmr.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_dmr.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f791486f774093f2ed3be", EStringCase.None);
                            break;
                        case "pistol":
                            eSenseItemType = ESenseItemType.Pistols;
                            color = AmandsSensePlugin.PistolsColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_pistols.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_pistols.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f792486f77447ed5636b3", EStringCase.None);
                            break;
                        case "smg":
                            eSenseItemType = ESenseItemType.SMGs;
                            color = AmandsSensePlugin.SMGsColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_smg.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_smg.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f796a86f774093f2ed3c0", EStringCase.None);
                            break;
                        case "shotgun":
                            eSenseItemType = ESenseItemType.Shotguns;
                            color = AmandsSensePlugin.ShotgunsColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_shotguns.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_shotguns.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f794b86f77409407a7f92", EStringCase.None);
                            break;
                        case "specialWeapon":
                            eSenseItemType = ESenseItemType.SpecialWeapons;
                            color = AmandsSensePlugin.SpecialWeaponsColor.Value;
                            if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_special.png"))
                            {
                                sprite = AmandsSenseClass.LoadedSprites["icon_weapons_special.png"];
                            }
                            type = AmandsSenseHelper.Localized("5b5f79eb86f77447ed5636b7", EStringCase.None);
                            break;
                        default:
                            eSenseItemType = AmandsSenseClass.SenseItemType(observedLootItem.Item.GetType());
                            break;
                    }
                }
                else
                {
                    eSenseItemType = AmandsSenseClass.SenseItemType(observedLootItem.Item.GetType());
                }

                // SenseItem Color, Sprite and Type
                switch (eSenseItemType)
                {
                    case ESenseItemType.All:
                        color = AmandsSensePlugin.ObservedLootItemColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("ObservedLootItem.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["ObservedLootItem.png"];
                        }
                        type = "ObservedLootItem";
                        break;
                    case ESenseItemType.Others:
                        color = AmandsSensePlugin.OthersColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_others.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_others.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2f4", EStringCase.None);
                        break;
                    case ESenseItemType.BuildingMaterials:
                        color = AmandsSensePlugin.BuildingMaterialsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_building.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_building.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2ee", EStringCase.None);
                        break;
                    case ESenseItemType.Electronics:
                        color = AmandsSensePlugin.ElectronicsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_electronics.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_electronics.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2ef", EStringCase.None);
                        break;
                    case ESenseItemType.EnergyElements:
                        color = AmandsSensePlugin.EnergyElementsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_energy.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_energy.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2ed", EStringCase.None);
                        break;
                    case ESenseItemType.FlammableMaterials:
                        color = AmandsSensePlugin.FlammableMaterialsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_flammable.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_flammable.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2f2", EStringCase.None);
                        break;
                    case ESenseItemType.HouseholdMaterials:
                        color = AmandsSensePlugin.HouseholdMaterialsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_household.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_household.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2f0", EStringCase.None);
                        break;
                    case ESenseItemType.MedicalSupplies:
                        color = AmandsSensePlugin.MedicalSuppliesColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_medical.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_medical.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2f3", EStringCase.None);
                        break;
                    case ESenseItemType.Tools:
                        color = AmandsSensePlugin.ToolsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_tools.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_tools.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2f6", EStringCase.None);
                        break;
                    case ESenseItemType.Valuables:
                        color = AmandsSensePlugin.ValuablesColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter_valuables.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_barter_valuables.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b2f1", EStringCase.None);
                        break;
                    case ESenseItemType.Backpacks:
                        color = AmandsSensePlugin.BackpacksColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_backpacks.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_backpacks.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f6f6c86f774093f2ecf0b", EStringCase.None);
                        break;
                    case ESenseItemType.BodyArmor:
                        color = AmandsSensePlugin.BodyArmorColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_armor.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_armor.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f701386f774093f2ecf0f", EStringCase.None);
                        break;
                    case ESenseItemType.Eyewear:
                        color = AmandsSensePlugin.EyewearColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_visors.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_visors.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b331", EStringCase.None);
                        break;
                    case ESenseItemType.Facecovers:
                        color = AmandsSensePlugin.FacecoversColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_facecovers.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_facecovers.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b32f", EStringCase.None);
                        break;
                    case ESenseItemType.GearComponents:
                        color = AmandsSensePlugin.GearComponentsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_components.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_components.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f704686f77447ec5d76d7", EStringCase.None);
                        break;
                    case ESenseItemType.Headgear:
                        color = AmandsSensePlugin.HeadgearColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_headwear.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_headwear.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b330", EStringCase.None);
                        break;
                    case ESenseItemType.Headsets:
                        color = AmandsSensePlugin.HeadsetsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_headsets.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_headsets.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f6f3c86f774094242ef87", EStringCase.None);
                        break;
                    case ESenseItemType.SecureContainers:
                        color = AmandsSensePlugin.SecureContainersColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_secured.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_secured.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f6fd286f774093f2ecf0d", EStringCase.None);
                        break;
                    case ESenseItemType.StorageContainers:
                        color = AmandsSensePlugin.StorageContainersColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_cases.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_cases.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f6fa186f77409407a7eb7", EStringCase.None);
                        break;
                    case ESenseItemType.TacticalRigs:
                        color = AmandsSensePlugin.TacticalRigsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_gear_rigs.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_gear_rigs.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f6f8786f77447ed563642", EStringCase.None);
                        break;
                    case ESenseItemType.FunctionalMods:
                        color = AmandsSensePlugin.FunctionalModsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_mods_functional.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_mods_functional.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f71b386f774093f2ecf11", EStringCase.None);
                        break;
                    case ESenseItemType.GearMods:
                        color = AmandsSensePlugin.GearModsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_mods_gear.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_mods_gear.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f750686f774093e6cb503", EStringCase.None);
                        break;
                    case ESenseItemType.VitalParts:
                        color = AmandsSensePlugin.VitalPartsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_mods_vital.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_mods_vital.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f75b986f77447ec5d7710", EStringCase.None);
                        break;
                    case ESenseItemType.MeleeWeapons:
                        color = AmandsSensePlugin.MeleeWeaponsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_melee.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_weapons_melee.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f7a0886f77409407a7f96", EStringCase.None);
                        break;
                    case ESenseItemType.Throwables:
                        color = AmandsSensePlugin.ThrowablesColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_weapons_throw.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_weapons_throw.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f7a2386f774093f2ed3c4", EStringCase.None);
                        break;
                    case ESenseItemType.AmmoPacks:
                        color = AmandsSensePlugin.AmmoPacksColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_ammo_boxes.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_ammo_boxes.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b33c", EStringCase.None);
                        break;
                    case ESenseItemType.Rounds:
                        color = AmandsSensePlugin.RoundsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_ammo_rounds.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_ammo_rounds.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b33b", EStringCase.None);
                        break;
                    case ESenseItemType.Drinks:
                        color = AmandsSensePlugin.DrinksColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_provisions_drinks.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_provisions_drinks.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b335", EStringCase.None);
                        break;
                    case ESenseItemType.Food:
                        color = AmandsSensePlugin.FoodColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_provisions_food.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_provisions_food.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b336", EStringCase.None);
                        break;
                    case ESenseItemType.Injectors:
                        color = AmandsSensePlugin.InjectorsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_medical_injectors.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_medical_injectors.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b33a", EStringCase.None);
                        break;
                    case ESenseItemType.InjuryTreatment:
                        color = AmandsSensePlugin.InjuryTreatmentColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_medical_injury.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_medical_injury.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b339", EStringCase.None);
                        break;
                    case ESenseItemType.Medkits:
                        color = AmandsSensePlugin.MedkitsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_medical_medkits.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_medical_medkits.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b338", EStringCase.None);
                        break;
                    case ESenseItemType.Pills:
                        color = AmandsSensePlugin.PillsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_medical_pills.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_medical_pills.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b337", EStringCase.None);
                        break;
                    case ESenseItemType.ElectronicKeys:
                        color = AmandsSensePlugin.ElectronicKeysColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_keys_electronic.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_keys_electronic.png"];
                        }
                        type = AmandsSenseHelper.Localized("5c518ed586f774119a772aee", EStringCase.None);
                        break;
                    case ESenseItemType.MechanicalKeys:
                        color = AmandsSensePlugin.MechanicalKeysColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_keys_mechanic.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_keys_mechanic.png"];
                        }
                        type = AmandsSenseHelper.Localized("5c518ec986f7743b68682ce2", EStringCase.None);
                        break;
                    case ESenseItemType.InfoItems:
                        color = AmandsSensePlugin.InfoItemsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_info.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_info.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b341", EStringCase.None);
                        break;
                    case ESenseItemType.SpecialEquipment:
                        color = AmandsSensePlugin.SpecialEquipmentColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_spec.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_spec.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b345", EStringCase.None);
                        break;
                    case ESenseItemType.Maps:
                        color = AmandsSensePlugin.MapsColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_maps.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_maps.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b47574386f77428ca22b343", EStringCase.None);
                        break;
                    case ESenseItemType.Money:
                        color = AmandsSensePlugin.MoneyColor.Value;
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_money.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_money.png"];
                        }
                        type = AmandsSenseHelper.Localized("5b5f78b786f77447ed5636af", EStringCase.None);
                        break;
                }

                // Quest SenseItem Color
                if (observedLootItem.Item.QuestItem) color = AmandsSensePlugin.QuestItemsColor.Value;

                // JSON SenseItem Color
                if (AmandsSenseClass.itemsJsonClass != null)
                {
                    if (AmandsSenseClass.itemsJsonClass.KappaItems != null)
                    {
                        if (AmandsSenseClass.itemsJsonClass.KappaItems.Contains(observedLootItem.Item.TemplateId))
                        {
                            color = AmandsSensePlugin.KappaItemsColor.Value;
                        }
                    }
                    if (AmandsSensePlugin.EnableFlea.Value && !observedLootItem.Item.CanSellOnRagfair && !AmandsSenseClass.itemsJsonClass.NonFleaExclude.Contains(observedLootItem.Item.TemplateId))
                    {
                        color = AmandsSensePlugin.NonFleaItemsColor.Value;
                    }
                    // Wishlist check using SPT 4.0.x WishlistManager API
                    if (AmandsSenseDeadPlayer.IsOnWishlist(observedLootItem.Item.TemplateId))
                    {
                        color = AmandsSensePlugin.WishListItemsColor.Value;
                    }
                    if (AmandsSenseClass.itemsJsonClass.RareItems != null)
                    {
                        if (AmandsSenseClass.itemsJsonClass.RareItems.Contains(observedLootItem.Item.TemplateId))
                        {
                            color = AmandsSensePlugin.RareItemsColor.Value;
                        }
                    }
                }

                if (AmandsSensePlugin.UseBackgroundColor.Value) color = AmandsSenseHelper.ToColor(observedLootItem.Item.BackgroundColor);

                // SenseItem Sprite
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.color = new Color(color.r, color.g, color.b, 0f);
                }

                // SenseItem Light
                if (light != null)
                {
                    light.color = new Color(color.r, color.g, color.b, 1f);
                    light.intensity = 0f;
                    light.range = AmandsSensePlugin.LightRange.Value;
                }

                // SenseItem Type
                if (typeText != null)
                {
                    typeText.fontSize = 0.5f;
                    typeText.text = type;
                    typeText.color = new Color(color.r, color.g, color.b, 0f);
                }

                if (AmandsSenseClass.inventoryControllerClass != null && !AmandsSenseClass.inventoryControllerClass.Examined(observedLootItem.Item))
                {
                    // SenseItem Unexamined Name - use tier color so fade reveals rarity
                    if (nameText != null)
                    {
                        nameText.fontSize = 1f;
                        nameText.text = "<b>???</b>";
                        nameText.color = new Color(color.r, color.g, color.b, 0f);
                    }
                    // SenseItem Unexamined Description
                    if (descriptionText != null)
                    {
                        descriptionText.text = "";
                        descriptionText.fontSize = 0.75f;
                        descriptionText.color = new Color(color.r, color.g, color.b, 0f);
                    }
                }
                else
                {
                    // SenseItem Name - use tier color so fade reveals rarity
                    if (nameText != null)
                    {
                        nameText.fontSize = 1f;
                        string Name = "<b>" + AmandsSenseHelper.Localized(observedLootItem.Item.Name, 0) + "</b>";
                        if (Name.Count() > 16) Name = "<b>" + AmandsSenseHelper.Localized(observedLootItem.Item.ShortName, 0) + "</b>";
                        if (observedLootItem.Item.StackObjectsCount > 1) Name = Name + " (" + observedLootItem.Item.StackObjectsCount + ")";
                        nameText.text = Name + "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + "<size=50%><voffset=0.5em> " + observedLootItem.Item.Weight + "kg";
                        nameText.color = new Color(color.r, color.g, color.b, 0f);
                    }

                    // SenseItem Description - use tier color so fade reveals rarity
                    if (descriptionText != null)
                    {
                        FoodDrinkComponent foodDrinkComponent;
                        if (observedLootItem.Item.TryGetItemComponent(out foodDrinkComponent) && ((int)foodDrinkComponent.MaxResource) > 1)
                        {
                            descriptionText.text = ((int)foodDrinkComponent.HpPercent) + "/" + ((int)foodDrinkComponent.MaxResource);
                        }
                        KeyComponent keyComponent;
                        if (observedLootItem.Item.TryGetItemComponent(out keyComponent))
                        {
                            int MaximumNumberOfUsage = Traverse.Create(Traverse.Create(keyComponent).Field("Template").GetValue<object>()).Field("MaximumNumberOfUsage").GetValue<int>();
                            descriptionText.text = (MaximumNumberOfUsage - keyComponent.NumberOfUsages) + "/" + MaximumNumberOfUsage;
                        }
                        MedKitComponent medKitComponent;
                        if (observedLootItem.Item.TryGetItemComponent(out medKitComponent) && medKitComponent.MaxHpResource > 1)
                        {
                            descriptionText.text = ((int)medKitComponent.HpResource) + "/" + medKitComponent.MaxHpResource;
                        }
                        RepairableComponent repairableComponent;
                        if (observedLootItem.Item.TryGetItemComponent(out repairableComponent))
                        {
                            descriptionText.text = ((int)repairableComponent.Durability) + "/" + ((int)repairableComponent.MaxDurability);
                        }
                        MagazineItemClass magazineClass = observedLootItem.Item as MagazineItemClass;
                        if (magazineClass != null)
                        {
                            descriptionText.text = magazineClass.Count + "/" + magazineClass.MaxCount;
                        }
                        descriptionText.fontSize = 0.75f;
                        descriptionText.color = new Color(color.r, color.g, color.b, 0f);
                    }
                }

                // SenseItem Sound
                if (AmandsSensePlugin.SenseRareSound.Value && AmandsSenseClass.LoadedAudioClips.ContainsKey("SenseRare.wav"))
                {
                    if (!AmandsSensePlugin.SenseAlwaysOn.Value)
                    {
                        Singleton<BetterAudio>.Instance.PlayAtPoint(transform.position, AmandsSenseClass.LoadedAudioClips["SenseRare.wav"], AmandsSensePlugin.AudioDistance.Value, BetterAudio.AudioSourceGroupType.Environment, AmandsSensePlugin.AudioRolloff.Value, AmandsSensePlugin.AudioVolume.Value, EOcclusionTest.Fast);
                    }
                }
                else
                {
                    if (!AmandsSensePlugin.SenseAlwaysOn.Value)
                    {
                        AudioClip itemClip = Singleton<GUISounds>.Instance.GetItemClip(observedLootItem.Item.ItemSound, EInventorySoundType.pickup);
                        if (itemClip != null)
                        {
                            Singleton<BetterAudio>.Instance.PlayAtPoint(transform.position, itemClip, AmandsSensePlugin.AudioDistance.Value, BetterAudio.AudioSourceGroupType.Environment, AmandsSensePlugin.AudioRolloff.Value, AmandsSensePlugin.AudioVolume.Value, EOcclusionTest.Fast);
                        }
                    }
                }
            }
            else if (amandsSenseWorld != null)
            {
                amandsSenseWorld.CancelSense();
            }
        }

        public override void UpdateIntensity(float Intensity)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(color.r, color.g, color.b, color.a * Intensity);
            }
            if (light != null)
            {
                light.intensity = AmandsSensePlugin.LightIntensity.Value * Intensity;
            }
            if (typeText != null)
            {
                typeText.color = new Color(color.r, color.g, color.b, Intensity);
            }
            // Use tier color for text so fade reveals rarity (not white)
            if (nameText != null)
            {
                nameText.color = new Color(color.r, color.g, color.b, Intensity);
            }
            if (descriptionText != null)
            {
                descriptionText.color = new Color(color.r, color.g, color.b, Intensity);
            }

            // Update background plate opacity
            UpdateBackgroundPlateOpacity(Intensity);
        }
        public override void RemoveSense()
        {
            if (observedLootItem != null && observedLootItem.gameObject.activeSelf && observedLootItem.Item != null)
            {
                AmandsSenseClass.SenseItems.Remove(observedLootItem.Item);
            }
            //Destroy(gameObject);
        }
    }
    public class AmandsSenseContainer : AmandsSenseConstructor
    {
        public LootableContainer lootableContainer;
        public bool emptyLootableContainer = false;
        public int itemCount = 0;
        public string ContainerId;
        public bool Drawer;

        public override void SetSense(LootableContainer LootableContainer)
        {
            lootableContainer = LootableContainer;
            if (lootableContainer != null && lootableContainer.gameObject.activeSelf)
            {
                Drawer = amandsSenseWorld.eSenseWorldType == ESenseWorldType.Drawer;
                // SenseContainer Defaults
                emptyLootableContainer = false;
                itemCount = 0;

                ContainerId = lootableContainer.Id;

                // SenseContainer Items - track value and special flags
                ESenseItemColor eSenseItemColor = ESenseItemColor.Default;
                int highestItemValue = 0;
                if (lootableContainer.ItemOwner != null && AmandsSenseClass.itemsJsonClass != null && AmandsSenseClass.itemsJsonClass.RareItems != null && AmandsSenseClass.itemsJsonClass.KappaItems != null && AmandsSenseClass.itemsJsonClass.NonFleaExclude != null && AmandsSenseClass.Player.Profile != null)
                {
                    CompoundItem lootItemClass = lootableContainer.ItemOwner.RootItem as CompoundItem;
                    if (lootItemClass != null)
                    {
                        object[] Grids = Traverse.Create(lootItemClass).Field("Grids").GetValue<object[]>();
                        if (Grids != null)
                        {
                            foreach (object grid in Grids)
                            {
                                IEnumerable<Item> Items = Traverse.Create(grid).Property("Items").GetValue<IEnumerable<Item>>();
                                if (Items != null)
                                {
                                    foreach (Item item in Items)
                                    {
                                        itemCount += 1;

                                        // Track highest value for value-based tiering
                                        int itemValue = AmandsSenseDeadPlayer.GetItemValue(item);
                                        if (itemValue > highestItemValue)
                                        {
                                            highestItemValue = itemValue;
                                        }

                                        // Check special flags: Wishlist > RareList > NonFlea > Kappa
                                        if (AmandsSenseDeadPlayer.IsOnWishlist(item.TemplateId))
                                        {
                                            if (eSenseItemColor != ESenseItemColor.Legendary && eSenseItemColor != ESenseItemColor.Epic)
                                            {
                                                eSenseItemColor = ESenseItemColor.WishList;
                                            }
                                        }
                                        else if (AmandsSenseClass.itemsJsonClass.RareItems.Contains(item.TemplateId))
                                        {
                                            eSenseItemColor = ESenseItemColor.Legendary;
                                        }
                                        else if (item.Template != null && !item.Template.CanSellOnRagfair && !AmandsSenseClass.itemsJsonClass.NonFleaExclude.Contains(item.TemplateId))
                                        {
                                            if (!AmandsSensePlugin.FleaIncludeAmmo.Value && TemplateIdToObjectMappingsClass.TypeTable["5485a8684bdc2da71d8b4567"].IsAssignableFrom(item.GetType()))
                                            {
                                                continue;
                                            }
                                            else if (AmandsSensePlugin.EnableFlea.Value && eSenseItemColor < ESenseItemColor.NonFlea)
                                            {
                                                eSenseItemColor = ESenseItemColor.NonFlea;
                                            }
                                        }
                                        else if (AmandsSenseClass.itemsJsonClass.KappaItems.Contains(item.TemplateId) && eSenseItemColor < ESenseItemColor.Kappa)
                                        {
                                            eSenseItemColor = ESenseItemColor.Kappa;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Apply value-based tier if no special flags matched
                if (eSenseItemColor == ESenseItemColor.Default && highestItemValue > 0)
                {
                    eSenseItemColor = AmandsSenseDeadPlayer.GetValueBasedTier(highestItemValue);
                }

                if (itemCount == 0)
                {
                    amandsSenseWorld.CancelSense();
                    return;
                }

                // SenseContainer Color and Sprite
                if (AmandsSenseClass.LoadedSprites.ContainsKey("LootableContainer.png"))
                {
                    sprite = AmandsSenseClass.LoadedSprites["LootableContainer.png"];
                }
                color = AmandsSenseDeadPlayer.GetTierColor(eSenseItemColor);

                // Override sprite for special types
                if (eSenseItemColor == ESenseItemColor.NonFlea && AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter.png"))
                {
                    sprite = AmandsSenseClass.LoadedSprites["icon_barter.png"];
                }
                else if (eSenseItemColor == ESenseItemColor.WishList && AmandsSenseClass.LoadedSprites.ContainsKey("icon_fav_checked.png"))
                {
                    sprite = AmandsSenseClass.LoadedSprites["icon_fav_checked.png"];
                }

                // SenseContainer Sprite
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.color = new Color(color.r, color.g, color.b, 0f);
                }

                // SenseContainer Light
                if (light != null)
                {
                    light.color = new Color(color.r, color.g, color.b, 1f);
                    light.intensity = 0f;
                    light.range = AmandsSensePlugin.LightRange.Value;
                }

                // SenseContainer Type
                if (typeText != null)
                {
                    typeText.fontSize = 0.5f;
                    typeText.text = AmandsSenseHelper.Localized("container", EStringCase.None);
                    typeText.color = new Color(color.r, color.g, color.b, 0f);
                }

                // SenseContainer Name - use tier color so fade reveals rarity
                if (nameText != null)
                {
                    nameText.fontSize = 1f;
                    //nameText.text = "Name";
                    nameText.text = "<b>" + lootableContainer.ItemOwner.ContainerName + "</b>";
                    nameText.color = new Color(color.r, color.g, color.b, 0f);
                }

                // SenseContainer Description - use tier color so fade reveals rarity
                if (descriptionText != null)
                {
                    descriptionText.fontSize = 0.75f;
                    if (AmandsSensePlugin.ContainerLootcount.Value)
                    {
                        descriptionText.text = AmandsSenseHelper.Localized("loot", EStringCase.None) + " " + itemCount;
                    }
                    else
                    {
                        descriptionText.text = "";
                    }
                    descriptionText.color = new Color(color.r, color.g, color.b, 0f);
                }

                // SenseContainer Sound
                if (AmandsSensePlugin.SenseRareSound.Value && AmandsSenseClass.LoadedAudioClips.ContainsKey("SenseRare.wav"))
                {
                    if (!AmandsSensePlugin.SenseAlwaysOn.Value)
                    {
                        Singleton<BetterAudio>.Instance.PlayAtPoint(transform.position, AmandsSenseClass.LoadedAudioClips["SenseRare.wav"], AmandsSensePlugin.AudioDistance.Value, BetterAudio.AudioSourceGroupType.Environment, AmandsSensePlugin.AudioRolloff.Value, AmandsSensePlugin.ContainerAudioVolume.Value, EOcclusionTest.Fast);
                    }
                }
                else
                {
                    if (!AmandsSensePlugin.SenseAlwaysOn.Value && !Drawer && lootableContainer.OpenSound.Length > 0)
                    {
                        AudioClip OpenSound = lootableContainer.OpenSound[0];
                        if (OpenSound != null)
                        {
                            Singleton<BetterAudio>.Instance.PlayAtPoint(transform.position, OpenSound, AmandsSensePlugin.AudioDistance.Value, BetterAudio.AudioSourceGroupType.Environment, AmandsSensePlugin.AudioRolloff.Value, AmandsSensePlugin.ContainerAudioVolume.Value, EOcclusionTest.Fast);
                        }
                    }
                }
            }
            else if (amandsSenseWorld != null)
            {
                amandsSenseWorld.CancelSense();
            }
        }
        public override void UpdateSense()
        {
            if (lootableContainer != null && lootableContainer.gameObject.activeSelf)
            {
                // SenseContainer Defaults
                emptyLootableContainer = false;
                itemCount = 0;

                ContainerId = lootableContainer.Id;

                // SenseContainer Items - track value and special flags
                ESenseItemColor eSenseItemColor = ESenseItemColor.Default;
                int highestItemValue = 0;
                if (lootableContainer.ItemOwner != null && AmandsSenseClass.itemsJsonClass != null && AmandsSenseClass.itemsJsonClass.RareItems != null && AmandsSenseClass.itemsJsonClass.KappaItems != null && AmandsSenseClass.itemsJsonClass.NonFleaExclude != null && AmandsSenseClass.Player.Profile != null)
                {
                    CompoundItem lootItemClass = lootableContainer.ItemOwner.RootItem as CompoundItem;
                    if (lootItemClass != null)
                    {
                        object[] Grids = Traverse.Create(lootItemClass).Field("Grids").GetValue<object[]>();
                        if (Grids != null)
                        {
                            foreach (object grid in Grids)
                            {
                                IEnumerable<Item> Items = Traverse.Create(grid).Property("Items").GetValue<IEnumerable<Item>>();
                                if (Items != null)
                                {
                                    foreach (Item item in Items)
                                    {
                                        itemCount += 1;

                                        // Track highest value for value-based tiering
                                        int itemValue = AmandsSenseDeadPlayer.GetItemValue(item);
                                        if (itemValue > highestItemValue)
                                        {
                                            highestItemValue = itemValue;
                                        }

                                        // Check special flags: Wishlist > RareList > NonFlea > Kappa
                                        if (AmandsSenseDeadPlayer.IsOnWishlist(item.TemplateId))
                                        {
                                            if (eSenseItemColor != ESenseItemColor.Legendary && eSenseItemColor != ESenseItemColor.Epic)
                                            {
                                                eSenseItemColor = ESenseItemColor.WishList;
                                            }
                                        }
                                        else if (AmandsSenseClass.itemsJsonClass.RareItems.Contains(item.TemplateId))
                                        {
                                            eSenseItemColor = ESenseItemColor.Legendary;
                                        }
                                        else if (item.Template != null && !item.Template.CanSellOnRagfair && !AmandsSenseClass.itemsJsonClass.NonFleaExclude.Contains(item.TemplateId))
                                        {
                                            if (!AmandsSensePlugin.FleaIncludeAmmo.Value && TemplateIdToObjectMappingsClass.TypeTable["5485a8684bdc2da71d8b4567"].IsAssignableFrom(item.GetType()))
                                            {
                                                continue;
                                            }
                                            else if (AmandsSensePlugin.EnableFlea.Value && eSenseItemColor < ESenseItemColor.NonFlea)
                                            {
                                                eSenseItemColor = ESenseItemColor.NonFlea;
                                            }
                                        }
                                        else if (AmandsSenseClass.itemsJsonClass.KappaItems.Contains(item.TemplateId) && eSenseItemColor < ESenseItemColor.Kappa)
                                        {
                                            eSenseItemColor = ESenseItemColor.Kappa;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Apply value-based tier if no special flags matched
                if (eSenseItemColor == ESenseItemColor.Default && highestItemValue > 0)
                {
                    eSenseItemColor = AmandsSenseDeadPlayer.GetValueBasedTier(highestItemValue);
                }

                if (itemCount == 0)
                {
                    amandsSenseWorld.CancelSense();
                    return;
                }

                // SenseContainer Color and Sprite
                if (AmandsSenseClass.LoadedSprites.ContainsKey("LootableContainer.png"))
                {
                    sprite = AmandsSenseClass.LoadedSprites["LootableContainer.png"];
                }
                color = AmandsSenseDeadPlayer.GetTierColor(eSenseItemColor);

                // Override sprite for special types
                if (eSenseItemColor == ESenseItemColor.NonFlea && AmandsSenseClass.LoadedSprites.ContainsKey("icon_barter.png"))
                {
                    sprite = AmandsSenseClass.LoadedSprites["icon_barter.png"];
                }
                else if (eSenseItemColor == ESenseItemColor.WishList && AmandsSenseClass.LoadedSprites.ContainsKey("icon_fav_checked.png"))
                {
                    sprite = AmandsSenseClass.LoadedSprites["icon_fav_checked.png"];
                }

                // SenseContainer Sprite
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.color = new Color(color.r, color.g, color.b, spriteRenderer.color.a);
                }

                // SenseContainer Light
                if (light != null)
                {
                    light.color = new Color(color.r, color.g, color.b, 1f);
                    light.range = AmandsSensePlugin.LightRange.Value;
                }

                // SenseContainer Type
                if (typeText != null)
                {
                    typeText.fontSize = 0.5f;
                    //typeText.text = "Type";
                    typeText.color = new Color(color.r, color.g, color.b, typeText.color.a);
                }

                // SenseContainer Name - use tier color so fade reveals rarity
                if (nameText != null)
                {
                    nameText.fontSize = 1f;
                    //nameText.text = "Name";
                    nameText.color = new Color(color.r, color.g, color.b, nameText.color.a);
                }

                // SenseContainer Description - use tier color so fade reveals rarity
                if (descriptionText != null)
                {
                    descriptionText.fontSize = 0.75f;
                    if (AmandsSensePlugin.ContainerLootcount.Value)
                    {
                        descriptionText.text = AmandsSenseHelper.Localized("loot", EStringCase.None) + " " + itemCount;
                    }
                    else
                    {
                        descriptionText.text = "";
                    }
                    descriptionText.color = new Color(color.r, color.g, color.b, descriptionText.color.a);
                }
            }
            else if (amandsSenseWorld != null)
            {
                amandsSenseWorld.CancelSense();
            }
        }
        public override void UpdateIntensity(float Intensity)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(color.r, color.g, color.b, color.a * Intensity);
            }
            if (light != null)
            {
                light.intensity = AmandsSensePlugin.LightIntensity.Value * Intensity * (Drawer ? 0.25f : 1f);
            }
            if (typeText != null)
            {
                typeText.color = new Color(color.r, color.g, color.b, Intensity);
            }
            // Use tier color for text so fade reveals rarity (not white)
            if (nameText != null)
            {
                nameText.color = new Color(color.r, color.g, color.b, Intensity);
            }
            if (descriptionText != null)
            {
                descriptionText.color = new Color(color.r, color.g, color.b, Intensity);
            }

            // Update background plate opacity
            UpdateBackgroundPlateOpacity(Intensity);
        }
        public override void RemoveSense()
        {
            //Destroy(gameObject);
        }
    }
    /// <summary>
    /// Sense constructor for dead player bodies (corpses).
    /// Displays faction icon, name, and highlights based on loot value.
    /// </summary>
    public class AmandsSenseDeadPlayer : AmandsSenseConstructor
    {
        public Player DeadPlayer;
        public Corpse corpse;

        /// <summary>
        /// True if the body has no lootable items (excluding dogtags, secure container, etc.)
        /// Used to apply reduced opacity for empty bodies.
        /// </summary>
        public bool emptyDeadPlayer = true;
        public string Name;
        public string RoleName;

        /// <summary>
        /// Cached head transform for positioning. Null if not available.
        /// </summary>
        private Transform _headTransform;

        /// <summary>
        /// Cached ribcage transform for fallback positioning. Null if not available.
        /// </summary>
        private Transform _ribcageTransform;

        public override void SetSense(Player player)
        {
            AmandsSensePlugin.Log.LogInfo($"[AmandsSense] SetSense(DeadPlayer) called, player={player?.Profile?.Nickname ?? "null"}");

            // Fallback text assignment - ensure we never show placeholders
            // This runs FIRST, then the proper logic below overwrites with correct values
            try
            {
                string fallbackName = player?.Profile?.Nickname ?? player?.name ?? "BODY";
                string fallbackRole = player?.Side.ToString() ?? "UNKNOWN";
                if (typeText != null) typeText.text = fallbackRole;
                if (nameText != null) nameText.text = fallbackName;
                if (descriptionText != null) descriptionText.text = "";
            }
            catch (System.Exception)
            {
                // Fallback text failed - will be set by main logic
            }

            DeadPlayer = player;
            if (DeadPlayer != null && DeadPlayer.gameObject.activeSelf)
            {
                AmandsSensePlugin.Log.LogInfo($"[AmandsSense] DeadPlayer valid, starting inventory scan...");
                corpse = DeadPlayer.gameObject.transform.GetComponent<Corpse>();

                // Cache bone transforms for positioning (may be null for some NPCs)
                CacheBoneTransforms();

                // SenseDeadPlayer Defaults - assume body has loot unless we confirm it's empty
                // (emptyDeadPlayer = true only after we successfully scan and find nothing)
                emptyDeadPlayer = false; // Assume has loot by default
                bool inventoryScanned = false; // Track if we actually scanned
                ESenseItemColor eSenseItemColor = ESenseItemColor.Default;
                int lootCount = 0;
                int highestItemValue = 0; // Track highest value item for tier scoring

                // Check if this is a boss - bosses get minimum Legendary tier
                bool isBossCorpse = IsBoss(DeadPlayer);

                // Set fallback Name/RoleName based on side (works even without itemsJsonClass)
                switch (DeadPlayer.Side)
                {
                    case EPlayerSide.Usec:
                        RoleName = "USEC";
                        break;
                    case EPlayerSide.Bear:
                        RoleName = "BEAR";
                        break;
                    case EPlayerSide.Savage:
                        RoleName = "SCAV";
                        break;
                    default:
                        RoleName = "UNKNOWN";
                        break;
                }

                // Set name from Profile if available, otherwise use gameObject name
                Name = DeadPlayer.Profile?.Nickname ?? DeadPlayer.name ?? "Unknown";
                if (DeadPlayer.Side == EPlayerSide.Savage && !string.IsNullOrEmpty(Name))
                {
                    Name = AmandsSenseHelper.Transliterate(Name);
                }

                // Enhanced role/name detection if itemsJsonClass is available
                bool itemsJsonValid = AmandsSenseClass.itemsJsonClass != null && AmandsSenseClass.itemsJsonClass.RareItems != null && AmandsSenseClass.itemsJsonClass.KappaItems != null && AmandsSenseClass.itemsJsonClass.NonFleaExclude != null;
                bool playerValid = AmandsSenseClass.Player != null && AmandsSenseClass.Player.Profile != null;
                AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Checking conditions: itemsJsonValid={itemsJsonValid}, playerValid={playerValid}");
                if (itemsJsonValid && playerValid)
                {
                    if (DeadPlayer.Profile != null)
                    {
                        switch (DeadPlayer.Side)
                        {
                            case EPlayerSide.Usec:
                                RoleName = "USEC";
                                Name = DeadPlayer.Profile.Nickname;
                                break;
                            case EPlayerSide.Bear:
                                RoleName = "BEAR";
                                Name = DeadPlayer.Profile.Nickname;
                                break;
                            case EPlayerSide.Savage:
                                try
                                {
                                    RoleName = AmandsSenseHelper.Localized(AmandsSenseHelper.GetScavRoleKey(Traverse.Create(Traverse.Create(DeadPlayer.Profile.Info).Field("Settings").GetValue<object>()).Field("Role").GetValue<WildSpawnType>()), EStringCase.Upper);
                                }
                                catch { RoleName = "SCAV"; }
                                Name = AmandsSenseHelper.Transliterate(DeadPlayer.Profile.Nickname);
                                break;
                        }
                        try
                        {
                            object Inventory = Traverse.Create(DeadPlayer.Profile).Field("Inventory").GetValue();
                            AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Inventory object: {(Inventory != null ? "valid" : "NULL")}");
                            if (Inventory != null)
                            {
                                IEnumerable<Item> AllRealPlayerItems = Traverse.Create(Inventory).Property("AllRealPlayerItems").GetValue<IEnumerable<Item>>();
                                AmandsSensePlugin.Log.LogInfo($"[AmandsSense] AllRealPlayerItems: {(AllRealPlayerItems != null ? "valid" : "NULL")}");
                                if (AllRealPlayerItems != null)
                                {
                                    inventoryScanned = true; // We're actually scanning inventory
                                    foreach (Item item in AllRealPlayerItems)
                                    {
                                        try
                                        {
                                            // Check if item has a parent before accessing - some items throw exceptions
                                            ItemAddress parent = null;
                                            try { parent = item.Parent; } catch { }

                                            if (parent != null)
                                            {
                                                // Use cached parent to avoid repeated exceptions
                                                if (parent.Container != null && parent.Container.ParentItem != null)
                                                {
                                                    try
                                                    {
                                                        if (TemplateIdToObjectMappingsClass.TypeTable["5448bf274bdc2dfc2f8b456a"].IsAssignableFrom(parent.Container.ParentItem.GetType()))
                                                            continue; // Skip secure container items
                                                    }
                                                    catch { }
                                                }
                                                Slot slot = parent.Container as Slot;
                                                if (slot != null)
                                                {
                                                    if (slot.Name == "Dogtag" || slot.Name == "SecuredContainer" ||
                                                        slot.Name == "Scabbard" || slot.Name == "ArmBand")
                                                    {
                                                        continue;
                                                    }
                                                }
                                            }

                                            // Found a lootable item - count it
                                            lootCount++;

                                            // Track highest value item for value-based tiering
                                            int itemValue = GetItemValue(item);
                                            if (itemValue > highestItemValue)
                                            {
                                                highestItemValue = itemValue;
                                            }

                                            // Check item color priority: Wishlist > RareList > NonFlea > Kappa
                                            // (Value-based tier is applied after if none of these match)
                                            if (IsOnWishlist(item.TemplateId))
                                            {
                                                if (eSenseItemColor != ESenseItemColor.Legendary && eSenseItemColor != ESenseItemColor.Epic)
                                                {
                                                    eSenseItemColor = ESenseItemColor.WishList;
                                                }
                                            }
                                            else if (AmandsSenseClass.itemsJsonClass.RareItems.Contains(item.TemplateId))
                                            {
                                                // User-defined rare items get Legendary
                                                eSenseItemColor = ESenseItemColor.Legendary;
                                            }
                                            else if (item.Template != null && !item.Template.CanSellOnRagfair && !AmandsSenseClass.itemsJsonClass.NonFleaExclude.Contains(item.TemplateId))
                                            {
                                                if (!AmandsSensePlugin.FleaIncludeAmmo.Value && TemplateIdToObjectMappingsClass.TypeTable["5485a8684bdc2da71d8b4567"].IsAssignableFrom(item.GetType()))
                                                {
                                                    continue;
                                                }
                                                else if (AmandsSensePlugin.EnableFlea.Value && eSenseItemColor < ESenseItemColor.NonFlea)
                                                {
                                                    eSenseItemColor = ESenseItemColor.NonFlea;
                                                }
                                            }
                                            else if (AmandsSenseClass.itemsJsonClass.KappaItems.Contains(item.TemplateId) && eSenseItemColor < ESenseItemColor.Kappa)
                                            {
                                                eSenseItemColor = ESenseItemColor.Kappa;
                                            }
                                        }
                                        catch (Exception itemEx)
                                        {
                                            // Skip this item if it causes an error
                                            AmandsSensePlugin.Log.LogWarning($"[AmandsSense] Error scanning item: {itemEx.Message}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception invEx)
                        {
                            AmandsSensePlugin.Log.LogError($"[AmandsSense] Inventory scan failed: {invEx.Message}");
                        }
                    }
                }
                else
                {
                    // Fallback: Basic inventory scan when itemsJsonClass unavailable
                    // Still try to count items and get values for tier detection
                    if (DeadPlayer.Profile != null)
                    {
                        try
                        {
                            object Inventory = Traverse.Create(DeadPlayer.Profile).Field("Inventory").GetValue();
                            if (Inventory != null)
                            {
                                IEnumerable<Item> AllRealPlayerItems = Traverse.Create(Inventory).Property("AllRealPlayerItems").GetValue<IEnumerable<Item>>();
                                if (AllRealPlayerItems != null)
                                {
                                    inventoryScanned = true;
                                    foreach (Item item in AllRealPlayerItems)
                                    {
                                        // Skip non-lootable slots
                                        if (item.Parent != null)
                                        {
                                            Slot slot = item.Parent.Container as Slot;
                                            if (slot != null)
                                            {
                                                if (slot.Name == "Dogtag" || slot.Name == "SecuredContainer" ||
                                                    slot.Name == "Scabbard" || slot.Name == "ArmBand")
                                                {
                                                    continue;
                                                }
                                            }
                                            // Skip items in secure containers
                                            if (item.Parent.Container?.ParentItem != null)
                                            {
                                                try
                                                {
                                                    if (TemplateIdToObjectMappingsClass.TypeTable["5448bf274bdc2dfc2f8b456a"].IsAssignableFrom(item.Parent.Container.ParentItem.GetType()))
                                                        continue;
                                                }
                                                catch { }
                                            }
                                        }

                                        lootCount++;
                                        int itemValue = GetItemValue(item);
                                        if (itemValue > highestItemValue)
                                            highestItemValue = itemValue;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Apply value-based tier if no special tier was assigned
                if (eSenseItemColor == ESenseItemColor.Default && highestItemValue > 0)
                {
                    eSenseItemColor = GetValueBasedTier(highestItemValue);
                }

                // Fallback: if items exist but no value detected, use Common tier minimum
                // This prevents bodies with unpriced items from staying white
                if (eSenseItemColor == ESenseItemColor.Default && inventoryScanned && lootCount > 0)
                {
                    eSenseItemColor = ESenseItemColor.Common;
                }

                // Boss corpses get minimum Legendary tier
                if (isBossCorpse && eSenseItemColor < ESenseItemColor.Legendary)
                {
                    eSenseItemColor = ESenseItemColor.Legendary;
                }

                // Determine if body is empty (only if we actually scanned inventory)
                if (inventoryScanned && lootCount == 0)
                {
                    emptyDeadPlayer = true;
                }

                // If body is empty and user doesn't want to see empty bodies, cancel
                if (emptyDeadPlayer && !AmandsSensePlugin.ShowEmptyBodies.Value)
                {
                    if (amandsSenseWorld != null)
                    {
                        amandsSenseWorld.CancelSense();
                    }
                    return;
                }

                switch (DeadPlayer.Side)
                {
                    case EPlayerSide.Usec:
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("Usec.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["Usec.png"];
                        }
                        break;
                    case EPlayerSide.Bear:
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("Bear.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["Bear.png"];
                        }
                        break;
                    case EPlayerSide.Savage:
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_kills_big.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_kills_big.png"];
                        }
                        break;
                }

                // Get color for the determined tier
                color = GetTierColor(eSenseItemColor);

                // Diagnostic: log body scan results
                AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Body '{Name}' ({RoleName}): scanned={inventoryScanned}, lootCount={lootCount}, highestValue={highestItemValue}, tier={eSenseItemColor}, boss={isBossCorpse}");

                // SenseDeadPlayer Sprite
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.color = new Color(color.r, color.g, color.b, 0f);
                }

                // SenseDeadPlayer Light
                if (light != null)
                {
                    light.color = new Color(color.r, color.g, color.b, 1f);
                    light.intensity = 0f;
                    light.range = AmandsSensePlugin.LightRange.Value;
                }

                // Determine loot tier text based on eSenseItemColor
                string lootTierText;
                if (emptyDeadPlayer)
                {
                    lootTierText = "EMPTY";
                }
                else
                {
                    switch (eSenseItemColor)
                    {
                        case ESenseItemColor.Rare:
                            lootTierText = "RARE";
                            break;
                        case ESenseItemColor.WishList:
                            lootTierText = "WISHLIST";
                            break;
                        case ESenseItemColor.NonFlea:
                            lootTierText = "NON-FLEA";
                            break;
                        case ESenseItemColor.Kappa:
                            lootTierText = "KAPPA";
                            break;
                        default:
                            lootTierText = RoleName; // Fallback to faction if no special loot
                            break;
                    }
                }

                // SenseDeadPlayer Type - show loot tier (uses tier color for fill)
                if (typeText != null)
                {
                    typeText.fontSize = 0.5f;
                    typeText.text = lootTierText;
                    typeText.color = new Color(color.r, color.g, color.b, 0f);
                    // Use dead body styling (tier color fill, dark outline)
                    ApplyDeadBodyTextStyle(typeText, color, 0.5f);
                }

                // SenseDeadPlayer Name - show player name with faction
                // For dead bodies, use tier color for better loot quality visibility
                if (nameText != null)
                {
                    nameText.fontSize = 1f;
                    nameText.text = "<b>" + Name + "</b> <size=50%>(" + RoleName + ")";
                    // Use tier color for dead body name text (not generic white)
                    nameText.color = new Color(color.r, color.g, color.b, 0f);
                    // Apply styling with tier color for outline glow
                    ApplyDeadBodyTextStyle(nameText, color, 1f);
                }

                // SenseDeadPlayer Description - show item count
                if (descriptionText != null)
                {
                    descriptionText.fontSize = 0.75f;
                    descriptionText.text = lootCount > 0 ? lootCount + " items" : "";
                    // Use tier color for description too
                    descriptionText.color = new Color(color.r, color.g, color.b, 0f);
                    ApplyDeadBodyTextStyle(descriptionText, color, 0.75f);
                }
            }
            else if (amandsSenseWorld != null)
            {
                amandsSenseWorld.CancelSense();
            }
        }
        public override void UpdateSense()
        {
            if (DeadPlayer != null && DeadPlayer.gameObject.activeSelf)// && bodyPartCollider != null && bodyPartCollider.gameObject.activeSelf && bodyPartCollider.Collider != null && AmandsSenseClass.localPlayer != null && bodyPartCollider.Collider.transform.position.y > AmandsSenseClass.localPlayer.Position.y + AmandsSensePlugin.MinHeight.Value && bodyPartCollider.Collider.transform.position.y < AmandsSenseClass.localPlayer.Position.y + AmandsSensePlugin.MaxHeight.Value)
            {
                // SenseDeadPlayer Defaults - assume has loot unless we confirm it's empty
                // DO NOT reset emptyDeadPlayer to true here - preserve SetSense's determination
                // Only update if we successfully scan inventory
                bool inventoryScanned = false;
                ESenseItemColor eSenseItemColor = ESenseItemColor.Default;
                int lootCount = 0;
                int highestItemValue = 0; // Track highest value item for tier scoring

                // Check if this is a boss - bosses get minimum Legendary tier
                bool isBossCorpse = IsBoss(DeadPlayer);

                if (AmandsSenseClass.itemsJsonClass != null && AmandsSenseClass.itemsJsonClass.RareItems != null && AmandsSenseClass.itemsJsonClass.KappaItems != null && AmandsSenseClass.itemsJsonClass.NonFleaExclude != null && AmandsSenseClass.Player != null && AmandsSenseClass.Player.Profile != null)
                {
                    if (DeadPlayer != null && DeadPlayer.Profile != null)
                    {
                        object Inventory = Traverse.Create(DeadPlayer.Profile).Field("Inventory").GetValue();
                        if (Inventory != null)
                        {
                            IEnumerable<Item> AllRealPlayerItems = Traverse.Create(Inventory).Property("AllRealPlayerItems").GetValue<IEnumerable<Item>>();
                            if (AllRealPlayerItems != null)
                            {
                                inventoryScanned = true; // We're actually scanning
                                foreach (Item item in AllRealPlayerItems)
                                {
                                    if (item.Parent != null)
                                    {
                                        if (item.Parent.Container != null && item.Parent.Container.ParentItem != null && TemplateIdToObjectMappingsClass.TypeTable["5448bf274bdc2dfc2f8b456a"].IsAssignableFrom(item.Parent.Container.ParentItem.GetType()))
                                        {
                                            continue;
                                        }
                                        Slot slot = item.Parent.Container as Slot;
                                        if (slot != null)
                                        {
                                            if (slot.Name == "Dogtag")
                                            {
                                                continue;
                                            }
                                            if (slot.Name == "SecuredContainer")
                                            {
                                                continue;
                                            }
                                            if (slot.Name == "Scabbard")
                                            {
                                                continue;
                                            }
                                            if (slot.Name == "ArmBand")
                                            {
                                                continue;
                                            }
                                        }
                                    }

                                    // Found a lootable item - count it
                                    lootCount++;

                                    // Track highest value item for value-based tiering
                                    int itemValue = GetItemValue(item);
                                    if (itemValue > highestItemValue)
                                    {
                                        highestItemValue = itemValue;
                                    }

                                    // Check item color priority: Wishlist > RareList > NonFlea > Kappa
                                    // (Value-based tier is applied after if none of these match)
                                    if (IsOnWishlist(item.TemplateId))
                                    {
                                        if (eSenseItemColor != ESenseItemColor.Legendary && eSenseItemColor != ESenseItemColor.Epic)
                                        {
                                            eSenseItemColor = ESenseItemColor.WishList;
                                        }
                                    }
                                    else if (AmandsSenseClass.itemsJsonClass.RareItems.Contains(item.TemplateId))
                                    {
                                        // User-defined rare items get Legendary
                                        eSenseItemColor = ESenseItemColor.Legendary;
                                    }
                                    else if (item.Template != null && !item.Template.CanSellOnRagfair && !AmandsSenseClass.itemsJsonClass.NonFleaExclude.Contains(item.TemplateId))
                                    {
                                        if (!AmandsSensePlugin.FleaIncludeAmmo.Value && TemplateIdToObjectMappingsClass.TypeTable["5485a8684bdc2da71d8b4567"].IsAssignableFrom(item.GetType()))
                                        {
                                            continue;
                                        }
                                        else if (AmandsSensePlugin.EnableFlea.Value && eSenseItemColor < ESenseItemColor.NonFlea)
                                        {
                                            eSenseItemColor = ESenseItemColor.NonFlea;
                                        }
                                    }
                                    else if (AmandsSenseClass.itemsJsonClass.KappaItems.Contains(item.TemplateId) && eSenseItemColor < ESenseItemColor.Kappa)
                                    {
                                        eSenseItemColor = ESenseItemColor.Kappa;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Fallback: Basic inventory scan when itemsJsonClass unavailable
                    // Still try to count items and get values for tier detection
                    if (DeadPlayer.Profile != null)
                    {
                        try
                        {
                            object Inventory = Traverse.Create(DeadPlayer.Profile).Field("Inventory").GetValue();
                            if (Inventory != null)
                            {
                                IEnumerable<Item> AllRealPlayerItems = Traverse.Create(Inventory).Property("AllRealPlayerItems").GetValue<IEnumerable<Item>>();
                                if (AllRealPlayerItems != null)
                                {
                                    inventoryScanned = true;
                                    foreach (Item item in AllRealPlayerItems)
                                    {
                                        // Skip non-lootable slots
                                        if (item.Parent != null)
                                        {
                                            Slot slot = item.Parent.Container as Slot;
                                            if (slot != null)
                                            {
                                                if (slot.Name == "Dogtag" || slot.Name == "SecuredContainer" ||
                                                    slot.Name == "Scabbard" || slot.Name == "ArmBand")
                                                {
                                                    continue;
                                                }
                                            }
                                            // Skip items in secure containers
                                            if (item.Parent.Container?.ParentItem != null)
                                            {
                                                try
                                                {
                                                    if (TemplateIdToObjectMappingsClass.TypeTable["5448bf274bdc2dfc2f8b456a"].IsAssignableFrom(item.Parent.Container.ParentItem.GetType()))
                                                        continue;
                                                }
                                                catch { }
                                            }
                                        }

                                        lootCount++;
                                        int itemValue = GetItemValue(item);
                                        if (itemValue > highestItemValue)
                                            highestItemValue = itemValue;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Apply value-based tier if no special tier was assigned
                if (eSenseItemColor == ESenseItemColor.Default && highestItemValue > 0)
                {
                    eSenseItemColor = GetValueBasedTier(highestItemValue);
                }

                // Fallback: if items exist but no value detected, use Common tier minimum
                // This prevents bodies with unpriced items from staying white
                if (eSenseItemColor == ESenseItemColor.Default && inventoryScanned && lootCount > 0)
                {
                    eSenseItemColor = ESenseItemColor.Common;
                }

                // Boss corpses get minimum Legendary tier
                if (isBossCorpse && eSenseItemColor < ESenseItemColor.Legendary)
                {
                    eSenseItemColor = ESenseItemColor.Legendary;
                }

                // Only update emptyDeadPlayer if we successfully scanned inventory
                // This allows real-time updates when player loots the body
                if (inventoryScanned)
                {
                    emptyDeadPlayer = (lootCount == 0);
                }
                // If scan failed, preserve SetSense's determination (don't reset to empty)

                switch (DeadPlayer.Side)
                {
                    case EPlayerSide.Usec:
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("Usec.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["Usec.png"];
                        }
                        break;
                    case EPlayerSide.Bear:
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("Bear.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["Bear.png"];
                        }
                        break;
                    case EPlayerSide.Savage:
                        if (AmandsSenseClass.LoadedSprites.ContainsKey("icon_kills_big.png"))
                        {
                            sprite = AmandsSenseClass.LoadedSprites["icon_kills_big.png"];
                        }
                        break;
                }

                // Get color for the determined tier
                color = GetTierColor(eSenseItemColor);

                // SenseDeadPlayer Sprite
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.color = new Color(color.r, color.g, color.b, spriteRenderer.color.a);
                }

                // SenseDeadPlayer Light
                if (light != null)
                {
                    light.color = new Color(color.r, color.g, color.b, 1f);
                    light.range = AmandsSensePlugin.LightRange.Value;
                }

                // Determine loot tier text based on eSenseItemColor (dynamic update)
                string lootTierText;
                if (emptyDeadPlayer)
                {
                    lootTierText = "EMPTY";
                }
                else
                {
                    switch (eSenseItemColor)
                    {
                        case ESenseItemColor.Rare:
                            lootTierText = "RARE";
                            break;
                        case ESenseItemColor.WishList:
                            lootTierText = "WISHLIST";
                            break;
                        case ESenseItemColor.NonFlea:
                            lootTierText = "NON-FLEA";
                            break;
                        case ESenseItemColor.Kappa:
                            lootTierText = "KAPPA";
                            break;
                        default:
                            lootTierText = RoleName; // Fallback to faction if no special loot
                            break;
                    }
                }

                // SenseDeadPlayer Type - update loot tier
                if (typeText != null)
                {
                    typeText.fontSize = 0.5f;
                    typeText.text = lootTierText;
                    typeText.color = new Color(color.r, color.g, color.b, typeText.color.a);
                    // Use dead body styling (tier color fill, dark outline)
                    ApplyDeadBodyTextStyle(typeText, color, 0.5f);
                }

                // SenseDeadPlayer Name - use tier color for visibility
                if (nameText != null)
                {
                    nameText.fontSize = 1f;
                    nameText.color = new Color(color.r, color.g, color.b, nameText.color.a);
                    ApplyDeadBodyTextStyle(nameText, color, 1f);
                }

                // SenseDeadPlayer Description - update item count with tier color
                if (descriptionText != null)
                {
                    descriptionText.fontSize = 0.75f;
                    descriptionText.text = lootCount > 0 ? lootCount + " items" : "";
                    descriptionText.color = new Color(color.r, color.g, color.b, descriptionText.color.a);
                    ApplyDeadBodyTextStyle(descriptionText, color, 0.75f);
                }
            }
            else if (amandsSenseWorld != null)
            {
                amandsSenseWorld.CancelSense();
            }
        }
        public override void UpdateIntensity(float Intensity)
        {
            // Apply empty body opacity modifier if body has no loot
            float opacityModifier = emptyDeadPlayer ? AmandsSensePlugin.EmptyBodyOpacity.Value : 1f;
            float adjustedIntensity = Intensity * opacityModifier;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(color.r, color.g, color.b, color.a * adjustedIntensity);
            }
            if (light != null)
            {
                light.intensity = AmandsSensePlugin.LightIntensity.Value * adjustedIntensity;
            }
            // Use tier color for all text (not white) - preserves loot quality visibility
            if (typeText != null)
            {
                typeText.color = new Color(color.r, color.g, color.b, adjustedIntensity);
            }
            if (nameText != null)
            {
                nameText.color = new Color(color.r, color.g, color.b, adjustedIntensity);
            }
            if (descriptionText != null)
            {
                descriptionText.color = new Color(color.r, color.g, color.b, adjustedIntensity);
            }

            // Update background plate opacity
            UpdateBackgroundPlateOpacity(adjustedIntensity);
        }

        /// <summary>
        /// Updates the marker position to follow the corpse.
        /// Uses head position when available, falls back to ribcage, then TrackableTransform.
        /// </summary>
        public override void UpdateSenseLocation()
        {
            if (gameObject?.transform?.parent == null)
                return;

            Vector3 targetPosition = GetBodyPosition();
            gameObject.transform.parent.position = targetPosition + (Vector3.up * AmandsSensePlugin.DeadBodyVerticalOffset.Value);
        }

        /// <summary>
        /// Gets the best available position for the body marker.
        /// Uses center of corpse bounds horizontally, with marker floating above at waist height.
        /// Ragdolled bodies can face any direction - we use world UP, not bone orientation.
        /// </summary>
        private const float MIN_MARKER_HEIGHT = 0.8f; // Minimum height above corpse floor for visibility

        private Vector3 GetBodyPosition()
        {
            // Strategy: Find the XZ center of the corpse bounds, then place marker above
            // This works regardless of how the body ragdolled (facing up, down, sideways, etc.)

            // Try corpse collider bounds first (most reliable for ragdolled bodies)
            if (corpse != null)
            {
                var colliders = corpse.GetComponentsInChildren<Collider>();
                if (colliders != null && colliders.Length > 0)
                {
                    // Compute combined bounds of all colliders
                    Bounds combinedBounds = new Bounds();
                    bool boundsInitialized = false;

                    foreach (var col in colliders)
                    {
                        if (col != null && col.enabled)
                        {
                            if (!boundsInitialized)
                            {
                                combinedBounds = col.bounds;
                                boundsInitialized = true;
                            }
                            else
                            {
                                combinedBounds.Encapsulate(col.bounds);
                            }
                        }
                    }

                    if (boundsInitialized)
                    {
                        // Use center XZ, and place at top of bounds + small offset
                        // This puts the marker at "waist height" above the body mass
                        Vector3 center = combinedBounds.center;
                        float markerY = combinedBounds.max.y;

                        // Ensure minimum height above the floor of the bounds
                        float floorY = combinedBounds.min.y;
                        if (markerY < floorY + MIN_MARKER_HEIGHT)
                        {
                            markerY = floorY + MIN_MARKER_HEIGHT;
                        }

                        return new Vector3(center.x, markerY, center.z);
                    }
                }

                // Fallback to trackable transform if no valid colliders
                Vector3 trackablePos = corpse.TrackableTransform.position;
                return new Vector3(trackablePos.x, trackablePos.y + MIN_MARKER_HEIGHT, trackablePos.z);
            }

            // Try ribcage as center reference (chest is roughly body center)
            if (_ribcageTransform != null)
            {
                Vector3 ribPos = _ribcageTransform.position;
                return new Vector3(ribPos.x, ribPos.y + 0.3f, ribPos.z); // Small offset above ribcage
            }

            // Last resort - use DeadPlayer position
            if (DeadPlayer != null)
            {
                Vector3 pos = DeadPlayer.Position;
                return new Vector3(pos.x, pos.y + MIN_MARKER_HEIGHT, pos.z);
            }

            return Vector3.zero;
        }

        /// <summary>
        /// Caches bone transforms for efficient position lookups.
        /// </summary>
        private void CacheBoneTransforms()
        {
            if (DeadPlayer?.PlayerBones == null)
                return;

            try
            {
                // Get head transform (BifacialTransform has Original property for the actual Transform)
                var headBone = DeadPlayer.PlayerBones.Head;
                if (headBone != null)
                {
                    _headTransform = headBone.Original;
                }

                // Get ribcage transform as fallback
                var ribcageBone = DeadPlayer.PlayerBones.Ribcage;
                if (ribcageBone != null)
                {
                    _ribcageTransform = ribcageBone.Original;
                }
            }
            catch (System.Exception)
            {
                // Some NPCs may not have all bones - silently ignore
                _headTransform = null;
                _ribcageTransform = null;
            }
        }

        /// <summary>
        /// Checks if an item is on the player's wishlist.
        /// Uses SPT 4.0.x WishlistManager API.
        /// </summary>
        public static bool IsOnWishlist(string templateId)
        {
            if (string.IsNullOrEmpty(templateId))
                return false;

            try
            {
                var player = AmandsSenseClass.Player;
                if (player?.Profile?.WishlistManager == null)
                    return false;

                var wishlist = player.Profile.WishlistManager.GetWishlist();
                return wishlist != null && wishlist.ContainsKey(templateId);
            }
            catch (System.Exception)
            {
                // Wishlist API may not be available in all scenarios
                return false;
            }
        }

        // Diagnostic: track price lookups
        private static int _priceLookupsTotal = 0;
        private static int _priceLookupsFromFlea = 0;
        private static int _priceLookupsFromCredits = 0;
        private static int _priceLookupsFromFallback = 0;
        private static bool _loggedPriceDiagnostics = false;

        /// <summary>
        /// Gets the best price for an item using real flea market and trader prices.
        /// Supports per-slot value calculation and threshold filtering.
        /// </summary>
        public static int GetItemValue(Item item)
        {
            if (item?.Template == null)
                return 0;

            _priceLookupsTotal++;

            try
            {
                string templateId = item.TemplateId;
                int bestPrice = 0;
                string priceSource = "none";

                // 1. Try flea market price (most accurate for player value)
                int fleaPrice = AmandsSenseClass.GetFleaPrice(templateId);
                if (fleaPrice > 0)
                {
                    bestPrice = fleaPrice;
                    priceSource = "flea";
                    _priceLookupsFromFlea++;
                }

                // 2. Try template CreditsPrice (vendor base price - always available)
                int creditsPrice = item.Template.CreditsPrice;
                if (creditsPrice > bestPrice)
                {
                    bestPrice = creditsPrice;
                    if (priceSource == "none") priceSource = "credits";
                    _priceLookupsFromCredits++;
                }

                // 3. Apply per-slot value calculation if enabled
                if (AmandsSensePlugin.UsePerSlotValue.Value && bestPrice > 0)
                {
                    try
                    {
                        var cellSize = item.CalculateCellSize();
                        int slotCount = cellSize.X * cellSize.Y;
                        if (slotCount > 1)
                        {
                            bestPrice = bestPrice / slotCount;
                        }
                    }
                    catch { } // CalculateCellSize may fail for some items
                }

                // Log first few lookups for diagnostics
                if (_priceLookupsTotal <= 5)
                {
                    AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Price lookup #{_priceLookupsTotal}: {item.Template.ShortName ?? templateId} -> flea={fleaPrice}, credits={creditsPrice}, best={bestPrice} ({priceSource})");
                }

                // Log summary after first body scan
                if (_priceLookupsTotal == 20 && !_loggedPriceDiagnostics)
                {
                    _loggedPriceDiagnostics = true;
                    AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Price diagnostics: {_priceLookupsTotal} lookups - flea={_priceLookupsFromFlea}, credits={_priceLookupsFromCredits}, fallback={_priceLookupsFromFallback}");
                }

                // 4. Apply threshold filter - items below threshold are treated as 0
                int threshold = AmandsSensePlugin.LootValueThreshold.Value;
                if (threshold > 0 && bestPrice < threshold)
                {
                    return 0; // Below threshold, treat as junk
                }

                if (bestPrice > 0)
                    return bestPrice;

                // 5. Fallback: Estimate based on item type
                _priceLookupsFromFallback++;
                string parentId = item.Template.Parent?._id ?? "";

                // Weapons - always valuable
                if (parentId == "5447b5cf4bdc2d65278b4567" || // assault rifle
                    parentId == "5447b5e04bdc2d62278b4567" || // SMG
                    parentId == "5447b6094bdc2dc3278b4567" || // shotgun
                    parentId == "5447b6194bdc2d67278b4568" || // marksman rifle
                    parentId == "5447b6254bdc2dc3278b4568" || // sniper rifle
                    parentId == "5447bed64bdc2d97278b4568" || // machine gun
                    parentId == "5447b5fc4bdc2d87278b4567" || // assault carbine
                    parentId == "5447b5f14bdc2d61278b4567")   // pistol
                    return 50000;

                // Armor
                if (parentId == "5448e54d4bdc2dcc718b4568" || // armor vest
                    parentId == "5a341c4086f77401f2541505" || // helmet
                    parentId == "5448e5284bdc2dcb718b4567")   // tactical rig
                    return 30000;

                // Containers and gear
                if (parentId == "5448e53e4bdc2d60728b4567" || // backpack
                    parentId == "5795f317245977243854e041")   // secure container
                    return 25000;

                // Medical
                if (parentId == "5448f3a64bdc2d60728b456a" || // medical
                    parentId == "5448f39d4bdc2d0a728b4568")   // medkit
                    return 15000;

                // Default: assume Uncommon tier minimum so items aren't white
                return 20000;
            }
            catch (Exception ex)
            {
                AmandsSensePlugin.Log.LogError($"[AmandsSense] GetItemValue error: {ex.Message}");
                return 20000; // Default to Uncommon tier minimum
            }
        }

        /// <summary>
        /// Determines the tier color based on the highest value item on a corpse.
        /// Value thresholds: Junk under 5k, Common 5-20k, Uncommon 20-50k, Rare 50-150k, Epic 150-500k, Legendary over 500k
        /// </summary>
        public static ESenseItemColor GetValueBasedTier(int highestItemValue)
        {
            if (highestItemValue >= 500000)
                return ESenseItemColor.Legendary;
            if (highestItemValue >= 150000)
                return ESenseItemColor.Epic;
            if (highestItemValue >= 50000)
                return ESenseItemColor.Rare;
            if (highestItemValue >= 20000)
                return ESenseItemColor.Uncommon;
            if (highestItemValue >= 5000)
                return ESenseItemColor.Common;
            return ESenseItemColor.Junk;
        }

        /// <summary>
        /// Checks if the dead player is a boss based on their WildSpawnType.
        /// Bosses get minimum Legendary tier.
        /// </summary>
        public static bool IsBoss(Player player)
        {
            if (player?.Profile?.Info == null)
                return false;

            try
            {
                // Get the WildSpawnType from profile settings
                var settings = Traverse.Create(player.Profile.Info).Field("Settings").GetValue<object>();
                if (settings == null)
                    return false;

                var role = Traverse.Create(settings).Field("Role").GetValue<WildSpawnType>();

                // Check for boss spawn types (including rogues/raiders which have high-tier loot)
                switch (role)
                {
                    // Main bosses
                    case WildSpawnType.bossKilla:
                    case WildSpawnType.bossKojaniy:
                    case WildSpawnType.bossBully:
                    case WildSpawnType.bossGluhar:
                    case WildSpawnType.bossSanitar:
                    case WildSpawnType.bossTagilla:
                    case WildSpawnType.bossKnight:
                    case WildSpawnType.bossZryachiy:
                    case WildSpawnType.bossBoar:
                    case WildSpawnType.bossKolontay:
                    case WildSpawnType.bossPartisan:
                    // Goons (count as bosses)
                    case WildSpawnType.followerBigPipe:
                    case WildSpawnType.followerBirdEye:
                    // Cultists
                    case WildSpawnType.sectantPriest:
                    case WildSpawnType.sectantWarrior:
                    // Rogues and Raiders (high-tier loot)
                    case WildSpawnType.exUsec:
                    case WildSpawnType.pmcBot:
                        return true;
                    default:
                        // Fallback: use game's native IsBoss check via reflection
                        return AmandsSenseHelper.IsBoss(role);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the color for the given tier.
        /// </summary>
        public static Color GetTierColor(ESenseItemColor tier)
        {
            switch (tier)
            {
                case ESenseItemColor.Legendary:
                    return AmandsSensePlugin.LegendaryColor.Value;
                case ESenseItemColor.Epic:
                    return AmandsSensePlugin.EpicColor.Value;
                case ESenseItemColor.Rare:
                    return AmandsSensePlugin.RareItemsColor.Value;
                case ESenseItemColor.WishList:
                    return AmandsSensePlugin.WishListItemsColor.Value;
                case ESenseItemColor.NonFlea:
                    return AmandsSensePlugin.NonFleaItemsColor.Value;
                case ESenseItemColor.Kappa:
                    return AmandsSensePlugin.KappaItemsColor.Value;
                case ESenseItemColor.Uncommon:
                    return AmandsSensePlugin.UncommonColor.Value;
                case ESenseItemColor.Common:
                    return AmandsSensePlugin.CommonColor.Value;
                case ESenseItemColor.Junk:
                    return AmandsSensePlugin.JunkColor.Value;
                default:
                    return AmandsSensePlugin.ObservedLootItemColor.Value;
            }
        }

        public override void RemoveSense()
        {
            // Cleanup cached references
            _headTransform = null;
            _ribcageTransform = null;
        }
    }
    public class AmandsSenseExfil : MonoBehaviour
    {
        public ExfiltrationPoint exfiltrationPoint;

        public Color color = Color.green;
        public Color textColor = AmandsSensePlugin.TextColor.Value;

        public SpriteRenderer spriteRenderer;
        public Sprite sprite;

        public Light light;

        public GameObject textGameObject;

        public TextMeshPro typeText;
        public TextMeshPro nameText;
        public TextMeshPro descriptionText;
        public TextMeshPro distanceText;

        public float Delay;
        public float LifeSpan;

        public bool UpdateIntensity = false;
        public bool Starting = true;
        public float Intensity = 0f;

        public void SetSense(ExfiltrationPoint ExfiltrationPoint)
        {
            exfiltrationPoint = ExfiltrationPoint;
            gameObject.transform.position = exfiltrationPoint.transform.position + (Vector3.up * AmandsSensePlugin.ExfilVerticalOffset.Value);
            gameObject.transform.localScale = new Vector3(-50,50,50);
        }

        public void Construct()
        {
            // AmandsSenseExfil Sprite GameObject
            GameObject spriteGameObject = new GameObject("Sprite");
            spriteGameObject.transform.SetParent(gameObject.transform, false);
            RectTransform spriteRectTransform = spriteGameObject.AddComponent<RectTransform>();
            spriteRectTransform.localScale /= 50f;

            // AmandsSenseExfil Sprite
            spriteRenderer = spriteGameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = new Color(color.r, color.g, color.b, 0f);

            // AmandsSenseExfil Sprite Light
            light = spriteGameObject.AddComponent<Light>();
            light.color = new Color(color.r, color.g, color.b, 1f);
            light.shadows = LightShadows.None;
            light.intensity = 0f;
            light.range = AmandsSensePlugin.ExfilLightRange.Value;

            // AmandsSenseExfil Text
            textGameObject = new GameObject("Text");
            textGameObject.transform.SetParent(gameObject.transform, false);
            RectTransform textRectTransform = textGameObject.AddComponent<RectTransform>();
            textRectTransform.localPosition = new Vector3(0.1f, 0, 0);
            textRectTransform.pivot = new Vector2(0, 0.5f);

            // AmandsSenseExfil VerticalLayoutGroup
            VerticalLayoutGroup verticalLayoutGroup = textGameObject.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.spacing = -0.02f;
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.childForceExpandWidth = false;
            verticalLayoutGroup.childControlHeight = true;
            verticalLayoutGroup.childControlWidth = true;
            ContentSizeFitter contentSizeFitter = textGameObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject typeTextGameObject = new GameObject("Type");
            typeTextGameObject.transform.SetParent(textGameObject.transform, false);
            typeText = typeTextGameObject.AddComponent<TextMeshPro>();
            typeText.autoSizeTextContainer = true;
            typeText.fontSize = 0.5f;
            typeText.text = "Type";
            typeText.color = new Color(color.r, color.g, color.b, 0f);

            GameObject nameTextGameObject = new GameObject("Name");
            nameTextGameObject.transform.SetParent(textGameObject.transform, false);
            nameText = nameTextGameObject.AddComponent<TextMeshPro>();
            nameText.autoSizeTextContainer = true;
            nameText.fontSize = 1f;
            nameText.text = "Name";
            nameText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);

            GameObject descriptionTextGameObject = new GameObject("Description");
            descriptionTextGameObject.transform.SetParent(textGameObject.transform, false);
            descriptionText = descriptionTextGameObject.AddComponent<TextMeshPro>();
            descriptionText.autoSizeTextContainer = true;
            descriptionText.fontSize = 0.75f;
            descriptionText.text = "";
            descriptionText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);

            GameObject distanceTextGameObject = new GameObject("Distance");
            distanceTextGameObject.transform.SetParent(gameObject.transform, false);
            distanceTextGameObject.transform.localPosition = new Vector3(0, -0.13f, 0);
            distanceText = distanceTextGameObject.AddComponent<TextMeshPro>();
            distanceText.alignment = TextAlignmentOptions.Center;
            distanceText.autoSizeTextContainer = true;
            distanceText.fontSize = 0.75f;
            distanceText.text = "Distance";
            distanceText.color = new Color(color.r, color.g, color.b, 0f);

            enabled = false;
            gameObject.SetActive(false);
        }
        public void ShowSense()
        {
            color = Color.green;
            textColor = AmandsSensePlugin.TextColor.Value;

            if (exfiltrationPoint != null && exfiltrationPoint.gameObject.activeSelf && AmandsSenseClass.Player != null && exfiltrationPoint.InfiltrationMatch(AmandsSenseClass.Player))
            {
                sprite = AmandsSenseClass.LoadedSprites["Exfil.png"];
                bool Unmet = exfiltrationPoint.UnmetRequirements(AmandsSenseClass.Player).ToArray().Any();
                color = Unmet ? AmandsSensePlugin.ExfilUnmetColor.Value : AmandsSensePlugin.ExfilColor.Value;
                // AmandsSenseExfil Sprite
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.color = new Color(color.r, color.g, color.b, 0f);
                }

                // AmandsSenseExfil Light
                if (light != null)
                {
                    light.color = new Color(color.r, color.g, color.b, 1f);
                    light.intensity = 0f;
                    light.range = AmandsSensePlugin.ExfilLightRange.Value;
                }

                // AmandsSenseExfil Type
                if (typeText != null)
                {
                    typeText.fontSize = 0.5f;
                    typeText.text = AmandsSenseHelper.Localized("exfil", EStringCase.None);
                    typeText.color = new Color(color.r, color.g, color.b, 0f);
                }

                // AmandsSenseExfil Name
                if (nameText != null)
                {
                    nameText.fontSize = 1f;
                    nameText.text = "<b>" + AmandsSenseHelper.Localized(exfiltrationPoint.Settings.Name,0) + "</b><color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + "<size=50%><voffset=0.5em> " + exfiltrationPoint.Settings.ExfiltrationTime + "s";
                    nameText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
                }

                // AmandsSenseExfil Description
                if (descriptionText != null)
                {
                    descriptionText.fontSize = 0.75f;
                    string tips = "";
                    if (Unmet)
                    {
                        foreach (string tip in exfiltrationPoint.GetTips(AmandsSenseClass.Player.ProfileId))
                        {
                            tips = tips + tip + "\n";
                        }
                    }
                    descriptionText.overrideColorTags = true;
                    descriptionText.text = tips;
                    descriptionText.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
                }

                // AmandsSenseExfil Distancce
                if (distanceText != null)
                {
                    distanceText.fontSize = 0.5f;
                    var cam = AmandsSenseClass.GameCamera;
                    if (cam != null) distanceText.text = (int)Vector3.Distance(transform.position, cam.transform.position) + "m";
                    distanceText.color = new Color(color.r, color.g, color.b, 0f);
                }

                gameObject.SetActive(true);
                enabled = true;

                LifeSpan = 0f;
                Starting = true;
                Intensity = 0f;
                UpdateIntensity = true;
            }
            if (exfiltrationPoint == null)
            {
                AmandsSenseClass.SenseExfils.Remove(this);
                Destroy(gameObject);
            }
        }
        public void UpdateSense()
        {
            if (exfiltrationPoint != null && exfiltrationPoint.gameObject.activeSelf && AmandsSenseClass.Player != null && exfiltrationPoint.InfiltrationMatch(AmandsSenseClass.Player))
            {
                sprite = AmandsSenseClass.LoadedSprites["Exfil.png"];
                bool Unmet = exfiltrationPoint.UnmetRequirements(AmandsSenseClass.Player).ToArray().Any();
                color = Unmet ? AmandsSensePlugin.ExfilUnmetColor.Value : AmandsSensePlugin.ExfilColor.Value;
                // AmandsSenseExfil Sprite
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                    spriteRenderer.color = new Color(color.r, color.g, color.b, color.a);
                }

                // AmandsSenseExfil Light
                if (light != null)
                {
                    light.color = new Color(color.r, color.g, color.b, 1f);
                    light.range = AmandsSensePlugin.ExfilLightRange.Value;
                }

                // AmandsSenseExfil Type
                if (typeText != null)
                {
                    typeText.fontSize = 0.5f;
                    typeText.text = AmandsSenseHelper.Localized("exfil", EStringCase.None);
                    typeText.color = new Color(color.r, color.g, color.b, color.a);
                }

                // AmandsSenseExfil Name
                if (nameText != null)
                {
                    nameText.fontSize = 1f;
                    nameText.text = "<b>" + AmandsSenseHelper.Localized(exfiltrationPoint.Settings.Name, 0) + "</b><color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + "<size=50%><voffset=0.5em> " + exfiltrationPoint.Settings.ExfiltrationTime + "s";
                    nameText.color = new Color(textColor.r, textColor.g, textColor.b, textColor.a);
                }

                // AmandsSenseExfil Description
                if (descriptionText != null)
                {
                    descriptionText.fontSize = 0.75f;
                    string tips = "";
                    if (Unmet)
                    {
                        foreach (string tip in exfiltrationPoint.GetTips(AmandsSenseClass.Player.ProfileId))
                        {
                            tips = tips + tip + "\n";
                        }
                    }
                    descriptionText.overrideColorTags = true;
                    descriptionText.text = tips;
                    descriptionText.color = new Color(textColor.r, textColor.g, textColor.b, textColor.a);
                }

                // AmandsSenseExfil Distancce
                if (distanceText != null)
                {
                    distanceText.fontSize = 0.5f;
                    var cam = AmandsSenseClass.GameCamera;
                    if (cam != null) distanceText.text = (int)Vector3.Distance(transform.position, cam.transform.position) + "m";
                    distanceText.color = new Color(color.r, color.g, color.b, color.a);
                }
            }
            if (exfiltrationPoint == null)
            {
                AmandsSenseClass.SenseExfils.Remove(this);
                Destroy(gameObject);
            }
        }
        public void Update()
        {
            if (UpdateIntensity)
            {
                if (Starting)
                {
                    Intensity += AmandsSensePlugin.IntensitySpeed.Value * Time.deltaTime;
                    if (Intensity >= 1f)
                    {
                        UpdateIntensity = false;
                        Starting = false;
                    }
                }
                else
                {
                    Intensity -= AmandsSensePlugin.IntensitySpeed.Value * Time.deltaTime;
                    if (Intensity <= 0f)
                    {
                        Starting = true;
                        UpdateIntensity = false;
                        enabled = false;
                        gameObject.SetActive(false);
                        return;
                    }
                }

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = new Color(color.r, color.g, color.b, color.a * Intensity);
                }
                if (light != null)
                {
                    light.intensity = Intensity * AmandsSensePlugin.ExfilLightIntensity.Value;
                }
                if (typeText != null)
                {
                    typeText.color = new Color(color.r, color.g, color.b, Intensity);
                }
                if (nameText != null)
                {
                    nameText.color = new Color(textColor.r, textColor.g, textColor.b, Intensity);
                }
                if (descriptionText != null)
                {
                    descriptionText.color = new Color(textColor.r, textColor.g, textColor.b, Intensity);
                }
                if (distanceText != null)
                {
                    distanceText.color = new Color(color.r, color.g, color.b, Intensity);
                }
            }
            else if (!Starting)
            {
                LifeSpan += Time.deltaTime;
                if (LifeSpan > AmandsSensePlugin.ExfilDuration.Value)
                {
                    UpdateIntensity = true;
                }
            }
            var cam = AmandsSenseClass.GameCamera;
            if (cam != null)
            {
                transform.LookAt(new Vector3(cam.transform.position.x, transform.position.y, cam.transform.position.z));
                if (distanceText != null)
                {
                    distanceText.text = (int)Vector3.Distance(transform.position, cam.transform.position) + "m";
                }
            }
        }
    }
    public class ItemsJsonClass
    {
        public List<string> RareItems { get; set; }
        public List<string> KappaItems { get; set; }
        public List<string> NonFleaExclude { get; set; }

        public ItemsJsonClass()
        {
            RareItems = new List<string>();
            KappaItems = new List<string>();
            NonFleaExclude = new List<string>();
        }
    }
    public enum ESenseItemType
    {
        All,
        ObservedLootItem,
        Others,
        BuildingMaterials,
        Electronics,
        EnergyElements,
        FlammableMaterials,
        HouseholdMaterials,
        MedicalSupplies,
        Tools,
        Valuables,
        Backpacks,
        BodyArmor,
        Eyewear,
        Facecovers,
        GearComponents,
        Headgear,
        Headsets,
        SecureContainers,
        StorageContainers,
        TacticalRigs,
        FunctionalMods,
        GearMods,
        VitalParts,
        AssaultCarbines,
        AssaultRifles,
        BoltActionRifles,
        GrenadeLaunchers,
        MachineGuns,
        MarksmanRifles,
        MeleeWeapons,
        Pistols,
        SMGs,
        Shotguns,
        SpecialWeapons,
        Throwables,
        AmmoPacks,
        Rounds,
        Drinks,
        Food,
        Injectors,
        InjuryTreatment,
        Medkits,
        Pills,
        ElectronicKeys,
        MechanicalKeys,
        InfoItems,
        QuestItems,
        SpecialEquipment,
        Maps,
        Money
    }
    public enum ESenseItemColor
    {
        Default,    // Fallback (white)
        Junk,       // < 5k value (gray)
        Common,     // 5k-20k (white)
        Uncommon,   // 20k-50k (green)
        Kappa,      // Kappa quest items (purple)
        NonFlea,    // Non-flea tradeable (purple)
        WishList,   // User wishlist (pink)
        Rare,       // 50k-150k or user-defined (blue)
        Epic,       // 150k-500k (purple)
        Legendary   // >500k or boss loot (gold)
    }
    public enum ESenseWorldType
    {
        Item,
        Container,
        Deadbody,
        Drawer
    }
    public enum EEnableSense
    {
        Off,
        On,
        OnText
    }
    public struct SenseDeadPlayerStruct
    {
        public Player victim;
        public Player aggressor;

        public SenseDeadPlayerStruct(Player Victim, Player Aggressor)
        {
            victim = Victim;
            aggressor = Aggressor;
        }
    }
}
