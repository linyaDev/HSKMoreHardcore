using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    // Диагностика «пешка не видит вещь для билла». Работает только в dev-режиме.
    // Постфикс на WorkGiver_DoBill.IsUsableIngredient: для оружия/одежды,
    // отвергнутых биллом, пишет в лог, какой фильтр отказал (фиксированный
    // рецепта, фильтр билла, фильтры ингредиентов) и какие спец-фильтры
    // совпали с вещью. Каждая пара вещь+билл логируется один раз за сессию.
    // ВАЖНО: если пешка стоит, а лога нет вообще — вещь не дошла до проверки
    // фильтров (запрещена, вне радиуса поиска, зарезервирована, недостижима).
    [StaticConstructorOnStartup]
    public static class BillIngredientDebug
    {
        private static readonly HashSet<(int, int)> logged = new HashSet<(int, int)>();

        static BillIngredientDebug()
        {
            var target = AccessTools.Method(typeof(WorkGiver_DoBill), "IsUsableIngredient");
            if (target == null)
            {
                Log.Warning("[HSKMoreHardcore] BillIngredientDebug: WorkGiver_DoBill.IsUsableIngredient not found.");
                return;
            }

            new Harmony("linya.hskmorehardcore.billingredientdebug").Patch(target,
                postfix: new HarmonyMethod(typeof(BillIngredientDebug), nameof(Postfix)));
        }

        public static void Postfix(Thing t, Bill bill, bool __result)
        {
            if (__result || !Prefs.DevMode)
                return;
            if (t?.def == null || bill?.recipe == null || !(t.def.IsWeapon || t.def.IsApparel))
                return;
            if (!logged.Add((t.thingIDNumber, bill.GetUniqueLoadID().GetHashCode())))
                return;

            var sb = new StringBuilder();
            sb.AppendLine($"[HSKMoreHardcore] БИЛЛ ОТВЕРГ: {t.LabelCap} (def={t.def.defName}) для «{bill.LabelCap}» ({bill.recipe.defName})");

            bool fixedOk = bill.recipe.fixedIngredientFilter == null || bill.recipe.fixedIngredientFilter.Allows(t);
            bool billOk = bill.ingredientFilter == null || bill.ingredientFilter.Allows(t);
            sb.AppendLine($"  fixedIngredientFilter: {(fixedOk ? "ok" : "ОТКАЗ")}, фильтр билла: {(billOk ? "ok" : "ОТКАЗ")}");

            for (int i = 0; i < bill.recipe.ingredients.Count; i++)
            {
                var ing = bill.recipe.ingredients[i];
                sb.AppendLine($"  ингредиент[{i}] filter: {(ing.filter.Allows(t) ? "ok" : "ОТКАЗ")}");
            }

            // Какие спец-фильтры совпадают с вещью и где они запрещены
            foreach (var sf in DefDatabase<SpecialThingFilterDef>.AllDefsListForReading)
            {
                if (!t.def.IsWithinCategory(sf.parentCategory))
                    continue;
                bool matches;
                try { matches = sf.Worker.Matches(t); }
                catch { continue; }
                if (!matches)
                    continue;

                var where = new List<string>();
                if (bill.recipe.fixedIngredientFilter != null && !bill.recipe.fixedIngredientFilter.Allows(sf))
                    where.Add("fixed");
                if (bill.ingredientFilter != null && !bill.ingredientFilter.Allows(sf))
                    where.Add("билл");
                for (int i = 0; i < bill.recipe.ingredients.Count; i++)
                {
                    if (!bill.recipe.ingredients[i].filter.Allows(sf))
                        where.Add($"ингредиент[{i}]");
                }

                sb.AppendLine($"  спец-фильтр {sf.defName}: совпал{(where.Count > 0 ? ", ЗАПРЕЩЁН в: " + string.Join(", ", where) : ", нигде не запрещён")}");
            }

            var hp = t.def.useHitPoints ? $"{t.HitPoints}/{t.MaxHitPoints}" : "-";
            sb.Append($"  HP={hp}, качество={(t.TryGetQuality(out var q) ? q.ToString() : "-")}");
            Log.Message(sb.ToString());
        }
    }
}
