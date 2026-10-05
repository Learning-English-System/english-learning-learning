namespace Learning.Domain.Algorithms;

public record ReviewScheduleResult(
    int RepetitionCount,
    double IntervalDays,
    double EaseFactor,
    int LapseCount,
    DateTimeOffset NextDueAt,
    bool IsMastered
);

public interface IReviewScheduler
{
    ReviewScheduleResult CalculateNextReview(
        int repetitionCount,
        double intervalDays,
        double easeFactor,
        int lapseCount,
        int grade,
        DateTimeOffset? nowUtc = null);
}

public class Sm2ReviewScheduler : IReviewScheduler
{
    public const double MinimumEaseFactor = 1.3;
    public const double DefaultEaseFactor = 2.5;
    public const double MasteryIntervalDaysThreshold = 21.0;
    public const int MasteryRepetitionsThreshold = 5;

    public ReviewScheduleResult CalculateNextReview(
        int repetitionCount,
        double intervalDays,
        double easeFactor,
        int lapseCount,
        int grade,
        DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;

        // Clamp grade to [0, 5]
        grade = Math.Clamp(grade, 0, 5);

        // Clamp input ease factor to minimum
        easeFactor = Math.Max(MinimumEaseFactor, easeFactor);

        int newRepetition;
        double newInterval;
        double newEaseFactor;
        int newLapseCount;

        if (grade >= 3)
        {
            // Successful recall
            if (repetitionCount == 0)
            {
                newInterval = 1.0;
            }
            else if (repetitionCount == 1)
            {
                newInterval = 6.0;
            }
            else
            {
                newInterval = Math.Round(intervalDays * easeFactor, 1);
            }

            newRepetition = repetitionCount + 1;
            newLapseCount = lapseCount;

            // SM-2 formula: EF' = EF + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02))
            int diff = 5 - grade;
            newEaseFactor = easeFactor + (0.1 - diff * (0.08 + diff * 0.02));
            newEaseFactor = Math.Round(Math.Max(MinimumEaseFactor, newEaseFactor), 2);
        }
        else
        {
            // Failure / Lapse
            newRepetition = 0;
            newInterval = 1.0;
            newLapseCount = lapseCount + 1;
            newEaseFactor = Math.Round(Math.Max(MinimumEaseFactor, easeFactor - 0.2), 2);
        }

        var nextDueAt = now.AddDays(newInterval);
        bool isMastered = newInterval >= MasteryIntervalDaysThreshold || newRepetition >= MasteryRepetitionsThreshold;

        return new ReviewScheduleResult(
            RepetitionCount: newRepetition,
            IntervalDays: newInterval,
            EaseFactor: newEaseFactor,
            LapseCount: newLapseCount,
            NextDueAt: nextDueAt,
            IsMastered: isMastered
        );
    }
}
