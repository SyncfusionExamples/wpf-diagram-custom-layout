using Microsoft.Msagl.Core;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Incremental;
using Microsoft.Msagl.Layout.LargeGraphLayout;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.Miscellaneous;
using Microsoft.Msagl.Prototype.Ranking;
using Microsoft.Msagl.Routing;
using Syncfusion.UI.Xaml.Diagram;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SfDiagramWithMSAGL
{
    /// <summary>
    /// MSAGL available layout types
    /// </summary>
    public enum Layout
    {
        Ranking,
        MDS,
        FastIncremental,
        Sugiyama,
        //LgLayout
    }

    /// <summary>
    /// Utility class to handle MSAGL layout settings
    /// </summary>
    public class MSAGLLayout : INotifyPropertyChanged
    {
        private Layout _layout;
        private EdgeRoutingSettings _routingSettings = null;
        private LayoutAlgorithmSettings _settings;
        private EdgeRoutingMode _routingMode = EdgeRoutingMode.Spline;

        public Layout Layout
        {
            get => _layout;
            set
            {
                _layout = value;
                switch (value)
                {
                    case Layout.Ranking:
                        Settings = new RankingLayoutSettings() { EdgeRoutingSettings = RoutingSettings };
                        break;
                    case Layout.MDS:
                        Settings = new MdsLayoutSettings { EdgeRoutingSettings = RoutingSettings };
                        break;
                    case Layout.FastIncremental:
                        Settings = new FastIncrementalLayoutSettings { EdgeRoutingSettings = RoutingSettings };
                        break;
                    case Layout.Sugiyama:
                        Settings = new SugiyamaLayoutSettings { EdgeRoutingSettings = RoutingSettings };
                        break;
                        //case Layout.LgLayout:

                        //    break;
                }
                OnPropertyChanged("Layout");
            }
        }

        public EdgeRoutingMode RoutingMode
        {
            get => _routingMode;
            set
            {
                _routingMode = value;
                if (RoutingSettings != null)
                {
                    RoutingSettings.EdgeRoutingMode = value;
                }
                OnPropertyChanged("RoutingMode");
            }
        }

        public EdgeRoutingSettings RoutingSettings
        {
            get => _routingSettings;
            set
            {
                _routingSettings = value;
                if (Settings != null)
                {
                    Settings.EdgeRoutingSettings = value;
                }
                OnPropertyChanged("RoutingSettings");
            }
        }
        public LayoutAlgorithmSettings Settings
        {
            get => _settings;
            private set
            {
                _settings = value;
                OnPropertyChanged("Settings");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        public virtual void UpdateLayout(GeometryGraph graph)
        {
            try
            {
                if (Settings is RankingLayoutSettings ||
                    Settings is MdsLayoutSettings ||
                    Settings is FastIncrementalLayoutSettings ||
                    Settings is SugiyamaLayoutSettings ||
                    Settings is LgLayoutSettings)
                {
                    LayoutHelpers.CalculateLayout(graph, Settings, null);
                }
            }catch(Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }

    /// <summary>
    /// Extention methods to sync between SfDiagram and MSAGL
    /// </summary>
    public static class Ext
    {
        // Convert model of SfDiagram to MSAGL
        public static GeometryGraph ToMSAGL(this IGraph sfDiagramModel)
        {
            // Create a graph
            GeometryGraph MSAGLmodel = new GeometryGraph();

            foreach (var node in (sfDiagramModel.Nodes as IEnumerable<INode>))
            {
                // Create MSAGL node
                Microsoft.Msagl.Core.Layout.Node msaglNode = new Microsoft.Msagl.Core.Layout.Node(

                    CurveFactory.CreateRectangle(

                        // Specify Size of a node
                        node.UnitWidth,
                        node.UnitHeight,

                        // Speficy empty position, as layout take care of position
                        new Microsoft.Msagl.Core.Geometry.Point()),

                    // Give reference to SfDiagram Node
                    node);

                // Add node into MSAGL model
                MSAGLmodel.Nodes.Add(msaglNode);
            }

            foreach (var con in sfDiagramModel.Connectors as IEnumerable<IConnector>)
            {
                // Create MSAGL connector
                MSAGLmodel.Edges.Add(
                    new Edge(
                        // Set source and target by finding MSAGL node based on SfDiagram node
                        MSAGLmodel.FindNodeByUserData(con.SourceNode),
                        MSAGLmodel.FindNodeByUserData(con.TargetNode))
                    {
                        Weight = 1,
                        UserData = con
                    });
            }
            return MSAGLmodel;
        }

        // Sync SfDiagram model based on MSAGL model
        public static void UpdateSfDiagram(this IGraph diagram, GeometryGraph graph)
        {
            // Move model to positive axis
            graph.UpdateBoundingBox();
            graph.Translate(new Microsoft.Msagl.Core.Geometry.Point(-graph.Left, -graph.Bottom));

            // Update nodes position
            foreach (var node in graph.Nodes)
            {
                (node.UserData as INode).OffsetX = node.BoundingBox.Center.X;
                (node.UserData as INode).OffsetY = node.BoundingBox.Center.Y;
            }

            // Update connector segments based on routing
            foreach (var edge in graph.Edges)
            {
                IConnector connector = edge.UserData as IConnector;
                connector.Segments = new ObservableCollection<object>();
                connector.SourcePoint = new Point(0, 0);
                connector.TargetPoint = new Point(0, 0);
                SyncSegments(connector, edge);
            }
        }

        // Sync segments of connector
        private static void SyncSegments(IConnector connector, Edge edge)
        {
            var segments = connector.Segments as ICollection<object>;

            // When curve is a line segment
            if (edge.Curve is LineSegment)
            {
                var line = edge.Curve as LineSegment;
                connector.SourcePoint = new Point(line.Start.X, line.Start.Y);
                segments.Add(new StraightSegment
                {
                    Point = new Point(line.Start.X, line.Start.Y)
                });
                segments.Add(new StraightSegment
                {
                    Point = new Point(line.End.X, line.End.Y)
                });
            }

            // When curve is a complex segment
            else if (edge.Curve is Curve)
            {
                Point? pt = null;
                foreach (var segment in (edge.Curve as Curve).Segments)
                {
                    // When curve contains a line segment
                    if (segment is LineSegment)
                    {
                        var line = segment as LineSegment;
                        if (pt == null)
                        {
                            pt = new Point(line.Start.X, line.Start.Y);
                            segments.Add(new StraightSegment
                            {
                                Point = pt
                            });
                        }
                        segments.Add(new StraightSegment
                        {
                            Point = new Point(line.End.X, line.End.Y)
                        });
                    }

                    // When curve contains a cubic bezier segment
                    else if (segment is CubicBezierSegment)
                    {
                        var bezier = segment as CubicBezierSegment;
                        pt = new Point(bezier.B(0).X, bezier.B(0).Y);
                        if (pt == null)
                        {
                            segments.Add(new StraightSegment
                            {
                                Point = pt
                            });
                        }
                        segments.Add(new CubicCurveSegment
                        {
                            Point1 = new Point(bezier.B(1).X, bezier.B(1).Y),
                            Point2 = new Point(bezier.B(2).X, bezier.B(2).Y),
                            Point3 = new Point(bezier.B(3).X, bezier.B(3).Y),
                        });
                    }

                    // When curve contains an arc
                    else if (segment is Ellipse)
                    {
                        var ellipse = segment as Ellipse;
                        var interval = (ellipse.ParEnd - ellipse.ParStart) / 5.0;
                        for (var i = ellipse.ParStart;
                                    i < ellipse.ParEnd;
                                    i += interval)
                        {
                            var p = ellipse.Center
                                + (Math.Cos(i) * ellipse.AxisA)
                                + (Math.Sin(i) * ellipse.AxisB);
                            segments.Add(new StraightSegment
                            {
                                Point = new Point(p.X, p.Y)
                            });
                        }
                    }
                    else
                    {

                    }
                }
                segments.Add(new StraightSegment());
            }
            else
            {

            }
        }
    }
}
