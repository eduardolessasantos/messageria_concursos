using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Concurso.Shared.Metrics;

public sealed class InMemoryAppMetrics : IAppMetrics
{
    private long _found;
    private long _published;
    private long _consumed;
    private long _persisted;
    private long _ignored;

    public void IncrementFound(int amount = 1) => Interlocked.Add(ref _found, amount);
    public void IncrementPublished(int amount = 1) => Interlocked.Add(ref _published, amount);
    public void IncrementConsumed(int amount = 1) => Interlocked.Add(ref _consumed, amount);
    public void IncrementPersisted(int amount = 1) => Interlocked.Add(ref _persisted, amount);
    public void IncrementIgnored(int amount = 1) => Interlocked.Add(ref _ignored, amount);

    public IDictionary<string, long> Snapshot() => new Dictionary<string, long>
    {
        ["found"] = Interlocked.Read(ref _found),
        ["published"] = Interlocked.Read(ref _published),
        ["consumed"] = Interlocked.Read(ref _consumed),
        ["persisted"] = Interlocked.Read(ref _persisted),
        ["ignored"] = Interlocked.Read(ref _ignored)
    };
}
