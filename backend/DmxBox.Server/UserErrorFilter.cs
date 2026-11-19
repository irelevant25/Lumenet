using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server;

/// <summary>
/// Stands around every call of a hub method and deals with a call that ends in an exception.
/// </summary>
/// <remarks>
/// <para>
/// A mistake of the user is told to the UI: SignalR hides the message of an exception from
/// the client, except for a <see cref="HubException"/>. So the two exceptions that carry a
/// message for the user are turned into one: a number out of range, and a
/// <see cref="DeskException"/>. In the log such a refusal is one line, without a stack
/// trace: the owner watches the console, and a stack trace there looks like a crash.
/// </para>
/// <para>
/// Everything else is a fault in the program. The UI only learns that the call failed; the
/// log gets the error with all its details.
/// </para>
/// <para>
/// SignalR would write an error of its own for both kinds, with a stack trace each time.
/// Those entries are switched off where the hub is set up (Program), which is why the faults
/// have to be logged here.
/// </para>
/// </remarks>
internal sealed class UserErrorFilter : IHubFilter
{
    private readonly ILogger<DeskHub> _logger;

    /// <summary>Makes the filter. The server has one for all calls.</summary>
    /// <param name="logger">The log of the hub: what the filter writes is about the hub's calls.</param>
    public UserErrorFilter(ILogger<DeskHub> logger)
    {
        _logger = logger;
    }

    /// <summary>Runs one call of a hub method.</summary>
    /// <param name="invocationContext">Which method, with which arguments, from which UI.</param>
    /// <param name="next">Runs the method itself.</param>
    /// <returns>What the method returned.</returns>
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or DeskException)
        {
            _logger.LogInformation("Refused {Method}: {Reason}", invocationContext.HubMethodName, e.Message);
            throw new HubException(e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Method} failed.", invocationContext.HubMethodName);
            throw;
        }
    }
}
