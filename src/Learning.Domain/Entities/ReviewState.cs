namespace Learning.Domain.Entities;

public class ReviewState
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VocabularyId { get; set; }
    public Guid UserId { get; set; }
    public int RepetitionCount { get; set; }
    public double IntervalDays { get; set; } = 1.0;
    public double EaseFactor { get; set; } = 2.5;
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastReviewedAt { get; set; }
    public int LapseCount { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }

    public VocabularyItem Vocabulary { get; set; } = null!;
}
