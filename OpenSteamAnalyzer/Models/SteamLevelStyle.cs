namespace OpenSteamAnalyzer.Models;

public static class SteamLevelStyle
{
    public static string GetLevelText(int? level)
    {
        return level is null ? "等级未知" : $"等级 {level}";
    }

    public static string GetLevelColor(int? level)
    {
        return level switch
        {
            null => "#667085",
            < 10 => "#667085",
            < 20 => "#2F80ED",
            < 30 => "#16A34A",
            < 40 => "#7C3AED",
            < 50 => "#F59E0B",
            < 100 => "#EF4444",
            _ => "#D97706"
        };
    }

    public static string GetLevelBadgeBackground(int? level)
    {
        return level switch
        {
            null => "#EEF2F7",
            < 10 => "#EEF2F7",
            < 20 => "#EAF2FF",
            < 30 => "#EAF8EF",
            < 40 => "#F1ECFF",
            < 50 => "#FFF4DB",
            < 100 => "#FEECEC",
            _ => "#FFF1CC"
        };
    }
}
