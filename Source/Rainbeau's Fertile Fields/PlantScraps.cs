using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;

namespace RFF_Code
{
    public class GrowZoneManager : MapComponent
    {
        //public Dictionary<Zone_Growing, bool> shouldReturnToSoil = new Dictionary<Zone_Growing, bool>();
        //public Dictionary<Zone_Growing, bool> shouldDesignateReplacements = new Dictionary<Zone_Growing, bool>();
        public List<Zone_Growing> noReturnToSoil = new List<Zone_Growing>();
        public List<Zone_Growing> noDesignateReplacements = new List<Zone_Growing>();

        public const bool defaultValue = true;

        public GrowZoneManager(Map map) : base(map) { }

        public bool ShouldNotReturnToSoil(Zone_Growing zone) => noReturnToSoil.Contains(zone);

        public bool ShouldReturnToSoil(Zone_Growing zone) => !ShouldNotReturnToSoil(zone);

        public bool ShouldNotDesignateReplacements(Zone_Growing zone) => noDesignateReplacements.Contains(zone);

        public bool ShouldDesignateReplacements(Zone_Growing zone) => !ShouldNotDesignateReplacements(zone);

        public void ToggleReturnToSoil(Zone_Growing zone)
        {
            if(ShouldReturnToSoil(zone))
            {
                noReturnToSoil.Add(zone);
            }
            else
            {
                noReturnToSoil.Remove(zone);
            }
        }

        public void SetReturnToSoil(Zone_Growing zone, bool value)
        {
            if (noReturnToSoil == null) noReturnToSoil = new List<Zone_Growing>();
            if(value == ShouldNotReturnToSoil(zone))
            {
                ToggleReturnToSoil(zone);
            }
        }
        
        public void ToggleDesignateReplacements(Zone_Growing zone)
        {
            if (ShouldDesignateReplacements(zone))
            {
                noDesignateReplacements.Add(zone);
            }
            else
            {
                noDesignateReplacements.Remove(zone);
            }
        }

        public void SetDesignateReplacements(Zone_Growing zone, bool value)
        {
            if (noDesignateReplacements == null) noDesignateReplacements = new List<Zone_Growing>();
            if (value == ShouldNotDesignateReplacements(zone))
            {
                ToggleDesignateReplacements(zone);
            }
        }

        public void RemoveReferences(Zone_Growing zone)
        {
            if (noReturnToSoil.Contains(zone)) noReturnToSoil.Remove(zone);
            if (noDesignateReplacements.Contains(zone)) noDesignateReplacements.Remove(zone);
        }

        public override void ExposeData()
        {
            List<Zone_Growing> oldZones = (from Zone_Growing zone in noReturnToSoil.Concat(noDesignateReplacements)
                                           where zone?.zoneManager?.AllZones?.Contains(zone) == false
                                           select zone).ToList();

            foreach(Zone_Growing zone in oldZones)
            {
                RemoveReferences(zone);
            }

            Scribe_Collections.Look(ref noReturnToSoil, "noRTS", LookMode.Reference);
            Scribe_Collections.Look(ref noDesignateReplacements, "noDR", LookMode.Reference);

            if (noReturnToSoil == null) noReturnToSoil = new List<Zone_Growing>();
            if (noDesignateReplacements == null) noDesignateReplacements = new List<Zone_Growing>();
        }
    }

    [HarmonyPatch(typeof(ZoneManager), "RegisterZone")]
    public static class RegisterZonePatch
    {
        public static void Postfix(ZoneManager __instance, Zone newZone)
        {
            if (newZone is Zone_Growing growZone)
            {
                __instance.map.GetComponent<GrowZoneManager>()?.SetReturnToSoil(growZone, Controller.Settings.newGrowZonesRTS);
                __instance.map.GetComponent<GrowZoneManager>()?.SetDesignateReplacements(growZone, Controller.Settings.newGrowZonesDR);
            }
        }
    }

