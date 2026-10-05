using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BuildingBlocks.Grpc.Interceptors;
using BuildingBlocks.Security.Constants;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using Learning.Contracts.DTOs;
using Learning.Contracts.Grpc;
using Learning.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Learning.IntegrationTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string AuthenticationScheme = "TestScheme";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authHeader = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string tokenValue = authHeader["Bearer ".Length..].Trim();

        // Check if internal service token simulation
        if (tokenValue.Equals("internal-service-token", StringComparison.OrdinalIgnoreCase))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "internal-test-service"),
                new Claim("sub", "internal-test-service"),
                new Claim("scope", $"{SecurityConstants.Scopes.InternalLearningRead} {SecurityConstants.Scopes.InternalAiInvoke}")
            };
            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
        }

        // Learner user token
        if (Guid.TryParse(tokenValue, out var guid))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, guid.ToString()),
                new Claim("sub", guid.ToString()),
                new Claim(ClaimTypes.Role, "Learner"),
                new Claim("scope", "openid profile email")
            };
            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationScheme)));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid test bearer token"));
    }
}

public class LearningTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.AuthenticationScheme, null);
        });
    }
}

public class LearningEndpointsTests : IClassFixture<LearningTestWebApplicationFactory>
{
    private readonly LearningTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LearningEndpointsTests(LearningTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetVocabularyList_Unauthenticated_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/vocabulary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Vocabulary_FullCrudAndOutboxEvent_Success()
    {
        var userId = Guid.NewGuid();
        using var authedClient = _factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var createReq = new CreateVocabularyRequest(
            Word: $"Ephemeral_{Guid.NewGuid():N}",
            Translation: "Phù du, chóng tàn",
            PartOfSpeech: "Adjective",
            Definition: "Lasting for a very short time",
            CefrLevel: "C1",
            Examples: new[]
            {
                new CreateVocabularyExampleRequest("Fashions are ephemeral.", "Thời trang chỉ mang tính phù du.")
            }
        );

        // 1. Create
        var createRes = await authedClient.PostAsJsonAsync("/api/vocabulary", createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createRes.Content.ReadFromJsonAsync<VocabularyDto>();
        created.Should().NotBeNull();
        created!.Word.Should().Be(createReq.Word);
        created.Status.Should().Be("New");
        created.Examples.Should().HaveCount(1);
        created.ReviewState.Should().NotBeNull();
        created.ReviewState!.RepetitionCount.Should().Be(0);

        // Verify Outbox Event created
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            var outboxMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.PartitionKey == userId.ToString() && m.EventType == "learning.vocabulary.created.v1");
            outboxMsg.Should().NotBeNull();
        }

        // 2. Get by Id
        var getRes = await authedClient.GetAsync($"/api/vocabulary/{created.Id}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getRes.Content.ReadFromJsonAsync<VocabularyDto>();
        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(created.Id);

        // 3. Update
        var updateReq = new UpdateVocabularyRequest(
            Translation: "Tồn tại trong thời gian ngắn",
            CefrLevel: "C2"
        );
        var updateRes = await authedClient.PutAsJsonAsync($"/api/vocabulary/{created.Id}", updateReq);
        updateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateRes.Content.ReadFromJsonAsync<VocabularyDto>();
        updated!.Translation.Should().Be("Tồn tại trong thời gian ngắn");
        updated.CefrLevel.Should().Be("C2");

        // 4. Soft Delete
        var delRes = await authedClient.DeleteAsync($"/api/vocabulary/{created.Id}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 5. Verify deleted not accessible
        var getAfterDel = await authedClient.GetAsync($"/api/vocabulary/{created.Id}");
        getAfterDel.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserIsolation_UserACannotAccessOrMutateUserBVocabulary()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userA.ToString());

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.ToString());

        var createReq = new CreateVocabularyRequest(
            Word: $"Serendipity_{Guid.NewGuid():N}",
            Translation: "Sự tình cờ may mắn",
            CefrLevel: "C1"
        );
        var createRes = await clientA.PostAsJsonAsync("/api/vocabulary", createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdA = await createRes.Content.ReadFromJsonAsync<VocabularyDto>();

        // User B attempts GET
        var getB = await clientB.GetAsync($"/api/vocabulary/{createdA!.Id}");
        getB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // User B attempts PUT
        var putB = await clientB.PutAsJsonAsync($"/api/vocabulary/{createdA.Id}", new UpdateVocabularyRequest(Translation: "Hacked"));
        putB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // User B attempts DELETE
        var delB = await clientB.DeleteAsync($"/api/vocabulary/{createdA.Id}");
        delB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // User B attempts Submit Review
        var revB = await clientB.PostAsJsonAsync($"/api/vocabulary/{createdA.Id}/reviews", new SubmitReviewRequest(5));
        revB.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReviewFlow_SubmittingReviews_UpdatesSM2_DueReviews_AndProgress()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var createReq = new CreateVocabularyRequest(
            Word: $"Tenacious_{Guid.NewGuid():N}",
            Translation: "Kiên trì, bền bỉ",
            CefrLevel: "B2"
        );
        var createRes = await client.PostAsJsonAsync("/api/vocabulary", createReq);
        var item = await createRes.Content.ReadFromJsonAsync<VocabularyDto>();

        // Initially it should be due for review
        var dueListRes = await client.GetAsync("/api/vocabulary/reviews/due");
        var dueList = await dueListRes.Content.ReadFromJsonAsync<List<VocabularyDto>>();
        dueList.Should().NotBeNull();
        dueList!.Any(v => v.Id == item!.Id).Should().BeTrue();

        // Submit review with grade 5 (perfect recall)
        var reviewRes1 = await client.PostAsJsonAsync($"/api/vocabulary/{item!.Id}/reviews", new SubmitReviewRequest(5));
        reviewRes1.StatusCode.Should().Be(HttpStatusCode.OK);
        var review1 = await reviewRes1.Content.ReadFromJsonAsync<ReviewResultDto>();
        review1.Should().NotBeNull();
        review1!.RepetitionCount.Should().Be(1);
        review1.IntervalDays.Should().Be(1.0);
        review1.Status.Should().Be("Learning");
        review1.IsCorrect.Should().BeTrue();

        // Check progress
        var progressRes = await client.GetAsync("/api/vocabulary/progress");
        progressRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var progress = await progressRes.Content.ReadFromJsonAsync<LearningProgressDto>();
        progress.Should().NotBeNull();
        progress!.TotalWords.Should().Be(1);
        progress.LearningWords.Should().Be(1);
        progress.RollingAccuracy.Should().Be(1.0);

        // Now advance repetitions until Mastered
        // Rep 2
        await client.PostAsJsonAsync($"/api/vocabulary/{item.Id}/reviews", new SubmitReviewRequest(5));
        // Rep 3
        await client.PostAsJsonAsync($"/api/vocabulary/{item.Id}/reviews", new SubmitReviewRequest(5));
        // Rep 4
        await client.PostAsJsonAsync($"/api/vocabulary/{item.Id}/reviews", new SubmitReviewRequest(5));
        // Rep 5 (mastery threshold: repetitions >= 5 or interval >= 21)
        var reviewRes5 = await client.PostAsJsonAsync($"/api/vocabulary/{item.Id}/reviews", new SubmitReviewRequest(5));
        var review5 = await reviewRes5.Content.ReadFromJsonAsync<ReviewResultDto>();
        review5!.Status.Should().Be("Mastered");

        // Verify mastery changed outbox event was recorded
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            var masteryMsg = await db.OutboxMessages.FirstOrDefaultAsync(m => m.PartitionKey == userId.ToString() && m.EventType == "learning.vocabulary.mastery-changed.v1");
            masteryMsg.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Categories_SystemCategoriesExist_And_CustomCategoryCreated()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        // Get categories
        var catRes = await client.GetAsync("/api/vocabulary/categories");
        catRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await catRes.Content.ReadFromJsonAsync<List<CategoryDto>>();
        categories.Should().NotBeNull();
        categories!.Select(c => c.Name).Should().Contain(new[] { "General", "Business", "Academic", "Travel", "Daily Life" });

        // Create custom category
        var createCatRes = await client.PostAsJsonAsync("/api/vocabulary/categories", new CreateCategoryRequest("Technology"));
        createCatRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var customCat = await createCatRes.Content.ReadFromJsonAsync<CategoryDto>();
        customCat.Should().NotBeNull();
        customCat!.Name.Should().Be("Technology");
        customCat.IsSystem.Should().BeFalse();
    }

    [Fact]
    public async Task InternalGrpc_GetVocabularyCandidates_WithServiceToken_ReturnsCandidates()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        await client.PostAsJsonAsync("/api/vocabulary", new CreateVocabularyRequest(
            Word: $"Candidate_{Guid.NewGuid():N}",
            Translation: "Ứng viên từ vựng",
            CefrLevel: "B1"
        ));

        // Create gRPC client using in-memory test server handler
        var channel = GrpcChannel.ForAddress(_client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = _client
        });
        var grpcClient = new LearningInternalService.LearningInternalServiceClient(channel);

