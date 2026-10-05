using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.Application.Interfaces;

public interface ILearningDbContext
{
    DbSet<VocabularyItem> VocabularyItems { get; }
    DbSet<VocabularyCategory> Categories { get; }
    DbSet<VocabularyExample> Examples { get; }
    DbSet<ReviewState> ReviewStates { get; }
    DbSet<LearningProgress> Progress { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<InboxMessage> InboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
