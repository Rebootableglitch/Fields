using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace RFF_Code
{
    public class DesignationDef_Terraform: DesignationDef
    {
        public Type designatorDropdown;

        public TerrainDef target;

        //public ResearchProjectDef researchPrerequisite;

        public List<TerraformPath> paths;

        //public bool hardmodeAllows = true;

        //public bool hardmodeAllowsOnIce = true;

        public bool Handles(TerrainDef source)
        {
            if (source.affordances == null || source == target) return false;

            foreach (TerraformPath path in paths)
            {
                if (source.affordances.Contains(path.origin) && path.UseableNow)
                    return true;
            }
            return false;
        }

        public TerraformPath PathFor(TerrainDef source)
        {
            if (source.affordances == null || source == target) return null;

            foreach (TerraformPath path in paths)
            {
                if (source.affordances.Contains(path.origin) && path.UseableNow)
                    return path;
            }
            return null;
        }

        public bool ShouldBeVisible => paths.Any((TerraformPath path) => path.UseableNow);

        public override void ResolveReferences()
        {
            foreach(TerraformPath path in paths)
            {
                path.ResolveReferences();
            }
        }
    }

    public class TerraformPath
    {
        public TerrainAffordanceDef origin;

        public List<ThingDef> steps;

        public List<ResearchProjectDef> researchPrerequisites;

        public List<BiomeDef> onlyAllowedBiomes;

        public List<BiomeDef> disallowedBiomes;

        public List<BiomeDef> hardmodeOnlyAllowedBiomes;

        public List<BiomeDef> hardmodeDisallowedBiomes;

        public List<ThingDefCountClass> totalCost;

        public List<ThingDefCountClass> totalYield;

        public bool hardmodeAllows = true;

        //public bool hardmodeAllowsOnIce = true;

        public bool UseableNow
        {
            get
            {
                if (DebugSettings.godMode) return true;
                if (researchPrerequisites != null && researchPrerequisites.Any((ResearchProjectDef project) => !project.IsFinished)) return false;
                if (onlyAllowedBiomes != null && Find.CurrentMap != null && !onlyAllowedBiomes.Contains(Find.CurrentMap.Biome)) return false;
                if (disallowedBiomes != null && Find.CurrentMap != null && disallowedBiomes.Contains(Find.CurrentMap.Biome)) return false;
                if (!hardmodeAllows && Controller.Settings.playHardMode) return false;
                if (Controller.Settings.playHardMode && hardmodeOnlyAllowedBiomes != null && Find.CurrentMap != null && !hardmodeOnlyAllowedBiomes.Contains(Find.CurrentMap.Biome)) return false;
                if (Controller.Settings.playHardMode && hardmodeDisallowedBiomes != null && Find.CurrentMap != null && hardmodeDisallowedBiomes.Contains(Find.CurrentMap.Biome)) return false;
                //if (!hardmodeAllowsOnIce && Controller.Settings.playHardMode && Find.CurrentMap != null && Find.CurrentMap.Biome.terrainsByFertility?.First()?.terrain == TerrainDefOf.Ice) return false;
                return true;
            }
        }

        public static List<ThingDefCountClass> Merge(List<ThingDefCountClass> originalList, List<ThingDefCountClass> newList)
        {
            if (newList == null) return originalList;

            if (originalList == null) originalList = new List<ThingDefCountClass>();

            foreach(ThingDefCountClass item in newList)
            {
                if (item.thingDef == null) continue;

                bool handled = false;

                foreach(ThingDefCountClass original in originalList)
                {
                    if(item.thingDef == original.thingDef)
                    {
                        original.count += item.count;
                        handled = true;
                        break;
                    }
                }

                if(handled == false)
                {
                    originalList.Add(new ThingDefCountClass(item.thingDef, item.count));
                }
            }

            return originalList;
        }

        public void ResolveReferences()
        {
            foreach (ThingDef step in steps)
            {
                Terraformer terraformer = step.GetModExtension<Terraformer>();

                if(terraformer != null)
                {
                    if (!terraformer.hardmodeAllows) hardmodeAllows = false;
                    //if (!terraformer.hardmodeAllowsOnIce) hardmodeAllowsOnIce = false;
                    if (step.researchPrerequisites != null)
                    {
                        if (researchPrerequisites == null)
                        {
                            researchPrerequisites = new List<ResearchProjectDef>();
                        }

                        researchPrerequisites = researchPrerequisites.Union(step.researchPrerequisites).ToList();
                    }
                    if (terraformer.biomesAllowedOn != null)
                    {
                        if (onlyAllowedBiomes == null)
                        {
                            onlyAllowedBiomes = new List<BiomeDef>();
                        }

                        onlyAllowedBiomes = onlyAllowedBiomes.Union(terraformer.biomesAllowedOn).ToList();
                    }
                    if (terraformer.biomesDisallowedOn != null)
                    {
                        if (disallowedBiomes == null)
                        {
                            disallowedBiomes = new List<BiomeDef>();
                        }

                        disallowedBiomes = disallowedBiomes.Union(terraformer.biomesDisallowedOn).ToList();
                    }
                    if (terraformer.hardmodeBiomesAllowedOn != null)
                    {
                        if (hardmodeOnlyAllowedBiomes == null)
                        {
                            hardmodeOnlyAllowedBiomes = new List<BiomeDef>();
                        }

                        hardmodeOnlyAllowedBiomes = hardmodeOnlyAllowedBiomes.Union(terraformer.hardmodeBiomesAllowedOn).ToList();
                    }
                    if (terraformer.hardmodeBiomesDisallowedOn != null)
                    {
                        if (hardmodeDisallowedBiomes == null)
                        {
                            hardmodeDisallowedBiomes = new List<BiomeDef>();
                        }

                        hardmodeDisallowedBiomes = hardmodeDisallowedBiomes.Union(terraformer.hardmodeBiomesDisallowedOn).ToList();
                    }

                    totalCost = Merge(totalCost, step.costList);
                    totalYield = Merge(totalYield, terraformer.products);
                }
            }
        }
    }

    public class Designator_Terraform: Designator
    {
        public override int DraggableDimensions => 2;

        public override bool DragDrawMeasurements => true;

        public virtual DesignationDef_Terraform DesignationTerraform => null;

        protected override DesignationDef Designation => DesignationTerraform ?? null;

        public override bool Visible => base.Visible && (DesignationTerraform?.ShouldBeVisible== true);

        public static List<IntVec3> workingCells = new List<IntVec3>();

        public static List<ThingDefCountClass> totalCost = new List<ThingDefCountClass>();

        public static List<ThingDefCountClass> totalYield = new List<ThingDefCountClass>();

        public Designator_Terraform()
        {
            //icon = DesignationTerraform.target.uiIcon;
            //icon = DesignationTerraform.iconMat;
            icon = ContentFinder<UnityEngine.Texture2D>.Get(DesignationTerraform.texturePath);
            defaultLabel = DesignationTerraform.label;
            defaultDesc = DesignationTerraform.description;
            useMouseIcon = false;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_Mine;

        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Map) || loc.Fogged(Map) || loc.Impassable(Map))
            {
                return AcceptanceReport.WasRejected;
            }
            if(!DesignationTerraform.Handles(Map.terrainGrid.TerrainAt(loc)))
            {
                return AcceptanceReport.WasRejected;
            }
            return AcceptanceReport.WasAccepted;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map) || c.Impassable(Map)) return;

            TerraformPath path = DesignationTerraform.PathFor(c.GetTerrain(Map));

            if (path == null) return;

            if(DebugSettings.godMode)
            {
                //Under godmode, skip to the end and simply place the last result.
                GenSpawn.Spawn(ThingMaker.MakeThing(path.steps.Last()), c, Map, WipeMode.FullRefund);
                return;
            }

            List<ThingDef> queue = path.steps.ListFullCopy();

            /*String str = "Designating terraform from " + c.GetTerrain(Map).defName + " to " + DesignationTerraform.target.defName + "; steps";
            foreach (ThingDef step in path.steps)
            {
                str += " " + step.defName;
            }
            Log.Message(str);*/

            Blueprint_Build blueprint = GenConstruct.PlaceBlueprintForBuild_NewTemp(queue.First(), c, Map, Rot4.North, Faction.OfPlayer, null);
            if(queue.Count > 1)
            {
                queue.RemoveAt(0);
                blueprint.AllComps.Add(new Comp_QueuedTerraformingSteps(queue));

                /*Comp_QueuedTerraformingSteps queued = blueprint.GetComp<Comp_QueuedTerraformingSteps>();
                if(queued == null)
                {
                    Log.Message("Blueprint did not recieve expected queue comp.");
                }*/
            }
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }

        public override void RenderHighlight(List<IntVec3> dragCells)
        {
            DesignatorUtility.RenderHighlightOverSelectableCells(this, dragCells);
        }

        public static readonly Vector2 DragPriceDrawOffset = new Vector2(19, 17);

        public const float RowHeight = 29;

        public override void DrawMouseAttachments()
        {
            base.DrawMouseAttachments();
            if (ArchitectCategoryTab.InfoRect.Contains(UI.MousePositionOnUIInverted))
            {
                return;
            }

            workingCells.Clear();
            totalCost.Clear();
            totalYield.Clear();

            DesignationDragger dragger = Find.DesignatorManager.Dragger;

            if(dragger.Dragging)
            {
                workingCells.AddRange(dragger.DragCells);
            }
            else if(CanDesignateCell(UI.MouseCell()).Accepted)
            {
                workingCells.Add(UI.MouseCell());
            }

            foreach (IntVec3 cell in workingCells)
            {
                TerraformPath path = DesignationTerraform.PathFor(cell.GetTerrain(Map));

                totalCost = TerraformPath.Merge(totalCost, path.totalCost);
                totalYield = TerraformPath.Merge(totalYield, path.totalYield);
            }

            if (totalCost.Count == 0 && totalYield.Count == 0) return; // no cost + no yield = no tooltip

            Vector2 vector = Event.current.mousePosition + DragPriceDrawOffset;
            float offsetY = 0f;
            
            if (totalCost.Count > 0)
            {
                float y = vector.y + offsetY;
                Rect rect = new Rect(vector.x, y, 999f, 29f);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rect, "RFF.Cost".Translate());
                Text.Anchor = TextAnchor.UpperLeft;

                offsetY += RowHeight;

                foreach (ThingDefCountClass item in totalCost)
                {
                    y = vector.y + offsetY;
                    Widgets.ThingIcon(new Rect(vector.x, y, 27f, 27f), item.thingDef);
                    rect = new Rect(vector.x + 29f, y, 999f, 29f);
                    string text = item.count.ToString();
                    if(Map.resourceCounter.GetCount(item.thingDef) < item.count)
                    {
                        GUI.color = Color.red;
                        text += " (" + "NotEnoughStoredLower".Translate() + ")";
                    }
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(rect, text);
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    offsetY += RowHeight;
                }
            }

            if(totalYield.Count > 0)
            {
                float y = vector.y + offsetY;
                Rect rect = new Rect(vector.x, y, 999f, 29f);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rect, "RFF.Yield".Translate());
                Text.Anchor = TextAnchor.UpperLeft;

                offsetY += RowHeight;

                foreach (ThingDefCountClass item in totalYield)
                {
                    y = vector.y + offsetY;
                    Widgets.ThingIcon(new Rect(vector.x, y, 27f, 27f), item.thingDef);
                    rect = new Rect(vector.x + 29f, y, 999f, 29f);
                    string text = item.count.ToString();
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(rect, text);
                    Text.Anchor = TextAnchor.UpperLeft;
                    //GUI.color = Color.white;
                    offsetY += RowHeight;
                }
            }

            /*for (int i = 0; i < list.Count; i++)
            {
                //ThingDefCountClass thingDefCountClass = list[i];
                //float y = vector.y + offsetY;
                //Widgets.ThingIcon(new Rect(vector.x, y, 27f, 27f), thingDefCountClass.thingDef);
                //Rect rect = new Rect(vector.x + 29f, y, 999f, 29f);
                int num3 = cells * thingDefCountClass.count;
                string text = num3.ToString();
                if (base.Map.resourceCounter.GetCount(thingDefCountClass.thingDef) < num3)
                {
                }
                offsetY += 29f;
            }*/
        }
    }

    [DefOf]
    public static class DesignationDefOf
    {
        public static DesignationDef_Terraform Stone;

        public static DesignationDef_Terraform RockySoil;

        public static DesignationDef_Terraform Sand;

        public static DesignationDef_Terraform Gravel;

        public static DesignationDef_Terraform PackedDirt;

        public static DesignationDef_Terraform Soil;

        public static DesignationDef_Terraform Mud;

        public static DesignationDef_Terraform MarshySoil;

        public static DesignationDef_Terraform Marsh;

        public static DesignationDef_Terraform WaterShallow;

        public static DesignationDef_Terraform WaterDeep;

        public static DesignationDef_Terraform RichSoil;

        public static DesignationDef_Terraform TilledSoil;

        public static DesignationDef_Terraform Topsoil;

        public static DesignationDef_Terraform RichTopsoil;

        static DesignationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DesignationDefOf));
        }
    }

    public class Designator_Terraform_Stone : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.Stone;
    }

    public class Designator_Terraform_RockySoil : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.RockySoil;
    }

    public class Designator_Terraform_Sand : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.Sand;
    }

    public class Designator_Terraform_Gravel : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.Gravel;
    }

    public class Designator_Terraform_PackedDirt : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.PackedDirt;
    }

    public class Designator_Terraform_Soil : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.Soil;
    }

    public class Designator_Terraform_Mud : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.Mud;
    }

    public class Designator_Terraform_MarshySoil : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.MarshySoil;
    }

    public class Designator_Terraform_Marsh : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.Marsh;
    }

    public class Designator_Terraform_WaterShallow : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.WaterShallow;
    }

    public class Designator_Terraform_WaterDeep : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.WaterDeep;
    }

    public class Designator_Terraform_RichSoil : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.RichSoil;
    }

    public class Designator_Terraform_TilledSoil : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.TilledSoil;
    }

    public class Designator_Terraform_Topsoil : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.Topsoil;
    }

    public class Designator_Terraform_RichTopsoil : Designator_Terraform
    {
        public override DesignationDef_Terraform DesignationTerraform => DesignationDefOf.RichTopsoil;
    }

    public class DesignationCategoryExtension: DefModExtension
    {
        public DesignatorDropdownGroupDef group;
    }

    [HarmonyPatch(typeof(DesignationCategoryDef), "ResolveDesignators")]
    public class DesignatorCategoryResolve
    {
        //TODO - replace destructive prefix with an awful, awful, awful transpiler

        public static bool Prefix(DesignationCategoryDef __instance, List<Designator> ___resolvedDesignators)
        {
            ___resolvedDesignators.Clear();
            // move this dictionary definition up from between the two foreaches
            Dictionary<DesignatorDropdownGroupDef, Designator_Dropdown> dictionary = new Dictionary<DesignatorDropdownGroupDef, Designator_Dropdown>();
            foreach (Type specialDesignatorClass in __instance.specialDesignatorClasses)
            {
                Designator designator = null;
                try
                {
                    designator = (Designator)Activator.CreateInstance(specialDesignatorClass);
                    designator.isOrder = true;
                }
                catch (Exception ex)
                {
                    Log.Error("DesignationCategoryDef" + __instance.defName + " could not instantiate special designator from class " + specialDesignatorClass + ".\n Exception: \n" + ex.ToString());
                }
                if (designator != null)
                {
                    // add this whole if statement
                    if (designator is Designator_Terraform d_t && d_t?.DesignationTerraform?.GetModExtension<DesignationCategoryExtension>()?.group is DesignatorDropdownGroupDef group)
                    {
                        if (!dictionary.ContainsKey(group))
                        {
                            dictionary[group] = new Designator_Dropdown();
                            ___resolvedDesignators.Add(dictionary[group]);
                        }
                        dictionary[group].Add(designator);
                    }
                    else
                    {
                        // this is what used to be here
                        ___resolvedDesignators.Add(designator);
                    }
                }
            }
            IEnumerable<BuildableDef> enumerable = from tDef in DefDatabase<TerrainDef>.AllDefs.Cast<BuildableDef>().Concat(DefDatabase<ThingDef>.AllDefs.Cast<BuildableDef>())
                                                   where tDef.designationCategory == __instance && tDef.canGenerateDefaultDesignator
                                                   select tDef;
            foreach (BuildableDef item in enumerable)
            {
                if (item.designatorDropdown != null)
                {
                    if (!dictionary.ContainsKey(item.designatorDropdown))
                    {
                        dictionary[item.designatorDropdown] = new Designator_Dropdown();
                        ___resolvedDesignators.Add(dictionary[item.designatorDropdown]);
                    }
                    dictionary[item.designatorDropdown].Add(new Designator_Build(item));
                }
                else
                {
                    ___resolvedDesignators.Add(new Designator_Build(item));
                }
            }
            return false;
        }
    }
}
