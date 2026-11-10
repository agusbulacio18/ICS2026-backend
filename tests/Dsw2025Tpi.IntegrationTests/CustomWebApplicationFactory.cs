using Dsw2025Tpi.Api;
using Dsw2025Tpi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dsw2025Tpi.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // An in-memory SQLite database only lives while its connection is open,
    // and every new connection is a new, empty database. Keep one open per context.
    private readonly SqliteConnection _appConnection = new("DataSource=:memory:");
    private readonly SqliteConnection _authConnection = new("DataSource=:memory:");

    public CustomWebApplicationFactory()
    {
        _appConnection.Open();
        _authConnection.Open();

        // Create both schemas before the host starts: Program.Main seeds roles
        // and customers right after builder.Build().
        using (var appDb = new Dsw2025TpiContext(
            new DbContextOptionsBuilder<Dsw2025TpiContext>().UseSqlite(_appConnection).Options))
        {
            appDb.Database.EnsureCreated();
        }

        using (var authDb = new AuthenticateContext(
            new DbContextOptionsBuilder<AuthenticateContext>().UseSqlite(_authConnection).Options))
        {
            authDb.Database.EnsureCreated();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            ReplaceWithSqlite<Dsw2025TpiContext>(services, _appConnection);
            ReplaceWithSqlite<AuthenticateContext>(services, _authConnection);
        });
    }

    private static void ReplaceWithSqlite<TContext>(IServiceCollection services, SqliteConnection connection)
        where TContext : DbContext
    {
        // EF Core 9 registers the provider in both services; removing only
        // DbContextOptions<TContext> leaves SQL Server configured as well.
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();
        services.AddDbContext<TContext>(options => options.UseSqlite(connection));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _appConnection.Dispose();
            _authConnection.Dispose();
        }
    }
}
