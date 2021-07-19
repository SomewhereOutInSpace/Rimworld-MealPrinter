﻿using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MealPrinter {
    public class Building_MealPrinter : RimWorld.Building_NutrientPasteDispenser
    {
		private ThingDef mealToPrint;

        private CompMealPrinter mealPrinterComp;

        public static List<ThingDef> validMeals = new List<ThingDef>();

        static Building_MealPrinter() {
            validMeals.Add(ThingDef.Named("MealSimple"));
        }

        //Set target meal after reload
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            mealPrinterComp = GetComp<CompMealPrinter>();
            mealToPrint = ThingDef.Named(mealPrinterComp.GetMealToPrint());
        }

        //Inspect pane string
        public override string GetInspectString()
        {
            if (mealToPrint == null) {
                mealToPrint = ThingDef.Named("MealSimple");
            }

            string text = base.GetInspectString();
            text = text + "CurrentPrintSetting".Translate(mealToPrint.label);
            text = text + "CurrentEfficiency".Translate(GetEfficiency());
            return text;
        }

        /*public override Color DrawColor
        {
            get
            {
                return new Color(33f / 85f, 53f / 85f, 226f / 255f);
            }
        }*/

        //Gizmos
        public override IEnumerable<Gizmo> GetGizmos()
        {            
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (mealToPrint == null)
            {
                mealToPrint = ThingDef.Named("MealSimple");
            }

            if (MealPrinter_ThingDefOf.MealPrinter_HighRes.IsFinished && !validMeals.Contains(ThingDef.Named("MealFine")))
            {
                validMeals.Add(ThingDef.Named("MealFine"));
            }

            if (MealPrinter_ThingDefOf.MealPrinter_Recombinators.IsFinished && !validMeals.Contains(ThingDef.Named("MealNutrientPaste")))
            {
                validMeals.Add(ThingDef.Named("MealNutrientPaste"));
            }

            yield return new Command_Action()
            {
                
                defaultLabel = "PrintSettingButton".Translate(mealToPrint.label),
                defaultDesc = GetMealDesc(),
                icon = getMealIcon(),
                order = -100,
                action = delegate ()
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    if (validMeals != null)
                    {
                        foreach (ThingDef meal in validMeals)
                        {
                            string label = meal.LabelCap;
                            FloatMenuOption option = new FloatMenuOption(label, delegate ()
                            {
                                SetMealToPrint(meal);
                            });
                            options.Add(option);
                        }
                    }

                    if (options.Count > 0)
                    {
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
            };

            if (MealPrinter_ThingDefOf.MealPrinter_DeepResequencing.IsFinished)
            {
                if (!this.powerComp.PowerOn)
                {
                    yield return new Command_Action()
                    {
                        defaultLabel = "ButtonBulkPrintBars".Translate(),
                        defaultDesc = "ButtonBulkPrintBarsDescNoPower".Translate(),
                        disabled = true,
                        icon = ContentFinder<Texture2D>.Get("UI/Buttons/NutriBar", true),
                        order = -100,
                        action = delegate ()
                        {
                            TryBulkPrintBars();
                        }
                    };
                }
                else {
                    yield return new Command_Action()
                    {
                        defaultLabel = "ButtonBulkPrintBars".Translate(),
                        defaultDesc = "ButtonBulkPrintBarsDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Buttons/NutriBar", true),
                        order = -100,
                        action = delegate ()
                        {
                            TryBulkPrintBars();
                        }
                    };
                }
            }
            else {
                yield return new Command_Action()
                {
                    defaultLabel = "ButtonBulkPrintBars".Translate(),
                    defaultDesc = "ButtonBulkPrintBarsDescNoResearch".Translate(),
                    disabled = true,
                    icon = ContentFinder<Texture2D>.Get("UI/Buttons/NutriBar", true),
                    order = -100,
                    action = delegate ()
                    {
                        TryBulkPrintBars();
                    }
                };
            }
        }

        //Util functions

        //Overriden base TryDispenseFood method
        public override Thing TryDispenseFood()
        {
            if (!CanDispenseNow)
            {
                return null;
            }

            float num = def.building.nutritionCostPerDispense - 0.0001f;
            if (mealToPrint.Equals(ThingDef.Named("MealNutrientPaste")))
            {
                num = (float)((def.building.nutritionCostPerDispense * 0.5) - 0.0001f);
            }
            else if (mealToPrint.Equals(ThingDef.Named("MealSimple")))
            {
                num = 0.5f - 0.0001f;
            }
            else if (mealToPrint.Equals(ThingDef.Named("MealFine"))) {
                num = 0.75f - 0.0001f;
            }

            List < ThingDef > list = new List<ThingDef>();
            do
            {
                Thing thing = FindFeedInAnyHopper();
                if (thing == null)
                {
                    Log.Error("Did not find enough food in hoppers while trying to dispense.");
                    return null;
                }
                int num2 = Mathf.Min(thing.stackCount, Mathf.CeilToInt(num / thing.GetStatValue(StatDefOf.Nutrition)));
                num -= (float)num2 * thing.GetStatValue(StatDefOf.Nutrition);
                list.Add(thing.def);
                thing.SplitOff(num2);
            }

            while (!(num <= 0f));
            def.building.soundDispense.PlayOneShot(new TargetInfo(base.Position, base.Map));
            Thing thing2 = ThingMaker.MakeThing(mealToPrint);
            CompIngredients compIngredients = thing2.TryGetComp<CompIngredients>();
            for (int i = 0; i < list.Count; i++)
            {
                compIngredients.RegisterIngredient(list[i]);
            }

            return thing2;
        }

        //Convert a given stack of feedstock into its equivalent in NutriBars
        public int FeedstockBarEquivalent(Thing feedStock) {
            float num = 0f;
            num += (float)feedStock.stackCount * feedStock.GetStatValue(StatDefOf.Nutrition);
            return (int)Math.Floor(num / 0.375f);
        }

        //Returns a valid hopper that also has enough feed for at least one NutriBar
        public Thing FindHopperWithEnoughFeedForBar()
        {
            for (int i = 0; i < AdjCellsCardinalInBounds.Count; i++)
            {
                Thing thing = null;
                Thing thing2 = null;
                List<Thing> thingList = AdjCellsCardinalInBounds[i].GetThingList(base.Map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    Thing thing3 = thingList[j];
                    if (IsAcceptableFeedstock(thing3.def))
                    {
                        thing = thing3;
                    }
                    if (thing3.def == ThingDefOf.Hopper)
                    {
                        thing2 = thing3;
                    }
                }
                if (thing != null && thing2 != null)
                {
                    if ((thing.GetStatValue(StatDefOf.Nutrition) * thing.stackCount) >= 0.375f) {
                        return thing;
                    }
                }
            }
            return null;
        }

        //Bulk bar printing button method
        private void TryBulkPrintBars() {
            Thing stack = FindHopperWithEnoughFeedForBar();
            if (stack == null || !CanDispenseNow) { 
                Messages.Message("CannotBulkPrintBars".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            float totalAvailableFeedstock = stack.stackCount;
            int maxPossibleBars = FeedstockBarEquivalent(stack);

            Func<int, string> textGetter;
            textGetter = ((int x) => "SetBarBatchSize".Translate(x, maxPossibleBars));
            Dialog_Slider window = new Dialog_Slider(textGetter, 1, maxPossibleBars, delegate (int x)
            {
                ConfirmAction(x, stack);
            }, 1);
            Find.WindowStack.Add(window);
        }

        //Bulk bar printing GUI
        public void ConfirmAction(int x, Thing feedStock)
        {
            def.building.soundDispense.PlayOneShot(new TargetInfo(base.Position, base.Map, false));

            float nutritionCost = x * 0.375f;
            int feedstockCost = (int)Math.Floor(nutritionCost/feedStock.GetStatValue(StatDefOf.Nutrition));
            feedStock.SplitOff(feedstockCost);

            Thing t = ThingMaker.MakeThing(MealPrinter_ThingDefOf.MealPrinter_NutriBar, null);
            t.stackCount = x;
            GenPlace.TryPlaceThing(t, InteractionCell, Map, ThingPlaceMode.Near);
        }

        //Internally define set meal
        private void SetMealToPrint(ThingDef mealDef) {
            mealToPrint = mealDef;
            mealPrinterComp.SetMealToPrint(mealToPrint.defName);
        }

        //Get meal icon for gizmo
        private Texture2D getMealIcon() {
            if (mealToPrint == ThingDef.Named("MealSimple"))
            {
                return ContentFinder<Texture2D>.Get("UI/Buttons/MealSimple", true);
            }
            else if (mealToPrint == ThingDef.Named("MealFine"))
            {
                return ContentFinder<Texture2D>.Get("UI/Buttons/MealFine", true);
            }
            else 
            {
                return ContentFinder<Texture2D>.Get("UI/Buttons/MealNutrientPaste", true);
            }
        }

        //Get meal desc for gizmo
        private string GetMealDesc() {
            if (mealToPrint == ThingDef.Named("MealSimple"))
            {
                return "SimpleMealDesc".Translate();
            }
            else if (mealToPrint == ThingDef.Named("MealFine"))
            {
                return "FineMealDesc".Translate();
            }
            else
            {
                return "PasteMealDesc".Translate();
            }
        }

        //Get print efficiency for inspect pane
        private int GetEfficiency() {
            if (mealToPrint == ThingDef.Named("MealSimple"))
            {
                return 50;
            }
            else if (mealToPrint == ThingDef.Named("MealFine"))
            {
                return 25;
            }
            else
            {
                return 85;
            }
        }

        //Get the ThingDef of the current meal
        public ThingDef GetMealThing() {
            return mealToPrint;
        }

    }



}
