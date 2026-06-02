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

        // Remove all weapons from pod/crate loot
        public static bool removePodWeapons = true;

        // Armor (apparel) HP nerf on loot — randomized like the weapon nerf, but the curve power
        // scales with the "tech gap" between the armor and the player's dev level.
        // The armor's tech tier is derived from its Combat Extended armor rating (Sharp/Blunt),
        // because the modpack's techLevel tags are unreliable.
        // TechLevel enum: Undefined=0, Animal=1, Neolithic=2, Medieval=3, Industrial=4, Spacer=5, Ultra=6, Archotech=7
        public static bool armorHpNerfEnabled = true;
        public static float armorHpMin = 0.15f;          // floor multiplier (best-case durability at huge gap)
        // Manual Sharp thresholds -> armor tech tier (below Medieval = Neolithic=2).
        // Armor's tier is a step function of its CE Sharp rating (techLevel tags are unreliable here).
        public static float armorSharpMedieval = 3f;     // Sharp >= 3  -> Medieval (3)
        public static float armorSharpIndustrial = 8f;   // Sharp >= 8  -> Industrial (4)
        public static float armorSharpSpacer = 16f;      // Sharp >= 16 -> Spacer (5)
        public static float armorSharpUltra = 26f;       // Sharp >= 26 -> Ultra (6)
        public static float armorGapPower = 2f;          // roll-curve power per tier of tech gap

        // Fishing
        public static int maxFishingPiers = 3;
        public static int maxFishTraps = 5;
        public static float fishSpawnSlowdown = 1f; // множитель замедления пополнения рыбы
    }
}
