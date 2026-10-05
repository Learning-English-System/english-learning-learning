using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Learning.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLearningModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.EventId, x.ConsumerName });
                });

            migrationBuilder.CreateTable(
                name: "LearningProgress",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalWords = table.Column<int>(type: "int", nullable: false),
                    LearningWords = table.Column<int>(type: "int", nullable: false),
                    MasteredWords = table.Column<int>(type: "int", nullable: false),
                    DueReviews = table.Column<int>(type: "int", nullable: false),
                    RollingAccuracy = table.Column<double>(type: "float", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningProgress", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventVersion = table.Column<int>(type: "int", nullable: false),
                    PartitionKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Word = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedWord = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Translation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PartOfSpeech = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Definition = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CefrLevel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyItems_VocabularyCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "VocabularyCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReviewStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VocabularyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepetitionCount = table.Column<int>(type: "int", nullable: false),
                    IntervalDays = table.Column<double>(type: "float", nullable: false),
                    EaseFactor = table.Column<double>(type: "float", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LapseCount = table.Column<int>(type: "int", nullable: false),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    IncorrectCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewStates_VocabularyItems_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "VocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VocabularyExamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VocabularyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sentence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Translation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VocabularyExamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VocabularyExamples_VocabularyItems_VocabularyId",
                        column: x => x.VocabularyId,
                        principalTable: "VocabularyItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedAtUtc_CreatedAtUtc",
                table: "OutboxMessages",
                columns: new[] { "PublishedAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewStates_UserId",
                table: "ReviewStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewStates_UserId_DueAt",
                table: "ReviewStates",
                columns: new[] { "UserId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewStates_VocabularyId",
                table: "ReviewStates",
                column: "VocabularyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyCategories_UserId_Name",
                table: "VocabularyCategories",
                columns: new[] { "UserId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyExamples_VocabularyId",
                table: "VocabularyExamples",
                column: "VocabularyId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyItems_CategoryId",
                table: "VocabularyItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyItems_UserId",
                table: "VocabularyItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VocabularyItems_UserId_NormalizedWord",
                table: "VocabularyItems",
                columns: new[] { "UserId", "NormalizedWord" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "LearningProgress");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ReviewStates");

            migrationBuilder.DropTable(
                name: "VocabularyExamples");

            migrationBuilder.DropTable(
                name: "VocabularyItems");

            migrationBuilder.DropTable(
                name: "VocabularyCategories");
        }
    }
}
