namespace Learning.Domain.Entities;

public class VocabularyItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Word { get; set; } = string.Empty;
    public string NormalizedWord { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string CefrLevel { get; set; } = "B1";
    public Guid? CategoryId { get; set; }
    public string Source { get; set; } = "Manual";
    public string Status { get; set; } = "New"; // "New", "Learning", "Mastered"
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public VocabularyCategory? Category { get; set; }
    public List<VocabularyExample> Examples { get; set; } = new();
    public ReviewState? ReviewState { get; set; }
}
