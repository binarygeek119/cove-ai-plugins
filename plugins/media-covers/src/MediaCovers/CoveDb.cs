using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaCovers;

internal static class CoveDb
{
    public static DbContext Require(IServiceProvider services)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("Cove.Data.CoveContext");
            if (type is null)
                continue;

            if (services.GetService(type) is DbContext db)
                return db;
        }

        throw new InvalidOperationException(
            "Cove's library database is not available to this plugin. Cove.Data.CoveContext must be registered in the host.");
    }
}
