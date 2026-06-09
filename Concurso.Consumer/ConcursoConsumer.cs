using System.Threading.Tasks;
using Concurso.Messaging.Events;
using MassTransit;

namespace Concurso.Consumer
{
    public class ConcursoConsumer : IConsumer<ConcursoPublicadoEvent>
    {
        public Task Consume(ConsumeContext<ConcursoPublicadoEvent> context)
        {
            var msg = context.Message;

            System.Console.WriteLine("Novo concurso recebido:");
            System.Console.WriteLine($"Titulo: {msg.Titulo}");
            System.Console.WriteLine($"Salario: {msg.Salario}");
            System.Console.WriteLine($"Link: {msg.Link}");

            return Task.CompletedTask;
        }
    }
}
