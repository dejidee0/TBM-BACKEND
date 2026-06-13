namespace TBM.Application.Helpers;

public static class DateRangeHelper
{
    public static List<DateTime> GetMonthStartsUtc(DateTime startMonthUtc, DateTime endMonthUtcInclusive)
    {
        var start = new DateTime(startMonthUtc.Year, startMonthUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(endMonthUtcInclusive.Year, endMonthUtcInclusive.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (end < start)
        {
            (start, end) = (end, start);
        }

        var months = new List<DateTime>();
        var current = start;
        while (current <= end)
        {
            months.Add(current);
            current = current.AddMonths(1);
        }

        return months;
    }

    public static int GetMonthSpanInclusive(DateTime startMonthUtc, DateTime endMonthUtcInclusive)
    {
        var start = new DateTime(startMonthUtc.Year, startMonthUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(endMonthUtcInclusive.Year, endMonthUtcInclusive.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (end < start)
        {
            (start, end) = (end, start);
        }

        return (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
    }
}
