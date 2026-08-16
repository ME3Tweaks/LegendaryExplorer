using LegendaryExplorer.SharedUI.Bases;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Controls;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Models;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Nodes;
using LegendaryExplorerCore.Misc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor
{
    /// <summary>
    /// Interaction logic for PathfindingNetworkEditorWindow.xaml
    /// </summary>
    public partial class PathfindingNetworkEditorWindow : TrackingNotifyPropertyChangedWindowBase
    {

        #region BusyHost
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _busyText;
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }
        #endregion

        private const double MaxCoverLineWidth = 10.0;

        private List<NavigationPoint> TemporaryHighlightedNodes = new List<NavigationPoint>();
        private NavigationPoint? _nodeWithTemporaryConnections;

        #region Cover display options
        private bool _showFirelinks = true;
        public bool ShowFirelinks
        {
            get => _showFirelinks;
            set
            {
                SetProperty(ref _showFirelinks, value);
                RefreshCoverSlotMarkerConnections();
            }
        }

        private bool _showDangerNavs;
        public bool ShowDangerNavs
        {
            get => _showDangerNavs;
            set
            {
                SetProperty(ref _showDangerNavs, value);
                RefreshCoverSlotMarkerConnections();
            }
        }

        private bool _showExposedCover;
        public bool ShowExposedCover
        {
            get => _showExposedCover;
            set
            {
                SetProperty(ref _showExposedCover, value);
                RefreshCoverSlotMarkerConnections();
            }
        }

        private bool _showRotation;
        public bool ShowRotation
        {
            get => _showRotation;
            set
            {
                SetProperty(ref _showRotation, value);
                if (GraphEditor is PathfindingNetworkGraphEditor pfne)
                    pfne.ShowRotation = value;
            }
        }

        private bool _followCamera;
        public bool FollowCamera
        {
            get => _followCamera;
            set => SetProperty(ref _followCamera, value);
        }
        #endregion

        #region Z filter
        private double _graphZMin;
        public double GraphZMin
        {
            get => _graphZMin;
            set => SetProperty(ref _graphZMin, value);
        }

        private double _graphZMax = 1;
        public double GraphZMax
        {
            get => _graphZMax;
            set => SetProperty(ref _graphZMax, value);
        }

        private double _zFilterMin;
        public double ZFilterMin
        {
            get => _zFilterMin;
            set
            {
                SetProperty(ref _zFilterMin, value);
                ApplyZFilter();
            }
        }

        private double _zFilterMax = 1;
        public double ZFilterMax
        {
            get => _zFilterMax;
            set
            {
                SetProperty(ref _zFilterMax, value);
                ApplyZFilter();
            }
        }

        private double _cameraZ;
        public double CameraZ
        {
            get => _cameraZ;
            set => SetProperty(ref _cameraZ, value);
        }

        private void ApplyZFilter()
        {
            if (GraphEditor is PathfindingNetworkGraphEditor pfne)
                pfne.SetZFilter(_zFilterMin, _zFilterMax);
        }
        #endregion

        private CameraNode? _currentCameraNode;

        public PathfindingNetworkEditorWindow() : base(nameof(PathfindingNetworkEditorWindow), true)
        {
            LoadCommands();
            InitializeComponent();

            Recents_Control.InitRecentControl("PathfindingNetworkEditor", Recents_MenuItem, OpenRecentsFile);

            GraphEditor.NodeHoverInfoProvider = node =>
            {
                if (node is NavigationPoint nav)
                    return [
                        ("Class",  nav.Export.ClassName),
                        ("Package", nav.Export.FileRef.FileNameNoExtension),
                        ("Path",   nav.Export.InstancedFullPath),
                        ("Pos",    $"{nav.X:F0}, {nav.Y:F0}, {nav.Z:F0}"),
                    ];
                return null;
            };

            GraphEditor.SelectionChanged += OnNodeSelectionChanged;
            GraphEditor.GraphMouseMoved += (_, p) => StatusBar_Coordinates.Text = $"X: {p.X:F0} Y: {p.Y:F0}";
        }



        private void OnCameraNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_currentCameraNode == null) return;
            if (e.PropertyName == nameof(GraphNode.Z))
                CameraZ = _currentCameraNode.Z;
            if (!FollowCamera) return;
            if (e.PropertyName == nameof(GraphNode.X) || e.PropertyName == nameof(GraphNode.Y))
            {
                var screenPos = GraphEditor.GraphToScreen(new Point(_currentCameraNode.X, _currentCameraNode.Y));
                double margin = 50;
                if (screenPos.X < margin || screenPos.X > GraphEditor.ActualWidth - margin ||
                    screenPos.Y < margin || screenPos.Y > GraphEditor.ActualHeight - margin)
                {
                    GraphEditor.AnimatedCenterOn(new Point(_currentCameraNode.X, _currentCameraNode.Y));
                }
            }
        }

        private void OnNodeSelectionChanged(object? sender, IReadOnlyList<GraphNode> selectedNodes)
        {
            // Remove highlights
            foreach (var thn in TemporaryHighlightedNodes)
            {
                thn.Unhighlight();
            }
            TemporaryHighlightedNodes.Clear();

            // Remove temporary connections from the previously selected node
            _nodeWithTemporaryConnections?.ClearTemporaryConnections(conn => GraphEditor.RemoveConnection(conn));
            _nodeWithTemporaryConnections = null;

            if (selectedNodes.Count == 1 && selectedNodes[0] is NavigationPoint nav)
            {
                // Load into Interpreter
                Interpreter_ExportLoader.LoadExport(nav.Export);
                NodeInfo_Panel.LoadNode(nav);

                // If CoverLink, highlight coverlink nodes
                if (nav is CoverLink cl)
                {
                    foreach (var csm in cl.Markers)
                    {
                        TemporaryHighlightedNodes.Add(csm);
                        csm.Highlight();
                    }
                }
                else if (nav is CoverSlotMarker coverSlotMarker)
                {
                    DrawCoverSlotMarkerConnections(coverSlotMarker);
                }
            }
            else
            {
                Interpreter_ExportLoader.UnloadExport();
                NodeInfo_Panel.LoadNode(null);
            }
        }

        private void RefreshCoverSlotMarkerConnections()
        {
            if (_nodeWithTemporaryConnections is CoverSlotMarker csm)
            {
                _nodeWithTemporaryConnections.ClearTemporaryConnections(conn => GraphEditor.RemoveConnection(conn));
                _nodeWithTemporaryConnections = null;
                DrawCoverSlotMarkerConnections(csm);
            }
        }

        private void DrawCoverSlotMarkerConnections(CoverSlotMarker coverSlotMarker)
        {
            if (coverSlotMarker.OwningNode is not { } owningLink ||
                coverSlotMarker.OwningLinkSlotIdx >= owningLink.Slots.Count)
                return;

            var slot = owningLink.Slots[coverSlotMarker.OwningLinkSlotIdx];

            if (ShowFirelinks)
            {
                foreach (var fireLink in slot.FireLinks)
                {
                    if (fireLink.ResolvedTargetMarker is { } target)
                    {
                        var conn = new GraphConnection(coverSlotMarker, target)
                        {
                            LineColor = Colors.Red,
                            LineStyle = ConnectionLineStyle.Dashed,
                            LineWidth = 2,
                        };
                        coverSlotMarker.AddTemporaryConnection(conn);
                        GraphEditor.AddConnection(conn);
                    }
                }
            }

            if (ShowDangerNavs)
            {
                foreach (var dangerNav in slot.DangerNavs)
                {
                    if (dangerNav.ResolvedNav is { } target)
                    {
                        var conn = new GraphConnection(coverSlotMarker, target)
                        {
                            LineColor = Colors.Blue,
                            LineStyle = ConnectionLineStyle.Dashed,
                            LineWidth = Math.Max(1.0, Math.Ceiling(dangerNav.DangerCost / 255.0 * MaxCoverLineWidth)),
                        };
                        coverSlotMarker.AddTemporaryConnection(conn);
                        GraphEditor.AddConnection(conn);
                    }
                }
            }

            if (ShowExposedCover)
            {
                foreach (var exposedLink in slot.ExposedCovers)
                {
                    if (exposedLink.ResolvedTargetMarker is { } target)
                    {
                        var conn = new GraphConnection(coverSlotMarker, target)
                        {
                            LineColor = Colors.Yellow,
                            LineStyle = ConnectionLineStyle.Dashed,
                            LineWidth = Math.Max(1.0, Math.Ceiling(exposedLink.ExposureScale / 255.0 * MaxCoverLineWidth)),
                        };
                        coverSlotMarker.AddTemporaryConnection(conn);
                        GraphEditor.AddConnection(conn);
                    }
                }
            }

            if (coverSlotMarker.TemporaryConnections.Count > 0)
                _nodeWithTemporaryConnections = coverSlotMarker;
        }

        private void LoadTilesFromFolder()
        {
            var inFolder = @"B:\ImageScanner\cermir_landing";
            var coordinates = DuplicatingIni.LoadIni(Path.Combine(inFolder, "coordinates.txt"));

            var section = coordinates.GetGlobalSection();
            var scanHeight = float.Parse(section.GetValue("ScanHeight").Value);
            var camPosY = float.Parse(section.GetValue("TopLeftY").Value);
            var top = camPosY + (scanHeight / 2);
            var bottom = camPosY - (scanHeight / 2);

            var left = float.Parse(section.GetValue("TopLeftX").Value);
            var tileWidth = int.Parse(section.GetValue("TilePixelWidth").Value);
            var tileCount = int.Parse(section.GetValue("TileCount").Value);
            var tiles = new List<BackgroundTile>();

            for (int idx = 0; idx < tileCount; idx++)
            {
                string path = Path.Combine(inFolder, $"tile_{idx.ToString().PadLeft(4, '0')}.png");
                double x = left + (idx * tileWidth);
                double y = top;

                Func<ImageSource?> factory = () =>
                {
                    using var stream = File.OpenRead(path);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad; // Fully decode before the stream is closed
                    bmp.EndInit();
                    bmp.Freeze(); // Required for best WPF rendering performance
                    return bmp;
                };
                tiles.Add(BackgroundTile.FromCorners(factory, x, y, x + tileWidth, bottom));
            }

            GraphEditor.SetBackgroundTiles(tiles);
        }
    }
}