using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Убирает качество у кроватей полностью: качество — это комп CompQuality в
    // дефе, без него нет ни ролла при постройке, ни лейбла, ни влияния на статы.
    // XML-патчем по всем кроватям не попасть — комп наследуется от разных
    // абстрактных баз, поэтому вычищаем по DefDatabase на старте (накрывает и
    // модовые кровати пака). Существующие кровати в сейве теряют качество при
    // загрузке; их сохранённое значение просто игнорируется.
    [StaticConstructorOnStartup]
    public static class BedQualityRemover
    {
        static BedQualityRemover()
        {
            int removed = 0;
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.IsBed || def.comps == null)
                    continue;
                removed += def.comps.RemoveAll(c => c?.compClass == typeof(CompQuality));
            }
            Log.Message($"[HSKMoreHardcore] BedQualityRemover: quality removed from {removed} bed defs.");
        }
    }
}
