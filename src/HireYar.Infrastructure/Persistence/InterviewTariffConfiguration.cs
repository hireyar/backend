using HireYar.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HireYar.Infrastructure.Persistence;

public sealed class InterviewTariffConfiguration : IEntityTypeConfiguration<InterviewTariff>
{
    public void Configure(EntityTypeBuilder<InterviewTariff> b)
    {
        b.ToTable("InterviewTariffs", t =>
        {
            t.HasCheckConstraint("CK_InterviewTariffs_Type", "\"InterviewType\" IN ('general', 'specialized')");
            t.HasCheckConstraint("CK_InterviewTariffs_Rate", "\"RatePerMinuteUsd\" > 0 AND \"RatePerMinuteUsd\" <= 1000");
        });
        b.HasKey(x => x.InterviewType);
        b.Property(x => x.InterviewType).HasMaxLength(20);
        b.Property(x => x.RatePerMinuteUsd).HasPrecision(18, 6);
        b.HasData(new InterviewTariff { InterviewType = "general", RatePerMinuteUsd = 0.3m },
            new InterviewTariff { InterviewType = "specialized", RatePerMinuteUsd = 0.8m });
    }
}
