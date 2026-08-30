using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Горящая пешка поджигает крышу Stratum слишком охотно: их Fire_Patch на
    // каждый расчётный тик любого огня зовёт TryIgniteRoof с шансом
    // fireSize * горючесть_крыши * 0.5. Префиксом пропускаем 4 из 5 попыток,
    // если огонь прикреплён к пешке — шанс поджога от пешки падает в 5 раз.
    // Огонь на земле и искры вбок (TrySpread) не трогаем.
    [StaticConstructorOnStartup]
    public static class RoofFireNerf
    {
        private const float PawnFireIgniteFactor = 0.2f;

        static RoofFireNerf()
        {
            var target = AccessTools.Method("SolarWeb.Stratum.Patches.RimWorld.Fire_Patch:TryIgniteRoof");
            if (target == null)
            {
                Log.Warning("[HSKMoreHardcore] RoofFireNerf: Stratum Fire_Patch.TryIgniteRoof not found, nerf disabled.");
                return;
            }

            var harmony = new Harmony("linya.hskmorehardcore.rooffirenerf");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(RoofFireNerf), nameof(Prefix)));
            Log.Message("[HSKMoreHardcore] RoofFireNerf applied.");
        }

        public static bool Prefix(Fire groundFire)
        {
            if (groundFire?.parent is Pawn && !Rand.Chance(PawnFireIgniteFactor))
                return false;
            return true;
        }
    }
}
