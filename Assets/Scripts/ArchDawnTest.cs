using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ArchDawnTest : MonoBehaviour, IAsyncStep
{
    public async Task SetupAsync(CancellationToken ct)
    {
        // Do your async work here: loading, addressables, auth, etc.

        await Awaitable.WaitForSecondsAsync(2, ct);//test
    }
}
