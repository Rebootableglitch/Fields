using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace RFF_Code
{
    public class JobDriver_ConstructFinishFrameGrowing : JobDriver
    {
        private const int JobEndInterval = 5000;

        public const float skillPerTick = 0.06f;

        private Frame Frame => (Frame)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            Toil build = new Toil();
            build.initAction = delegate {
                GenClamor.DoClamor(build.actor, 15f, ClamorDefOf.Construction);
            };
            build.tickAction = delegate {
                Pawn actor = build.actor;
                Frame frame = Frame;
                actor.skills.Learn(SkillDefOf.Plants, skillPerTick, false);
                float statValue = actor.GetStatValue(FFDefOf.FarmConstructionSpeed, true);
                float workToMake = frame.WorkToBuild;
                if (actor.Faction == Faction.OfPlayer)
                {
                    float statValue2 = actor.GetStatValue(FFDefOf.FarmConstructSuccessChance, true);
                    if (Rand.Value < 1f - Mathf.Pow(statValue2, statValue / workToMake))
                    {
                        frame.FailConstruction(actor);
                        ReadyForNextToil();
                        return;
                    }
                }
                Map.snowGrid.SetDepth(frame.Position, 0f);
                frame.workDone += statValue;
                if (frame.workDone >= workToMake)
                {
                    frame.CompleteConstruction(actor);
                    ReadyForNextToil();
                    return;
                }
            };
            build.WithEffect(() => ((Frame)build.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing).ConstructionEffect, TargetIndex.A);
            build.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            build.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            build.FailOn(() => !GenConstruct.CanConstruct(Frame, pawn, true, false));
            build.defaultCompleteMode = ToilCompleteMode.Delay;
            build.defaultDuration = 5000;
            yield return build;
        }
    }

    public class JobDriver_ConstructFinishFrameConstruction : JobDriver
    {
        private const int JobEndInterval = 5000;

        private Frame Frame => (Frame)job.GetTarget(TargetIndex.A).Thing;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
            Toil build = new Toil();
            build.initAction = delegate {
                GenClamor.DoClamor(build.actor, 15f, ClamorDefOf.Construction);
            };
            build.tickAction = delegate {
                Pawn actor = build.actor;
                Frame frame = this.Frame;
                actor.skills.Learn(SkillDefOf.Construction, 0.06f, false);
                float statValue = actor.GetStatValue(StatDefOf.ConstructionSpeed, true);
                float workToMake = frame.WorkToBuild;
                if (actor.Faction == Faction.OfPlayer)
                {
                    float statValue2 = actor.GetStatValue(StatDefOf.ConstructSuccessChance, true);
                    if (Rand.Value < 1f - Mathf.Pow(statValue2, statValue / workToMake))
                    {
                        frame.FailConstruction(actor);
                        ReadyForNextToil();
                        return;
                    }
                }
                Map.snowGrid.SetDepth(frame.Position, 0f);
                frame.workDone += statValue;
                if (frame.workDone >= workToMake)
                {
                    frame.CompleteConstruction(actor);
                    ReadyForNextToil();
                    return;
                }
            };
            build.WithEffect(() => ((Frame)build.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing).ConstructionEffect, TargetIndex.A);
            build.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            build.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            build.FailOn(() => !GenConstruct.CanConstruct(Frame, pawn, true, false));
            build.defaultCompleteMode = ToilCompleteMode.Delay;
            build.defaultDuration = 5000;
            yield return build;
        }
    }

    public class WorkGiver_ConstructFinishFramesGrowing : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode
        {
            get { return PathEndMode.Touch; }
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn.Map == null) return null;
            return from Thing thing in pawn.Map.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.BuildingFrame))
                   where thing?.def?.entityDefToBuild?.GetModExtension<Terraformer>()?.isGrowingJob == true
                   select thing;
        }

        /*public override ThingRequest PotentialWorkThingRequest
        {
            get { return ThingRequest.ForGroup(ThingRequestGroup.BuildingFrame); }
        }*/

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Faction != pawn.Faction && (pawn.guest?.IsPrisoner == false || t.Faction != pawn.guest?.HostFaction))
            {
                return null;
            }
            Frame frame = t as Frame;
            if (frame == null)
            {
                return null;
            }
            if (frame.MaterialsNeeded().Count > 0)
            {
                return null;
            }
            /*if (frame.def.entityDefToBuild?.GetModExtension<Terraformer>()?.isGrowingJob != true)
            {
                //Log.Message("Logging farming terraforming job.");
                return null;
            }*/
            if (GenConstruct.FirstBlockingThing(frame, pawn) != null)
            {
                return GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
            }
            if (!GenConstruct.CanConstruct(frame, pawn, true, forced))
            {
                return null;
            }
            return new Job(FFDefOf.FinishFrameGrowing, frame);
        }
    }

    public class WorkGiver_ConstructFinishFramesTerraforming: WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode
        {
            get { return PathEndMode.Touch; }
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (pawn.Map == null) return null;
            return from Thing thing in pawn.Map.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.BuildingFrame))
                   where thing?.def?.entityDefToBuild?.GetModExtension<Terraformer>()?.isGrowingJob == false
                   select thing;
        }

        /*public override ThingRequest PotentialWorkThingRequest
        {
            get { return ThingRequest.ForGroup(ThingRequestGroup.BuildingFrame); }
        }*/

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Faction != pawn.Faction && (pawn.guest?.IsPrisoner == false || t.Faction != pawn.guest?.HostFaction))
            {
                return null;
            }
            Frame frame = t as Frame;
            if (frame == null)
            {
                return null;
            }
            if (frame.MaterialsNeeded().Count > 0)
            {
                return null;
            }
            /*if (frame.def.entityDefToBuild?.GetModExtension<Terraformer>()?.isGrowingJob != false)
            {
                //Log.Message("Logging construction terraforming job.");
                return null;
            }*/
            if (GenConstruct.FirstBlockingThing(frame, pawn) != null)
            {
                return GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
            }
            if (!GenConstruct.CanConstruct(frame, pawn, true, forced))
            {
                return null;
            }
            return new Job(FFDefOf.FinishFrameConstruction, frame);
        }
    }

    public class WorkGiver_ConstructDeliverResourcesToBlueprintsGrowing : WorkGiver_ConstructDeliverResources
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get { return ThingRequest.ForGroup(ThingRequestGroup.Blueprint); }
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Faction != pawn.Faction && (pawn.guest?.IsPrisoner == false || t.Faction != pawn.guest?.HostFaction))
            {
                return null;
            }
            Blueprint blueprint = t as Blueprint;
            if (blueprint == null)
            {
                return null;
            }
            if (blueprint.def.entityDefToBuild?.GetModExtension<Terraformer>()?.isGrowingJob != true)
            {
                return null;
            }
            if (GenConstruct.FirstBlockingThing(blueprint, pawn) != null)
            {
                return GenConstruct.HandleBlockingThingJob(blueprint, pawn, forced);
            }
            if (!GenConstruct.CanConstruct(blueprint, pawn, true, forced))
            {
                return null;
            }
            Job job = RemoveExistingFloorJob(pawn, blueprint);
            if (job != null)
            {
                return job;
            }
            Job job2 = ResourceDeliverJobFor(pawn, blueprint, true);
            if (job2 != null)
            {
                return job2;
            }
            Job job3 = NoCostFrameMakeJobFor(pawn, blueprint);
            if (job3 != null)
            {
                return job3;
            }
            return null;
        }

        private Job NoCostFrameMakeJobFor(Pawn pawn, IConstructible c)
        {
            if (c is Blueprint_Install)
            {
                return null;
            }
            if (c is Blueprint && c.MaterialsNeeded().Count == 0)
            {
                Job job = JobMaker.MakeJob(JobDefOf.PlaceNoCostFrame);
                job.targetA = (Thing)c;
                return job;
            }
            return null;
        }
    }

    public class WorkGiver_ConstructDeliverResourcesToBlueprintsTerraforming : WorkGiver_ConstructDeliverResources
    {
        public override ThingRequest PotentialWorkThingRequest
        {
            get { return ThingRequest.ForGroup(ThingRequestGroup.Blueprint); }
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t.Faction != pawn.Faction && (pawn.guest?.IsPrisoner == false || t.Faction != pawn.guest?.HostFaction))
            {
                return null;
            }
            Blueprint blueprint = t as Blueprint;
            if (blueprint == null)
            {
                return null;
            }
            if (blueprint.def.entityDefToBuild?.GetModExtension<Terraformer>()?.isGrowingJob != false)
            {
                return null;
            }
            if (GenConstruct.FirstBlockingThing(blueprint, pawn) != null)
            {
                return GenConstruct.HandleBlockingThingJob(blueprint, pawn, forced);
            }
            if (!GenConstruct.CanConstruct(blueprint, pawn, true, forced))
            {
                return null;
            }
            Job job = RemoveExistingFloorJob(pawn, blueprint);
            if (job != null)
            {
                return job;
            }
            Job job2 = ResourceDeliverJobFor(pawn, blueprint, true);
            if (job2 != null)
            {
                return job2;
            }
            Job job3 = NoCostFrameMakeJobFor(pawn, blueprint);
            if (job3 != null)
            {
                return job3;
            }
            return null;
        }

        private Job NoCostFrameMakeJobFor(Pawn pawn, IConstructible c)
        {
            if (c is Blueprint_Install)
            {
                return null;
            }
            if (c is Blueprint && c.MaterialsNeeded().Count == 0)
            {
                Job job = JobMaker.MakeJob(JobDefOf.PlaceNoCostFrame);
                job.targetA = (Thing)c;
                return job;
            }
            return null;
        }
    }
}
