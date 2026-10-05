using BuildingBlocks.Security.Extensions;
using Learning.Application.Interfaces;
using Learning.Contracts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learning.Api.Controllers;

[ApiController]
[Route("api/vocabulary")]
[Authorize]
public class VocabularyController : ControllerBase
{
    private readonly ILearningService _learningService;

    public VocabularyController(ILearningService learningService)
    {
        _learningService = learningService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateVocabulary([FromBody] CreateVocabularyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.Translation))
        {
            return BadRequest(new { error = "Word and translation are required." });
        }

        try
        {
            var userId = User.GetRequiredUserId();
            var item = await _learningService.CreateVocabularyAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetVocabularyById), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVocabularyById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var item = await _learningService.GetVocabularyByIdAsync(userId, id, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }
        return Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> GetVocabularyList(
        [FromQuery] string? categoryId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetRequiredUserId();
        var items = await _learningService.GetVocabularyListAsync(userId, categoryId, search, status, skip, take, cancellationToken);
        return Ok(items);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateVocabulary(Guid id, [FromBody] UpdateVocabularyRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var item = await _learningService.UpdateVocabularyAsync(userId, id, request, cancellationToken);
        if (item == null)
        {
            return NotFound();
        }
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteVocabulary(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var deleted = await _learningService.DeleteVocabularyAsync(userId, id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpGet("reviews/due")]
    public async Task<IActionResult> GetDueReviews([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var userId = User.GetRequiredUserId();
        var items = await _learningService.GetDueReviewsAsync(userId, limit, cancellationToken);
        return Ok(items);
    }

    [HttpPost("{id:guid}/reviews")]
    public async Task<IActionResult> SubmitReview(Guid id, [FromBody] SubmitReviewRequest request, CancellationToken cancellationToken)
    {
        if (request.QualityRating < 0 || request.QualityRating > 5)
        {
            return BadRequest(new { error = "QualityRating must be between 0 and 5." });
        }

        var userId = User.GetRequiredUserId();
        var result = await _learningService.SubmitReviewAsync(userId, id, request.QualityRating, cancellationToken);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress(CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var progress = await _learningService.GetProgressAsync(userId, cancellationToken);
        return Ok(progress);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var categories = await _learningService.GetCategoriesAsync(userId, cancellationToken);
        return Ok(categories);
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Category name is required." });
        }

        var userId = User.GetRequiredUserId();
        var cat = await _learningService.CreateCategoryAsync(userId, request, cancellationToken);
        return Ok(cat);
    }
}
