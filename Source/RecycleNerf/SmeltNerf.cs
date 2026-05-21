using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class SmeltNerf
    {
        private static HashSet<string> bannedSmeltProducts;

        static SmeltNerf()
        {
            bannedSmeltProducts = new HashSet<string>
            {
                "Weapon_Parts",
                "Pistol_Component",
                "SMG_Component",
                "Shotgun_Component",
                "Rifle_Component",
                "AdvRifle_Component",
                "Sniper_Component",
                "AdvSniper_Component",
                "Heavy_Component",
                "Cannon_Component",
                "Launcher_Component",
                "Charged_Component",
                "Plasma_Component",
                "Laser_Component"
            };

            var harmony = new Harmony("linya.hskmorehardcore.smeltnerf");

            var smeltProducts = AccessTools.Method(typeof(Thing), "SmeltProducts");
            if (smeltProducts != null)
            {
                harmony.Patch(smeltProducts,
                    postfix: new HarmonyMethod(typeof(SmeltNerf), nameof(Postfix)));
                Log.Message("[HSKMoreHardcore] SmeltNerf applied.");
            }
        }

        private static ThingDef steelBarDef;

        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result)
        {
            bool replaced = false;
            foreach (var thing in __result)
            {
                if (bannedSmeltProducts.Contains(thing.def.defName))
                {
                    Log.Message($"[SmeltNerf] Skipped {thing.def.defName} x{thing.stackCount}");

                    if (!replaced)
                    {
                        if (steelBarDef == null)
                            steelBarDef = DefDatabase<ThingDef>.GetNamedSilentFail("SteelBar");

                        if (steelBarDef != null)
                        {
                            Thing steel = ThingMaker.MakeThing(steelBarDef);
                            steel.stackCount = 5;
                            Log.Message($"[SmeltNerf] Added SteelBar x3");
                            yield return steel;
                        }
                        replaced = true;
                    }
                    continue;
                }
                yield return thing;
            }
        }
    }
}
