using System;

namespace Concurso.Consumer.Data.Entities;

public sealed class ConcursoEntity
{
    public int Id { get; set; }
    public required string DeduplicationKey { get; set; }
    public required string Titulo { get; set; }
    public required string Orgao { get; set; }
    public required string Cargo { get; set; }
    public required string Salario { get; set; }
    public required string Link { get; set; }
    public required DateTimeOffset DataPublicacao { get; set; }
    public required DateTimeOffset DataCaptura { get; set; }
    public required string Fonte { get; set; }
    public string? Descricao { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
