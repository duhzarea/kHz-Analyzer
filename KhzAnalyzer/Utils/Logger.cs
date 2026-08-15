using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KhzAnalyzer.Utils;

public static class Logger
{
    private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kHzAnalyzer", "logs");
    private static readonly string LogFile = Path.Combine(LogDir, $"app_{DateTime.Now:yyyyMMdd}.log");
    private static readonly ConcurrentQueue<string> _queue = new();
    private static int _isWriting = 0;

    static Logger()
    {
        try { Directory.CreateDirectory(LogDir); } catch { }
    }

    public static void Log(string message)
    {
        _queue.Enqueue($"[{DateTime.Now:O}] {message}");
        if (Interlocked.CompareExchange(ref _isWriting, 1, 0) == 0)
        {
            Task.Run(FlushQueue);
        }
    }

    private static void FlushQueue()
    {
        try
        {
            using var sw = new StreamWriter(LogFile, append: true);
            while (_queue.TryDequeue(out var msg))
            {
                sw.WriteLine(msg);
            }
        }
        catch { }
        finally
        {
            Volatile.Write(ref _isWriting, 0);
            if (!_queue.IsEmpty && Interlocked.CompareExchange(ref _isWriting, 1, 0) == 0)
            {
                Task.Run(FlushQueue);
            }
        }
    }
}
