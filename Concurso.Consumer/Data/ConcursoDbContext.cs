using Concurso.Consumer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Concurso.Consumer.Data;

public sealed class ConcursoDbContext : DbContext
{
    public ConcursoDbContext(DbContextOptions<ConcursoDbContext> options) : base(options) { }

    public DbSet<ConcursoEntity> Concursos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConcursoEntity>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.DeduplicationKey).IsUnique();
            b.Property(e => e.Titulo).IsRequired().HasMaxLength(1024);
            b.Property(e => e.Fonte).IsRequired().HasMaxLength(256);
            b.Property(e => e.DeduplicationKey).IsRequired().HasMaxLength(64);
            b.Property(e => e.CreatedAt).IsRequired();
        });
    }
}
