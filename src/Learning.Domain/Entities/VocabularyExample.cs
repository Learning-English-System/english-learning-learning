namespace Learning.Domain.Entities;

public class VocabularyExample
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VocabularyId { get; set; }
    public string Sentence { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public VocabularyItem Vocabulary { get; set; } = null!;
}
