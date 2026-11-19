using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorer.SwarmBridge;

namespace LegendaryExplorer.Lightmaps.Lightmass;

/// <summary>A message the agent relayed (MESSAGE_JOB_STATE, TASK_STATE, INFO, ALERT, QUIT), split into its fields.</summary>
public sealed record SwarmMessage(string Type, string[] Fields);

/// <summary>
/// UnrealSwarm through LEX.SwarmBridge.exe (a .NET Framework process, since the agent's API is .NET Remoting): one request at a
/// time, replies in order, agent messages raised on <see cref="Message"/> from a background thread. Channel files are written
/// directly into the agent's cache, as UE3's managed Swarm interface does. If the bridge exits, or a message handler throws, the
/// client sets <see cref="Failure"/>; a job's wait loop has to check it, since no further messages will come.
/// </summary>
public sealed class SwarmClient : ILightmassChannels, IDisposable
{
    public const string BridgeFileName = "LEX.SwarmBridge.exe";

    readonly Process process;
    readonly CancellationToken cancel;
    readonly TimeSpan requestTimeout;
    readonly SemaphoreSlim requestGate = new(1);
    readonly CancellationTokenSource lifetime = new();
    readonly Thread outputThread;
    int disposed;
    readonly BlockingCollection<string> replies = new();
    readonly Dictionary<Stream, int> openChannels = new();
    readonly StringBuilder errorOutput = new();
    string failure;
    volatile bool disposing;

    public string CacheFolder { get; private set; }
    public string JobFolder { get; private set; }
    public event Action<SwarmMessage> Message;

    /// <summary>Why Swarm can't be relied on any more (the bridge exited, or handling a message failed), or null.</summary>
    public string Failure => Volatile.Read(ref failure);

    void Fail(string why) => Interlocked.CompareExchange(ref failure, why, null);

    /// <param name="binariesDirectory">The UDK Binaries folder with SwarmAgent.exe and AgentInterface.dll.</param>
    /// <param name="bridgePath">LEX.SwarmBridge.exe; by default next to the running program.</param>
    public SwarmClient(string binariesDirectory, string bridgePath = null, CancellationToken cancel = default)
        : this(BridgeStartInfo(bridgePath), binariesDirectory, cancel, TimeSpan.FromSeconds(90))
    {
    }

    static ProcessStartInfo BridgeStartInfo(string bridgePath)
    {
        bridgePath ??= Path.Combine(AppContext.BaseDirectory, BridgeFileName);
        if (!File.Exists(bridgePath)) throw new FileNotFoundException("The Swarm bridge is missing", bridgePath);
        return new ProcessStartInfo(bridgePath);
    }

