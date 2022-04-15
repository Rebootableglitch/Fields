using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;

namespace RFF_Code
{
    public class Building_Terraform : Building
    {
        static List<IntVec3> workingCells = new List<IntVec3>();

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            PlaceProduct();
            if (!Destroyed)
            {
                Destroy(DestroyMode.Vanish);
            }

            Comp_QueuedTerraformingSteps queue = GetComp<Comp_QueuedTerraformingSteps>();
            if (queue != null)
            {
                /*String str = "Resolving queue with members ";
                foreach (ThingDef step in queue.steps)
                {
                    str += " " + step.defName;
                }
                Log.Message(str);*/

                //DeSpawn();
                Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild(queue.steps.First(), Position, map, Rot4.North, Faction.OfPlayer, null);
                if (queue.steps.Count > 1)
                {
                    queue.steps.RemoveAt(0);
                    blueprint.AllComps.Add(new Comp_QueuedTerraformingSteps(queue.steps));
                }
            }
        }

        void PlaceProduct()
        {
            Terraformer terrain = def.GetModExtension<Terraformer>();
            if (terrain == null)
            {
                Log.Error("Fertile Fields: " + this + " lacks terraformer mod extension");
                return;
            }

            Map.snowGrid.SetDepth(Position, 0);

            if (terrain.products != null)
            {
                Thing thing;
                foreach (var product in terrain.products)
                {
                    if (product?.thingDef == null)
                    {
                        Log.Warning("Fertile Fields: terraformer " + def.defName + " attempted to yield an invalid resource.");
                        continue;
                    }
                    thing = ThingMaker.MakeThing(product.thingDef, null);
                    thing.stackCount = product.count;
                    GenPlace.TryPlaceThing(thing, Position, Map, ThingPlaceMode.Near, null);
                }
            }
            if (terrain.result != null)
            {
                if (terrain.result == FFDefOf.Granite_Rough)
                {
                    TerrainDef rock = RockAt(Map, Position);
                    Map.terrainGrid.SetTerrain(Position, RockAt(Map, Position));
                }
                else
                {
                    Map.terrainGrid.SetTerrain(Position, terrain.result);
                    if(terrain.result.HasModExtension<TerraformTrench>())
                    {
                        //Log.Message("Attempting to log new terraforming trench at " + Position.ToString());
                        Map.GetComponent<TerraformingComponent>()?.Register(Position);
                    }
                }
            }
            if (terrain.thingResult != null)
            {
                GenSpawn.Spawn(ThingMaker.MakeThing(terrain.thingResult), Position, Map, WipeMode.VanishOrMoveAside);
            }
        }

