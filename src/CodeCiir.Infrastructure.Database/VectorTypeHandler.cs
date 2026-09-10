using Dapper;
using Pgvector;
using System.Data;

namespace CodeCiir.Infrastructure.Database;

/// <summary>
/// Lets Dapper pass a <see cref="Vector"/> straight through as a query parameter. Without this,
/// Dapper rejects it while inferring a <see cref="DbType"/>, since <c>vector</c> has no built-in
/// ADO.NET mapping - Npgsql's own pgvector plugin (enabled via <c>UseVector()</c>) takes it from there.
/// </summary>
#pragma warning disable CS8765 // Dapper's TypeHandler<T> base is nullable-oblivious; Vector is never actually null here.
internal sealed class VectorTypeHandler : SqlMapper.TypeHandler<Vector>
{
    public override void SetValue(IDbDataParameter parameter, Vector value) => parameter.Value = value;

    public override Vector Parse(object value) => (Vector)value;
}
#pragma warning restore CS8765
