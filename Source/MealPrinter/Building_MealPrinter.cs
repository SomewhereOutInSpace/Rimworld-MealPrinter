using System;
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

        private const float barNutritionCost = 0.5f;
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

        //Gizmos
        public override IEnumerable<Gizmo> GetGizmos() 
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (mealToPrint == null) //New printers will have null for mealToPrint so this catches that
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

            float num = GetNutritionCost();

            List<ThingDef> list = new List<ThingDef>();
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

        //Overriden base HasEnoughFeedstock method
        //This version considers the selected meal type
        public override bool HasEnoughFeedstockInHoppers()
        {
            float num = 0f;
            for (int i = 0; i < AdjCellsCardinalInBounds.Count; i++)
            {
                IntVec3 c = AdjCellsCardinalInBounds[i];
                Thing thing = null;
                Thing thing2 = null;
                List<Thing> thingList = c.GetThingList(base.Map);
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
                    num += (float)thing.stackCount * thing.GetStatValue(StatDefOf.Nutrition);
                }
                if (num >= GetNutritionCost())
                {
                    return true;
                }
            }
            return false;
        }
        
        public List<Thing> GetAllHopperedFeedstock() {
            List<Thing> allStock = new List<Thing>();
            for (int i = 0; i < AdjCellsCardinalInBounds.Count; i++)
            {
                Building edifice = AdjCellsCardinalInBounds[i].GetEdifice(base.Map);
                if (edifice != null && edifice.def == ThingDefOf.Hopper)
                {
                    List<Thing> thingList = edifice.Position.GetThingList(base.Map);
                    for (int j = 0; j < thingList.Count; j++)
                    {
                        Thing thing = thingList[j];
                        if (IsAcceptableFeedstock(thing.def))
                        {
                            allStock.Add(thing);
                        }
                    }
                }
            }
            return allStock;
        }

        //Convert a given list of feedstock stacks into its equivalent in NutriBars
        public int FeedstockBarEquivalent(List<Thing> feedStocks) {
            float num = 0f;
            for (int i = 0; i < feedStocks.Count; i++) {
                num += (float)feedStocks[i].stackCount * feedStocks[i].GetStatValue(StatDefOf.Nutrition);
            }
            return (int)Math.Floor(num / barNutritionCost);
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
                    if ((thing.GetStatValue(StatDefOf.Nutrition) * thing.stackCount) >= barNutritionCost) {
                        return thing;
                    }
                }
            }
            return null;
        }

        //Bulk bar printing button method
        private void TryBulkPrintBars() {
            List<Thing> feedStock = GetAllHopperedFeedstock();

            if (feedStock == null || FeedstockBarEquivalent(GetAllHopperedFeedstock()) <= 0) {
                Messages.Message("CannotBulkPrintBars".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            int maxPossibleBars = FeedstockBarEquivalent(feedStock);
            int maxAllowedBars = 30;
            if (maxPossibleBars > maxAllowedBars) {
                maxPossibleBars = 30;
            }

            Func<int, string> textGetter;
            textGetter = ((int x) => "SetBarBatchSize".Translate(x, maxAllowedBars));
            Dialog_PrintBars window = new Dialog_PrintBars(textGetter, 1, maxPossibleBars, delegate (int x, bool forbidden, bool rear)
            {
                ConfirmAction(x, feedStock, forbidden, rear);
            }, 1);
            Find.WindowStack.Add(window);
        }

        //Bulk bar printing GUI
        public void ConfirmAction(int x, List<Thing> feedStock, bool forbidden, bool rear)
        {
            def.building.soundDispense.PlayOneShot(new TargetInfo(base.Position, base.Map, false));

            float nutritionCost = x * barNutritionCost;

            float nutritionRemaining = nutritionCost;
            List<ThingDef> list = new List<ThingDef>();
            do
            {
                Thing feed = FindFeedInAnyHopper();
                int nutritionToConsume = Mathf.Min(feed.stackCount, Mathf.CeilToInt(barNutritionCost / feed.GetStatValue(StatDefOf.Nutrition)));
                nutritionRemaining -= (float)nutritionToConsume * feed.GetStatValue(StatDefOf.Nutrition);
                list.Add(feed.def);
                feed.SplitOff(nutritionToConsume);

                Thing bars = ThingMaker.MakeThing(MealPrinter_ThingDefOf.MealPrinter_NutriBar);
                CompIngredients compIngredients = bars.TryGetComp<CompIngredients>();
                for (int i = 0; i < list.Count; i++)
                {
                    compIngredients.RegisterIngredient(list[i]);
                }
                if (rear)
                {
                    //0 1 2 3
                    //facing up right down left
                    String rot = this.Rotation.ToString();
                    IntVec3 targetCell = InteractionCell;
                    if (rot.Equals("0"))
                    {
                        targetCell.z -= 6;
                        GenPlace.TryPlaceThing(bars, targetCell, Map, ThingPlaceMode.Near);
                    }
                    else if (rot.Equals("1"))
                    {
                        targetCell.x -= 6;
                        GenPlace.TryPlaceThing(bars, targetCell, Map, ThingPlaceMode.Near);
                    }
                    else if (rot.Equals("2"))
                    {
                        targetCell.z += 6;
                        GenPlace.TryPlaceThing(bars, targetCell, Map, ThingPlaceMode.Near);
                    }
                    else if (rot.Equals("3"))
                    {
                        targetCell.x += 6;
                        GenPlace.TryPlaceThing(bars, targetCell, Map, ThingPlaceMode.Near);
                    }
                    else
                    {
                        Log.Error("[MealPrinter] Couldn't get printer rotation, falling back onto Interaction Cell.");
                        GenPlace.TryPlaceThing(bars, InteractionCell, Map, ThingPlaceMode.Near);
                    }
                }
                else {
                    GenPlace.TryPlaceThing(bars, InteractionCell, Map, ThingPlaceMode.Near);
                }

                if (forbidden) {
                    bars.SetForbidden(true);
                }
            }

            while (!(nutritionRemaining <= 0f));
            def.building.soundDispense.PlayOneShot(new TargetInfo(base.Position, base.Map));

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
        private String GetEfficiency() {
            if (mealToPrint == ThingDef.Named("MealSimple"))
            {
                return "SimpleMealEff".Translate();
            }
            else if (mealToPrint == ThingDef.Named("MealFine"))
            {
                return "FineMealEff".Translate();
            }
            else
            {
                return "PasteMealEff".Translate();
            }
        }

        //Get nutrition cost for each potential meal type
        private float GetNutritionCost() {
            float num = 0.3f;
            if (mealToPrint.Equals(ThingDef.Named("MealNutrientPaste")))
            {
                num = (float)((def.building.nutritionCostPerDispense * 0.5) - 0.0001f);
            }
            else if (mealToPrint.Equals(ThingDef.Named("MealSimple")))
            {
                num = 0.5f - 0.0001f;
            }
            else if (mealToPrint.Equals(ThingDef.Named("MealFine")))
            {
                num = 0.75f - 0.0001f;
            }
            return num;
        }

        //Get the ThingDef of the current meal
        public ThingDef GetMealThing() {
            return mealToPrint;
        }

        //Check if pawn's food restrictions allow consumption of the set meal
        public bool CanPawnPrint(Pawn p) {
            return p.foodRestriction.CurrentFoodRestriction.Allows(mealToPrint);
        }

    }



}
