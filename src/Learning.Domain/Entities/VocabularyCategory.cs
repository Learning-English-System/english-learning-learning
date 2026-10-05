namespace Learning.Domain.Entities;

public class VocabularyCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; } // Null for system categories
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<VocabularyItem> Items { get; set; } = new();
}
