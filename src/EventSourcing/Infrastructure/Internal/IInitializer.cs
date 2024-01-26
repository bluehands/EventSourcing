using System.Threading.Tasks;

namespace EventSourcing.Infrastructure.Internal;

public interface IInitializer
{
    Task Initialize();
}