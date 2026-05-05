using OpenSteamAnalyzer.Models;

namespace OpenSteamAnalyzer.Services;

public interface IStoreDealsService
{
    Task<IReadOnlyList<StoreDiscountGame>> GetCurrentDiscountsAsync(int count, CancellationToken cancellationToken);
}
