using Dapper;
using System.Runtime.CompilerServices;

namespace CodeCiir.Infrastructure.Database;

internal static class VectorTypeHandlerRegistration
{
#pragma warning disable CA2255 // Deliberate: register the handler as soon as this assembly loads, regardless of DI wiring.
    [ModuleInitializer]
    internal static void Register() => SqlMapper.AddTypeHandler(new VectorTypeHandler());
#pragma warning restore CA2255
}
