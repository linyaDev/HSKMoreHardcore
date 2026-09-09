using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Игровые настройки мода (Options -> Mod settings -> HSK More Hardcore).
    // Здесь только переключатели поведения; числовые константы баланса живут
    // в Defs/Misc/HardcoreSettings.xml.
    public class HardcoreModSettings : ModSettings
    {
        // Микробную грязь болезней нельзя убирать первые 2 дня (DiseaseFilthNerf).
        // Выключено — микробы чистятся сразу, как в стоковом HSK.
        public bool diseaseFilthNoClean = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref diseaseFilthNoClean, "diseaseFilthNoClean", true);
        }
    }

    public class HSKMoreHardcoreMod : Mod
    {
        public static HardcoreModSettings Settings { get; private set; }

        public HSKMoreHardcoreMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HardcoreModSettings>();
        }

        public override string SettingsCategory() => "HSK More Hardcore";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("HMH_DiseaseFilthNoClean".Translate(),
                ref Settings.diseaseFilthNoClean,
                "HMH_DiseaseFilthNoCleanTip".Translate());
            list.End();
        }
    }
}
