using Npgsql;
using System;

var cs = "Host=localhost;Port=5432;Database=logistica_envios;Username=logistica;Password=logistica;Search Path=checkout,public";
using var conn = new NpgsqlConnection(cs);
conn.Open();
foreach (var table in new[] { "checkouts", "checkout_items", "outbox_messages" })
{
    Console.WriteLine($"--- {table} ---");
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"SELECT table_schema, table_name, column_name, is_nullable, data_type, column_default FROM information_schema.columns WHERE table_schema='checkout' AND table_name='{table}' ORDER BY ordinal_position;";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        var defaultValue = reader.IsDBNull(5) ? "NULL" : reader.GetValue(5);
        Console.WriteLine($"{reader.GetString(0)}.{reader.GetString(1)}.{reader.GetString(2)} nullable={reader.GetString(3)} type={reader.GetString(4)} default={defaultValue}");
    }
    Console.WriteLine();
}
