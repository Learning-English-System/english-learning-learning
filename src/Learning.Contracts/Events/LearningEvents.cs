namespace Learning.Contracts.Events;

public record VocabularyCreatedEvent(
    Guid VocabularyId,
    Guid UserId,
    string Word,
    string CefrLevel,
    DateTimeOffset CreatedAt
);

public record VocabularyMasteryChangedEvent(
    Guid VocabularyId,
    Guid UserId,
    string Word,
    string OldStatus,
    string NewStatus,
    DateTimeOffset ChangedAt
);
