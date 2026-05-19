using TransactionRiskEngine.Api.Dtos;

namespace TransactionRiskEngine.Api.Services.Graph;

public interface IUserGraphService {
    Task<GraphResponseDto> BuildGraphAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GraphPath>> FindRiskPathsAsync(
        Guid userId,
        int maxDepth,
        CancellationToken cancellationToken);
}
