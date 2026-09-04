using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concurso.Shared.Metrics;

public interface IAppMetrics
{
    void IncrementFound(int amount = 1);
    void IncrementPublished(int amount = 1);
    void IncrementConsumed(int amount = 1);
    void IncrementPersisted(int amount = 1);
    void IncrementIgnored(int amount = 1);

    IDictionary<string, long> Snapshot();
}
