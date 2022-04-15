using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace RFF_Code
{
    /*[StaticConstructorOnStartup]
    internal static class RFF_Initializer
    {
        static RFF_Initializer()
        {
            Harmony harmony = new Harmony("net.rainbeau.rimworld.mod.fertilefields");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }*/

    // ==================================== //
    // ========== Config Options ========== //
    // ==================================== //

    public class Controller : Mod
    {
        public static Settings Settings;
        public override string SettingsCategory() { return "RFF.FertileFields".Translate(); }
        public override void DoSettingsWindowContents(Rect canvas) { Settings.DoWindowContents(canvas); }
        public Controller(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Settings>();
        }

        static Controller()
        {
            //Log.Message("Initializing harmony patches for RFF");
            Harmony harmony = new Harmony("net.rainbeau.rimworld.mod.fertilefields");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    public class DepleteRecordEntry : IExposable
    {
        public string defName;

        public TerrainDef terrain = null;

        public float mtbDays = 999f;

        public float returnToSoilFactor = 1f;

        public DepleteRecordEntry()
        {

        }

        public DepleteRecordEntry(TerrainDef terrain, float mtbDays, float returnToSoilFactor)
        {
            this.terrain = terrain;
            defName = terrain.defName;
            this.mtbDays = mtbDays;
            this.returnToSoilFactor = returnToSoilFactor;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref mtbDays, "mtbDays");
            Scribe_Values.Look(ref returnToSoilFactor, "rtsFactor");
            terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(defName);
        }
    }

    [StaticConstructorOnStartup]
    public static class DepleteHelper
    {
        static DepleteHelper()
        {
            if (Controller.Settings.depleteRecords == null) Controller.Settings.depleteRecords = new List<DepleteRecordEntry>();

            foreach (TerrainDef terrain in DefDatabase<TerrainDef>.AllDefsListForReading)
            {
                if (terrain.HasModExtension<DepletableSoil>())
                {
                    NewEntry(terrain);
                }
            }
        }

        public static bool HasEntryFor(TerrainDef terrain)
        {
            foreach (DepleteRecordEntry entry in Controller.Settings.depleteRecords)
            {
                if (entry.defName == terrain.defName)
                    return true;
            }
            return false;

        }

        public static DepleteRecordEntry EntryFor(TerrainDef terrain)
        {
            foreach(DepleteRecordEntry entry in Controller.Settings.depleteRecords)
            {
                if (entry.defName == terrain.defName)
                    return entry;
            }
            return null;
        }

        public static void NewEntry(TerrainDef terrain)
        {
            DepletableSoil dep = terrain.GetModExtension<DepletableSoil>();

            if (dep == null || HasEntryFor(terrain)) return;

            Controller.Settings.depleteRecords.Add(new DepleteRecordEntry(terrain, dep.mtbDays, dep.returnToSoilFactor));
        }

        public static float MTBDaysFor(TerrainDef terrain)
        {
            if(HasEntryFor(terrain))
            {
                return EntryFor(terrain).mtbDays;
            }
            return terrain.GetModExtension<DepletableSoil>()?.mtbDays ?? 999f;
        }

        public static float RTSFactorFor(TerrainDef terrain)
        {
            if (HasEntryFor(terrain))
            {
                return EntryFor(terrain).returnToSoilFactor;
            }
            return terrain.GetModExtension<DepletableSoil>()?.returnToSoilFactor ?? 1f;
        }
    }

    public class Settings : ModSettings
    {
        public float depleteChanceMult = 100.5f;
        public float plantScrapsPercent = 100.5f;
        public bool autoRecompost = true;
        public bool newGrowZonesRTS = false;
        public bool newGrowZonesDR = true;
        public bool nonYieldPlantsCanDeplete = false;
        public bool playHardMode = false;
        public bool smartScrapForbidding = true;

        public List<DepleteRecordEntry> depleteRecords;// = new List<DepleteRecordEntry>();

        public const int heightWithoutSpecificDepleteList = 16 * 24 + 36;
        public const int heightPerSpecificDepleteListEntry = 4 * 24 + 12;

        private Vector2 pos = new Vector2(0f, 0f);
        private Rect scrollRect;

        public void DoWindowContents(Rect canvas)
        {
            if (depleteRecords == null) depleteRecords = new List<DepleteRecordEntry>();
            
            Listing_Standard list = new Listing_Standard();

            list.ColumnWidth = canvas.width - 20;
            list.Begin(canvas);
            scrollRect.x = canvas.x; scrollRect.y = canvas.y; scrollRect.width = canvas.width - 16; scrollRect.height = heightWithoutSpecificDepleteList + heightPerSpecificDepleteListEntry * depleteRecords.Count();
            Widgets.BeginScrollView(new Rect(canvas.x, canvas.y + 20, canvas.width, 500f), ref pos, scrollRect);
            list.Gap(36);
            //Text.Font = GameFont.Medium;
            //list.Label("RFF.FarmingOptions".Translate());
            Text.Font = GameFont.Small;
            list.CheckboxLabeled("RFF.NewGrowZonesRTS".Translate(), ref newGrowZonesRTS, "RFF.NewGrowZonesRTSTip".Translate());
            list.Gap(24);
            list.CheckboxLabeled("RFF.NewGrowZonesDR".Translate(), ref newGrowZonesDR, "RFF.NewGrowZonesDRTip".Translate());
            list.Gap(24);
            list.CheckboxLabeled("RFF.AutoRecompost".Translate(), ref autoRecompost, "RFF.AutoRecompostTip".Translate());
            list.Gap(24);
            list.CheckboxLabeled("RFF.NonYieldPlants".Translate(), ref nonYieldPlantsCanDeplete, "RFF.NonYieldPlantsTip".Translate());
            list.Gap(24);
            list.CheckboxLabeled("RFF.SmartScraps".Translate(), ref smartScrapForbidding, "RFF.SmartScrapsTip".Translate());
            list.Gap(24);
            list.CheckboxLabeled("RFF.PlayHardMode".Translate(), ref playHardMode, "RFF.PlayHardModeTip".Translate());
            list.Gap(24);
            list.Label("RFF.PlantScraps".Translate() + "  " + (int)plantScrapsPercent + "%", tooltip: "RFF.PlantScrapsTip".Translate());
            plantScrapsPercent = list.Slider(plantScrapsPercent, 0f, 200.99f);
            list.Label("RFF.DepleteChance".Translate() + "  " + (int)depleteChanceMult + "%", tooltip: "RFF.DepleteChanceTip".Translate());
            depleteChanceMult = list.Slider(depleteChanceMult, 0f, 200.99f);
            list.Gap();
            Text.Font = GameFont.Medium;
            list.Label("RFF.SpecificDepletes".Translate());
            Text.Font = GameFont.Small;
            foreach(DepleteRecordEntry entry in depleteRecords)
            {
                if (entry.terrain == null) entry.terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(entry.defName);
                if (entry.terrain == null) { scrollRect.height -= heightPerSpecificDepleteListEntry; continue; }
                list.Label("RFF.SpecificDepleteEntryMTB".Translate().Replace("{0}", entry.terrain.LabelCap).Replace("{1}", entry.terrain.GetModExtension<DepletableSoil>()?.mtbDays.ToStringByStyle(ToStringStyle.FloatMaxOne)) + " " + entry.mtbDays.ToStringByStyle(ToStringStyle.FloatMaxOne),
                    tooltip: "RFF.SpecificDepleteEntryMTBTip".Translate());
                entry.mtbDays = list.Slider(entry.mtbDays, 0.1f, 30f);
                list.Label("RFF.SpecificDepleteEntryRTS".Translate().Replace("{0}", entry.terrain.LabelCap).Replace("{1}", entry.terrain.GetModExtension<DepletableSoil>()?.returnToSoilFactor.ToStringPercent()) + " " + entry.returnToSoilFactor.ToStringPercent(),
                    tooltip: "RFF.SpecificDepleteEntryRTSTip".Translate());
                entry.returnToSoilFactor = list.Slider(entry.returnToSoilFactor, 0f, 1.0f);
                list.Gap();
            }
            
            Widgets.EndScrollView();
            list.End();
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoRecompost, "autoRecompost", true);
            Scribe_Values.Look(ref newGrowZonesRTS, "newGrowZoneRTS", false);
            Scribe_Values.Look(ref newGrowZonesDR, "newGrowZonesDR", true);
            Scribe_Values.Look(ref playHardMode, "playHardMode", false);
            Scribe_Values.Look(ref nonYieldPlantsCanDeplete, "nonYieldPlants", false);
            Scribe_Values.Look(ref smartScrapForbidding, "smartScraps", true);
            Scribe_Values.Look(ref plantScrapsPercent, "plantScrapsPercent", 100.5f);
            Scribe_Values.Look(ref depleteChanceMult, "depleteChance", 100.5f);
            Scribe_Collections.Look(ref depleteRecords, "depleteRecords", LookMode.Deep);
        }
    }
}
