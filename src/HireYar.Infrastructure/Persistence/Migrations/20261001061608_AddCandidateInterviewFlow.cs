using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateInterviewFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_InterviewTemplates_OrganizationId_JobPositionId_Id",
                table: "InterviewTemplates",
                columns: new[] { "OrganizationId", "JobPositionId", "Id" });

            migrationBuilder.CreateTable(
                name: "CandidateApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateApplications", x => x.Id);
                    table.UniqueConstraint("AK_CandidateApplications_OrganizationId_JobPositionId_Id", x => new { x.OrganizationId, x.JobPositionId, x.Id });
                    table.CheckConstraint("CK_Application_Status", "\"Status\" IN ('Invited','Interviewing','InterviewCompleted')");
                    table.ForeignKey(
                        name: "FK_CandidateApplications_JobPositions_OrganizationId_JobPositi~",
                        columns: x => new { x.OrganizationId, x.JobPositionId },
                        principalTable: "JobPositions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionAudios",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AudioStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AudioModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AudioVoice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AudioSpeed = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionAudios", x => new { x.OrganizationId, x.AudioHash });
                    table.ForeignKey(
                        name: "FK_QuestionAudios_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.UniqueConstraint("AK_Skills_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Skills_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentQuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionReason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    MaxQuestionCount = table.Column<int>(type: "integer", nullable: false),
                    MaxDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    ProcessingLeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessingLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessingRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccessRevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewSessions", x => x.Id);
                    table.UniqueConstraint("AK_InterviewSessions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Interview_Limits", "\"MaxQuestionCount\" BETWEEN 1 AND 30 AND \"MaxDurationSeconds\" BETWEEN 300 AND 3600");
                    table.CheckConstraint("CK_Interview_Status", "\"Status\" IN ('Created','WaitingForAnswer','ProcessingAnswer','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_InterviewSessions_CandidateApplications_OrganizationId_JobP~",
                        columns: x => new { x.OrganizationId, x.JobPositionId, x.CandidateApplicationId },
                        principalTable: "CandidateApplications",
                        principalColumns: new[] { "OrganizationId", "JobPositionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewSessions_InterviewTemplates_OrganizationId_JobPosi~",
                        columns: x => new { x.OrganizationId, x.JobPositionId, x.InterviewTemplateId },
                        principalTable: "InterviewTemplates",
                        principalColumns: new[] { "OrganizationId", "JobPositionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionSkills",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TargetDifficulty = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionSkills", x => new { x.OrganizationId, x.JobPositionId, x.SkillId });
                    table.CheckConstraint("CK_PositionSkill_Difficulty", "\"TargetDifficulty\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_PositionSkill_Priority", "\"Priority\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_PositionSkills_JobPositions_OrganizationId_JobPositionId",
                        columns: x => new { x.OrganizationId, x.JobPositionId },
                        principalTable: "JobPositions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionSkills_Skills_OrganizationId_SkillId",
                        columns: x => new { x.OrganizationId, x.SkillId },
                        principalTable: "Skills",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewSkillSnapshots",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetDifficulty = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewSkillSnapshots", x => new { x.InterviewSessionId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_InterviewSkillSnapshots_InterviewSessions_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankQuestions", x => x.Id);
                    table.UniqueConstraint("AK_BankQuestions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_BankQuestion_Difficulty", "\"Difficulty\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_BankQuestions_PositionSkills_OrganizationId_JobPositionId_S~",
                        columns: x => new { x.OrganizationId, x.JobPositionId, x.SkillId },
                        principalTable: "PositionSkills",
                        principalColumns: new[] { "OrganizationId", "JobPositionId", "SkillId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSkillStates",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentScore = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSkillStates", x => new { x.InterviewSessionId, x.SkillId });
                    table.CheckConstraint("CK_SkillState_Ranges", "\"CurrentScore\" BETWEEN 0 AND 1 AND \"Confidence\" BETWEEN 0 AND 1 AND \"EvidenceCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_CandidateSkillStates_InterviewSessions_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateSkillStates_InterviewSkillSnapshots_InterviewSessi~",
                        columns: x => new { x.InterviewSessionId, x.SkillId },
                        principalTable: "InterviewSkillSnapshots",
                        principalColumns: new[] { "InterviewSessionId", "SkillId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewCoveredTopics",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Coverage = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewCoveredTopics", x => new { x.InterviewSessionId, x.SkillId, x.Topic });
                    table.CheckConstraint("CK_Topic_Coverage", "\"Coverage\" BETWEEN 0 AND 1 AND \"EvidenceCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_InterviewCoveredTopics_InterviewSessions_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewCoveredTopics_InterviewSkillSnapshots_InterviewSes~",
                        columns: x => new { x.InterviewSessionId, x.SkillId },
                        principalTable: "InterviewSkillSnapshots",
                        principalColumns: new[] { "InterviewSessionId", "SkillId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewQuestionSnapshots",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewQuestionSnapshots", x => new { x.InterviewSessionId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_InterviewQuestionSnapshots_InterviewSessions_InterviewSessi~",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewQuestionSnapshots_InterviewSkillSnapshots_Intervie~",
                        columns: x => new { x.InterviewSessionId, x.SkillId },
                        principalTable: "InterviewSkillSnapshots",
                        principalColumns: new[] { "InterviewSessionId", "SkillId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AudioContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AudioSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Transcript = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    TranscriptProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TranscriptModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DecisionModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FailureStage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SelectionSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewAnswers", x => x.Id);
                    table.UniqueConstraint("AK_InterviewAnswers_InterviewSessionId_Id", x => new { x.InterviewSessionId, x.Id });
                    table.CheckConstraint("CK_Answer_Size", "\"AudioSizeBytes\" > 0");
                    table.CheckConstraint("CK_Answer_Status", "\"Status\" IN ('Uploaded','Transcribing','Transcribed','Evaluating','Processed','Failed')");
                    table.ForeignKey(
                        name: "FK_InterviewAnswers_InterviewQuestionSnapshots_InterviewSessio~",
                        columns: x => new { x.InterviewSessionId, x.QuestionId },
                        principalTable: "InterviewQuestionSnapshots",
                        principalColumns: new[] { "InterviewSessionId", "QuestionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewAnswers_InterviewSessions_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CandidateSkillEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewAnswerId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreBefore = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    ScoreAfter = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    Delta = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSkillEvidence", x => x.Id);
                    table.CheckConstraint("CK_Evidence_Ranges", "\"ScoreBefore\" BETWEEN 0 AND 1 AND \"ScoreAfter\" BETWEEN 0 AND 1 AND \"Confidence\" BETWEEN 0 AND 1 AND \"Delta\" BETWEEN -1 AND 1");
                    table.ForeignKey(
                        name: "FK_CandidateSkillEvidence_InterviewAnswers_InterviewSessionId_~",
                        columns: x => new { x.InterviewSessionId, x.InterviewAnswerId },
                        principalTable: "InterviewAnswers",
                        principalColumns: new[] { "InterviewSessionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateSkillEvidence_InterviewQuestionSnapshots_Interview~",
                        columns: x => new { x.InterviewSessionId, x.QuestionId },
                        principalTable: "InterviewQuestionSnapshots",
                        principalColumns: new[] { "InterviewSessionId", "QuestionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateSkillEvidence_InterviewSessions_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CandidateSkillEvidence_InterviewSkillSnapshots_InterviewSes~",
                        columns: x => new { x.InterviewSessionId, x.SkillId },
                        principalTable: "InterviewSkillSnapshots",
                        principalColumns: new[] { "InterviewSessionId", "SkillId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankQuestions_OrganizationId_JobPositionId_IsActive",
                table: "BankQuestions",
                columns: new[] { "OrganizationId", "JobPositionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_BankQuestions_OrganizationId_JobPositionId_SkillId",
                table: "BankQuestions",
                columns: new[] { "OrganizationId", "JobPositionId", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateApplications_OrganizationId_JobPositionId_CreatedA~",
                table: "CandidateApplications",
                columns: new[] { "OrganizationId", "JobPositionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSkillEvidence_InterviewSessionId_InterviewAnswerId",
                table: "CandidateSkillEvidence",
                columns: new[] { "InterviewSessionId", "InterviewAnswerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSkillEvidence_InterviewSessionId_QuestionId",
                table: "CandidateSkillEvidence",
                columns: new[] { "InterviewSessionId", "QuestionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSkillEvidence_InterviewSessionId_SkillId",
                table: "CandidateSkillEvidence",
                columns: new[] { "InterviewSessionId", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewAnswers_InterviewSessionId_QuestionId",
                table: "InterviewAnswers",
                columns: new[] { "InterviewSessionId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewQuestionSnapshots_InterviewSessionId_SkillId",
                table: "InterviewQuestionSnapshots",
                columns: new[] { "InterviewSessionId", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_AccessTokenHash",
                table: "InterviewSessions",
                column: "AccessTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_CandidateApplicationId",
                table: "InterviewSessions",
                column: "CandidateApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_JobPositionId_CandidateApp~",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "JobPositionId", "CandidateApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_JobPositionId_InterviewTem~",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "JobPositionId", "InterviewTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_JobPositionId_Status",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "JobPositionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionSkills_OrganizationId_SkillId",
                table: "PositionSkills",
                columns: new[] { "OrganizationId", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_OrganizationId_Name",
                table: "Skills",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankQuestions");

            migrationBuilder.DropTable(
                name: "CandidateSkillEvidence");

            migrationBuilder.DropTable(
                name: "CandidateSkillStates");

            migrationBuilder.DropTable(
                name: "InterviewCoveredTopics");

            migrationBuilder.DropTable(
                name: "QuestionAudios");

            migrationBuilder.DropTable(
                name: "PositionSkills");

            migrationBuilder.DropTable(
                name: "InterviewAnswers");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "InterviewQuestionSnapshots");

            migrationBuilder.DropTable(
                name: "InterviewSkillSnapshots");

            migrationBuilder.DropTable(
                name: "InterviewSessions");

            migrationBuilder.DropTable(
                name: "CandidateApplications");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InterviewTemplates_OrganizationId_JobPositionId_Id",
                table: "InterviewTemplates");
        }
    }
}