        // 1. Calling without internal service token returns Unauthenticated / PermissionDenied
        var unauthedMetadata = new Metadata();
        Func<Task> callUnauthed = async () =>
        {
            await grpcClient.GetVocabularyCandidatesAsync(new GetVocabularyCandidatesRequest
            {
                UserId = userId.ToString(),
                Count = 5
            }, unauthedMetadata);
        };
        await callUnauthed.Should().ThrowAsync<RpcException>();

        // 2. Calling with internal service token succeeds
        var authedMetadata = new Metadata
        {
            { "Authorization", "Bearer internal-service-token" }
        };

        var grpcResponse = await grpcClient.GetVocabularyCandidatesAsync(new GetVocabularyCandidatesRequest
        {
            UserId = userId.ToString(),
            Count = 5
        }, authedMetadata);

        grpcResponse.Should().NotBeNull();
        grpcResponse.Items.Should().NotBeEmpty();
        grpcResponse.Items.First().UserIdShouldOrWord().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReviewFlow_ConsecutiveBlackouts_ClampsEaseFactorToMinimum()
    {
        var userId = Guid.NewGuid();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        var createRes = await client.PostAsJsonAsync("/api/vocabulary", new CreateVocabularyRequest(
            Word: $"DifficultWord_{Guid.NewGuid():N}",
            Translation: "Từ khó nhớ",
            CefrLevel: "C2"
        ));
        var item = await createRes.Content.ReadFromJsonAsync<VocabularyDto>();

        // Submit 10 consecutive Grade 0 reviews (complete blackouts)
        ReviewResultDto? lastResult = null;
        for (int i = 0; i < 10; i++)
        {
            var res = await client.PostAsJsonAsync($"/api/vocabulary/{item!.Id}/reviews", new SubmitReviewRequest(0));
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            lastResult = await res.Content.ReadFromJsonAsync<ReviewResultDto>();
        }

        // Assert: Ease factor is clamped exactly at 1.30, repetition stays 0
        lastResult.Should().NotBeNull();
        lastResult!.EaseFactor.Should().Be(1.30);
        lastResult.RepetitionCount.Should().Be(0);
        lastResult.Status.Should().Be("Learning");

        // Verify LapseCount on vocabulary item detail
        var getVocabRes = await client.GetAsync($"/api/vocabulary/{item.Id}");
        getVocabRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var vocabDetail = await getVocabRes.Content.ReadFromJsonAsync<VocabularyDto>();
        vocabDetail!.ReviewState.Should().NotBeNull();
        vocabDetail.ReviewState!.LapseCount.Should().Be(10);
    }
}



public static class TestExtensions
{
    public static string UserIdShouldOrWord(this VocabularyCandidateItem item) => item.Word;
}
