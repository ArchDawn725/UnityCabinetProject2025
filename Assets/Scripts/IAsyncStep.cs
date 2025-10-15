using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents a unit of work that must be initialized asynchronously before use.
/// Implementations should be safe to call multiple times (idempotent) and must honor cancellation.
/// </summary>
public interface IAsyncStep
{
    /// <summary>
    /// Performs any asynchronous setup/initialization required by this step.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
    Task SetupAsync(CancellationToken cancellationToken = default, Initializer initializer = null);
}
