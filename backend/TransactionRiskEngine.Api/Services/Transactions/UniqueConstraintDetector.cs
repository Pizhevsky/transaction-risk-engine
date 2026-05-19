using Microsoft.EntityFrameworkCore;
using PostgresException = Npgsql.PostgresException;

namespace TransactionRiskEngine.Api.Services.Transactions;

public static class UniqueConstraintDetector {
    public static bool IsUniqueConstraintViolation(DbUpdateException exception) {
        return exception.InnerException is PostgresException { SqlState: "23505" };
    }
}