        static TerrainDef RockAt(Map map, IntVec3 pos)
        {
            const float skipChance = 0.5f;

            workingCells = (from IntVec3 cell in GenRadial.RadialCellsAround(pos, 10, true)
                            where cell.x >= 0 && cell.y >= 0 && cell.x < map.Size.x && cell.y < map.Size.y
                            select cell).ToList();

            foreach (IntVec3 cell in workingCells)
            {
                if (Rand.Value < skipChance) continue;

                TerrainDef terrain = cell.GetTerrain(map);

                if (terrain.affordances.Contains(FFDefOf.RoughStone))
                {
                    //return terrain;
                    if(StoneHelper.roughStoneTypeFor.ContainsKey(terrain))
                    {
                        return StoneHelper.roughStoneTypeFor[terrain];
                    }
                }
            }

            return Find.World.NaturalRockTypesIn(map.Tile).RandomElement().building.naturalTerrain;
        }
    }

    public class TerraformingComponent: MapComponent
    {
        List<IntVec3> cells = new List<IntVec3>();

        List<IntVec3> finishedCells = new List<IntVec3>();

        public TerraformingComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            cells = (from IntVec3 cell in map.AllCells
                     where cell.GetTerrain(map).HasModExtension<TerraformTrench>()
                     select cell).ToList();
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksAbs % 1000 == 0)
            {
                //Log.Message("Trench manager has " + cells.Count + " cells.");
            }

            foreach (IntVec3 cell in cells)
            {
                TerraformTrench trench = cell.GetTerrain(map).GetModExtension<TerraformTrench>();

                if (trench == null)
                {
                    finishedCells.Add(cell);
                    continue;
                }

                bool adjWater = false;
                bool adjOcean = false;
                bool adjRiver = false;
                bool canBeAdjRiver = map.waterInfo.riverFlowMapBounds.Contains(cell);

                foreach (IntVec3 near in GenAdjFast.AdjacentCells8Way(cell))
                {
                    if (near.x < 0 || near.z < 0 || near.x >= map.Size.x || near.z >= map.Size.z) continue;

                    TerrainDef terrain = near.GetTerrain(map);

                    if (!terrain.IsWater) continue;

                    adjWater = true;
                    //Log.Message("Found water cell at " + near.ToString());

                    if(canBeAdjRiver && terrain.IsRiver)
                    {
                        adjRiver = true;
                        //Log.Message("Found river cell at " + near.ToString());
                    }
                    else if(terrain == TerrainDefOf.WaterOceanDeep || terrain == TerrainDefOf.WaterOceanShallow)
                    {
                        adjOcean = true;
                        //Log.Message("Found ocean cell at " + near.ToString());
                    }
                }

                if(adjRiver && trench.riverTerrain != null)
                {
                    map.terrainGrid.SetTerrain(cell, trench.riverTerrain);
                    finishedCells.Add(cell);
                }
                else if(adjOcean && trench.oceanTerrain != null)
                {
                    map.terrainGrid.SetTerrain(cell, trench.oceanTerrain);
                    finishedCells.Add(cell);
                }
                else if(adjWater && trench.lakeTerrain != null)
                {
                    map.terrainGrid.SetTerrain(cell, trench.lakeTerrain);
                    finishedCells.Add(cell);
                }
            }

            cells = cells.Except(finishedCells).ToList();
            finishedCells.Clear();
        }

        public void Register(IntVec3 cell)
        {
            if(!cells.Contains(cell))
            {
                cells.Add(cell);
                //Log.Message("Registered new trench at position " + cell.ToString());
            }
        }
    }

    /*public class Building_TerraformingTrench: Building
    {
        List<IntVec3> surroundingCells = new List<IntVec3>();

        public bool DeepTrench
        {
            get
            {
                return def.GetModExtension<TerraformTrench>()?.isDeep == true;
            }
        }

        public bool Marsh
        {
            get
            {
                return Position.GetTerrain(Map) == FFDefOf.MarshyTerrain || Position.GetTerrain(Map) == FFDefOf.Mud;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            surroundingCells = GenAdj.CellsAdjacent8WayAndInside(this).ToList();
        }

        public override void TickRare()
        {
            bool adjOcean = false;
            bool adjLake = false;

            foreach (IntVec3 cell in surroundingCells)
            {
                TerrainDef terrain = cell.GetTerrain(Map);

                if (!terrain.IsWater) continue;

                if (terrain.IsRiver && Map.waterInfo.riverFlowMapBounds.Contains(Position))
                {
                    if (DeepTrench)
                    {
                        Map.terrainGrid.SetTerrain(Position, TerrainDefOf.WaterMovingChestDeep);
                    }
                    else
                    {
                        Map.terrainGrid.SetTerrain(Position, TerrainDefOf.WaterMovingShallow);
                    }
                    Destroy();
                    return;
                }
                else if (terrain == TerrainDefOf.WaterOceanShallow || terrain == TerrainDefOf.WaterOceanDeep)
                {
                    adjOcean = true;
                }
                else
                {
                    adjLake = true;
                }
            }

            if(DeepTrench && (adjLake))
            {
                Map.terrainGrid.SetTerrain(Position, TerrainDefOf.WaterDeep);
                Destroy();
            }
            else if (DeepTrench && adjOcean)
            {
                Map.terrainGrid.SetTerrain(Position, TerrainDefOf.WaterOceanDeep);
                Destroy();
            }
            else if (adjOcean)
            {
                Map.terrainGrid.SetTerrain(Position, TerrainDefOf.WaterOceanShallow);
                Destroy();
            }
            else if (adjLake)
            {
                if (Marsh)
                {
                    Map.terrainGrid.SetTerrain(Position, FFDefOf.Marsh);
                    Destroy();
                }
                else
                {
                    Map.terrainGrid.SetTerrain(Position, TerrainDefOf.WaterShallow);
                    Destroy();
                }
            }
        }
    }*/
}
