using System.Threading.Tasks;

namespace AsyncAwaitPattern
{
    public interface IRunnable
    {
        Task Run();
    }
}