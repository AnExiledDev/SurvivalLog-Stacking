using System;
using System.IO;
using System.Threading;
using BepInEx.Logging;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>
/// Writes to the BepInEx log and to an append-only file that survives restarts
/// (LogOutput.log and Player.log are both overwritten on the next launch).
/// File lines are queued and appended by a worker thread, so no hook or frame waits on the disk; the worker writes the
/// queue as soon as a line arrives, and what is left is written when the game quits.
/// Never throws: it is called from inside Load and the stacking hooks, where an escaping exception would reach the game.
/// </summary>
internal sealed class StackLog
{
    private const long TrimAboveBytes = 1024 * 1024;
    private const int KeepBytes = 200 * 1024;

    private readonly ManualLogSource _bepinex;
    private readonly string _path;
    private readonly object _fileGate = new();
    private readonly LogLines _lines = new();
    private readonly AutoResetEvent _wake = new(false);

    public StackLog(ManualLogSource bepinex, string path)
    {
        _bepinex = bepinex;
        _path = path;
    }

    /// <summary>Starts the worker that appends queued lines to the file, and the last write on quit.</summary>
    public void Start()
    {
        new Thread(WriteLoop) { IsBackground = true, Name = "Stacking log" }.Start();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => WritePending();
    }

    public void Write(string message)
    {
        if (HookFailure.Is(message)) _bepinex.LogError(message);
        else _bepinex.LogInfo(message);

        WriteFileOnly(message);
    }

    /// <summary>For chatty lines (each stack decision) that would flood LogOutput.log. Only queues the line.</summary>
    public void WriteFileOnly(string message)
    {
        if (_lines.Add(DateTime.UtcNow, message)) _wake.Set();
    }

    /// <summary>Stack decision lines add up over a long session, so past 1 MB only the newest ~200 KB of whole lines is kept.</summary>
    public void TrimIfLarge()
    {
        lock (_fileGate)
        {
            try
            {
                var file = new FileInfo(_path);

                if (!file.Exists || file.Length <= TrimAboveBytes) return;

                var bytes = File.ReadAllBytes(_path);
                var cut = bytes.Length - KeepBytes;
                var nextLine = Array.IndexOf(bytes, (byte)'\n', cut) + 1;

                File.WriteAllBytes(_path, bytes[(nextLine > 0 ? nextLine : cut)..]);
            }
            catch (Exception)
            {
                // An oversized log is harmless; failing to trim it must not stop the plugin loading.
            }
        }
    }

    private void WriteLoop()
    {
        while (true)
        {
            _wake.WaitOne();
            WritePending();
        }
    }

    /// <summary>On the worker, or on quit; the file lock keeps the two from writing one batch over another.</summary>
    private void WritePending()
    {
        lock (_fileGate)
        {
            try
            {
                if (_lines.TakeBatch() is { } batch) File.AppendAllText(_path, batch);
            }
            catch (Exception)
            {
                // A locked or read-only file costs the file copy, never the plugin.
            }
        }
    }
}
