namespace Concurso.Web.Models;

public class ConcursoDto
{
    public int Id { get; set; }
    public string DeduplicationKey { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Orgao { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Salario { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTimeOffset DataPublicacao { get; set; }
    public DateTimeOffset DataCaptura { get; set; }
    public string Fonte { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public DateTime CreatedAt { get; set; }
}
