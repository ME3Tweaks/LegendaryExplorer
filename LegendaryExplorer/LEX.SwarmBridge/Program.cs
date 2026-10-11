using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Text;
using System.Threading;

namespace LegendaryExplorer.SwarmBridge;

/// <summary>
/// A line protocol between LEX and UnrealSwarm's SwarmAgent: the control half of UE3's FSwarmInterfaceManagedImpl
/// (IB Src/UnrealSwarm/Src/SwarmInterface.cpp). LEX writes and reads the channel files itself; this process asks the agent for
/// the connection, the job, the tasks and the channel bookkeeping, and relays the agent's messages.
///
/// Requests on stdin, one per line (<see cref="SwarmProtocol"/>). Each gets one reply line, "ok[\tfields]" or "err\tmessage".
/// Agent messages are interleaved as "msg\t..." lines.
/// </summary>
internal static class Program
{
    static readonly object OutputLock = new();
    static Agent agent;

    static int Main()
    {
        Console.InputEncoding = Console.OutputEncoding = new UTF8Encoding(false);
        string line;
        while ((line = Console.ReadLine()) != null)
        {
            string[] args = SwarmProtocol.Split(line);
            try
            {
                Write(new[] { "ok" }.Concat(Handle(args)));
                if (args[0] == "close") return 0;
            }
            catch (Exception e)
            {
                var inner = e is TargetInvocationException { InnerException: { } i } ? i : e;
                Write(["err", inner.GetType().Name + ": " + inner.Message]);
            }
        }
        agent?.Close();
        return 0;
    }

    /// <summary>Carries out a request; returns its reply's fields.</summary>
    static string[] Handle(string[] a)
    {
        switch (a[0])
        {
            case "connect":
                if (agent != null) throw new InvalidOperationException("already connected");
                agent = new Agent(a[1], message => Write(new[] { "msg" }.Concat(message)));
                return agent.Connect();
            case "openjob":
                return [agent.OpenJob(ParseGuid(a[1]))];
            case "testchannel":
                return [agent.TestChannel(a[1]).ToString()];
            case "openchannel":
                return agent.OpenChannel(a[1], int.Parse(a[2]));
            case "closechannel":
                agent.CloseChannel(int.Parse(a[1]), a[2], int.Parse(a[3]));
                return [];
            case "abortchannel":
                agent.AbortChannel(int.Parse(a[1]), a[2], int.Parse(a[3]));
                return [];
            case "beginjob":
                agent.BeginJobSpecification(a[1], a[2], int.Parse(a[3]), a[4].Length == 0 ? new string[0] : a[4].Split('|'), a[5]);
                return [];
            case "addtask":
                agent.AddTask(ParseGuid(a[1]), a[2], int.Parse(a[3]));
                return [];
            case "endjob":
                agent.EndJobSpecification();
                return [];
            case "closejob":
                agent.CloseJob();
                return [];
            case "close":
                agent?.Close();
                return [];
            default:
                throw new ArgumentException("unknown request " + a[0]);
        }
    }

    /// <summary>A UE3 FGuid as 32 hex digits (A, B, C, D as %08X).</summary>
    static uint[] ParseGuid(string s) => Enumerable.Range(0, 4).Select(i => Convert.ToUInt32(s.Substring(i * 8, 8), 16)).ToArray();

    static void Write(IEnumerable<string> fields)
    {
        string line = SwarmProtocol.Join(fields);
        lock (OutputLock)
        {
            Console.Out.WriteLine(line);
            Console.Out.Flush();
        }
    }
}

/// <summary>The agent's IAgentInterface, used through reflection on the user's AgentInterface.dll.</summary>
internal sealed class Agent(string binariesDirectory, Action<string[]> onMessage)
{
    const int Version = 16; // ESwarmVersionValue.VER_1_0
    const int ChannelPersistent = 0x1, ChannelJobOnly = 0x2, ChannelRead = 0x10, ChannelWrite = 0x20;

    Assembly assembly;
    Type interfaceType, guidType;
    object connection;
    int handle = -1;
    string cacheFolder;
    bool pureLocal;
    object jobGuid;
    string jobGuidString;
    int localChannelHandles;
    readonly List<object> pendingTasks = new();

    Type T(string name) => assembly.GetType("AgentInterface." + name, true);

