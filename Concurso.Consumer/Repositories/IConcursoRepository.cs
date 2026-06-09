using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Concurso.Consumer.Repositories;

public interface IConcursoRepository
{
    Task<bool> ExistsAsync(string deduplicationKey, CancellationToken cancellationToken = default);
    Task AddAsync(Data.Entities.ConcursoEntity entity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
