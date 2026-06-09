using System;

namespace Concurso.Messaging.Events
{
    /// <summary>
    /// Contrato de evento publicado pelo Producer quando um concurso relevante é detectado.
    /// Mantenha este contrato imutável e pequeno; evolua com versionamento quando necessário.
    /// </summary>
    public sealed record ConcursoPublicadoEvent
    {
        public required string DeduplicationKey { get; init; }
        public required string Titulo { get; init; }
        public required string Orgao { get; init; }
        public required string Cargo { get; init; }
        public required string Salario { get; init; }
        public required string Link { get; init; }
        public required System.DateTimeOffset DataPublicacao { get; init; }
        public required System.DateTimeOffset DataCaptura { get; init; }
        public required string Fonte { get; init; }
        public string? Descricao { get; init; }
    }
}
