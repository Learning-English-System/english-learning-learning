using FluentAssertions;
using Learning.Domain.Algorithms;
using Xunit;

namespace Learning.UnitTests;

public class Sm2ReviewSchedulerTests
{
    private readonly Sm2ReviewScheduler _scheduler = new();
    private readonly DateTimeOffset _fixedNow = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalculateNextReview_InitialSuccessfulReview_SetsRepetition1AndInterval1Day()
    {
        // Arrange: first review with good recall (grade 4)
        var result = _scheduler.CalculateNextReview(
            repetitionCount: 0,
            intervalDays: 1.0,
            easeFactor: 2.5,
            lapseCount: 0,
            grade: 4,
            nowUtc: _fixedNow);

        // Assert
        result.RepetitionCount.Should().Be(1);
        result.IntervalDays.Should().Be(1.0);
        result.EaseFactor.Should().Be(2.5); // diff 1 => 0.1 - 1 * 0.10 = 0.0 => 2.5
        result.LapseCount.Should().Be(0);
        result.NextDueAt.Should().Be(_fixedNow.AddDays(1.0));
        result.IsMastered.Should().BeFalse();
    }

    [Fact]
    public void CalculateNextReview_SecondSuccessfulReview_SetsRepetition2AndInterval6Days()
    {
        // Arrange: second review with perfect recall (grade 5)
        var result = _scheduler.CalculateNextReview(
            repetitionCount: 1,
            intervalDays: 1.0,
            easeFactor: 2.5,
            lapseCount: 0,
            grade: 5,
            nowUtc: _fixedNow);

        // Assert
        result.RepetitionCount.Should().Be(2);
        result.IntervalDays.Should().Be(6.0);
        result.EaseFactor.Should().Be(2.6); // diff 0 => +0.1 => 2.6
        result.LapseCount.Should().Be(0);
        result.NextDueAt.Should().Be(_fixedNow.AddDays(6.0));
        result.IsMastered.Should().BeFalse();
    }

    [Fact]
    public void CalculateNextReview_ThirdSuccessfulReview_MultipliesIntervalByEaseFactor()
    {
        // Arrange: third review with grade 4
        var result = _scheduler.CalculateNextReview(
            repetitionCount: 2,
            intervalDays: 6.0,
            easeFactor: 2.6,
            lapseCount: 0,
            grade: 4,
            nowUtc: _fixedNow);

        // Assert: 6.0 * 2.6 = 15.6 days
        result.RepetitionCount.Should().Be(3);
        result.IntervalDays.Should().Be(15.6);
        result.EaseFactor.Should().Be(2.6);
        result.NextDueAt.Should().Be(_fixedNow.AddDays(15.6));
        result.IsMastered.Should().BeFalse();
    }

    [Fact]
    public void CalculateNextReview_FailedRecall_ResetsRepetitionAndIntervalTo1Day()
    {
        // Arrange: lapse with grade 2 (wrong recall)
        var result = _scheduler.CalculateNextReview(
            repetitionCount: 3,
            intervalDays: 15.6,
            easeFactor: 2.6,
            lapseCount: 0,
            grade: 2,
            nowUtc: _fixedNow);

        // Assert: reset
        result.RepetitionCount.Should().Be(0);
        result.IntervalDays.Should().Be(1.0);
        result.EaseFactor.Should().Be(2.4); // 2.6 - 0.2
        result.LapseCount.Should().Be(1);
        result.NextDueAt.Should().Be(_fixedNow.AddDays(1.0));
        result.IsMastered.Should().BeFalse();
    }

    [Fact]
    public void CalculateNextReview_RepeatedFailures_ClampsEaseFactorTo1_3()
    {
        // Arrange: easeFactor already near bottom
        var result = _scheduler.CalculateNextReview(
            repetitionCount: 0,
            intervalDays: 1.0,
            easeFactor: 1.35,
            lapseCount: 5,
            grade: 1,
            nowUtc: _fixedNow);

        // Assert: clamp to 1.3
        result.EaseFactor.Should().Be(1.3);
        result.RepetitionCount.Should().Be(0);
        result.IntervalDays.Should().Be(1.0);
        result.LapseCount.Should().Be(6);
    }

    [Fact]
    public void CalculateNextReview_WhenIntervalReaches21Days_FlagsMastered()
    {
        // Arrange: 4th review with 15.6 days * 2.5 = 39 days
        var result = _scheduler.CalculateNextReview(
            repetitionCount: 3,
            intervalDays: 15.6,
            easeFactor: 2.5,
            lapseCount: 0,
            grade: 5,
            nowUtc: _fixedNow);

        // Assert
        result.IntervalDays.Should().BeGreaterThanOrEqualTo(21.0);
        result.IsMastered.Should().BeTrue();
    }
}
