using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BCOBCA.BLL.Common;

public static class DatabaseConflicts
{
    public static bool IsConcurrency(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: "40001" or "40P01" }) return true;
        return false;
    }
}
