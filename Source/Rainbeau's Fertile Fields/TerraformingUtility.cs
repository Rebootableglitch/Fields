using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using Verse.AI;
using Verse.AI.Group;

namespace RFF_Code
{
    public static class StoneHelper
    {
        public static Dictionary<TerrainDef, TerrainDef> roughStoneTypeFor = new Dictionary<TerrainDef, TerrainDef>();

        public static TerrainDef mostRecentlyAssignedTerrainDef = null;

        public static TerrainDef mostRecentlyEncounteredSmoothedDef = null;
    }

    [HarmonyPatch(typeof(TerrainDefGenerator_Stone), "ImpliedTerrainDefs")]
    public static class StoneDefGeneratorPatch
    {
        public static void Postfix(ref IEnumerable<TerrainDef> __result)
        {
            List<TerrainDef> result = __result.ToList();
            foreach(TerrainDef terrain in result)
            {
                //Log.Message("patching implied stone terrain def: " + terrain.defName);
                
                terrain.affordances.Add(FFDefOf.RoughStone);
                if(!terrain.affordances.Contains(TerrainAffordanceDefOf.SmoothableStone))
                {
                    terrain.affordances.Add(FFDefOf.SmoothStone);
                }

                if(terrain.smoothedTerrain != null && terrain.smoothedTerrain != StoneHelper.mostRecentlyEncounteredSmoothedDef)
                {
                    StoneHelper.mostRecentlyAssignedTerrainDef = terrain;
                    StoneHelper.mostRecentlyEncounteredSmoothedDef = terrain.smoothedTerrain;
                }
                if (!StoneHelper.roughStoneTypeFor.ContainsKey(terrain) && StoneHelper.mostRecentlyAssignedTerrainDef != null)
                {
                    StoneHelper.roughStoneTypeFor.Add(terrain, StoneHelper.mostRecentlyAssignedTerrainDef);
                    //Log.Message("Added match between " + terrain.defName + " and " + StoneHelper.mostRecentlyAssignedTerrainDef?.defName);
                }
            }
            __result = result;
        }
    }
    
    public class Terraformer : DefModExtension
    {
        public bool hardmodeAllows = true;
        //public bool hardmodeAllowsOnIce = true;
        public bool isGrowingJob = false;
        public bool hidden = false;
        //public List<TerrainDef> above;
        //public List<TerrainDef> near;
        public TerrainDef result;
        public ThingDef thingResult;
        public List<ThingDefCountClass> products;
        public List<BiomeDef> biomesAllowedOn;
        public List<BiomeDef> biomesDisallowedOn;
        public List<BiomeDef> hardmodeBiomesAllowedOn;
        public List<BiomeDef> hardmodeBiomesDisallowedOn;
    }

/*    [StaticConstructorOnStartup]
    public static class TerraformingUtility
    {
        static TerraformingUtility()
        {
            System.Reflection.MethodInfo blueprint = typeof(ThingDefGenerator_Buildings).GetMethod("NewBlueprintDef_Thing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if(blueprint == null)
            {
                Log.Error("Can't find NewBlueprintDef_Thing");
                return;
            }
            System.Reflection.MethodInfo frame = typeof(ThingDefGenerator_Buildings).GetMethod("NewFrameDef_Thing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (blueprint == null)
            {
                Log.Error("Can't find NewFrameDef_Thing");
                return;
            }

            List<ThingDef> hiddenDefs = (from ThingDef def2 in DefDatabase<ThingDef>.AllDefs
                                         where def2.GetModExtension<Terraformer>()?.hidden == true
                                         select def2).ToList();

            foreach (ThingDef def in hiddenDefs)
            {
                DefGenerator.AddImpliedDef(blueprint.Invoke(null, new object[] { def, false, null }) as ThingDef);
                DefGenerator.AddImpliedDef(frame.Invoke(null, new object[] { def }) as ThingDef);
            }
        }
    }*/

    public class TerraformTrench : DefModExtension
    {
        public TerrainDef riverTerrain;
        public TerrainDef oceanTerrain;
        public TerrainDef lakeTerrain;
    }

