namespace HSKMoreHardcore
{
    public static class NerfSettings
    {
        public static float ammoDropMultiplier = 0.2f;
        public static float medicineDropMultiplier = 0.1f;

        // Away from home map (quest sites, etc.)
        public static float ammoDropMultiplierAway = 0.05f;
        public static float medicineDropMultiplierAway = 0.05f;
        public static float weaponHpMultiplierHome = 0.25f; // 25% HP
        public static float weaponHpMultiplierAway = 0.05f; // 5% HP

        // Trader ammo
        public static float traderAmmoMultiplier = 0.15f;
        public static float ammoPriceMultiplier = 4f;
        public static float weaponPriceMultiplier = 2f;

        // Fishing
        public static int maxFishingPiers = 6;
        public static float fishSpawnSlowdown = 1f; // множитель замедления пополнения рыбы
    }
}
