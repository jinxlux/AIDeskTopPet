using AiService.Host.Contracts;

namespace AiService.Host.Services;

/// <summary>
/// Defines one free web retrieval provider.
/// </summary>
public interface IWebSearchProvider
{
    /// <summary>
    /// Provider name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Searches provider and returns normalized result items.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="maxResults">Max result count.</param>
    /// <param name="category">Requested category route: news, weather, pet, general.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<AgentSearchItem>> SearchAsync(string query, int maxResults, string category, CancellationToken cancellationToken);
}
