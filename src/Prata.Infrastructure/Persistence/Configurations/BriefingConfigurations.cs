using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using Prata.Domain.Briefing;

namespace Prata.Infrastructure.Persistence.Configurations;

internal sealed class BriefingTemplateConfiguration : IEntityTypeConfiguration<BriefingTemplate>
{
    public void Configure(EntityTypeBuilder<BriefingTemplate> builder)
    {
        builder.ToTable("briefing_template");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TenantId).IsRequired();
        builder.Property(t => t.ServiceTypeId).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Version).IsRequired();
        builder.Property(t => t.IsPublished).IsRequired();
        builder
            .HasMany<Question>("_questions")
            .WithOne()
            .HasForeignKey(q => q.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_questions").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => new { t.TenantId, t.ServiceTypeId, t.Version }).IsUnique();
        builder.Ignore(t => t.DomainEvents);
        builder.Ignore(t => t.Questions);
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("briefing_question");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.TenantId).IsRequired();
        builder.Property(q => q.TemplateId).IsRequired();
        builder.Property(q => q.Code).HasMaxLength(16).IsRequired();
        builder.Property(q => q.Prompt).HasMaxLength(2000).IsRequired();
        builder.Property(q => q.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(q => q.Block).HasConversion<string>().HasMaxLength(8);
        builder.Property(q => q.SortOrder).IsRequired();
        builder.Property(q => q.IsRequired).IsRequired();
        builder.Property(q => q.IsSensitive).IsRequired();
        builder.Property(q => q.VisibleWhen).HasMaxLength(500);
        builder
            .Property<List<QuestionOption>>("_options")
            .HasField("_options")
            .HasColumnName("options_json")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<QuestionOption>>(v, JsonOptions) ?? new List<QuestionOption>()
            );
        builder.Ignore(q => q.Options);
        builder.HasIndex(q => q.TenantId);
        builder.HasIndex(q => new { q.TenantId, q.TemplateId, q.Code }).IsUnique();
    }
}

internal sealed class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("briefing_answer");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.OrderId).IsRequired();
        builder.Property(a => a.QuestionCode).HasMaxLength(16).IsRequired();
        builder.Property(a => a.PromptSnapshot).HasMaxLength(2000).IsRequired();
        builder.Property(a => a.TypeSnapshot).HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.IsSensitive).IsRequired();
        builder.Property(a => a.ValueJson).HasColumnName("value").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => new { a.TenantId, a.OrderId, a.QuestionCode }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.IsSensitive });
    }
}

internal sealed class BriefingConsentConfiguration : IEntityTypeConfiguration<BriefingConsent>
{
    public void Configure(EntityTypeBuilder<BriefingConsent> builder)
    {
        builder.ToTable("briefing_consent");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.OrderId).IsRequired();
        builder.Property(c => c.Scope).HasMaxLength(64).IsRequired();
        builder.Property(c => c.ConsentedAt).IsRequired();
        builder.Property(c => c.Ip).HasMaxLength(64).IsRequired();
        builder.Property(c => c.UserAgent).HasMaxLength(512).IsRequired();
        builder.Property(c => c.PurposeTextSnapshot).HasMaxLength(4000).IsRequired();
        builder.Property(c => c.RevokedAt);
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => new { c.TenantId, c.OrderId, c.Scope });
        builder.Ignore(c => c.IsActive);
    }
}
