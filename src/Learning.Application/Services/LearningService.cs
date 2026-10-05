using BuildingBlocks.Messaging.Contracts;
using BuildingBlocks.Messaging.Outbox;
using Learning.Application.Interfaces;
using Learning.Contracts.DTOs;
using Learning.Contracts.Events;
using Learning.Domain.Algorithms;
using Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.Application.Services;

public class LearningService : ILearningService
{
    private readonly ILearningDbContext _dbContext;
    private readonly IReviewScheduler _reviewScheduler;

    private static readonly string[] DefaultCategoryNames =
    {
        "General",
        "Business",
        "Academic",
        "Travel",
        "Daily Life"
    };

    public LearningService(
        ILearningDbContext dbContext,
        IReviewScheduler reviewScheduler)
    {
        _dbContext = dbContext;
        _reviewScheduler = reviewScheduler;
    }

    public async Task<VocabularyDto> CreateVocabularyAsync(
        Guid userId,
        CreateVocabularyRequest request,
        CancellationToken cancellationToken = default)
    {
        var trimmedWord = request.Word.Trim();
        var normalized = trimmedWord.ToLowerInvariant();

        var existing = await _dbContext.VocabularyItems
            .FirstOrDefaultAsync(v => v.UserId == userId && v.NormalizedWord == normalized && !v.IsDeleted, cancellationToken);

        if (existing != null)
        {
            throw new InvalidOperationException($"Vocabulary item '{trimmedWord}' already exists in your vocabulary list.");
        }

        var now = DateTimeOffset.UtcNow;
        var item = new VocabularyItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Word = trimmedWord,
            NormalizedWord = normalized,
            Translation = request.Translation.Trim(),
            PartOfSpeech = request.PartOfSpeech?.Trim() ?? string.Empty,
            Definition = request.Definition?.Trim() ?? string.Empty,
            CefrLevel = string.IsNullOrWhiteSpace(request.CefrLevel) ? "B1" : request.CefrLevel.Trim().ToUpperInvariant(),
            CategoryId = request.CategoryId,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "Manual" : request.Source.Trim(),
            Status = "New",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (request.Examples != null && request.Examples.Count > 0)
        {
            foreach (var eg in request.Examples)
            {
                if (!string.IsNullOrWhiteSpace(eg.Sentence))
                {
                    item.Examples.Add(new VocabularyExample
                    {
                        Id = Guid.NewGuid(),
                        VocabularyId = item.Id,
                        Sentence = eg.Sentence.Trim(),
                        Translation = eg.Translation?.Trim() ?? string.Empty,
                        CreatedAt = now
                    });
                }
            }
        }

        var reviewState = new ReviewState
        {
            Id = Guid.NewGuid(),
            VocabularyId = item.Id,
            UserId = userId,
            RepetitionCount = 0,
            IntervalDays = 1.0,
            EaseFactor = 2.5,
            DueAt = now, // Available for immediate initial review
            LastReviewedAt = null,
            LapseCount = 0,
            CorrectCount = 0,
            IncorrectCount = 0
        };

        item.ReviewState = reviewState;

        await _dbContext.VocabularyItems.AddAsync(item, cancellationToken);
        await _dbContext.ReviewStates.AddAsync(reviewState, cancellationToken);

        // Add outbox message for vocabulary created
        var envelope = IntegrationEventEnvelope<VocabularyCreatedEvent>.Create(
            eventType: "learning.vocabulary.created.v1",
            eventVersion: 1,
            producer: "Learning.Service",
            correlationId: Guid.NewGuid().ToString(),
            payload: new VocabularyCreatedEvent(item.Id, userId, item.Word, item.CefrLevel, now)
        );

        await _dbContext.OutboxMessages.AddAsync(
            OutboxMessage.FromEnvelope(userId.ToString(), envelope),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await UpdateProgressAsync(userId, cancellationToken);

        return MapToDto(item);
    }

    public async Task<VocabularyDto?> GetVocabularyByIdAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.VocabularyItems
            .Include(v => v.Category)
            .Include(v => v.Examples)
            .Include(v => v.ReviewState)
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId && !v.IsDeleted, cancellationToken);

