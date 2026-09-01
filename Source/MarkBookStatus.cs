using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MarkBookStatus
{
    [StaticConstructorOnStartup]
    public static class MarkBookStatusStartup
    {
        static MarkBookStatusStartup()
        {
            var harmony = new Harmony("com.uranv.markbookstatus");
            var labelGetter = AccessTools.PropertyGetter(typeof(Book), "LabelNoCount");
            if (labelGetter == null)
            {
                Log.Error("[BookStatusMarker] could not find Book.LabelNoCount getter.");
                return;
            }

            harmony.Patch(
                labelGetter,
                postfix: new HarmonyMethod(typeof(BookLabelNoCountPatch), nameof(BookLabelNoCountPatch.Postfix)));
        }
    }

    internal static class BookLabelNoCountPatch
    {
        private static readonly FieldInfo ResearchValuesField =
            typeof(ReadingOutcomeDoerGainResearch).GetField(
                "values", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo QuestField =
            typeof(BookOutcomeDoer_GiveQuest).GetField(
                "quest", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Postfix(Book __instance, ref string __result)
        {
            var marker = GetMarker(__instance);
            if (string.IsNullOrEmpty(marker) || string.IsNullOrEmpty(__result))
            {
                return;
            }

            // Book.LabelNoCount has already appended quality/health here.
            // Insert the marker before the closing parenthesis.
            var closingParenthesis = __result.LastIndexOf(')');
            if (closingParenthesis == __result.Length - 1)
            {
                __result = __result.Insert(closingParenthesis, " " + marker);
            }
            else
            {
                // Maps normally have no quality suffix.
                __result += " (" + marker + ")";
            }
        }

        private static string GetMarker(Book book)
        {
            if (book?.def == null || book.BookComp == null)
            {
                return null;
            }

            switch (book.def.defName)
            {
                case "Schematic":
                {
                    foreach (var doer in book.BookComp.Doers)
                    {
                        if (doer is ReadingOutcomeDoerGainResearch researchDoer &&
                            researchDoer.GetType() == typeof(ReadingOutcomeDoerGainResearch) &&
                            AllAssignedProjectsFinished(researchDoer))
                        {
                            return "BookStatusMarkers_Unlocked".Translate().Resolve();
                        }
                    }

                    break;
                }
                case "Map":
                {
                    foreach (var doer in book.BookComp.Doers)
                    {
                        if (doer is BookOutcomeDoer_GiveQuest questDoer && IsQuestDiscovered(questDoer))
                        {
                            return "BookStatusMarkers_Discovered".Translate().Resolve();
                        }
                    }

                    break;
                }
                case "Tome":
                {
                    foreach (var doer in book.BookComp.Doers)
                    {
                        if (doer is ReadingOutcomeDoerGainAnomalyResearch researchDoer && AllAssignedProjectsFinished(researchDoer))
                        {
                            return "BookStatusMarkers_Unlocked".Translate().Resolve();
                        }
                    }

                    break;
                }
            }

            return null;
        }

        private static bool AllAssignedProjectsFinished(ReadingOutcomeDoerGainResearch doer)
        {
            if (ResearchValuesField != null)
            {
                if (ResearchValuesField.GetValue(doer) is IDictionary<ResearchProjectDef, float> { Count: > 0 } values)
                {
                    foreach (var project in values.Keys)
                    {
                        if (project is not { IsFinished: true })
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            // Covers explicitly included projects before generated values exist.
            var props = doer.Props;
            if (props.include == null || props.include.Count == 0)
            {
                return false;
            }

            foreach (var item in props.include)
            {
                if (item?.project is not { IsFinished: true })
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsQuestDiscovered(BookOutcomeDoer_GiveQuest doer)
        {
            return QuestField != null && QuestField.GetValue(doer) != null;
        }
    }
}
