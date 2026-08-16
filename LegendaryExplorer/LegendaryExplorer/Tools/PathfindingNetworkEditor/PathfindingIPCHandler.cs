using LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Globalization;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor
{
    public static class PathfindingIPCHandler
    {
        public class GameIPCState
        {
            public MEGame Game { get; }
            public PlayerNode Player { get; } = new PlayerNode();
            public CameraNode Camera { get; } = new CameraNode();

            private CancellationTokenSource _cts = new CancellationTokenSource();

            public GameIPCState(MEGame game)
            {
                Game = game;
                StartListening();
            }

            private void StartListening()
            {
                Task.Run(async () =>
                {
                    var pipeName = $"pathfindingnetworkeditor{Game}";
                    while (!_cts.IsCancellationRequested)
                    {
                        try
                        {
                            using var pipeServer = new NamedPipeServerStream(
                                pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                            await pipeServer.WaitForConnectionAsync(_cts.Token);

                            using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
                            while (!reader.EndOfStream && pipeServer.IsConnected && !_cts.IsCancellationRequested)
                            {
                                var line = await reader.ReadLineAsync();
                                if (line == null) break;

                                ProcessMessage(line);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Pipe error: {ex}");
                            await Task.Delay(1000, _cts.Token);
                        }
                    }
                });
            }

            private void ProcessMessage(string message)
            {
                // Simple parsing: TARGET X Y Z PITCH YAW ROLL [FOV]
                // TARGET can be PLAYER or CAMERA
                var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 7) return;

                bool isPlayer = string.Equals(parts[0], "PLAYER", StringComparison.OrdinalIgnoreCase);
                bool isCamera = string.Equals(parts[0], "CAMERA", StringComparison.OrdinalIgnoreCase);

                if (!isPlayer && !isCamera) return;

                if (double.TryParse(parts[1], CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(parts[2], CultureInfo.InvariantCulture, out var y) &&
                    double.TryParse(parts[3], CultureInfo.InvariantCulture, out var z) &&
                    int.TryParse(parts[5], CultureInfo.InvariantCulture, out var yaw))
                {
                    // Apply to main thread for data binding / graph rendering safety
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (isPlayer)
                        {
                            Player.X = x;
                            Player.Y = y;
                            Player.Z = z;
                            Player.RotationYaw = yaw;
                        }
                        else
                        {
                            Camera.X = x;
                            Camera.Y = y;
                            Camera.Z = z;
                            Camera.RotationYaw = yaw;

                            if (parts.Length >= 8 && float.TryParse(parts[7], CultureInfo.InvariantCulture, out var fov))
                            {
                                Camera.FOV = fov;
                            }
                        }
                    });
                }
            }

            public void Stop()
            {
                _cts.Cancel();
            }
        }

        private static ConcurrentDictionary<MEGame, GameIPCState> _states = new();

        public static GameIPCState GetState(MEGame game)
        {
            return _states.GetOrAdd(game, g => new GameIPCState(g));
        }
    }
}