        return item == null ? null : MapToDto(item);
    }

    public async Task<IReadOnlyList<VocabularyDto>> GetVocabularyListAsync(
        Guid userId,
        string? categoryId = null,
        string? search = null,
        string? status = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VocabularyItems
            .Include(v => v.Category)
            .Include(v => v.Examples)
            .Include(v => v.ReviewState)
            .Where(v => v.UserId == userId && !v.IsDeleted);

        if (!string.IsNullOrWhiteSpace(categoryId) && Guid.TryParse(categoryId, out var catId))
        {
            query = query.Where(v => v.CategoryId == catId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(v => v.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchNorm = search.Trim().ToLowerInvariant();
            query = query.Where(v => v.NormalizedWord.Contains(searchNorm) || v.Translation.Contains(searchNorm));
        }

        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip(skip)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);

        return items.Select(MapToDto).ToList();
    }

    public async Task<VocabularyDto?> UpdateVocabularyAsync(
        Guid userId,
        Guid id,
        UpdateVocabularyRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.VocabularyItems
            .Include(v => v.Category)
            .Include(v => v.Examples)
            .Include(v => v.ReviewState)
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId && !v.IsDeleted, cancellationToken);

        if (item == null)
        {
            return null;
        }

        if (request.Translation != null) item.Translation = request.Translation.Trim();
        if (request.PartOfSpeech != null) item.PartOfSpeech = request.PartOfSpeech.Trim();
        if (request.Definition != null) item.Definition = request.Definition.Trim();
        if (request.CefrLevel != null) item.CefrLevel = request.CefrLevel.Trim().ToUpperInvariant();
        if (request.CategoryId.HasValue) item.CategoryId = request.CategoryId.Value;

        if (request.Examples != null)
        {
            _dbContext.Examples.RemoveRange(item.Examples);
            item.Examples.Clear();
            var now = DateTimeOffset.UtcNow;
            foreach (var eg in request.Examples)
            {
                if (!string.IsNullOrWhiteSpace(eg.Sentence))
                {
                    item.Examples.Add(new VocabularyExample
                    {
                        Id = Guid.NewGuid(),
                        VocabularyId = item.Id,
                        Sentence = eg.Sentence.Trim(),
                        Translation = eg.Translation?.Trim() ?? string.Empty,
                        CreatedAt = now
                    });
                }
            }
        }

        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDto(item);
    }

    public async Task<bool> DeleteVocabularyAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.VocabularyItems
            .FirstOrDefaultAsync(v => v.Id == id && v.UserId == userId && !v.IsDeleted, cancellationToken);

        if (item == null)
        {
            return false;
        }

        item.IsDeleted = true;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await UpdateProgressAsync(userId, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<VocabularyDto>> GetDueReviewsAsync(
        Guid userId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var safeLimit = Math.Clamp(limit, 1, 100);

        var items = await _dbContext.VocabularyItems
            .Include(v => v.Category)
            .Include(v => v.Examples)
            .Include(v => v.ReviewState)
            .Where(v => v.UserId == userId && !v.IsDeleted && v.ReviewState != null && v.ReviewState.DueAt <= now)
            .OrderBy(v => v.ReviewState!.DueAt)
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        return items.Select(MapToDto).ToList();
    }

    public async Task<ReviewResultDto?> SubmitReviewAsync(
        Guid userId,
        Guid vocabularyId,
        int qualityRating,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.VocabularyItems
            .Include(v => v.ReviewState)
            .FirstOrDefaultAsync(v => v.Id == vocabularyId && v.UserId == userId && !v.IsDeleted, cancellationToken);

        if (item == null)
        {
            return null;
        }

        var clampedQuality = Math.Clamp(qualityRating, 0, 5);
        var now = DateTimeOffset.UtcNow;

        var reviewState = item.ReviewState ?? new ReviewState
        {
            Id = Guid.NewGuid(),
            VocabularyId = item.Id,
            UserId = userId,
            RepetitionCount = 0,
            IntervalDays = 1.0,
            EaseFactor = 2.5,
            DueAt = now
        };

        var nextSchedule = _reviewScheduler.CalculateNextReview(
            reviewState.RepetitionCount,
            reviewState.IntervalDays,
            reviewState.EaseFactor,
            reviewState.LapseCount,
            clampedQuality,
            now
        );

        reviewState.RepetitionCount = nextSchedule.RepetitionCount;
        reviewState.IntervalDays = nextSchedule.IntervalDays;
        reviewState.EaseFactor = nextSchedule.EaseFactor;
        reviewState.LapseCount = nextSchedule.LapseCount;
        reviewState.DueAt = nextSchedule.NextDueAt;
        reviewState.LastReviewedAt = now;

        var isCorrect = clampedQuality >= 3;
        if (isCorrect)
        {
            reviewState.CorrectCount++;
        }
        else
        {
            reviewState.IncorrectCount++;
        }

        var oldStatus = item.Status;
        string newStatus = nextSchedule.IsMastered ? "Mastered" : "Learning";

        if (oldStatus != newStatus)
        {
            item.Status = newStatus;
            var masteryEvent = IntegrationEventEnvelope<VocabularyMasteryChangedEvent>.Create(
                eventType: "learning.vocabulary.mastery-changed.v1",
                eventVersion: 1,
                producer: "Learning.Service",
                correlationId: Guid.NewGuid().ToString(),
                payload: new VocabularyMasteryChangedEvent(item.Id, userId, item.Word, oldStatus, newStatus, now)
            );

            await _dbContext.OutboxMessages.AddAsync(
                OutboxMessage.FromEnvelope(userId.ToString(), masteryEvent),
                cancellationToken);
        }

        item.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await UpdateProgressAsync(userId, cancellationToken);

        return new ReviewResultDto(
            item.Id,
            reviewState.RepetitionCount,
            reviewState.IntervalDays,
            reviewState.EaseFactor,
            reviewState.DueAt,
            newStatus,
            isCorrect
        );
    }

    public async Task<LearningProgressDto> GetProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var progress = await _dbContext.Progress
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (progress == null)
        {
            return await UpdateProgressAsync(userId, cancellationToken);
        }

        return new LearningProgressDto(
            progress.UserId,
            progress.TotalWords,
            progress.LearningWords,
            progress.MasteredWords,
            progress.DueReviews,
            progress.RollingAccuracy,
            progress.UpdatedAt
        );
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.Categories
            .Where(c => c.IsSystem || c.UserId == userId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.IsSystem,
                c.Items.Count(i => i.UserId == userId && !i.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        return categories;
    }

    public async Task<CategoryDto> CreateCategoryAsync(
        Guid userId,
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name.Trim();
        var category = new VocabularyCategory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmedName,
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dbContext.Categories.AddAsync(category, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, false, 0);
    }

    public async Task EnsureDefaultCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var existingNames = await _dbContext.Categories
            .Where(c => c.IsSystem)
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        var missing = DefaultCategoryNames.Except(existingNames).ToList();
        if (missing.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var name in missing)
            {
                await _dbContext.Categories.AddAsync(new VocabularyCategory
                {
                    Id = Guid.NewGuid(),
                    UserId = null,
                    Name = name,
                    IsSystem = true,
                    CreatedAt = now
                }, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<LearningProgressDto> UpdateProgressAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var activeItems = await _dbContext.VocabularyItems
            .Include(v => v.ReviewState)
            .Where(v => v.UserId == userId && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        var total = activeItems.Count;
        var learning = activeItems.Count(i => i.Status == "Learning");
        var mastered = activeItems.Count(i => i.Status == "Mastered");
        var due = activeItems.Count(i => i.ReviewState != null && i.ReviewState.DueAt <= now);

        var totalCorrect = activeItems.Sum(i => i.ReviewState?.CorrectCount ?? 0);
        var totalIncorrect = activeItems.Sum(i => i.ReviewState?.IncorrectCount ?? 0);
        var totalAttempts = totalCorrect + totalIncorrect;
        var rollingAccuracy = totalAttempts > 0 ? (double)totalCorrect / totalAttempts : 0.0;

        var progress = await _dbContext.Progress
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (progress == null)
        {
            progress = new LearningProgress
            {
                UserId = userId,
                TotalWords = total,
                LearningWords = learning,
                MasteredWords = mastered,
                DueReviews = due,
                RollingAccuracy = Math.Round(rollingAccuracy, 4),
                UpdatedAt = now
            };
            await _dbContext.Progress.AddAsync(progress, cancellationToken);
        }
        else
        {
            progress.TotalWords = total;
            progress.LearningWords = learning;
            progress.MasteredWords = mastered;
            progress.DueReviews = due;
            progress.RollingAccuracy = Math.Round(rollingAccuracy, 4);
            progress.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LearningProgressDto(
            progress.UserId,
            progress.TotalWords,
            progress.LearningWords,
            progress.MasteredWords,
            progress.DueReviews,
            progress.RollingAccuracy,
            progress.UpdatedAt
        );
    }

    private static VocabularyDto MapToDto(VocabularyItem v)
    {
        return new VocabularyDto(
            v.Id,
            v.UserId,
            v.Word,
            v.NormalizedWord,
            v.Translation,
            v.PartOfSpeech,
            v.Definition,
            v.CefrLevel,
            v.CategoryId,
            v.Category?.Name,
            v.Source,
            v.Status,
            v.CreatedAt,
            v.UpdatedAt,
            v.Examples.Select(e => new VocabularyExampleDto(e.Id, e.Sentence, e.Translation)).ToList(),
            v.ReviewState == null ? null : new ReviewStateDto(
                v.ReviewState.RepetitionCount,
                v.ReviewState.IntervalDays,
                v.ReviewState.EaseFactor,
                v.ReviewState.DueAt,
                v.ReviewState.LastReviewedAt,
                v.ReviewState.LapseCount,
                v.ReviewState.CorrectCount,
                v.ReviewState.IncorrectCount
            )
        );
    }
}
