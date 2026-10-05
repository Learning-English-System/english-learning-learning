namespace Learning.Contracts.DTOs;

public record VocabularyDto(
    Guid Id,
    Guid UserId,
    string Word,
    string NormalizedWord,
    string Translation,
    string PartOfSpeech,
    string Definition,
    string CefrLevel,
    Guid? CategoryId,
    string? CategoryName,
    string Source,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<VocabularyExampleDto> Examples,
    ReviewStateDto? ReviewState
);

public record VocabularyExampleDto(
    Guid Id,
    string Sentence,
    string Translation
);

public record ReviewStateDto(
    int RepetitionCount,
    double IntervalDays,
    double EaseFactor,
    DateTimeOffset DueAt,
    DateTimeOffset? LastReviewedAt,
    int LapseCount,
    int CorrectCount,
    int IncorrectCount
);

public record CreateVocabularyExampleRequest(
    string Sentence,
    string Translation
);

public record CreateVocabularyRequest(
    string Word,
    string Translation,
    string? PartOfSpeech = null,
    string? Definition = null,
    string? CefrLevel = "B1",
    Guid? CategoryId = null,
    string? Source = "Manual",
    IReadOnlyList<CreateVocabularyExampleRequest>? Examples = null
);

public record UpdateVocabularyRequest(
    string? Translation = null,
    string? PartOfSpeech = null,
    string? Definition = null,
    string? CefrLevel = null,
    Guid? CategoryId = null,
    IReadOnlyList<CreateVocabularyExampleRequest>? Examples = null
);

public record SubmitReviewRequest(
    int QualityRating
);

public record ReviewResultDto(
    Guid VocabularyId,
    int RepetitionCount,
    double IntervalDays,
    double EaseFactor,
    DateTimeOffset DueAt,
    string Status,
    bool IsCorrect
);

public record LearningProgressDto(
    Guid UserId,
    int TotalWords,
    int LearningWords,
    int MasteredWords,
    int DueReviews,
    double RollingAccuracy,
    DateTimeOffset UpdatedAt
);

public record CategoryDto(
    Guid Id,
    string Name,
    bool IsSystem,
    int WordCount
);

public record CreateCategoryRequest(
    string Name
);
