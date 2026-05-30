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

        // Fishing
        public static int maxFishingPiers = 3;
        public static int maxFishTraps = 5;
        public static float fishSpawnSlowdown = 1f; // множитель замедления пополнения рыбы
    }
}
