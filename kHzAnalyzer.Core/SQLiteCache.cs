using System;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace KhzAnalyzer.Core;

public class AnalysisCache : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _lock = new object();

    public AnalysisCache(string dbPath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        using var command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Cache (
                FileHash TEXT PRIMARY KEY,
                FilePath TEXT,
                FileSize INTEGER,
                LastModified INTEGER,
                SampleRate INTEGER,
                Bits INTEGER,
                FreqMax REAL,
                Verdict TEXT,
                Error TEXT
            )";
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private string GetFileHash(string path, FileInfo fi)
    {
        // Using FilePath + Size + LastWriteTime as a fast pseudo-hash for cache invalidation
        string input = $"{path}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}";
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    public AnalysisResult? TryGet(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return null;

            string hash = GetFileHash(path, fi);

            lock (_lock)
            {
                using var command = _connection.CreateCommand();
                command.CommandText = "SELECT SampleRate, Bits, FreqMax, Verdict, Error FROM Cache WHERE FileHash = $hash";
                command.Parameters.AddWithValue("$hash", hash);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new AnalysisResult
                    {
                        Arquivo = path,
                        SampleRate = reader.GetInt32(0),
                        Bits = reader.GetInt32(1),
                        FreqMaximaDetectada = reader.GetDouble(2),
                        Veredito = reader.GetString(3),
                        Erro = reader.IsDBNull(4) ? null : reader.GetString(4)
                    };
                }
            }
        }
        catch
        {
            // Fail silently, just skip cache
        }
        return null;
    }

    public void Save(AnalysisResult result)
    {
        try
        {
            var fi = new FileInfo(result.Arquivo);
            if (!fi.Exists) return;

            string hash = GetFileHash(result.Arquivo, fi);

            lock (_lock)
            {
                using var command = _connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Cache (FileHash, FilePath, FileSize, LastModified, SampleRate, Bits, FreqMax, Verdict, Error)
                    VALUES ($hash, $path, $size, $mod, $sr, $bits, $freq, $verdict, $err)
                    ON CONFLICT(FileHash) DO UPDATE SET
                        SampleRate = excluded.SampleRate,
                        Bits = excluded.Bits,
                        FreqMax = excluded.FreqMax,
                        Verdict = excluded.Verdict,
                        Error = excluded.Error";

                command.Parameters.AddWithValue("$hash", hash);
                command.Parameters.AddWithValue("$path", result.Arquivo);
                command.Parameters.AddWithValue("$size", fi.Length);
                command.Parameters.AddWithValue("$mod", fi.LastWriteTimeUtc.Ticks);
                command.Parameters.AddWithValue("$sr", result.SampleRate);
                command.Parameters.AddWithValue("$bits", result.Bits);
                command.Parameters.AddWithValue("$freq", result.FreqMaximaDetectada);
                command.Parameters.AddWithValue("$verdict", result.Veredito);
                command.Parameters.AddWithValue("$err", result.Erro ?? (object)DBNull.Value);

                command.ExecuteNonQuery();
            }
        }
        catch
        {
            // Fail silently
        }
    }
}
