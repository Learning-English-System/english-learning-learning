using Grpc.Core;
using Learning.Application.Interfaces;
using Learning.Contracts.Grpc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Learning.Infrastructure.Grpc;

[Authorize(Policy = "InternalLearningRead")]
public class LearningInternalGrpcService : LearningInternalService.LearningInternalServiceBase
{
    private readonly ILearningDbContext _dbContext;

    public LearningInternalGrpcService(ILearningDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override async Task<GetVocabularyCandidatesResponse> GetVocabularyCandidates(
        GetVocabularyCandidatesRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user_id GUID"));
        }

        var query = _dbContext.VocabularyItems
            .Include(v => v.ReviewState)
            .Where(v => v.UserId == userId && !v.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.CategoryId) && Guid.TryParse(request.CategoryId, out var catId))
        {
            query = query.Where(v => v.CategoryId == catId);
        }

        var count = request.Count > 0 ? Math.Min(request.Count, 100) : 10;

        var items = await query
            .OrderBy(v => v.ReviewState != null ? v.ReviewState.DueAt : DateTimeOffset.MaxValue)
            .Take(count)
            .ToListAsync(context.CancellationToken);

        var response = new GetVocabularyCandidatesResponse();
        foreach (var item in items)
        {
            response.Items.Add(new VocabularyCandidateItem
            {
                Id = item.Id.ToString(),
                Word = item.Word,
                Translation = item.Translation,
                PartOfSpeech = item.PartOfSpeech,
                CefrLevel = item.CefrLevel,
                Status = item.Status,
                ReviewStage = item.ReviewState?.RepetitionCount ?? 0
            });
        }

        return response;
    }

    public override async Task<GetVocabularySnapshotsResponse> GetVocabularySnapshots(
        GetVocabularySnapshotsRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user_id GUID"));
        }

        var guidList = new List<Guid>();
        foreach (var idStr in request.VocabularyIds)
        {
            if (Guid.TryParse(idStr, out var id))
            {
                guidList.Add(id);
            }
        }

        var items = await _dbContext.VocabularyItems
            .Include(v => v.ReviewState)
            .Where(v => v.UserId == userId && guidList.Contains(v.Id) && !v.IsDeleted)
            .ToListAsync(context.CancellationToken);

        var response = new GetVocabularySnapshotsResponse();
        foreach (var item in items)
        {
            response.Items.Add(new VocabularyCandidateItem
            {
                Id = item.Id.ToString(),
                Word = item.Word,
                Translation = item.Translation,
                PartOfSpeech = item.PartOfSpeech,
                CefrLevel = item.CefrLevel,
                Status = item.Status,
                ReviewStage = item.ReviewState?.RepetitionCount ?? 0
            });
        }

        return response;
    }
}
