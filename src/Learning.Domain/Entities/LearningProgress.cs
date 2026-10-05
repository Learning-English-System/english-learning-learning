namespace Learning.Domain.Entities;

public class LearningProgress
{
    public Guid UserId { get; set; }
    public int TotalWords { get; set; }
    public int LearningWords { get; set; }
    public int MasteredWords { get; set; }
    public int DueReviews { get; set; }
    public double RollingAccuracy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
