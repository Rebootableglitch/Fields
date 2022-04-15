using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using UnityEngine;
using System.Diagnostics;

namespace RFF_Code
{
    [StaticConstructorOnStartup]
    public class Building_CompostBarrel : Building
    {
        private Material barFilledCachedMat;
        private static readonly Vector2 BarSize = new Vector2(0.55f, 0.1f);
        private static readonly Color BarZeroProgressColor = new Color(0.4f, 0.27f, 0.22f);
        private static readonly Color BarFermentedColor = new Color(0.9f, 0.85f, 0.2f);
        private static readonly Material BarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));

        private int compostcount;
        private float progressInt;
        public CompPowerTrader compPowerTrader;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            compPowerTrader = GetComp<CompPowerTrader>();
        }

        public float Progress
        {
            get
            {
                return progressInt;
            }
            set
            {
                if (value == progressInt)
                {
                    return;
                }
                progressInt = value;
                barFilledCachedMat = null;
            }
        }

        private Material BarFilledMat
        {
            get
            {
                if (barFilledCachedMat == null)
                {
                    barFilledCachedMat = SolidColorMaterials.SimpleSolidColorMaterial(Color.Lerp(BarZeroProgressColor, BarFermentedColor, Progress));
                }
                return barFilledCachedMat;
            }
        }

        private float Temperature
        {
            get
            {
                if (MapHeld == null)
                {
                    Log.ErrorOnce("RFF.NullMapHeld".Translate(), 847163513);
                    return 7f;
                }
                return PositionHeld.GetTemperature(MapHeld);
            }
        }

        public int SpaceLeftForCompost
        {
            get
            {
                if (Distilled)
                {
                    return 0;
                }
                return 30 - compostcount;
            }
        }
        private bool Empty
        {
            get
            {
                return compostcount <= 0;
            }
        }
        public bool Distilled
        {
            get
            {
                return !Empty && Progress >= 1f;
            }
        }
        private float CurrentTempProgressSpeedFactor
        {
            get
            {
                CompProperties_TemperatureRuinable compProperties = def.GetCompProperties<CompProperties_TemperatureRuinable>();
                float temperature = Temperature;
                if (temperature < compProperties.minSafeTemperature)
                {
                    return 0.1f;
                }
                if (temperature < 7f)
                {
                    return GenMath.LerpDouble(compProperties.minSafeTemperature, 7f, 0.1f, 1f, temperature);
                }
                return 1f;
            }
        }

        private float ProgressPerTickAtCurrentTemp
        {
            get
            {
                float progPerTick = 5.55555555E-06f * CurrentTempProgressSpeedFactor;
                if (!this.compPowerTrader.PowerOn)
                {
                    progPerTick = progPerTick / 2;
                }
                return progPerTick;
            }
        }

        private int EstimatedTicksLeft
        {
            get
            {
                return Mathf.Max(Mathf.RoundToInt((1f - Progress) / ProgressPerTickAtCurrentTemp), 0);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref compostcount, "compostcount", 0, false);
            Scribe_Values.Look(ref progressInt, "progress", 0f, false);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!Empty)
            {
                Progress = Mathf.Min(Progress + 250f * ProgressPerTickAtCurrentTemp, 1f);
            }
        }
        public void Addoil(int count)
        {
            if (this.Distilled)
            {
                Log.Warning("RFF.FullOfFert".Translate());
                return;
            }
            int num = Mathf.Min(count, 30 - this.compostcount);
            if (num <= 0)
            {
                return;
            }
            this.Progress = GenMath.WeightedAverage(0f, (float)num, this.Progress, (float)this.compostcount);
            this.compostcount += num;
            base.GetComp<CompTemperatureRuinable>().Reset();
        }

        protected override void ReceiveCompSignal(string signal)
        {
            if (signal == "RuinedByTemperature")
            {
                Reset();
            }
        }

        private void Reset()
        {
            compostcount = 0;
            Progress = 0f;
        }

        public void Addoil(Thing RawCompost)
        {
            CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
            if (comp.Ruined)
            {
                comp.Reset();
            }
            this.Addoil(RawCompost.stackCount);
            RawCompost.Destroy(DestroyMode.Vanish);
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(base.GetInspectString());
            if (stringBuilder.Length != 0)
            {
                stringBuilder.AppendLine();
            }
            CompTemperatureRuinable comp = base.GetComp<CompTemperatureRuinable>();
            if (!this.Empty && !comp.Ruined)
            {
                if (this.Distilled)
                {
                    stringBuilder.AppendLine("RFF.ContainsFert".Translate() + " " + (int)this.compostcount + "/" + "30");
                }
                else
                {
                    stringBuilder.AppendLine("RFF.ContainsComp".Translate() + " " + (int)this.compostcount + "/" + "30");
                }
            }
            if (!this.Empty)
            {
                if (this.Distilled)
                {
                    stringBuilder.AppendLine("RFF.Composted".Translate());
                }
                else
                {
                    stringBuilder.AppendLine("RFF.CompProg".Translate() + " " + this.Progress.ToStringPercent() + " ~ " + this.EstimatedTicksLeft.ToStringTicksToPeriod());
                    if (this.CurrentTempProgressSpeedFactor != 1f)
                    {
                        stringBuilder.AppendLine("RFF.OutOfTemp".Translate() + " " + this.CurrentTempProgressSpeedFactor.ToStringPercent());
                    }
                }
            }
            if (base.MapHeld != null)
            {
                stringBuilder.AppendLine(string.Concat(new string[] {
                    "RFF.Temp".Translate() + " " + this.Temperature.ToStringTemperature("F0"),
                    " (" + "RFF.IdealTemp".Translate() + " ",
                    7f.ToStringTemperature("F0"),
                    " ~ ",
                    comp.Props.maxSafeTemperature.ToStringTemperature("F0") + ")"
                }));
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public Thing TakeOutFertilizer()
        {
            if (!this.Distilled)
            {
                Log.Warning("RFF.NotReady".Translate());
                return null;
            }
            Thing thing = ThingMaker.MakeThing(FFDefOf.Fertilizer, null);
            thing.stackCount = this.compostcount;
            this.Reset();
            return thing;
        }

        public override void Draw()
        {
            base.Draw();
            if (!this.Empty)
            {
                Vector3 drawPos = this.DrawPos;
                drawPos.y += 0.05f;
                drawPos.z += 0.25f;
                GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
                {
                    center = drawPos,
                    size = Building_CompostBarrel.BarSize,
                    fillPercent = (float)this.compostcount / 30f,
                    filledMat = this.BarFilledMat,
                    unfilledMat = Building_CompostBarrel.BarUnfilledMat,
                    margin = 0.1f,
                    rotation = Rot4.North
                });
            }
        }

        [DebuggerHidden]
        public override IEnumerable<Gizmo> GetGizmos()
        {
            IEnumerator<Gizmo> enumerator = base.GetGizmos().GetEnumerator();
            while (enumerator.MoveNext())
            {
                Gizmo current = enumerator.Current;
                yield return current;
            }
            if (Prefs.DevMode && !this.Empty)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Debug: Set progress to 1",
                    action = delegate {
                        this.Progress = 1f;
                    }
                };
            }
            yield break;
        }
    }
}
