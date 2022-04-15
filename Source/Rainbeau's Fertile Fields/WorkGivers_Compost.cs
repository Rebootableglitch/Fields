using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using Verse.AI;
using UnityEngine;

namespace RFF_Code
{
    public class WorkGiver_FillCompostBarrel : WorkGiver_Scanner
    {
        private static string TemperatureTrans;
        private static string NoCompostTrans;
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForDef(FFDefOf.CompostBarrel);
            }
        }
        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }
        public static void Reset()
        {
            WorkGiver_FillCompostBarrel.TemperatureTrans = "RFF.BadTemp".Translate();
            WorkGiver_FillCompostBarrel.NoCompostTrans = "RFF.NoComp".Translate();
        }
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_CompostBarrel Building_RFF_CompostBarrel = t as Building_CompostBarrel;
            if (Building_RFF_CompostBarrel == null || Building_RFF_CompostBarrel.Distilled || Building_RFF_CompostBarrel.SpaceLeftForCompost <= 0)
            {
                return false;
            }
            float temperature = Building_RFF_CompostBarrel.Position.GetTemperature(Building_RFF_CompostBarrel.Map);
            CompProperties_TemperatureRuinable compProperties = Building_RFF_CompostBarrel.def.GetCompProperties<CompProperties_TemperatureRuinable>();
            if (temperature < compProperties.minSafeTemperature + 2f || temperature > compProperties.maxSafeTemperature - 2f)
            {
                JobFailReason.Is(WorkGiver_FillCompostBarrel.TemperatureTrans);
                return false;
            }
            if (t.IsForbidden(pawn) || !pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1))
            {
                return false;
            }
            if (pawn.Map.designationManager.DesignationOn(t, RimWorld.DesignationDefOf.Deconstruct) != null)
            {
                return false;
            }
            if (this.Findmech_oil(pawn, Building_RFF_CompostBarrel) == null)
            {
                JobFailReason.Is(WorkGiver_FillCompostBarrel.NoCompostTrans);
                return false;
            }
            return !t.IsBurning();
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_CompostBarrel Building_RFF_CompostBarrel = (Building_CompostBarrel)t;
            Thing t2 = this.Findmech_oil(pawn, Building_RFF_CompostBarrel);
            return new Job(FFDefOf.FillCompostBarrel, t, t2)
            {
                count = Building_RFF_CompostBarrel.SpaceLeftForCompost
            };
        }
        private Thing Findmech_oil(Pawn pawn, Building_CompostBarrel barrel)
        {
            Predicate<Thing> predicate = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1);
            Predicate<Thing> validator = predicate;
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(FFDefOf.RawCompost), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false), 9999f, validator, null, 0, -1, false);
        }
    }

    public class WorkGiver_EmptyCompostBarrel : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForDef(FFDefOf.CompostBarrel);
            }
        }
        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_CompostBarrel Building_RFF_CompostBarrel = t as Building_CompostBarrel;
            return Building_RFF_CompostBarrel != null && Building_RFF_CompostBarrel.Distilled && !t.IsBurning() && !t.IsForbidden(pawn) && pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1);
        }
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return new Job(FFDefOf.EmptyCompostBarrel, t);
        }
    }
}
