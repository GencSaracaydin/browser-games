using LongShadow.Core;

namespace LongShadow.Tests.EditMode.Mission
{
    /// <summary>Assets/_Game/Data/markup_ids.csv (TECH_PLAN Appendix A) parsed with Core's MarkupIdTable.</summary>
    internal static class MarkupCsv
    {
        private static MarkupIdTable s_table;

        public static MarkupIdTable Table => s_table ?? (s_table = MarkupIdTable.Parse(TestPaths.Read("Assets", "_Game", "Data", "markup_ids.csv")));
    }
}
