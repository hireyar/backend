using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HireYar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HiringTextInterviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_InterviewTemplates_OrganizationId_Id",
                table: "InterviewTemplates",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "Candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                    table.UniqueConstraint("AK_Candidates_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Candidates_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.UniqueConstraint("AK_JobApplications_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Application_Status", "\"Status\" IN ('Active','Withdrawn','Closed')");
                    table.ForeignKey(
                        name: "FK_JobApplications_Candidates_OrganizationId_CandidateId",
                        columns: x => new { x.OrganizationId, x.CandidateId },
                        principalTable: "Candidates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_JobPositions_OrganizationId_JobPositionId",
                        columns: x => new { x.OrganizationId, x.JobPositionId },
                        principalTable: "JobPositions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceInterviewTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AvailableFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeadlineAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InterviewTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CandidateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CandidateEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxFollowUpQuestionsPerQuestion = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentAcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewSessions", x => x.Id);
                    table.UniqueConstraint("AK_InterviewSessions_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Session_Duration", "\"DurationMinutes\" BETWEEN 5 AND 60");
                    table.CheckConstraint("CK_Session_FollowUps", "\"MaxFollowUpQuestionsPerQuestion\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Session_Status", "\"Status\" IN ('NotStarted','InProgress','Completed','Expired','Cancelled')");
                    table.CheckConstraint("CK_Session_Window", "\"AvailableFromUtc\" < \"ExpiresAtUtc\"");
                    table.ForeignKey(
                        name: "FK_InterviewSessions_InterviewTemplates_OrganizationId_SourceI~",
                        columns: x => new { x.OrganizationId, x.SourceInterviewTemplateId },
                        principalTable: "InterviewTemplates",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewSessions_JobApplications_OrganizationId_JobApplica~",
                        columns: x => new { x.OrganizationId, x.JobApplicationId },
                        principalTable: "JobApplications",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewInvitations",
                columns: table => new
                {
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveryStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LastSentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SendAttempts = table.Column<int>(type: "integer", nullable: false),
                    CredentialHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CredentialExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewInvitations", x => x.InterviewSessionId);
                    table.CheckConstraint("CK_InterviewInvitation_Delivery", "\"DeliveryStatus\" IN ('Pending','Sent','Failed')");
                    table.ForeignKey(
                        name: "FK_InterviewInvitations_InterviewSessions_OrganizationId_Inter~",
                        columns: x => new { x.OrganizationId, x.InterviewSessionId },
                        principalTable: "InterviewSessions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SnapshotQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Competency = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EvaluationGuidance = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnapshotQuestions", x => x.Id);
                    table.UniqueConstraint("AK_SnapshotQuestions_InterviewSessionId_Id", x => new { x.InterviewSessionId, x.Id });
                    table.CheckConstraint("CK_Snapshot_Order", "\"Order\" BETWEEN 1 AND 30");
                    table.ForeignKey(
                        name: "FK_SnapshotQuestions_InterviewSessions_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InterviewAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InterviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewAnswers", x => x.Id);
                    table.CheckConstraint("CK_Answer_Content", "(\"Status\" <> 'Skipped' OR \"Text\" IS NULL) AND (\"Status\" <> 'Submitted' OR length(btrim(\"Text\")) > 0 AND \"Text\" IS NOT NULL)");
                    table.CheckConstraint("CK_Answer_Status", "\"Status\" IN ('Draft','Submitted','Skipped')");
                    table.ForeignKey(
                        name: "FK_InterviewAnswers_InterviewSessions_InterviewSessionId",
                        column: x => x.InterviewSessionId,
                        principalTable: "InterviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InterviewAnswers_SnapshotQuestions_InterviewSessionId_Snaps~",
                        columns: x => new { x.InterviewSessionId, x.SnapshotQuestionId },
                        principalTable: "SnapshotQuestions",
                        principalColumns: new[] { "InterviewSessionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_OrganizationId_CreatedAtUtc_Id",
                table: "Candidates",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidates_OrganizationId_NormalizedEmail",
                table: "Candidates",
                columns: new[] { "OrganizationId", "NormalizedEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewAnswers_InterviewSessionId_SnapshotQuestionId",
                table: "InterviewAnswers",
                columns: new[] { "InterviewSessionId", "SnapshotQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewInvitations_CredentialHash",
                table: "InterviewInvitations",
                column: "CredentialHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewInvitations_OrganizationId_DeliveryStatus",
                table: "InterviewInvitations",
                columns: new[] { "OrganizationId", "DeliveryStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewInvitations_OrganizationId_InterviewSessionId",
                table: "InterviewInvitations",
                columns: new[] { "OrganizationId", "InterviewSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewInvitations_TokenHash",
                table: "InterviewInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_CreatedAtUtc_Id",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_IdempotencyKey",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_JobApplicationId",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "JobApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_SourceInterviewTemplateId",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "SourceInterviewTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_InterviewSessions_OrganizationId_Status_ExpiresAtUtc",
                table: "InterviewSessions",
                columns: new[] { "OrganizationId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_OrganizationId_CandidateId_JobPositionId",
                table: "JobApplications",
                columns: new[] { "OrganizationId", "CandidateId", "JobPositionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_OrganizationId_JobPositionId_CreatedAtUtc_Id",
                table: "JobApplications",
                columns: new[] { "OrganizationId", "JobPositionId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_OrganizationId_Status",
                table: "JobApplications",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SnapshotQuestions_InterviewSessionId_Order",
                table: "SnapshotQuestions",
                columns: new[] { "InterviewSessionId", "Order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewAnswers");

            migrationBuilder.DropTable(
                name: "InterviewInvitations");

            migrationBuilder.DropTable(
                name: "SnapshotQuestions");

            migrationBuilder.DropTable(
                name: "InterviewSessions");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "Candidates");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InterviewTemplates_OrganizationId_Id",
                table: "InterviewTemplates");
        }
    }
}
