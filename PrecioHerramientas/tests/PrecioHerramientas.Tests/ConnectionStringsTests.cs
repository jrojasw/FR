using Microsoft.Extensions.Configuration;
using Npgsql;
using PrecioHerramientas.Infrastructure.Persistence;

namespace PrecioHerramientas.Tests;

public class ConnectionStringsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

    [Fact]
    public void DATABASE_URL_tiene_prioridad_y_se_convierte()
    {
        var cs = ConnectionStrings.Resolve(Config(
            ("ConnectionStrings:Default", "Host=localhost;Database=otra"),
            ("DATABASE_URL", "postgresql://postgres:cl%40ve@db.railway.internal:5432/railway?sslmode=require")));

        var b = new NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("db.railway.internal", b.Host);
        Assert.Equal(5432, b.Port);
        Assert.Equal("railway", b.Database);
        Assert.Equal("postgres", b.Username);
        Assert.Equal("cl@ve", b.Password);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void Sin_DATABASE_URL_usa_la_cadena_configurada() =>
        Assert.Equal("Host=localhost;Database=x", ConnectionStrings.Resolve(Config(("ConnectionStrings:Default", "Host=localhost;Database=x"))));
}