    // Separate process setup also permits exercising the protocol against a deliberately unresponsive bridge.
    internal SwarmClient(ProcessStartInfo startInfo, string binariesDirectory, CancellationToken cancel, TimeSpan requestTimeout)
    {
        this.cancel = cancel;
        this.requestTimeout = requestTimeout;
        cancel.ThrowIfCancellationRequested();
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = new UTF8Encoding(false);
        startInfo.StandardInputEncoding = new UTF8Encoding(false);
        process = Process.Start(startInfo) ?? throw new InvalidOperationException("couldn't start " + startInfo.FileName);
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errorOutput) errorOutput.AppendLine(e.Data); };
        process.BeginErrorReadLine();
        outputThread = new Thread(ReadOutput) { IsBackground = true, Name = "SwarmBridgeOutput" };
        outputThread.Start();

        try
        {
            CacheFolder = Request("connect", binariesDirectory)[0];
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    void ReadOutput()
    {
        try
        {
            string line;
            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                if (!line.StartsWith("msg\t", StringComparison.Ordinal))
                {
                    replies.Add(line);
                    continue;
                }
                var fields = SwarmProtocol.Split(line);
                try
                {
                    Message?.Invoke(new SwarmMessage(fields[1], fields[2..]));
                }
                catch (Exception e)
                {
                    // This is a background thread: an exception escaping it would take the whole program down.
                    Fail($"Handling Swarm's {fields[1]} message failed: {e.Message}");
                }
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
        {
            Fail($"Reading from the Swarm bridge failed: {e.Message}");
        }
        finally
        {
            if (!disposing) Fail("The Swarm bridge exited unexpectedly. " + StandardError());
            replies.CompleteAdding();
        }
    }

    string StandardError()
    {
        lock (errorOutput) return errorOutput.ToString();
    }

    /// <summary>Sends a request and waits for its reply's fields; throws on an error reply.</summary>
    public string[] Request(params string[] fields) => Request(fields, cancel, requestTimeout);

    string[] Request(string[] fields, CancellationToken requestCancel, TimeSpan timeout)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(requestCancel, lifetime.Token);
        deadline.CancelAfter(timeout);
        bool entered = false;
        try
        {
            requestGate.Wait(deadline.Token);
            entered = true;
            if (Failure is { } failed) throw new InvalidOperationException(failed);
            process.StandardInput.WriteLineAsync(SwarmProtocol.Join(fields).AsMemory(), deadline.Token)
                .WaitAsync(deadline.Token).GetAwaiter().GetResult();
            process.StandardInput.FlushAsync(deadline.Token).WaitAsync(deadline.Token).GetAwaiter().GetResult();
            if (!replies.TryTake(out string reply, Timeout.Infinite, deadline.Token))
                throw new InvalidOperationException("The Swarm bridge exited unexpectedly. " + StandardError());
            var parts = SwarmProtocol.Split(reply);
            if (parts[0] == "err") throw new InvalidOperationException($"Swarm {fields[0]}: {parts.ElementAtOrDefault(1)}");
            return parts.Skip(1).ToArray();
        }
        catch (OperationCanceledException)
        {
            // Replies carry no request IDs. After abandoning one, the connection must never be reused.
            Fail($"Swarm {fields[0]} was cancelled or timed out.");
            StopBridge();
            requestCancel.ThrowIfCancellationRequested();
            if (lifetime.IsCancellationRequested) throw new ObjectDisposedException(nameof(SwarmClient));
            throw new InvalidOperationException($"Swarm {fields[0]} did not reply within {timeout.TotalSeconds:g} seconds.");
        }
        catch (IOException e)
        {
            Fail($"Writing to the Swarm bridge failed: {e.Message}");
            StopBridge();
            throw new InvalidOperationException(Failure, e);
        }
        finally
        {
            if (entered) requestGate.Release();
        }
    }

    void StopBridge()
    {
        try { if (!process.HasExited) process.Kill(); }
        catch (InvalidOperationException) { } // The process exited between checking and killing it.
    }

    public void OpenJob(Guid jobGuid) => JobFolder = Request("openjob", LightmassFormat.Ue3String(jobGuid))[0];

    public bool PersistentChannelExists(string channelName) => int.Parse(Request("testchannel", channelName)[0]) >= 0;

    public Stream OpenWrite(string channelName, int flags)
    {
        var reply = Request("openchannel", channelName, flags.ToString());
        var stream = new FileStream(reply[1], FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        lock (openChannels) openChannels[stream] = int.Parse(reply[0]);
        return stream;
    }

    public void Close(string channelName, int flags, Stream stream)
    {
        int handle;
        lock (openChannels)
        {
            handle = openChannels[stream];
            openChannels.Remove(stream);
        }
        stream.Dispose();
        Request("closechannel", handle.ToString(), channelName, flags.ToString());
    }

    public void Abort(string channelName, int flags, Stream stream)
    {
        int handle;
        lock (openChannels)
        {
            handle = openChannels[stream];
            openChannels.Remove(stream);
        }
        stream.Dispose();
        Request("abortchannel", handle.ToString(), channelName, flags.ToString());
    }

    /// <summary>A job channel's file (results), or null if Lightmass hasn't written it.</summary>
    public string JobChannelPath(string channelName)
    {
        string path = Path.Combine(JobFolder, channelName);
        return File.Exists(path) ? path : null;
    }

    /// <param name="executable">Full path of UnrealLightmass.exe</param>
    /// <param name="dependencies">Full paths of files the job needs beside it (AgentInterface.dll)</param>
    public void BeginJobSpecification(string executable, string parameters, int flags, IEnumerable<string> dependencies, string mapName) =>
        Request("beginjob", executable, parameters, flags.ToString(), string.Join("|", dependencies), mapName);

    public void AddTask(Guid taskGuid, string parameters, int cost) => Request("addtask", LightmassFormat.Ue3String(taskGuid), parameters, cost.ToString());
    public void EndJobSpecification() => Request("endjob");
    // Closing the job must still work after the bake token is cancelled, but cleanup has a short deadline.
    public void CloseJob() => Request(["closejob"], CancellationToken.None, TimeSpan.FromSeconds(5));

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        disposing = true;
        lifetime.Cancel();
        try
        {
            // EOF asks a healthy bridge to close its connection. Never await a protocol reply during shutdown.
            // A write already in flight is interrupted by killing the bridge instead of closing its busy writer.
            if (requestGate.Wait(0))
            {
                try { process.StandardInput.Close(); }
                finally { requestGate.Release(); }
                if (!process.WaitForExit(5000)) StopBridge();
            }
            else StopBridge();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            StopBridge();
        }
        finally
        {
            lock (openChannels)
            {
                foreach (var stream in openChannels.Keys) stream.Dispose();
                openChannels.Clear();
            }
            outputThread.Join(5000);
            process.Dispose();
        }
    }
}