    [HarmonyPatch(typeof(ZoneManager), "DeregisterZone")]
    public static class DeregisterZonePatch
    {
        public static void Postfix(ZoneManager __instance, Zone oldZone)
        {
            if (oldZone is Zone_Growing growZone)
            {
                __instance.map.GetComponent<GrowZoneManager>()?.RemoveReferences(growZone);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class GrowingZoneIcon
    {
        public static readonly Texture2D ReturnToSoil = ContentFinder<Texture2D>.Get("Stuff/Scraps");
        public static readonly Texture2D DesignateReplacements = ContentFinder<Texture2D>.Get("Fertilizer/Fertilizer_a");
    }

    [HarmonyPatch(typeof(Zone_Growing), "GetGizmos")]
    public static class GrowingZoneGizmoPatch
    {
        public static void Postfix(ref IEnumerable<Gizmo> __result, Zone_Growing __instance)
        {
            //Log.Message("GetGizmos for growing zone: has " + __result.Count() + " gizmos.");

            List<Gizmo> list = __result.ToList();

            GrowZoneManager gzm = __instance.Map.GetComponent<GrowZoneManager>();

            list.Add(new Command_Toggle
            {
                defaultLabel = "RFF.ReturnToSoil".Translate(),
                defaultDesc = "RFF.ReturnToSoilDesc".Translate(),
                icon = GrowingZoneIcon.ReturnToSoil,
                isActive = (() => gzm.ShouldReturnToSoil(__instance)),
                toggleAction = delegate { gzm.ToggleReturnToSoil(__instance); }
            });
            list.Add(new Command_Toggle
            {
                defaultLabel = "RFF.ReplaceDesignators".Translate(),
                defaultDesc = "RFF.ReplaceDesignatorsDesc".Translate(),
                icon = GrowingZoneIcon.DesignateReplacements,
                isActive = (() => gzm.ShouldDesignateReplacements(__instance)),
                toggleAction = delegate { gzm.ToggleDesignateReplacements(__instance); }
            });

            //Log.Message("List now has " + __result.Count() + " gizmos.");

            __result = list;

            //Log.Message("Growing zone now has " + __result.Count() + " gizmos.");
        }
    }

    [HarmonyPatch(typeof(Plant), "PlantCollected")]
    public static class PlantHarvestPatch
    {
        public static void Prefix(Plant __instance)
        {
            Map map = __instance.Map;
            IntVec3 cell = __instance.Position;
            GrowZoneManager gzm = map.GetComponent<GrowZoneManager>();

            //if (defName == "Plant_Grass" || defName == "Plant_TallGrass" || defName == "Plant_Dandelion" || defName == "Plant_Astragalus" || defName == "Plant_Moss" || defName == "Plant_Daylily")
            int qty = GenMath.RoundRandom(Math.Max(__instance.def.plant.harvestYield, 2) * (0.25f + 0.75f * __instance.Growth) * Controller.Settings.plantScrapsPercent / 100);

            if (map.zoneManager.ZoneAt(cell) is Zone_Growing growingZone && (Controller.Settings.nonYieldPlantsCanDeplete || __instance.def.plant.harvestYield > 0) && __instance.def.plant.HarvestDestroys)
            {
                TerrainDef terrain = cell.GetTerrain(map);
                DepletableSoil dep = terrain.GetModExtension<DepletableSoil>();

                float depOdds = -1f;

                if (dep != null)
                {
                    depOdds = (__instance.def.plant.growDays * __instance.Growth / DepleteHelper.MTBDaysFor(terrain)) * (Controller.Settings.depleteChanceMult / 100);

                    if (gzm.ShouldReturnToSoil(growingZone) && DepleteHelper.RTSFactorFor(terrain) > 0f)
                    {
                        depOdds *= (1 - DepleteHelper.RTSFactorFor(terrain));
                        qty = 0;
                    }
                }
                else
                {
                    qty = GenMath.RoundRandom(qty / 2f);
                }

                if(qty > 0)
                {
                    Thing scraps = ThingMaker.MakeThing(FFDefOf.PlantScraps);
                    scraps.stackCount = qty;
                    GenPlace.TryPlaceThing(scraps, cell, map, ThingPlaceMode.Near);
                    TryForbidIfNonColony(scraps);
                }

                if(depOdds > 0 && Rand.Value < depOdds)
                {
                    map.terrainGrid.SetTerrain(cell, dep.terrain);
                    if (gzm.ShouldDesignateReplacements(growingZone))
                    {
                        GenConstruct.PlaceBlueprintForBuild_NewTemp(dep.replanterDef, cell, map, Rot4.North, Faction.OfPlayer, null);
                    }
                }
            }
            else
            {
                // we're not in a growing zone. Leave partial plant scraps.

                //Log.Message("leave plant scraps");
                qty = GenMath.RoundRandom((float)qty / 2);

                if (qty > 0)
                {
                    Thing scraps = ThingMaker.MakeThing(FFDefOf.PlantScraps);
                    scraps.stackCount = qty;
                    GenPlace.TryPlaceThing(scraps, __instance.Position, __instance.Map, ThingPlaceMode.Near);
                    TryForbidIfNonColony(scraps);
                }
            }
        }

        public const float scrapProximity = 4.0f;

        public static void TryForbidIfNonColony(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map == null || thing.Map?.areaManager?.Home == null) return;

            if (thing.Map.areaManager.Home[thing.Position])
            {
                //never forbid scraps in the home area
                return;
            }
            if (Controller.Settings.smartScrapForbidding)
            {
                //if smart scraps is turned on, check all colonists for proximity and return if any are within range
                foreach (Pawn p in thing.Map.mapPawns.FreeColonistsSpawned)
                {
                    if(p.Position.InHorDistOf(thing.Position, scrapProximity))
                    {
                        return;
                    }
                }
            }
            
            ForbidUtility.SetForbidden(thing, true, false);
        }
    }

    public class DepletableSoil: DefModExtension
    {
        public TerrainDef terrain;

        public ThingDef replanterDef;

        public float mtbDays;

        public float returnToSoilFactor = 1f;
    }
}