    public class PlaceWorker_Dynamic : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            Terraformer config = checkingDef.GetModExtension<Terraformer>();
            if (config == null) return false;
            if (loc.GetTerrain(map) == config.result) return new AcceptanceReport("RFF.OwnTerrain".Translate());
            if (config.biomesAllowedOn != null && !config.biomesAllowedOn.Contains(map.Biome)) return new AcceptanceReport("RFF.Biome".Translate());
            if (config.biomesDisallowedOn != null && config.biomesDisallowedOn.Contains(map.Biome)) return new AcceptanceReport("RFF.Biome".Translate());
            if (Controller.Settings.playHardMode)
            {
                if (!config.hardmodeAllows) return new AcceptanceReport("RFF.Hardmode".Translate());
                if (config.hardmodeBiomesAllowedOn != null && !config.hardmodeBiomesAllowedOn.Contains(map.Biome)) return new AcceptanceReport("RFF.HardModeBiome".Translate());
                if (config.hardmodeBiomesDisallowedOn != null && config.hardmodeBiomesDisallowedOn.Contains(map.Biome)) return new AcceptanceReport("RFF.HardModeBiome".Translate());
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Designator_Dropdown), "GetDesignatorCost")]
    public static class DesignatorDropdownCostPatch
    {
        static bool Prefix(ThingDef __result, Designator des)
        {
            if (des is Designator_Place plc && plc.PlacingDef is ThingDef td && td.thingClass == typeof(Building_Terraform)) { __result = null; return false; }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints), "JobOnThing")]
    public static class WorkGiver_ConstructDeliverResourcesToBlueprints_JobOnThing
    {
        static bool Prefix(Thing t, ref Job __result)
        {
            if (t.def?.entityDefToBuild?.GetModExtension<Terraformer>() != null)
            {
                __result = null;
                return false;
            }
            return true;
        }

        /*static void Postfix(Thing t, ref Job __result)
        {
            if (__result != null && t.def.entityDefToBuild?.GetModExtension<Terraformer>() != null) __result = null;
        }*/
    }

    [HarmonyPatch(typeof(GenConstruct), "HandleBlockingThingJob")]
    public static class GenConstruct_HandleBlockingThingJob
    {
        static bool Prefix(Thing constructible, Pawn worker, bool forced, ref Job __result)
        {
            Thing thing = GenConstruct.FirstBlockingThing(constructible, worker);
            if (thing.def.category == ThingCategory.Building)
            {
                if (thing.def.Minifiable)
                {
                    if (worker.CanReserveAndReach(thing, PathEndMode.Touch, worker.NormalMaxDanger(), 1, -1, null, forced))
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Uninstall, thing);
                        job.ignoreDesignations = true;
                        __result = job;
                        return false;
                    }
                }
            }
            return true;
        }
    }

    /*[HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints), "NoCostFrameMakeJobFor")]
    public static class WorkGiver_ConstructDeliverResourcesToBlueprints_NoCostFrameMakeJobFor
    {
        static void Postfix(IConstructible c, ref Job __result)
        {
            if (__result != null && c is Thing t && t.def.entityDefToBuild?.GetModExtension<Terraformer>()?.isGrowingJob == true) __result = null;
        }
    }*/

    [HarmonyPatch(typeof(WorkGiver_ConstructFinishFrames), "JobOnThing")]
    public static class WorkGiver_ConstructFinishFrames_JobOnThing
    {
        static bool Prefix(Thing t, ref Job __result)
        {
            if (t.def?.entityDefToBuild?.GetModExtension<Terraformer>() != null)
            {
                __result = null;
                return false;
            }
            return true;
        }

        /*static void Postfix(Thing t, ref Job __result)
        {
            //Log.Message("ConstructFinishFrames job logged");
            if (__result != null && t.def.entityDefToBuild?.GetModExtension<Terraformer>() != null) __result = null;
        }*/
    }

    public class Comp_QueuedTerraformingSteps: ThingComp
    {
        public List<ThingDef> steps;

        public Comp_QueuedTerraformingSteps(List<ThingDef> steps)
        {
            /*String str = "Creating new queue with steps";
            foreach (ThingDef step in steps)
            {
                str += " " + step.defName;
            }
            Log.Message(str);*/

            this.steps = steps;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Collections.Look(ref steps, "queuedSteps", LookMode.Def);
        }
    }

    [HarmonyPatch(typeof(Blueprint_Build), "MakeSolidThing")]
    public static class MakeFramePostfix
    {
        public static void Postfix(ref Blueprint __instance, ref Thing __result)
        {
            Comp_QueuedTerraformingSteps queue = __instance.GetComp<Comp_QueuedTerraformingSteps>();
            if(queue != null && __result is ThingWithComps twc)
            {
                //Log.Message("Passing queued item comp from blueprint to frame for item " + __result.def.defName);

                twc.AllComps.Add(new Comp_QueuedTerraformingSteps(queue.steps));
            }
        }
    }

    [HarmonyPatch(typeof(Frame), "FailConstruction")]
    public static class FrameFailPrefix
    {
        public static bool Prefix(ref Frame __instance, Pawn worker)
        {
            if (__instance.def.entityDefToBuild is ThingDef thingDef && thingDef.thingClass == typeof(Building_Terraform))
            {
                Map map = __instance.Map;
                __instance.Destroy(DestroyMode.FailConstruction);
                Blueprint_Build blueprint_Build = null;
                if (__instance.def.entityDefToBuild.blueprintDef != null)
                {
                    blueprint_Build = (Blueprint_Build)ThingMaker.MakeThing(__instance.def.entityDefToBuild.blueprintDef);
                    blueprint_Build.SetFactionDirect(__instance.Faction);

                    Comp_QueuedTerraformingSteps queue = __instance.GetComp<Comp_QueuedTerraformingSteps>();
                    if (queue != null)
                    {
                        blueprint_Build.AllComps.Add(new Comp_QueuedTerraformingSteps(queue.steps));
                    }

                    GenSpawn.Spawn(blueprint_Build, __instance.Position, map, __instance.Rotation, WipeMode.FullRefund);
                }
                worker.GetLord()?.Notify_ConstructionFailed(worker, __instance, blueprint_Build);
                MoteMaker.ThrowText(__instance.DrawPos, map, "TextMote_ConstructionFail".Translate(), 6f);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Frame), "CompleteConstruction")]
    public static class FrameCompletePrefix
    {
        public static bool Prefix(ref Frame __instance)
        {
            if(__instance.def.entityDefToBuild is ThingDef thingDef && thingDef.thingClass == typeof(Building_Terraform))
            {
                //Log.Message("Handling frame for Terraforming entity " + __instance.def.defName);

                __instance.resourceContainer.ClearAndDestroyContents();
                Map map = __instance.Map;
                __instance.Destroy();

                Thing thing = ThingMaker.MakeThing(thingDef);
                thing.SetFactionDirect(__instance.Faction);

                Comp_QueuedTerraformingSteps queue = __instance.GetComp<Comp_QueuedTerraformingSteps>();

                if (queue != null && thing is ThingWithComps twc)
                {
                    /*String str = "Passing on queue with steps";
                    foreach (ThingDef step in queue.steps)
                    {
                        str += " " + step.defName;
                    }
                    Log.Message(str);

                    Log.Message("Before: terraforming entity has " + twc.AllComps.Count + " comps.");*/

                    //twc.AllComps.Add(new Comp_QueuedTerraformingSteps(queue.steps));
                    //twc.allComps = new List<ThingComp>();
                    twc.AllComps.Add(queue);

                    /*Log.Message("Terraforming entity now has " + twc.AllComps.Count + " comps.");

                    Comp_QueuedTerraformingSteps queued = twc.GetComp<Comp_QueuedTerraformingSteps>();
                    if (queued == null)
                    {
                        Log.Message("Terraforming entity did not recieve expected queue comp.");
                    }*/
                }

                GenSpawn.Spawn(thing, __instance.Position, map, __instance.Rotation, WipeMode.FullRefund);
                return false;
            }
            return true;
        }
    }
}
