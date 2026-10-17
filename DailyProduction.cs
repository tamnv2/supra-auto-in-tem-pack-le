using System.Globalization;

namespace SupraAutoPackLe;

/// <summary>Daily statistics based solely on persisted business history.
/// Completed UI workflows are not verified physical print outcomes.</summary>
internal sealed record PersonProduction(
    string Person,int Attempts,int Completed,int UniqueDo,int NeedsReview,
    long ProcessingSeconds,long? MedianSeconds,decimal? Points,int ScoredAttempts,
    string FirstStart,string LastEnd);

internal sealed record DailyProduction(
    int Attempts,int Completed,int UniqueDo,int NeedsReview,
    long ProcessingSeconds,decimal? Points,int ScoredAttempts,
    IReadOnlyList<PersonProduction> People)
{
    public static DailyProduction From(IReadOnlyList<HistoryRow> rows)
    {
        static bool Done(HistoryRow r)=>r.Result=="UI_WORKFLOW_COMPLETE";
        static long? Median(IEnumerable<long> values)
        {
            var ordered=values.Where(x=>x>=0).Order().ToArray();
            if(ordered.Length==0)return null;
            int n=ordered.Length;
            return n%2==1?ordered[n/2]:(long)Math.Round((ordered[n/2-1]+(double)ordered[n/2])/2);
        }
        static string First(IEnumerable<string> values)=>values
            .Where(x=>TimeSpan.TryParseExact(x,@"hh\:mm\:ss",CultureInfo.InvariantCulture,out _))
            .OrderBy(x=>x,StringComparer.Ordinal).FirstOrDefault()??"—";
        static string Last(IEnumerable<string> values)=>values
            .Where(x=>TimeSpan.TryParseExact(x,@"hh\:mm\:ss",CultureInfo.InvariantCulture,out _))
            .OrderByDescending(x=>x,StringComparer.Ordinal).FirstOrDefault()??"—";
        var people=rows.Where(r=>!string.IsNullOrWhiteSpace(r.Person))
            .GroupBy(r=>r.Person.Trim(),StringComparer.OrdinalIgnoreCase)
            .Select(g=>
            {
                var good=g.Where(Done).ToArray();
                var score=good.Where(r=>r.Points.HasValue).ToArray();
                return new PersonProduction(g.Key,g.Count(),good.Length,
                    good.Select(r=>r.DO).Where(x=>!string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    g.Count(r=>!Done(r)),
                    good.Sum(r=>Math.Max(0,r.Seconds)),
                    Median(good.Select(r=>r.Seconds)),
                    score.Length>0?score.Sum(r=>r.Points!.Value):null,
                    score.Length,First(g.Select(r=>r.Start)),Last(g.Select(r=>r.End)));
            }).OrderBy(x=>x.Person,StringComparer.CurrentCultureIgnoreCase).ToList();
        var done=rows.Where(Done).ToArray();
        var scored=done.Where(x=>x.Points.HasValue).ToArray();
        return new DailyProduction(rows.Count,done.Length,
            done.Select(r=>r.DO).Where(x=>!string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            rows.Count(r=>!Done(r)),
            done.Sum(r=>Math.Max(0,r.Seconds)),
            scored.Length>0?scored.Sum(x=>x.Points!.Value):null,
            scored.Length,people);
    }
}
