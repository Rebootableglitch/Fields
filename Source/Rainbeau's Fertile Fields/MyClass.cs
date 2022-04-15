using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RFF_Code
{
	// ===================================== //
	// ========== Harmony Patches ========== //
	// ===================================== //

	[HarmonyPatch (typeof (CompRottable), "CompTickRare")]
	public static class CompRottable_CompTickRare {
		static void Prefix (CompRottable __instance, ref State __state) {
			__state = new State (__instance);
		}
		static void Postfix(CompRottable __instance, ref State __state) {
			if (__instance.parent.Destroyed) {
				if (__instance.parent.def.thingCategories == null) { return; }
				if (__instance.parent.def.defName.Contains("__Corpse")) { return; }
				if (__instance.parent.def.defName.Contains("Mizu_")) { return; }
				if (__state.map == null) { return; }

				ThingDef nextStageOfRot = ThingDef.Named("RottedMush");

				if (__instance.parent.def.IsMeat) //it is meat
                {
					nextStageOfRot = ThingDef.Named("InsectJelly");
				} 
				else if (!__instance.parent.def.IsMeat) //if it is not meat
				{
					nextStageOfRot = ThingDef.Named("EggChickenUnfertilized");
				}
				
				if (__instance.parent.def.defName == "InsectJelly" || __instance.parent.def.defName == "EggChickenUnfertilized")
                {
					nextStageOfRot = ThingDef.Named("RottedMush");
				}

				
				var rotItem = ThingMaker.MakeThing (nextStageOfRot, null);
				rotItem.stackCount = __state.stackCount;
				if (!__state.map.areaManager.Home[__instance.parent.Position]) {
					rotItem.SetForbidden(true, false);
				}
				GenPlace.TryPlaceThing (rotItem, __instance.parent.Position, __state.map, ThingPlaceMode.Near, null);
			}
		}
		struct State {
			public Map map;
			public int stackCount;
			public State(CompRottable instance) {
				map = instance.parent.Map;
				stackCount = instance.parent.stackCount;
			}
		}
	}

    [HarmonyPatch(typeof(GenConstruct), "BlocksConstruction", null)]
    public static class GenConstruct_BlocksConstruction
    {
        public static void Postfix(Thing constructible, Thing t, ref bool __result)
        {
            if (!__result) return; // we're not going to DISallow anything, so return if already false
            if (t?.def != FFDefOf.BrickWall) return; // if it's not a brick wall we don't care
            if (constructible == null) return;

            ThingDef td = null;

            if (constructible is Blueprint)
            {
                td = constructible.def;
            }
            else if (constructible is Frame)
            {
                td = constructible.def.entityDefToBuild.blueprintDef;
            }
            else
            {
                td = constructible.def.blueprintDef;
            }

            if (td?.entityDefToBuild is ThingDef td2 && td2?.building?.canPlaceOverWall == true)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(GenConstruct), "CanPlaceBlueprintOver", null)]
    public static class GenConstruct_CanPlaceBlueprintOver
    {
        public static void Postfix(BuildableDef newDef, ThingDef oldDef, ref bool __result)
        {
            if (__result) return;
            if (GenConstruct.BuiltDefOf(oldDef) != FFDefOf.BrickWall) return;
            if (newDef is ThingDef newThingDef && newThingDef?.building?.canPlaceOverWall == true)
            {
                __result = true;
            }
        }
    }
	
	[HarmonyPatch(typeof(GenLeaving), "DoLeavingsFor", new Type[] { typeof(TerrainDef), typeof(IntVec3), typeof(Map) })]
	public static class GenLeaving_DoLeavingsFor {
		public static bool Prefix(TerrainDef terrain, IntVec3 cell, Map map) {
			if (terrain == TerrainDef.Named("Topsoil") || terrain == TerrainDef.Named("DirtFert")) {
				Thing leaving = ThingMaker.MakeThing(ThingDef.Named("PileofDirt"), null);
				leaving.stackCount = 2;
				GenPlace.TryPlaceThing(leaving, cell, map, ThingPlaceMode.Near, null);
				return false;
			}
			if (terrain == TerrainDef.Named("SoilTilled")) {
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(GenSpawn), "SpawningWipes", null)]
	public static class GenSpawn_SpawningWipes {
		public static bool Prefix(BuildableDef newEntDef, BuildableDef oldEntDef, ref bool __result) {
			ThingDef thingDef = newEntDef as ThingDef;
			ThingDef thingDef1 = oldEntDef as ThingDef;
			if (thingDef == null || thingDef1 == null) {
				return true;
			}
			ThingDef thingDef2 = thingDef.entityDefToBuild as ThingDef;
			if (thingDef1.IsBlueprint) {
				if (thingDef.IsBlueprint) {
					if (thingDef2 != null && thingDef2.building != null && thingDef2.building.canPlaceOverWall) {
						if (thingDef1.entityDefToBuild is ThingDef) {
							if (thingDef1.entityDefToBuild.defName.Equals("BrickWall")) {
								__result = true;
								return false;
							}
						}
					}
				}
			}
			return true;
		}
	}

    [HarmonyPatch(typeof(WorkGiver_GrowerSow), "JobOnCell")]
    public static class GrowerSow_JobOnCell
    {
        public static void Postfix(ref Job __result, Pawn pawn, IntVec3 c)
        {
            if (__result == null) return;

            foreach(Thing thing in c.GetThingList(pawn.Map))
            {
                if(thing?.def?.thingClass == typeof(Building_Terraform) || ( thing?.def?.entityDefToBuild is ThingDef td && td.thingClass == typeof(Building_Terraform)))
                {
                    __result = null;
                    return;
                }
            }
        }
    }

	// =============================== //
	// ========== Cremating ========== //
	// =============================== //

	public class IngredientValueGetter_Mass : IngredientValueGetter {
		public IngredientValueGetter_Mass() { }
		public override string BillRequirementsDescription(RecipeDef r, IngredientCount ing) {
			return string.Concat("RFF.BillRequires".Translate(new object[] { ing.GetBaseCount() }), " (", ing.filter.Summary, ")");
		}
		public override float ValuePerUnitOf(ThingDef t) {
			return t.GetStatValueAbstract(StatDefOf.Mass, null);
		}
	}
	
	// ================================= //
	// ========== Compost Bin ========== //
	// ================================= //

	public class Building_CompostBin : Building {
		private CompCompostBin compostBinComp;
		private int fermentTicks;
		private int TargetTicks {
			get {
				return this.compostBinComp.Props.fermentTicks;
			}
		}
		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look<int>(ref this.fermentTicks, "fermentTicks", 0, false);
		}
		public override void SpawnSetup(Map currentGame, bool respawningAfterLoad) {
			base.SpawnSetup(currentGame, respawningAfterLoad);
			this.compostBinComp = base.GetComp<CompCompostBin>();
		}
		public override void TickRare() {
			base.TickRare();
			if (this.fermentTicks < this.TargetTicks) {
				this.fermentTicks++;
			}
			if (this.fermentTicks >= this.TargetTicks) {
				this.PlaceProduct();
			}
		}
		private void PlaceProduct() {
			IntVec3 position = base.Position;
			Map map = base.Map;
			Thing product = ThingMaker.MakeThing(ThingDef.Named(this.compostBinComp.Props.product), null);
			product.stackCount = 3;
			GenPlace.TryPlaceThing(product, position, map, ThingPlaceMode.Near, null);
			System.Random rnd = new System.Random();
			int Chance1 = rnd.Next(3);
			int Chance2 = rnd.Next(3); 
			if (Chance1 < 2) {
				GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDef.Named("WoodLog"), null), position, map, ThingPlaceMode.Near, null);
			}
			if (Chance2 < 1) {
				GenPlace.TryPlaceThing(ThingMaker.MakeThing(ThingDef.Named("WoodLog"), null), position, map, ThingPlaceMode.Near, null);
			}
			this.Destroy(DestroyMode.Vanish);
			if (Controller.Settings.autoRecompost.Equals(true)) {
				GenConstruct.PlaceBlueprintForBuild_NewTemp(ThingDef.Named("CompostBin"), position, map, Rot4.North, Faction.OfPlayer, null);
			}
		}
		public override string GetInspectString() {
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(base.GetInspectString());
			if (stringBuilder.Length != 0) {
				stringBuilder.AppendLine();
			}
			stringBuilder.AppendLine("RFF.Progress".Translate() + " " + ((float)this.fermentTicks / (float)this.TargetTicks * 100f).ToString("#0.00") + "%");
			return stringBuilder.ToString().TrimEndNewlines();
		}
	}

	public class CompCompostBin : ThingComp {
		public CompProperties_CompostBin Props {
			get {
				return (CompProperties_CompostBin)this.props;
			}
		}
	}

	public class CompProperties_CompostBin : CompProperties {
		public string product;
		public int fermentTicks;
		public CompProperties_CompostBin() {
			this.compClass = typeof(CompCompostBin);
		}
	}

	// ==================================== //
	// ========== Compost Barrel ========== //
	// ==================================== //

	
	public class JobDriver_FillCompostBarrel : JobDriver {
		protected Building_CompostBarrel Barrel {
			get {
				return (Building_CompostBarrel)this.job.GetTarget(TargetIndex.A).Thing;
			}
		}
		protected Thing RawCompost {
			get {
				return this.job.GetTarget(TargetIndex.B).Thing;
			}
		}
		public override bool TryMakePreToilReservations(bool errorOnFailed) {
			return this.pawn.Reserve(this.Barrel, this.job, 1, -1, null, errorOnFailed) && this.pawn.Reserve(this.RawCompost, this.job, 1, -1, null, errorOnFailed);
		}
		[DebuggerHidden]
		protected override IEnumerable<Toil> MakeNewToils() {
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			this.FailOnBurningImmobile(TargetIndex.A);
			base.AddEndCondition(() => (this.Barrel.SpaceLeftForCompost > 0) ? JobCondition.Ongoing : JobCondition.Succeeded);
			yield return Toils_General.DoAtomic(delegate {
				this.job.count = this.Barrel.SpaceLeftForCompost;
			});
			Toil reserveWort = Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
			yield return reserveWort;
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.B).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
			yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false).FailOnDestroyedNullOrForbidden(TargetIndex.B);
			yield return Toils_Haul.CheckForGetOpportunityDuplicate(reserveWort, TargetIndex.B, TargetIndex.None, true, null);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.B).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
			yield return new Toil {
				initAction = delegate {
					this.Barrel.Addoil(this.RawCompost);
				},
				defaultCompleteMode = ToilCompleteMode.Instant
			};
		}
	}
	
	public class JobDriver_EmptyCompostBarrel : JobDriver {
		protected Building_CompostBarrel Barrel {
			get {
				return (Building_CompostBarrel)this.job.GetTarget(TargetIndex.A).Thing;
			}
		}
		protected Thing Fertilizer {
			get {
				return this.job.GetTarget(TargetIndex.B).Thing;
			}
		}
		public override bool TryMakePreToilReservations(bool errorOnFailed) {
			return this.pawn.Reserve(this.Barrel, this.job, 1, -1, null, errorOnFailed);
		}
		[DebuggerHidden]
		protected override IEnumerable<Toil> MakeNewToils() {
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			this.FailOnBurningImmobile(TargetIndex.A);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.Wait(200).FailOnDestroyedNullOrForbidden(TargetIndex.A).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch).FailOn(() => !this.Barrel.Distilled).WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
			yield return new Toil {
				initAction = delegate {
					Thing thing = this.Barrel.TakeOutFertilizer();
					GenPlace.TryPlaceThing(thing, this.pawn.Position, this.Map, ThingPlaceMode.Near, null);
					StoragePriority currentPriority = StoreUtility.CurrentStoragePriorityOf(thing);
                    if (StoreUtility.TryFindBestBetterStoreCellFor(thing, this.pawn, this.Map, currentPriority, this.pawn.Faction, out IntVec3 c, true))
                    {
                        this.job.SetTarget(TargetIndex.C, c);
                        this.job.SetTarget(TargetIndex.B, thing);
                        this.job.count = thing.stackCount;
                    }
                    else
                    {
                        this.EndJobWith(JobCondition.Incompletable);
                    }
                },
				defaultCompleteMode = ToilCompleteMode.Instant
			};
			yield return Toils_Reserve.Reserve(TargetIndex.B, 1, -1, null);
			yield return Toils_Reserve.Reserve(TargetIndex.C, 1, -1, null);
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
			yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, false, false);
			Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.C);
			yield return carryToCell;
			yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, carryToCell, true);
		}
	}

}
