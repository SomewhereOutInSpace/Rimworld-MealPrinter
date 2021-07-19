using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;
using System.Reflection;

namespace MealPrinter
{
    public class MealPrinterMod : Mod
    {

        public static bool allowForbidden;
        public static bool allowDispenserFull;
        public static Pawn getter;
        public static Pawn eater;
        public static bool allowSociallyImproper;
        public static bool BestFoodSourceOnMap;

        public MealPrinterMod(ModContentPack content) : base(content)
        {
            Log.Message("[MealPrinter] Okay, showtime!");          
            Harmony har = new Harmony("MealPrinter");
            har.PatchAll(Assembly.GetExecutingAssembly());
        }

    }
}
