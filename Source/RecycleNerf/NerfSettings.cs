using System.Collections.Generic;

namespace HSKMoreHardcore
{
    public static class NerfSettings
    {
        public static float ammoDropMultiplier = 0.2f;
        public static float medicineDropMultiplier = 0.1f;

        // Away from home map (quest sites, etc.)
        public static float ammoDropMultiplierAway = 0.05f;
        public static float medicineDropMultiplierAway = 0.05f;
        public static float weaponHpMultiplierHomeMin = 0.15f;
        public static float weaponHpMultiplierHomeMax = 1.0f;
        public static float weaponHpCurvePower = 3f;
        public static float weaponHpMultiplierAway = 0.05f; // 5% HP

        // Trader ammo
        public static float traderAmmoMultiplier = 0.15f;
        public static float rewardAmmoMultiplier = 0.15f;
        public static float drugDropMultiplier = 0.33f;
        public static float podDrugMultiplier = 0.5f;
        public static float ammoPriceMultiplier = 4f;
        public static float weaponPriceMultiplier = 2f;

        // Banned materials in pods
        public static HashSet<string> bannedPodMaterials = new HashSet<string>
        {
            "AlphaPoly"
        };

        // Armor (apparel) HP nerf on loot — scales with armor protection and player dev level.
        // Uses Combat Extended armor scale (Sharp ~mmRHA, Blunt ~MPa) — the two are NOT summed,
        // each is normalized against a top-tier reference (cataphract) and the max is used.
        public static bool armorHpNerfEnabled = true;
        public static float armorHpMin = 0.15f;          // floor multiplier at maximum nerf
        public static float armorSharpRef = 28f;         // CE Sharp of top-tier armor = "fully protective"
        public static float armorBluntRef = 60f;         // CE Blunt of top-tier armor = "fully protective"
        public static int armorDevLevelMin = 1;          // bottom of the scale = Neolithic (Animal treated as Neolithic)
        public static int armorDevLevelRef = 4;          // dev level (Spacer) at which the nerf fully fades

        // Fishing
        public static int maxFishingPiers = 3;
        public static int maxFishTraps = 5;
        public static float fishSpawnSlowdown = 1f; // множитель замедления пополнения рыбы
    }
}