    /// <summary>Calls a (ConnectionHandle, InParameters, ref OutParameters) method; returns its result and the output table.</summary>
    (int Result, Hashtable Out) Call(string method, Hashtable inParameters)
    {
        object[] args = [handle, inParameters, null];
        var m = interfaceType.GetMethod(method) ?? throw new MissingMethodException("IAgentInterface." + method);
        int result = (int)m.Invoke(connection, args);
        return (result, args[2] as Hashtable);
    }

    static Hashtable Parameters(params (string Key, object Value)[] entries)
    {
        var table = new Hashtable { ["Version"] = Version };
        foreach (var (key, value) in entries) table[key] = value;
        return table;
    }

    static void Check(int result, string what)
    {
        if (result < 0) throw new InvalidOperationException($"{what} failed with Swarm error {result}");
    }

    /// <summary>FSwarmInterfaceManagedImpl::OpenConnection: starts the agent if needed, connects and starts the message pump.</summary>
    public string[] Connect()
    {
        string interfacePath = Path.Combine(binariesDirectory, "AgentInterface.dll");
        assembly = Assembly.LoadFrom(interfacePath);
        // Remoting deserializes the agent's types by name; point it at the copy we loaded.
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => new AssemblyName(e.Name).Name == "AgentInterface" ? assembly : null;
        interfaceType = T("IAgentInterface");
        guidType = T("AgentGuid");

        ChannelServices.RegisterChannel(new TcpClientChannel(), false);
        // Only one agent can listen on Swarm's port, and it must be the one whose AgentInterface.dll was loaded (and was checked by LEX).
        string agentPath = Path.GetFullPath(Path.Combine(binariesDirectory, "SwarmAgent.exe"));
        var running = Process.GetProcessesByName("SwarmAgent");
        var agentProcess = running.FirstOrDefault(p => string.Equals(ExecutablePath(p), agentPath, StringComparison.OrdinalIgnoreCase));
        if (agentProcess == null && running.Length > 0)
            throw new InvalidOperationException($"another SwarmAgent is running ({ExecutablePath(running[0]) ?? "location unknown"}). " +
                                                $"Close it from its tray icon, so that the one in {binariesDirectory} can be used.");
        bool owner = false;
        if (agentProcess == null)
        {
            agentProcess = Process.Start(new ProcessStartInfo(agentPath) { WorkingDirectory = binariesDirectory, UseShellExecute = true })
                           ?? throw new InvalidOperationException("couldn't start " + agentPath);
            owner = true;
        }
        connection = Activator.GetObject(interfaceType, "tcp://127.0.0.1:8008/SwarmAgent");

        // Wait for the agent to answer (it may be starting up)
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                object[] args = [0, null, null];
                interfaceType.GetMethod("Method").Invoke(connection, args);
                break;
            }
            catch (TargetInvocationException) when (timer.Elapsed.TotalSeconds < 60 && !agentProcess.HasExited)
            {
                Thread.Sleep(500);
            }
        }

        object[] open = [Parameters(("ProcessID", Process.GetCurrentProcess().Id), ("ProcessIsOwner", owner), ("LoggingFlags", Enum.ToObject(T("ELogFlags"), 0))), null];
        handle = (int)interfaceType.GetMethod("OpenConnection").Invoke(connection, open);
        Check(handle, "OpenConnection");
        var output = (Hashtable)open[1];
        cacheFolder = (string)output["AgentCachePath"];
        pureLocal = output.ContainsKey("IsPureLocalConnection") && (bool)output["IsPureLocalConnection"];
        if (string.IsNullOrEmpty(cacheFolder)) throw new InvalidOperationException("the agent has no cache folder");

        new Thread(MessagePump) { IsBackground = true, Name = "SwarmMessages" }.Start();
        return [cacheFolder, pureLocal ? "1" : "0"];
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

    /// <summary>A process's executable, or null if it can't be read. (Process.MainModule needs more access than an elevated agent gives.)</summary>
    static string ExecutablePath(Process process)
    {
        try
        {
            var name = new StringBuilder(1024);
            int size = name.Capacity;
            return QueryFullProcessImageName(process.Handle, 0, name, ref size) ? Path.GetFullPath(name.ToString(0, size)) : null;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return null; // access denied, or it has exited
        }
    }

    void MessagePump()
    {
        try
        {
            while (true)
            {
                var (result, output) = Call("GetMessage", Parameters(("Timeout", -1)));
                if (result < 0)
                {
                    onMessage(["ERROR", $"GetMessage failed with Swarm error {result}"]);
                    break;
                }
                if (output?["Message"] is not { } message) continue;
                string type = Field(message, "Type").ToString();
                onMessage(type switch
                {
                    "JOB_STATE" => [type, Field(message, "JobState").ToString(), Field(message, "JobMessage") as string],
                    "TASK_STATE" => [type, GuidString(Field(message, "TaskGuid")), Field(message, "TaskState").ToString(), Field(message, "TaskMessage") as string],
                    "INFO" => [type, Field(message, "TextMessage") as string],
                    "ALERT" => [type, Field(message, "AlertLevel").ToString(), GuidString(Field(message, "ObjectGuid")), Field(message, "TypeId").ToString(),
                        Field(message, "TextMessage") as string],
                    _ => [type],
                });
                if (type == "QUIT") break;
            }
        }
        catch (Exception e)
        {
            onMessage(["ERROR", e.InnerException?.Message ?? e.Message]);
        }
    }

    static object Field(object o, string name) => o.GetType().GetField(name)?.GetValue(o);

    static string GuidString(object agentGuid) => agentGuid == null ? "" :
        string.Concat(new[] { "A", "B", "C", "D" }.Select(f => ((uint)Field(agentGuid, f)).ToString("X8")));

    object NewGuid(uint[] parts) => Activator.CreateInstance(guidType, parts[0], parts[1], parts[2], parts[3]);

    public string OpenJob(uint[] guid)
    {
        jobGuid = NewGuid(guid);
        jobGuidString = jobGuid.ToString();
        Check(Call("OpenJob", Parameters(("JobGuid", jobGuid))).Result, "OpenJob");
        pendingTasks.Clear();
        return Path.Combine(Path.Combine(cacheFolder, "Jobs"), "Job-" + jobGuidString);
    }

    /// <summary>GenerateFullChannelName: persistent writes go to the staging area, persistent reads to the cache, job channels to the job's folder.</summary>
    string FullChannelName(string name, int flags)
    {
        if ((flags & ChannelPersistent) != 0)
            return (flags & ChannelWrite) != 0 ? Path.Combine(Path.Combine(cacheFolder, "AgentStagingArea"), name) : Path.Combine(cacheFolder, name);
        if ((flags & ChannelJobOnly) != 0)
            return Path.Combine(Path.Combine(Path.Combine(cacheFolder, "Jobs"), "Job-" + (jobGuidString ?? "none")), name);
        throw new ArgumentException("channel flags without a type");
    }

    public int TestChannel(string name)
    {
        if (pureLocal) return File.Exists(FullChannelName(name, ChannelPersistent | ChannelRead)) ? 0 : -2;
        return Call("TestChannel", Parameters(("ChannelName", name))).Result;
    }

    public string[] OpenChannel(string name, int flags)
    {
        int channel;
        if (pureLocal) channel = Interlocked.Increment(ref localChannelHandles);
        else
        {
            channel = Call("OpenChannel", Parameters(("ChannelName", name), ("ChannelFlags", Enum.ToObject(T("EChannelFlags"), flags)))).Result;
            Check(channel, "OpenChannel " + name);
        }
        string path = FullChannelName(name, flags);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        return [channel.ToString(), path];
    }

    public void CloseChannel(int channel, string name, int flags)
    {
        if (!pureLocal)
        {
            Check(Call("CloseChannel", Parameters(("ChannelHandle", channel))).Result, "CloseChannel " + name);
            return;
        }
        // A pure local connection moves a written persistent channel from the staging area into the cache itself.
        if ((flags & ChannelPersistent) != 0 && (flags & ChannelWrite) != 0)
        {
            string source = FullChannelName(name, ChannelPersistent | ChannelWrite), destination = FullChannelName(name, ChannelPersistent | ChannelRead);
            DeleteChannelFile(destination);
            File.Move(source, destination);
        }
    }

    /// <summary>
    /// Discards a channel whose writing failed. Its file is deleted rather than committed, so a partial channel never reaches the cache, where
    /// every later bake of the same content (persistent channels are named by content) would reuse it.
    /// </summary>
    public void AbortChannel(int channel, string name, int flags)
    {
        DeleteChannelFile(FullChannelName(name, flags));
        if (!pureLocal)
        {
            try { Call("CloseChannel", Parameters(("ChannelHandle", channel))); }
            catch (TargetInvocationException)
            {
                // The agent may object that the file it would commit is gone. The channel is discarded either way.
            }
        }
        // A persistent channel is only written when the cache doesn't have it, so a cached copy now is this partial one.
        if ((flags & ChannelPersistent) != 0) DeleteChannelFile(FullChannelName(name, ChannelPersistent | ChannelRead));
    }

    static void DeleteChannelFile(string path)
    {
        if (!File.Exists(path)) return;
        File.SetAttributes(path, FileAttributes.Normal);
        File.Delete(path);
    }

    /// <summary>CacheFileAndConvertName: executables and dependencies enter the cache as name_mtime_size[-64bit].ext.</summary>
    string CacheFile(string fullPath, bool is64bit)
    {
        var info = new FileInfo(fullPath);
        if (!info.Exists) throw new FileNotFoundException("Swarm job file not found", fullPath);
        string cached = string.Format("{0}_{1}_{2}{3}{4}", Path.GetFileNameWithoutExtension(fullPath), info.LastWriteTimeUtc.ToString("yyyy-MM-dd_HH-mm-ss"),
            info.Length, is64bit ? "-64bit" : "", info.Extension);
        if (Call("TestChannel", Parameters(("ChannelName", cached))).Result < 0)
            Check(Call("AddChannel", Parameters(("FullPath", fullPath), ("ChannelName", cached))).Result, "AddChannel " + fullPath);
        return cached;
    }

    /// <summary>BeginJobSpecification with a 64-bit specification only (Lightmass is run from Win64).</summary>
    public void BeginJobSpecification(string executable, string parameters, int flags, string[] dependencies, string mapName)
    {
        const int Flag64Bit = 4;
        var names = new Dictionary<string, string>();
        string cachedExecutable = CacheFile(executable, true);
        names[cachedExecutable] = Path.GetFileName(executable);
        var listType = typeof(List<>).MakeGenericType(typeof(string));
        var required = (IList)Activator.CreateInstance(listType);
        foreach (string dependency in dependencies)
        {
            string cached = CacheFile(dependency, true);
            required.Add(cached);
            names[cached] = Path.GetFileName(dependency);
        }
        var specification = Activator.CreateInstance(T("AgentJobSpecification"), jobGuid, Enum.ToObject(T("EJobTaskFlags"), flags | Flag64Bit),
            cachedExecutable, parameters, required, null);
        specification.GetType().GetField("DependenciesOriginalNames").SetValue(specification, names);
        var description = Parameters(("MapName", mapName), ("GameName", "LegendaryExplorer"), ("QualityLevel", "0"));
        Check(Call("BeginJobSpecification", Parameters(("Specification32", null), ("Specification64", specification),
            ("Description32", null), ("Description64", description))).Result, "BeginJobSpecification");
    }

    public void AddTask(uint[] guid, string parameters, int cost)
    {
        pendingTasks.Add(Activator.CreateInstance(T("AgentTaskSpecification"), jobGuid, NewGuid(guid), 0, parameters, cost, null));
    }

    public void EndJobSpecification()
    {
        var listType = typeof(List<>).MakeGenericType(T("AgentTaskSpecification"));
        var list = (IList)Activator.CreateInstance(listType);
        foreach (var task in pendingTasks) list.Add(task);
        Check(Call("AddTask", Parameters(("Specifications", list))).Result, "AddTask");
        pendingTasks.Clear();
        Check(Call("EndJobSpecification", (Hashtable)null).Result, "EndJobSpecification");
    }

    public void CloseJob()
    {
        Check(Call("CloseJob", (Hashtable)null).Result, "CloseJob");
        jobGuid = null;
    }

    public void Close()
    {
        if (connection == null || handle < 0) return;
        try { Call("CloseConnection", (Hashtable)null); }
        catch (TargetInvocationException)
        {
            // The agent may already be gone; there's nothing left to close then.
        }
        handle = -1;
    }
}
