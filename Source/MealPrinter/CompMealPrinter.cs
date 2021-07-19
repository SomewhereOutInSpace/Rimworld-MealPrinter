using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace MealPrinter
{

    public class CompProperties_MealPrinter : CompProperties
    {
        public CompProperties_MealPrinter()
        {
            compClass = typeof(CompMealPrinter);
        }
    }

    public class CompMealPrinter : ThingComp
    {
        private string mealToPrint = "MealSimple";

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref mealToPrint, "mealToPrint", defaultValue: "MealSimple");
        }

        public string GetMealToPrint()
        {
            return mealToPrint;
        }

        public void SetMealToPrint(String mealDef)
        {
            mealToPrint = mealDef;
        }
    }
}
