using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Analyzer.src
{
    public class TreeViewNode : DependencyObject
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsFile { get; set; }
        public ObservableCollection<TreeViewNode> Children { get; } = new ObservableCollection<TreeViewNode>();

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(string), typeof(TreeViewNode));

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }

    public class SolutionLoader
    {
        public ObservableCollection<TreeViewNode> RootNodes { get; } = new ObservableCollection<TreeViewNode>();
        private readonly string _solutionPath;

        public SolutionLoader(string solutionPath)
        {
            _solutionPath = solutionPath ?? throw new ArgumentNullException(nameof(solutionPath));
        }

        public async Task LoadSolution()
        {
            if (!File.Exists(_solutionPath))
            {
                MessageBox.Show($"Solution file not found: {_solutionPath}");
                return;
            }

            try
            {
                using var workspace = MSBuildWorkspace.Create();
                workspace.WorkspaceFailed += (o, e) =>
                    Console.WriteLine($"Workspace warning: {e.Diagnostic.Message}");

                var solution = await workspace.OpenSolutionAsync(_solutionPath);

                foreach (var project in solution.Projects)
                {
                    if (ShouldSkipProject(project)) continue;

                    var projectNode = CreateProjectNode(project);
                    RootNodes.Add(projectNode);
                    await BuildProjectTree(project, projectNode);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading solution: {ex.Message}");
            }
        }

        private bool ShouldSkipProject(Project project)
        {
            // Пропускаем временные проекты и проекты без файлов
            return string.IsNullOrEmpty(project.FilePath) ||
                   project.FilePath.Contains("\\obj\\");
        }

        private TreeViewNode CreateProjectNode(Project project)
        {
            return new TreeViewNode
            {
                Name = project.Name,
                FullPath = project.FilePath,
                Icon = "/Images/project.png"
            };
        }

        private async Task BuildProjectTree(Project project, TreeViewNode projectNode)
        {
            var pathNodeMap = new Dictionary<string, TreeViewNode>(StringComparer.OrdinalIgnoreCase)
            {
                [project.FilePath] = projectNode
            };

            foreach (var document in project.Documents)
            {
                if (ShouldSkipDocument(document)) continue;

                var directory = Path.GetDirectoryName(document.FilePath);
                var parentNode = FindOrCreateParentNodes(directory, project, projectNode, pathNodeMap);

                parentNode.Children.Add(CreateFileNode(document));
            }
        }

        private bool ShouldSkipDocument(Document document)
        {
            return string.IsNullOrEmpty(document.FilePath) ||
                   document.FilePath.Contains("\\obj\\");
        }

        private TreeViewNode FindOrCreateParentNodes(
            string currentPath,
            Project project,
            TreeViewNode projectNode,
            Dictionary<string, TreeViewNode> pathNodeMap)
        {
            if (string.IsNullOrEmpty(currentPath) ||
                currentPath.Equals(Path.GetDirectoryName(project.FilePath),
                StringComparison.OrdinalIgnoreCase))
            {
                return projectNode;
            }

            var parts = currentPath
                .Substring(Path.GetDirectoryName(project.FilePath).Length + 1)
                .Split(Path.DirectorySeparatorChar);

            var cumulativePath = Path.GetDirectoryName(project.FilePath);
            var currentNode = projectNode;

            foreach (var part in parts)
            {
                cumulativePath = Path.Combine(cumulativePath, part);

                if (!pathNodeMap.TryGetValue(cumulativePath, out var nextNode))
                {
                    nextNode = CreateFolderNode(part, cumulativePath);
                    currentNode.Children.Add(nextNode);
                    pathNodeMap[cumulativePath] = nextNode;
                }

                currentNode = nextNode;
            }

            return currentNode;
        }

        private TreeViewNode CreateFolderNode(string name, string fullPath)
        {
            return new TreeViewNode
            {
                Name = name,
                FullPath = fullPath,
                Icon = "/Images/folder.png"
            };
        }

        private TreeViewNode CreateFileNode(Document document)
        {
            return new TreeViewNode
            {
                Name = document.Name,
                FullPath = document.FilePath,
                Icon = "/Images/file.png",
                IsFile = true
            };
        }
    }
}