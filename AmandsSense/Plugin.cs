// ============================================================================
// AmandsSense - Loot Sensing and Highlighting Mod for SPT
// ============================================================================
// Original Author: Amands2Mello
// Updated for SPT 4.0.x: LT Studio (2025)
// License: MIT
//
// This mod provides a "sixth sense" ability that highlights nearby loot items,
// containers, corpses, and extraction points when activated.
// ============================================================================

using SPT.Reflection.Patching;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using EFT;
using EFT.Interactive;
using EFT.HealthSystem;
using EFT.UI;
using UnityEngine.SceneManagement;
using static EFT.Player;

namespace AmandsSense
{
    /// <summary>
    /// Plugin metadata constants.
    /// </summary>
    internal static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.Amanda.Sense";
        public const string PLUGIN_NAME = "AmandsSense";
        public const string PLUGIN_VERSION = "3.0.0";
    }

    /// <summary>
    /// Main plugin class for AmandsSense - a loot highlighting mod for SPT.
    /// Provides visual indicators for nearby loot, containers, corpses, and extracts.
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class AmandsSensePlugin : BaseUnityPlugin
    {
        /// <summary>Logger instance for this plugin.</summary>
        internal static ManualLogSource Log;

        /// <summary>Persistent GameObject that hosts the sense system.</summary>
        public static GameObject Hook;

        /// <summary>Main sense controller component.</summary>
        public static AmandsSenseClass AmandsSenseClassComponent;

        // ============================================================
        // Configuration Entries
        // ============================================================

        #region General Settings
        public static ConfigEntry<EEnableSense> EnableSense { get; set; }
        public static ConfigEntry<bool> EnableExfilSense { get; set; }
        public static ConfigEntry<bool> SenseAlwaysOn { get; set; }

        public static ConfigEntry<KeyboardShortcut> SenseKey { get; set; }
        public static ConfigEntry<bool> DoubleClick { get; set; }
        public static ConfigEntry<float> Cooldown { get; set; }

        public static ConfigEntry<float> Duration { get; set; }
        public static ConfigEntry<float> ExfilDuration { get; set; }
        public static ConfigEntry<int> Radius { get; set; }
        public static ConfigEntry<int> DeadPlayerRadius { get; set; }
        public static ConfigEntry<float> Speed { get; set; }
        public static ConfigEntry<float> MaxHeight { get; set; }
        public static ConfigEntry<float> MinHeight { get; set; }

        public static ConfigEntry<bool> ContainerLootcount { get; set; }
        public static ConfigEntry<bool> EnableFlea { get; set; }
        public static ConfigEntry<bool> FleaIncludeAmmo { get; set; }

        public static ConfigEntry<bool> UseBackgroundColor { get; set; }

        public static ConfigEntry<float> Size { get; set; }
        public static ConfigEntry<float> IconSize { get; set; }
        public static ConfigEntry<float> SizeClamp { get; set; }

        public static ConfigEntry<float> VerticalOffset { get; set; }
        public static ConfigEntry<float> DeadBodyVerticalOffset { get; set; }
        public static ConfigEntry<float> TextOffset { get; set; }
        public static ConfigEntry<float> ExfilVerticalOffset { get; set; }

        public static ConfigEntry<bool> ShowEmptyBodies { get; set; }
        public static ConfigEntry<float> EmptyBodyOpacity { get; set; }
        public static ConfigEntry<bool> ShowCooldownFeedback { get; set; }
        public static ConfigEntry<float> DoubleClickWindow { get; set; }

        // Loot Value Filtering
        public static ConfigEntry<int> LootValueThreshold { get; set; }
        public static ConfigEntry<bool> UsePerSlotValue { get; set; }

        // UI Polish Settings
        public static ConfigEntry<float> UIScale { get; set; }
        public static ConfigEntry<float> TextOutlineWidth { get; set; }
        public static ConfigEntry<Color> TextOutlineColor { get; set; }
        public static ConfigEntry<bool> EnableTextShadow { get; set; }
        public static ConfigEntry<float> ShadowOffsetX { get; set; }
        public static ConfigEntry<float> ShadowOffsetY { get; set; }
        public static ConfigEntry<bool> EnableBackgroundPlate { get; set; }
        public static ConfigEntry<float> BackgroundPlateOpacity { get; set; }
        public static ConfigEntry<bool> UseRarityGlow { get; set; }

        public static ConfigEntry<float> IntensitySpeed { get; set; }

        public static ConfigEntry<float> AlwaysOnFrequency { get; set; }

        public static ConfigEntry<float> LightIntensity { get; set; }
        public static ConfigEntry<float> LightRange { get; set; }
        public static ConfigEntry<bool> LightShadows { get; set; }

        public static ConfigEntry<float> ExfilLightIntensity { get; set; }
        public static ConfigEntry<float> ExfilLightRange { get; set; }
        public static ConfigEntry<bool> ExfilLightShadows { get; set; }

        public static ConfigEntry<float> AudioDistance { get; set; }
        public static ConfigEntry<int> AudioRolloff { get; set; }
        public static ConfigEntry<float> AudioVolume { get; set; }
        public static ConfigEntry<float> ContainerAudioVolume { get; set; }
        public static ConfigEntry<float> ActivateSenseVolume { get; set; }
        public static ConfigEntry<bool> SenseRareSound { get; set; }

        public static ConfigEntry<bool> useDof { get; set; }

        public static ConfigEntry<Color> ExfilColor { get; set; }
        public static ConfigEntry<Color> ExfilUnmetColor { get; set; }
        public static ConfigEntry<Color> TextColor { get; set; }
        // Value-based tier colors (Polish.md D&D/ARPG style)
        public static ConfigEntry<Color> JunkColor { get; set; }
        public static ConfigEntry<Color> CommonColor { get; set; }
        public static ConfigEntry<Color> UncommonColor { get; set; }
        public static ConfigEntry<Color> RareItemsColor { get; set; }
        public static ConfigEntry<Color> EpicColor { get; set; }
        public static ConfigEntry<Color> LegendaryColor { get; set; }
        public static ConfigEntry<Color> WishListItemsColor { get; set; }
        public static ConfigEntry<Color> NonFleaItemsColor { get; set; }
        public static ConfigEntry<Color> KappaItemsColor { get; set; }
        public static ConfigEntry<Color> LootableContainerColor { get; set; }
        public static ConfigEntry<Color> ObservedLootItemColor { get; set; }
        public static ConfigEntry<Color> OthersColor { get; set; }
        public static ConfigEntry<Color> BuildingMaterialsColor { get; set; }
        public static ConfigEntry<Color> ElectronicsColor { get; set; }
        public static ConfigEntry<Color> EnergyElementsColor { get; set; }
        public static ConfigEntry<Color> FlammableMaterialsColor { get; set; }
        public static ConfigEntry<Color> HouseholdMaterialsColor { get; set; }
        public static ConfigEntry<Color> MedicalSuppliesColor { get; set; }
        public static ConfigEntry<Color> ToolsColor { get; set; }
        public static ConfigEntry<Color> ValuablesColor { get; set; }
        public static ConfigEntry<Color> BackpacksColor { get; set; }
        public static ConfigEntry<Color> BodyArmorColor { get; set; }
        public static ConfigEntry<Color> EyewearColor { get; set; }
        public static ConfigEntry<Color> FacecoversColor { get; set; }
        public static ConfigEntry<Color> GearComponentsColor { get; set; }
        public static ConfigEntry<Color> HeadgearColor { get; set; }
        public static ConfigEntry<Color> HeadsetsColor { get; set; }
        public static ConfigEntry<Color> SecureContainersColor { get; set; }
        public static ConfigEntry<Color> StorageContainersColor { get; set; }
        public static ConfigEntry<Color> TacticalRigsColor { get; set; }
        public static ConfigEntry<Color> FunctionalModsColor { get; set; }
        public static ConfigEntry<Color> GearModsColor { get; set; }
        public static ConfigEntry<Color> VitalPartsColor { get; set; }
        public static ConfigEntry<Color> AssaultCarbinesColor { get; set; }
        public static ConfigEntry<Color> AssaultRiflesColor { get; set; }
        public static ConfigEntry<Color> BoltActionRiflesColor { get; set; }
        public static ConfigEntry<Color> GrenadeLaunchersColor { get; set; }
        public static ConfigEntry<Color> MachineGunsColor { get; set; }
        public static ConfigEntry<Color> MarksmanRiflesColor { get; set; }
        public static ConfigEntry<Color> PistolsColor { get; set; }
        public static ConfigEntry<Color> SMGsColor { get; set; }
        public static ConfigEntry<Color> ShotgunsColor { get; set; }
        public static ConfigEntry<Color> SpecialWeaponsColor { get; set; }
        public static ConfigEntry<Color> MeleeWeaponsColor { get; set; }
        public static ConfigEntry<Color> ThrowablesColor { get; set; }
        public static ConfigEntry<Color> AmmoPacksColor { get; set; }
        public static ConfigEntry<Color> RoundsColor { get; set; }
        public static ConfigEntry<Color> DrinksColor { get; set; }
        public static ConfigEntry<Color> FoodColor { get; set; }
        public static ConfigEntry<Color> InjectorsColor { get; set; }
        public static ConfigEntry<Color> InjuryTreatmentColor { get; set; }
        public static ConfigEntry<Color> MedkitsColor { get; set; }
        public static ConfigEntry<Color> PillsColor { get; set; }
        public static ConfigEntry<Color> ElectronicKeysColor { get; set; }
        public static ConfigEntry<Color> MechanicalKeysColor { get; set; }
        public static ConfigEntry<Color> InfoItemsColor { get; set; }
        public static ConfigEntry<Color> QuestItemsColor { get; set; }
        public static ConfigEntry<Color> SpecialEquipmentColor { get; set; }
        public static ConfigEntry<Color> MapsColor { get; set; }
        public static ConfigEntry<Color> MoneyColor { get; set; }

        public static ConfigEntry<string> Version { get; set; }
        #endregion

        private static bool RequestDefaultValues = false;

        /// <summary>
        /// Called when the plugin is loaded. Initializes the sense system.
        /// </summary>
        private void Awake()
        {
            Log = Logger;
            // BUILD ID to verify correct DLL is loaded - change this value each build
            const string BUILD_ID = "BUILD-20251229-FIX5-WISHLIST";
            Log.LogWarning($"[AmandsSense] *** {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loading *** BUILD={BUILD_ID}");
            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loading...");

            Hook = new GameObject("AmandsSense");
            AmandsSenseClassComponent = Hook.AddComponent<AmandsSenseClass>();
            AmandsSenseClass.SenseAudioSource = Hook.AddComponent<AudioSource>();
            DontDestroyOnLoad(Hook);

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} initialized");
        }
        private void Start()
        {
            Version = Config.Bind("Versioning", "Version", "0.0.0", new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1, ReadOnly = true, IsAdvanced = true }));

            if (Version.Value == "0.0.0")
            {
                // Using New Config File
                Version.Value = Info.Metadata.Version.ToString();
                RequestDefaultValues = true;
            }
            else if (Version.Value != Info.Metadata.Version.ToString())
            {
                // Using Old Config File
                Version.Value = Info.Metadata.Version.ToString();
                RequestDefaultValues = true;
            }
            else
            {
                // Valid Config File
            }

            EnableSense = Config.Bind("AmandsSense", "EnableSense", EEnableSense.OnText, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 380 }));
            EnableExfilSense = Config.Bind("AmandsSense", "EnableExfilSense", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 370 }));
            SenseAlwaysOn = Config.Bind("AmandsSense", "AlwaysOn", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 360 }));

            SenseKey = Config.Bind("AmandsSense", "SenseKey", new KeyboardShortcut(KeyCode.F), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 350 }));
            DoubleClick = Config.Bind("AmandsSense", "DoubleClick", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 340, IsAdvanced = true }));
            Cooldown = Config.Bind("AmandsSense", "Cooldown", 2f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 330, IsAdvanced = true }));

            Duration = Config.Bind("AmandsSense", "Duration", 10f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 320 }));
            ExfilDuration = Config.Bind("AmandsSense", "Exfil Duration", 30f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 320 }));
            Radius = Config.Bind("AmandsSense", "Radius", 10, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 310 }));
            DeadPlayerRadius = Config.Bind("AmandsSense", "DeadPlayerRadius", 20, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 310 }));
            Speed = Config.Bind("AmandsSense", "Speed", 20f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 300 }));
            MaxHeight = Config.Bind("AmandsSense", "MaxHeight", 3f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 290, IsAdvanced = true }));
            MinHeight = Config.Bind("AmandsSense", "MinHeight", -1f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 280, IsAdvanced = true }));

            ContainerLootcount = Config.Bind("AmandsSense", "ContainerLootcount", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 272, IsAdvanced = true }));
            EnableFlea = Config.Bind("AmandsSense", "Enable Flea", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 270 }));
            FleaIncludeAmmo = Config.Bind("AmandsSense", "Flea Include Ammo", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 260 }));

            UseBackgroundColor = Config.Bind("AmandsSense", "Use Background Color", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 250 }));

            Size = Config.Bind("AmandsSense", "Size", 0.5f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 250 }));
            IconSize = Config.Bind("AmandsSense", "IconSize", 0.1f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 240 }));
            SizeClamp = Config.Bind("AmandsSense", "Size Clamp", 3.0f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 230, IsAdvanced = true }));

            VerticalOffset = Config.Bind("AmandsSense", "Vertical Offset", 0.22f, new ConfigDescription("Height offset for item markers above objects", null, new ConfigurationManagerAttributes { Order = 220, IsAdvanced = true }));
            DeadBodyVerticalOffset = Config.Bind("AmandsSense", "Dead Body Vertical Offset", 0.4f, new ConfigDescription("Height offset for corpse markers above the body center", null, new ConfigurationManagerAttributes { Order = 219 }));
            TextOffset = Config.Bind("AmandsSense", "Text Offset", 0.15f, new ConfigDescription("Horizontal offset for text labels", null, new ConfigurationManagerAttributes { Order = 217, IsAdvanced = true }));
            ExfilVerticalOffset = Config.Bind("AmandsSense", "ExfilVertical Offset", 40f, new ConfigDescription("Height offset for extraction point markers", null, new ConfigurationManagerAttributes { Order = 215, IsAdvanced = true }));

            ShowEmptyBodies = Config.Bind("AmandsSense", "Show Empty Bodies", true, new ConfigDescription("Show markers for bodies with no lootable items", null, new ConfigurationManagerAttributes { Order = 214 }));
            EmptyBodyOpacity = Config.Bind("AmandsSense", "Empty Body Opacity", 0.3f, new ConfigDescription("Opacity multiplier for empty body markers (0.0-1.0)", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 213 }));
            ShowCooldownFeedback = Config.Bind("AmandsSense", "Show Cooldown Feedback", true, new ConfigDescription("Play sound when sense is on cooldown", null, new ConfigurationManagerAttributes { Order = 212 }));
            DoubleClickWindow = Config.Bind("AmandsSense", "Double Click Window", 0.5f, new ConfigDescription("Time window for double-click activation (seconds)", new AcceptableValueRange<float>(0.2f, 1f), new ConfigurationManagerAttributes { Order = 211, IsAdvanced = true }));

            // Loot Value Filtering
            LootValueThreshold = Config.Bind("Loot Filtering", "Loot Value Threshold", 0, new ConfigDescription("Minimum item value to highlight (0 = show all). Items below this value on bodies will be ignored for tier calculation.", new AcceptableValueRange<int>(0, 200000), new ConfigurationManagerAttributes { Order = 210 }));
            UsePerSlotValue = Config.Bind("Loot Filtering", "Use Per-Slot Value", false, new ConfigDescription("Calculate item value per inventory slot (value / slots). Helps compare item value density.", null, new ConfigurationManagerAttributes { Order = 209 }));

            // UI Polish Settings
            UIScale = Config.Bind("UI Polish", "UI Scale", 1.0f, new ConfigDescription("Scale multiplier for UI elements (1.0 = default, 1.25-1.5 for 1440p/4K)", new AcceptableValueRange<float>(0.5f, 2.0f), new ConfigurationManagerAttributes { Order = 209 }));
            TextOutlineWidth = Config.Bind("UI Polish", "Text Outline Width", 0.15f, new ConfigDescription("Width of text outline for visibility (0 = disabled)", new AcceptableValueRange<float>(0f, 0.5f), new ConfigurationManagerAttributes { Order = 208 }));
            TextOutlineColor = Config.Bind("UI Polish", "Text Outline Color", new Color(0.05f, 0.06f, 0.08f, 1.0f), new ConfigDescription("Color of text outline (dark for contrast)", null, new ConfigurationManagerAttributes { Order = 207 }));
            EnableTextShadow = Config.Bind("UI Polish", "Enable Text Shadow", true, new ConfigDescription("Add subtle shadow behind text for depth", null, new ConfigurationManagerAttributes { Order = 206 }));
            ShadowOffsetX = Config.Bind("UI Polish", "Shadow Offset X", 0.0f, new ConfigDescription("Horizontal shadow offset", new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { Order = 205, IsAdvanced = true }));
            ShadowOffsetY = Config.Bind("UI Polish", "Shadow Offset Y", -0.5f, new ConfigDescription("Vertical shadow offset (negative = below)", new AcceptableValueRange<float>(-1f, 1f), new ConfigurationManagerAttributes { Order = 204, IsAdvanced = true }));
            EnableBackgroundPlate = Config.Bind("UI Polish", "Enable Background Plate", true, new ConfigDescription("Add semi-transparent background behind labels for readability on bright scenes", null, new ConfigurationManagerAttributes { Order = 203 }));
            BackgroundPlateOpacity = Config.Bind("UI Polish", "Background Plate Opacity", 0.4f, new ConfigDescription("Opacity of background plate (0.0-1.0)", new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = 202 }));
            UseRarityGlow = Config.Bind("UI Polish", "Use Rarity Glow", true, new ConfigDescription("Apply glow effect to rare/special item markers", null, new ConfigurationManagerAttributes { Order = 201 }));

            IntensitySpeed = Config.Bind("AmandsSense", "Intensity Speed", 2f, new ConfigDescription("Speed of fade in/out animation", null, new ConfigurationManagerAttributes { Order = 210, IsAdvanced = true }));

            AlwaysOnFrequency = Config.Bind("AmandsSense", "AlwaysOn Frequency", 2f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 200, IsAdvanced = true }));

            LightIntensity = Config.Bind("AmandsSense", "LightIntensity", 0.6f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 190 }));
            LightRange = Config.Bind("AmandsSense", "LightRange", 2.5f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 180 }));
            LightShadows = Config.Bind("AmandsSense", "LightShadows", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 170, IsAdvanced = true }));

            ExfilLightIntensity = Config.Bind("AmandsSense", "Exfil LightIntensity", 1.0f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 160 }));
            ExfilLightRange = Config.Bind("AmandsSense", "Exfil LightRange", 50f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 150 }));
            ExfilLightShadows = Config.Bind("AmandsSense", "Exfil LightShadows", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 140 }));

            AudioDistance = Config.Bind("AmandsSense", "AudioDistance", 99f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 130, IsAdvanced = true }));
            AudioRolloff = Config.Bind("AmandsSense", "AudioRolloff", 100, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 120, IsAdvanced = true }));
            AudioVolume = Config.Bind("AmandsSense", "AudioVolume", 0.5f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 112 }));
            ContainerAudioVolume = Config.Bind("AmandsSense", "ContainerAudioVolume", 0.5f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 110 }));
            ActivateSenseVolume = Config.Bind("AmandsSense", "ActivateSenseVolume", 0.5f, new ConfigDescription("requires a custom sound .wav file named Sense.wav", null, new ConfigurationManagerAttributes { Order = 108 }));
            SenseRareSound = Config.Bind("AmandsSense", "SenseRareSound", false, new ConfigDescription("requires a custom sound .wav file named SenseRare.wav", null, new ConfigurationManagerAttributes { Order = 106 }));

            useDof = Config.Bind("AmandsSense Effects", "useDof", true, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100 }));

            ExfilColor = Config.Bind("Colors", "ExfilColor", new Color(0.01f, 1.0f, 0.01f, 1.0f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 570 }));
            ExfilUnmetColor = Config.Bind("Colors", "ExfilUnmetColor", new Color(1.0f, 0.01f, 0.01f, 1.0f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 560 }));

            TextColor = Config.Bind("Colors", "TextColor", new Color(0.84f, 0.88f, 0.95f, 1.0f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 550 }));

            // D&D/ARPG Value-Based Tier Colors (Polish.md palette)
            JunkColor = Config.Bind("Colors", "JunkColor", new Color(0.498f, 0.498f, 0.498f, 0.9f), new ConfigDescription("Gray for junk items <5k (#7f7f7f)", null, new ConfigurationManagerAttributes { Order = 548 }));
            CommonColor = Config.Bind("Colors", "CommonColor", new Color(0.91f, 0.91f, 0.91f, 0.9f), new ConfigDescription("White for common items 5-20k (#e8e8e8)", null, new ConfigurationManagerAttributes { Order = 546 }));
            UncommonColor = Config.Bind("Colors", "UncommonColor", new Color(0.271f, 0.78f, 0.412f, 0.9f), new ConfigDescription("Green for uncommon items 20-50k (#45c769)", null, new ConfigurationManagerAttributes { Order = 544 }));
            RareItemsColor = Config.Bind("Colors", "RareItemsColor", new Color(0.227f, 0.627f, 1.0f, 0.9f), new ConfigDescription("Blue for rare items 50-150k (#3aa0ff)", null, new ConfigurationManagerAttributes { Order = 542 }));
            EpicColor = Config.Bind("Colors", "EpicColor", new Color(0.706f, 0.294f, 1.0f, 0.9f), new ConfigDescription("Purple for epic items 150-500k (#b44bff)", null, new ConfigurationManagerAttributes { Order = 540 }));
            LegendaryColor = Config.Bind("Colors", "LegendaryColor", new Color(1.0f, 0.608f, 0.184f, 0.9f), new ConfigDescription("Gold for legendary items >500k or bosses (#ff9b2f)", null, new ConfigurationManagerAttributes { Order = 538 }));
            WishListItemsColor = Config.Bind("Colors", "WishListItemsColor", new Color(1.0f, 0.4f, 0.769f, 0.9f), new ConfigDescription("Pink/magenta for wishlist items (#ff66c4)", null, new ConfigurationManagerAttributes { Order = 530 }));
            NonFleaItemsColor = Config.Bind("Colors", "NonFleaItemsColor", new Color(0.706f, 0.294f, 1.0f, 0.9f), new ConfigDescription("Epic purple for non-flea items (#b44bff)", null, new ConfigurationManagerAttributes { Order = 520 }));
            KappaItemsColor = Config.Bind("Colors", "KappaItemsColor", new Color(0.706f, 0.294f, 1.0f, 0.9f), new ConfigDescription("Epic purple for Kappa items (#b44bff)", null, new ConfigurationManagerAttributes { Order = 510 }));

            LootableContainerColor = Config.Bind("Colors", "LootableContainerColor", new Color(0.36f, 0.18f, 1.0f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 500 }));
            ObservedLootItemColor = Config.Bind("Colors", "ObservedLootItemColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 490 }));

            OthersColor = Config.Bind("Colors", "OthersColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 480 }));
            BuildingMaterialsColor = Config.Bind("Colors", "BuildingMaterialsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 470 }));
            ElectronicsColor = Config.Bind("Colors", "ElectronicsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 460 }));
            EnergyElementsColor = Config.Bind("Colors", "EnergyElementsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 450 }));
            FlammableMaterialsColor = Config.Bind("Colors", "FlammableMaterialsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 440 }));
            HouseholdMaterialsColor = Config.Bind("Colors", "HouseholdMaterialsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 430 }));
            MedicalSuppliesColor = Config.Bind("Colors", "MedicalSuppliesColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 420 }));
            ToolsColor = Config.Bind("Colors", "ToolsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 410 }));
            ValuablesColor = Config.Bind("Colors", "ValuablesColor", new Color(0.36f, 0.18f, 1.0f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 400 }));

            BackpacksColor = Config.Bind("Colors", "BackpacksColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 390 }));
            BodyArmorColor = Config.Bind("Colors", "BodyArmorColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 380 }));
            EyewearColor = Config.Bind("Colors", "EyewearColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 370 }));
            FacecoversColor = Config.Bind("Colors", "FacecoversColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 360 }));
            GearComponentsColor = Config.Bind("Colors", "GearComponentsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 350 }));
            HeadgearColor = Config.Bind("Colors", "HeadgearColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 340 }));
            HeadsetsColor = Config.Bind("Colors", "HeadsetsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 330 }));
            SecureContainersColor = Config.Bind("Colors", "SecureContainersColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 320 }));
            StorageContainersColor = Config.Bind("Colors", "StorageContainersColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 310 }));
            TacticalRigsColor = Config.Bind("Colors", "TacticalRigsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 300 }));

            FunctionalModsColor = Config.Bind("Colors", "FunctionalModsColor", new Color(0.1f, 0.35f, 0.65f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 290 }));
            GearModsColor = Config.Bind("Colors", "GearModsColor", new Color(0.15f, 0.5f, 0.1f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 280 }));
            VitalPartsColor = Config.Bind("Colors", "VitalPartsColor", new Color(0.7f, 0.2f, 0.1f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 270 }));

            AssaultCarbinesColor = Config.Bind("Colors", "AssaultCarbinesColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 260 }));
            AssaultRiflesColor = Config.Bind("Colors", "AssaultRiflesColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 250 }));
            BoltActionRiflesColor = Config.Bind("Colors", "BoltActionRiflesColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 240 }));
            GrenadeLaunchersColor = Config.Bind("Colors", "GrenadeLaunchersColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 230 }));
            MachineGunsColor = Config.Bind("Colors", "MachineGunsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 220 }));
            MarksmanRiflesColor = Config.Bind("Colors", "MarksmanRiflesColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 210 }));
            MeleeWeaponsColor = Config.Bind("Colors", "MeleeWeaponsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 200 }));
            PistolsColor = Config.Bind("Colors", "PistolsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 190 }));
            SMGsColor = Config.Bind("Colors", "SMGsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 180 }));
            ShotgunsColor = Config.Bind("Colors", "ShotgunsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 170 }));
            SpecialWeaponsColor = Config.Bind("Colors", "SpecialWeaponsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 160 }));
            ThrowablesColor = Config.Bind("Colors", "ThrowablesColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 150 }));

            AmmoPacksColor = Config.Bind("Colors", "AmmoPacksColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 140 }));
            RoundsColor = Config.Bind("Colors", "RoundsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 130 }));
            DrinksColor = Config.Bind("Colors", "DrinksColor", new Color(0.13f, 0.66f, 1.0f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 120 }));
            FoodColor = Config.Bind("Colors", "FoodColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 110 }));
            InjectorsColor = Config.Bind("Colors", "InjectorsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 100 }));
            InjuryTreatmentColor = Config.Bind("Colors", "InjuryTreatmentColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 90 }));
            MedkitsColor = Config.Bind("Colors", "MedkitsColor", new Color(0.3f, 1.0f, 0.13f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 80 }));
            PillsColor = Config.Bind("Colors", "PillsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 70 }));

            ElectronicKeysColor = Config.Bind("Colors", "ElectronicKeysColor", new Color(1.0f, 0.01f, 0.01f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 60 }));
            MechanicalKeysColor = Config.Bind("Colors", "MechanicalKeysColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 50 }));

            InfoItemsColor = Config.Bind("Colors", "InfoItemsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 40 }));
            QuestItemsColor = Config.Bind("Colors", "QuestItemsColor", new Color(1.0f, 1.0f, 0.01f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 38 }));
            SpecialEquipmentColor = Config.Bind("Colors", "SpecialEquipmentColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 30 }));
            MapsColor = Config.Bind("Colors", "MapsColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 20 }));
            MoneyColor = Config.Bind("Colors", "MoneyColor", new Color(0.84f, 0.88f, 0.95f, 0.8f), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 10 }));

            if (RequestDefaultValues) DefaultValues();

            new AmandsPlayerPatch().Enable();
            new AmandsKillPatch().Enable();
            new AmandsSenseExfiltrationPatch().Enable();
            new AmandsSensePrismEffectsPatch().Enable();

            // Re-enable the component BEFORE AmandsSenseHelper.Init() in case it was disabled during chainloader
            Log.LogInfo($"[AmandsSense] Checking component state: Hook={Hook != null}, Component={AmandsSenseClassComponent != null}");

            if (Hook == null)
            {
                Log.LogWarning("[AmandsSense] Hook is null! Recreating...");
                Hook = new GameObject("AmandsSense");
                AmandsSenseClassComponent = Hook.AddComponent<AmandsSenseClass>();
                AmandsSenseClass.SenseAudioSource = Hook.AddComponent<AudioSource>();
                DontDestroyOnLoad(Hook);
            }

            Hook.SetActive(true);
            if (AmandsSenseClassComponent != null)
            {
                AmandsSenseClassComponent.enabled = true;
                Log.LogInfo($"[AmandsSense] Component re-enabled. enabled={AmandsSenseClassComponent.enabled}, gameObject.activeSelf={Hook.activeSelf}");
            }
            else
            {
                Log.LogError("[AmandsSense] AmandsSenseClassComponent is null even after Hook exists!");
            }

            AmandsSenseHelper.Init();
        }
        private void DefaultValues()
        {
            Size.Value = (float)Size.DefaultValue;
            IconSize.Value = (float)IconSize.DefaultValue;
            SizeClamp.Value = (float)SizeClamp.DefaultValue;
            VerticalOffset.Value = (float)VerticalOffset.DefaultValue;
            TextOffset.Value = (float)TextOffset.DefaultValue;
            ExfilVerticalOffset.Value = (float)ExfilVerticalOffset.DefaultValue;
        }
    }
    /// <summary>
    /// Harmony patch for Player.Init - initializes the sense system when the player spawns.
    /// </summary>
    internal class AmandsPlayerPatch : ModulePatch
    {
        private static readonly FieldInfo _inventoryControllerField =
            AccessTools.Field(typeof(Player), "_inventoryController");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), nameof(Player.Init));
        }

        [PatchPostfix]
        private static void PatchPostFix(Player __instance)
        {
            if (__instance == null)
            {
                AmandsSensePlugin.Log.LogWarning("[AmandsSense] Player.Init patch: __instance is null");
                return;
            }

            // Log all players for debugging
            AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Player.Init fired: {__instance.Profile?.Nickname ?? "unknown"}, IsYourPlayer={__instance.IsYourPlayer}");

            if (!__instance.IsYourPlayer)
                return;

            AmandsSenseClass.Player = __instance;
            AmandsSenseClass.inventoryControllerClass =
                _inventoryControllerField.GetValue(__instance) as PlayerInventoryController;
            AmandsSenseClass.Clear();
            AmandsSenseClass.scene = SceneManager.GetActiveScene().name;

            // Load sprites if not already loaded (Start() may not have run yet)
            bool spritesLoaded = AmandsSenseClass.LoadedSprites.Count > 0;
            AmandsSenseClass.ReloadFiles(__instance, onlySounds: spritesLoaded);

            AmandsSensePlugin.Log.LogInfo($"[AmandsSense] Player set: {__instance.Profile?.Nickname}, scene={AmandsSenseClass.scene}, sprites={AmandsSenseClass.LoadedSprites.Count}");
        }
    }
    /// <summary>
    /// Harmony patch for PrismEffects.OnEnable - configures DOF effect for sense activation.
    /// </summary>
    internal class AmandsSensePrismEffectsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(PrismEffects), nameof(PrismEffects.OnEnable));
        }

        [PatchPostfix]
        private static void PatchPostFix(PrismEffects __instance)
        {
            if (__instance.gameObject.name != "FPS Camera")
                return;

            AmandsSenseClass.prismEffects = __instance;
            __instance.debugDofPass = false;
            __instance.dofForceEnableMedian = false;
            __instance.dofBokehFactor = 157f;
            __instance.dofFocusDistance = 2f;
            __instance.dofNearFocusDistance = 100f;
            __instance.dofRadius = 0f;
        }
    }

    /// <summary>
    /// Harmony patch for Player.OnBeenKilledByAggressor - tracks dead players for corpse highlighting.
    /// Method signature: OnBeenKilledByAggressor(IPlayer aggressor, DamageInfoStruct damageInfo, EBodyPart bodyPart, EDamageType lethalDamageType)
    /// </summary>
    internal class AmandsKillPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), nameof(Player.OnBeenKilledByAggressor));
        }

        [PatchPostfix]
        private static void PatchPostFix(Player __instance, IPlayer aggressor, DamageInfoStruct damageInfo, EBodyPart bodyPart, EDamageType lethalDamageType)
        {
            try
            {
                if (__instance == null)
                    return;

                // Cast IPlayer to Player (bots and human players are always Player instances)
                Player aggressorPlayer = aggressor as Player;

                AmandsSenseClass.DeadPlayers.Add(new SenseDeadPlayerStruct(__instance, aggressorPlayer));
            }
            catch (System.Exception ex)
            {
                AmandsSensePlugin.Log.LogError($"[AmandsSense] Kill patch error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Harmony patch for ExfiltrationPoint.Awake - registers extraction points for highlighting.
    /// </summary>
    internal class AmandsSenseExfiltrationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ExfiltrationPoint), "Awake");
        }

        [PatchPostfix]
        private static void PatchPostFix(ExfiltrationPoint __instance)
        {
            if (__instance == null)
                return;

            var exfilGameObject = new GameObject("SenseExfil");
            var senseExfil = exfilGameObject.AddComponent<AmandsSenseExfil>();
            senseExfil.SetSense(__instance);
            senseExfil.Construct();
            senseExfil.ShowSense();
            AmandsSenseClass.SenseExfils.Add(senseExfil);
        }
    }
}
