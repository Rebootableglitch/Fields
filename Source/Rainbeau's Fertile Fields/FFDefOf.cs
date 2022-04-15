using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace RFF_Code
{
    [DefOf]
    class FFDefOf
    {
    #pragma warning disable 0649

        public static ThingDef CompostBarrel;

        public static ThingDef Fertilizer;

        public static ThingDef RawCompost;

        public static ThingDef PlantScraps;

        public static ThingDef BrickWall;

        //public static ThingDef TrenchShallow;

        //public static ThingDef TrenchDeep;

        public static JobDef EmptyCompostBarrel;

        public static JobDef FillCompostBarrel;

        public static JobDef FinishFrame;

        public static JobDef FinishFrameGrowing;

        public static JobDef FinishFrameConstruction;

        public static JobDef PlaceNoCostFrame;

        public static TerrainDef Marsh;

        public static TerrainDef MarshyTerrain;

        public static TerrainDef Mud;

        public static TerrainDef Granite_Rough;

        public static StatDef FarmConstructionSpeed;

        public static StatDef FarmConstructSuccessChance;

        public static TerrainAffordanceDef RoughStone;

        public static TerrainAffordanceDef SmoothStone;

        static FFDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FFDefOf));
        }
    }
}
