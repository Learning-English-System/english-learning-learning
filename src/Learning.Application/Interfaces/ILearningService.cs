using Learning.Contracts.DTOs;

namespace Learning.Application.Interfaces;

public interface ILearningService
{
    Task<VocabularyDto> CreateVocabularyAsync(Guid userId, CreateVocabularyRequest request, CancellationToken cancellationToken = default);
    Task<VocabularyDto?> GetVocabularyByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VocabularyDto>> GetVocabularyListAsync(Guid userId, string? categoryId = null, string? search = null, string? status = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default);
    Task<VocabularyDto?> UpdateVocabularyAsync(Guid userId, Guid id, UpdateVocabularyRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteVocabularyAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VocabularyDto>> GetDueReviewsAsync(Guid userId, int limit = 20, CancellationToken cancellationToken = default);
    Task<ReviewResultDto?> SubmitReviewAsync(Guid userId, Guid vocabularyId, int qualityRating, CancellationToken cancellationToken = default);
    Task<LearningProgressDto> GetProgressAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateCategoryAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task EnsureDefaultCategoriesAsync(CancellationToken cancellationToken = default);
}
