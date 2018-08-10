using Microsoft.Msagl.Core;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Incremental;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.Miscellaneous;
using Microsoft.Msagl.Prototype.Ranking;
using Microsoft.Msagl.Routing;
using Syncfusion.UI.Xaml.Diagram;
using Syncfusion.UI.Xaml.Diagram.Layout;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;
using Microsoft.Msagl.DebugHelpers.Persistence;
using Microsoft.Win32;
using System.Windows;
using Syncfusion.UI.Xaml.Diagram.Controls;

namespace SfDiagramWithMSAGL.ViewModel
{
    public class DiagramVM
    {
        private GraphFile _selectedFile;

        public ICommand Fit { get; set; }
        public ICommand Load { get; set; }
        public ICommand Refresh { get; set; }
        public ObservableCollection<GraphFile> Files { get; set; }
        public MSAGLLayout Layout { get; set; }
        public GraphFile SelectedFile
        {
            get => _selectedFile;
            set
            {
                _selectedFile = value;
                Load.Execute(null);
            }
        }
        public DiagramViewModel SfDiagramModel { get; set; }
        public GeometryGraph MSAGLModel { get; set; }

        public DiagramVM()
        {
            SfDiagramModel = new DiagramViewModel
            {
                Nodes = new NodeCollection(),
                Connectors = new ConnectorCollection(),
                DefaultConnectorType = ConnectorType.PolyLine
            };

            Load = new DelegateCommand((a) =>
            {
                LoadDiagram(SfDiagramModel, SelectedFile.File);
                Refresh.Execute(null);
            });

            Fit = new DelegateCommand((a) =>
            {
                if (SfDiagramModel.Info != null)
                {
                    var graph = SfDiagramModel.Info as IGraphInfo;
                    graph.Commands.Zoom.Execute(
                        new ZoomPositionParameter
                        {
                            ZoomTo = 500,
                            ZoomCommand = ZoomCommand.ZoomIn
                        });
#pragma warning disable CS0618 // Type or member is obsolete
                    (graph.ScrollInfo as ScrollViewer).Page.UpdateLayout();
#pragma warning restore CS0618 // Type or member is obsolete
                    graph.Commands.FitToPage.Execute(
                        new FitToPageParameter
                        {
                            FitToPage = FitToPage.FitToPage,
                            CanZoomIn = true
                        });
                }
            });

            Refresh = new DelegateCommand((a) =>
            {
                Layout.UpdateLayout(MSAGLModel);
                SfDiagramModel.UpdateSfDiagram(MSAGLModel);
                Fit.Execute(null);
            });
            
            Layout = new MSAGLLayout
            {
                Layout = SfDiagramWithMSAGL.Layout.Sugiyama,
                RoutingSettings = new EdgeRoutingSettings
                {
                    BundlingSettings = new BundlingSettings()
                },
                RoutingMode = EdgeRoutingMode.Spline
            };
            Layout.PropertyChanged += (s, e) =>
            {
                if ((e.PropertyName == "Layout")||
                (e.PropertyName == "RoutingMode"))
                {
                    Refresh.Execute(null);
                }
            };

            Files = PrepareFiles();
            SelectedFile = Files[0];
        }

        private ObservableCollection<GraphFile> PrepareFiles()
        {
            var files = new ObservableCollection<GraphFile>();
            foreach (var file in Directory.GetFiles("Graph"))
            {
                int start = file.IndexOf('\\') + 1;
                files.Add(new GraphFile { File = file, Name = file.Substring(start, file.IndexOf('.') - start) });
            }
            return files;
        }
        
        private void LoadDiagram(IGraph graph, string file)
        {
            var nodes = graph.Nodes as NodeCollection;
            var connectors = graph.Connectors as ConnectorCollection;
            nodes.Clear();
            connectors.Clear();
            Dictionary<string, INode> nodeDict = new Dictionary<string, INode>();

            var lines = File.ReadLines(file);
            foreach (var line in lines)
            {
                var ns = line.Split(new string[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                INode head = null;
                foreach (var node in ns)
                {
                    if (head == null)
                    {
                        head = GetNode(nodeDict, node);
                        if (!nodes.Contains(head))
                        {
                            nodes.Add(head as NodeViewModel);
                        }
                    }
                    else
                    {
                        var tail = GetNode(nodeDict, node);
                        if (!nodes.Contains(tail))
                        {
                            nodes.Add(tail as NodeViewModel);
                        }
                        connectors.Add(new ConnectorViewModel
                        {
                            SourceNode = head,
                            TargetNode = tail,
                            Segments = new ObservableCollection<object> { new StraightSegment() }
                        });
                        head = tail;
                    }
                }
            }

            MSAGLModel = SfDiagramModel.ToMSAGL();
        }

        private INode GetNode(Dictionary<string, INode> nodes, string node)
        {
            nodes.TryGetValue(node, out INode found);
            if (found == null)
            {
                found = new NodeViewModel() { UnitWidth = 25, UnitHeight = 25, Content = node };
                nodes.Add(node, found);
            }
            return found;
        }

    }
}
