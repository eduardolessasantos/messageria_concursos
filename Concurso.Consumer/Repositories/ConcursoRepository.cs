using Concurso.Consumer.Data;
using Concurso.Consumer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Consumer.Repositories;

public sealed class ConcursoRepository : IConcursoRepository
{
    private readonly ConcursoDbContext _db;
    private readonly ILogger<ConcursoRepository> _logger;

    public ConcursoRepository(ConcursoDbContext db, ILogger<ConcursoRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> ExistsAsync(string deduplicationKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deduplicationKey)) return false;
        return await _db.Concursos.AnyAsync(c => c.DeduplicationKey == deduplicationKey, cancellationToken);
    }

    public async Task AddAsync(ConcursoEntity entity, CancellationToken cancellationToken = default)
    {
        await _db.Concursos.AddAsync(entity, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Provável colisão de índice -> tratar como idempotência conflitante
            _logger.LogWarning(ex, "Falha ao salvar Concurso (possível duplicidade detectada).");
            throw;
        }
    }
}
