using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Api.Domain;

namespace OpenEmrNext.PatientManagement.Api.Persistence;

/// <summary>
/// Reads and writes against the shared <c>openemr</c> database, through the least-privilege user ADR
/// 0050 provisions (scoped to <c>patient_data</c> and <c>patient_management_outbox</c> only). One
/// <see cref="SaveChangesAsync"/> call across both tables is what makes the outbox pattern's atomicity
/// guarantee hold for free -- see ADR 0049.
/// </summary>
public sealed class PatientManagementDbContext(DbContextOptions<PatientManagementDbContext> options)
    : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Patient> Patients => Set<Patient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("patient_management_outbox");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(m => m.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(m => m.Payload).HasColumnName("payload").IsRequired();
            entity.Property(m => m.OccurredAtUtc).HasColumnName("occurred_at");
            entity.Property(m => m.PublishedAtUtc).HasColumnName("published_at");
        });

        // patient_data already exists, owned by OpenEMR -- this context never creates or migrates it
        // (ADR 0049), only maps the columns registration actually touches. Every property here is
        // read-only outside Patient's own constructors, so field access is used throughout: EF writes
        // straight to each auto-property's backing field, the supported pattern for immutable-style
        // entities, rather than needing public setters that would let anything bypass Patient.Register.
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.ToTable("patient_data", t => t.ExcludeFromMigrations());
            entity.UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(p => p.Pid).HasColumnName("pid");
            entity.Property(p => p.Pubpid).HasColumnName("pubpid");
            entity.Property(p => p.Uuid).HasColumnName("uuid")
                .HasConversion(
                    uuid => Rfc4122GuidConverter.ToRfc4122Bytes(uuid),
                    bytes => Rfc4122GuidConverter.FromRfc4122Bytes(bytes));
            entity.Property(p => p.FirstName).HasColumnName("fname");
            entity.Property(p => p.LastName).HasColumnName("lname");
            entity.Property(p => p.Sex).HasColumnName("sex");
            entity.Property(p => p.DateOfBirth).HasColumnName("DOB");
            // email/email_direct are NOT NULL DEFAULT '' in the real schema. Patient maps its own
            // _email/_trustedEmail fields here instead of the Email/TrustedEmail properties directly --
            // those fields are always a real non-null string, matching the column exactly; the
            // properties layer string? on top for application code. (A ValueConverter looked simpler at
            // first, but EF skips running a converter whose source value is null by default, so it never
            // avoided the NOT NULL violation -- confirmed against a real database, not just reasoned
            // about.)
            entity.Property<string>("_email").HasColumnName("email");
            entity.Property<string>("_trustedEmail").HasColumnName("email_direct");
            entity.Property(p => p.RegisteredAtUtc).HasColumnName("regdate");
            entity.Property(p => p.RegisteredBy).HasColumnName("created_by");

            // "date" is patient_data's own last-updated timestamp, not a date tied to any one concept
            // (docs/m4-domain-notes.md's naming trap) -- registration sets it once, to the same moment
            // as regdate, exactly as OpenEMR's own databaseInsert() does, but nothing here ever reads
            // it back, so it isn't mapped as a property at all; a raw SQL default covers the insert
            // (see the seed/migration step, not yet written).
        });
    }
}
