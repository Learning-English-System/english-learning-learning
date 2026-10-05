using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using Learning.Application.Interfaces;
using Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure.Data;

public class LearningDbContext : DbContext, ILearningDbContext
{
    public LearningDbContext(DbContextOptions<LearningDbContext> options) : base(options)
    {
    }

    public DbSet<VocabularyItem> VocabularyItems => Set<VocabularyItem>();
    public DbSet<VocabularyCategory> Categories => Set<VocabularyCategory>();
    public DbSet<VocabularyExample> Examples => Set<VocabularyExample>();
    public DbSet<ReviewState> ReviewStates => Set<ReviewState>();
    public DbSet<LearningProgress> Progress => Set<LearningProgress>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // VocabularyCategory
        modelBuilder.Entity<VocabularyCategory>(b =>
        {
            b.ToTable("VocabularyCategories");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(100);
            b.HasIndex(c => new { c.UserId, c.Name });
        });

        // VocabularyItem
        modelBuilder.Entity<VocabularyItem>(b =>
        {
            b.ToTable("VocabularyItems");
            b.HasKey(v => v.Id);
            b.Property(v => v.Word).IsRequired().HasMaxLength(200);
            b.Property(v => v.NormalizedWord).IsRequired().HasMaxLength(200);
            b.Property(v => v.Translation).IsRequired().HasMaxLength(500);
            b.Property(v => v.PartOfSpeech).IsRequired().HasMaxLength(50);
            b.Property(v => v.Definition).IsRequired().HasMaxLength(1000);
            b.Property(v => v.CefrLevel).IsRequired().HasMaxLength(10);
            b.Property(v => v.Source).IsRequired().HasMaxLength(50);
            b.Property(v => v.Status).IsRequired().HasMaxLength(50);

            b.HasIndex(v => v.UserId);
            b.HasIndex(v => new { v.UserId, v.NormalizedWord })
             .HasFilter("[IsDeleted] = 0");

            b.HasOne(v => v.Category)
             .WithMany(c => c.Items)
             .HasForeignKey(v => v.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(v => v.ReviewState)
             .WithOne(r => r.Vocabulary)
             .HasForeignKey<ReviewState>(r => r.VocabularyId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(v => v.Examples)
             .WithOne(e => e.Vocabulary)
             .HasForeignKey(e => e.VocabularyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // VocabularyExample
        modelBuilder.Entity<VocabularyExample>(b =>
        {
            b.ToTable("VocabularyExamples");
            b.HasKey(e => e.Id);
            b.Property(e => e.Sentence).IsRequired().HasMaxLength(1000);
            b.Property(e => e.Translation).IsRequired().HasMaxLength(1000);
            b.HasIndex(e => e.VocabularyId);
        });

        // ReviewState
        modelBuilder.Entity<ReviewState>(b =>
        {
            b.ToTable("ReviewStates");
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.UserId);
            b.HasIndex(r => new { r.UserId, r.DueAt });
        });

        // LearningProgress
        modelBuilder.Entity<LearningProgress>(b =>
        {
            b.ToTable("LearningProgress");
            b.HasKey(p => p.UserId);
            b.Property(p => p.UserId).ValueGeneratedNever();
        });

        // OutboxMessage
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages");
            b.HasKey(o => o.Id);
            b.Property(o => o.EventType).HasMaxLength(128).IsRequired();
            b.Property(o => o.PartitionKey).HasMaxLength(128).IsRequired();
            b.Property(o => o.PayloadJson).IsRequired();
            b.HasIndex(o => new { o.PublishedAtUtc, o.CreatedAtUtc });
        });

        // InboxMessage
        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages");
            b.HasKey(i => new { i.EventId, i.ConsumerName });
            b.Property(i => i.ConsumerName).HasMaxLength(100);
        });
    }
}
