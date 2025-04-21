using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Analyzer.src
{
    public class DesignPattern
    {
        // Добавьте уникальный идентификатор паттерна
        public Guid Id { get; } = Guid.NewGuid();

        // Остальные свойства остаются без изменений
        public string PatternName { get; set; }
        public string Description { get; set; }
        public string IconPath { get; set; }
        public string Category { get; set; }
        public List<PatternClass> Classes { get; set; } = new();
        public List<PatternRelation> Relations { get; set; } = new();

        // Для группировки по имени паттерна и классам
        public override bool Equals(object obj) =>
            obj is DesignPattern other &&
            PatternName == other.PatternName &&
            Classes.Select(c => c.Name).SequenceEqual(other.Classes.Select(c => c.Name));

        public override int GetHashCode() =>
            HashCode.Combine(PatternName, string.Join(",", Classes.Select(c => c.Name)));
    }

    public class PatternClass
    {
        public string Name { get; set; }
        public string Type { get; set; } // Class, Interface, AbstractClass
        public List<string> Methods { get; set; } = new();
        public string FilePath { get; set; }
    }

    public class PatternRelation
    {
        public string Type { get; set; } // Inheritance, Composition, Aggregation, Dependency
        public string FromClass { get; set; }
        public string ToClass { get; set; }
    }

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

        public MetricsResult Metrics { get; private set; } = new();

        public List<DesignPattern> DetectedPatterns { get; private set; } = new();
        private PatternAnalyzer _patternAnalyzer;
        private Compilation _compilation;
        private Solution _solution;

        public SolutionLoader(string solutionPath)
        {
            _solutionPath = solutionPath ?? throw new ArgumentNullException(nameof(solutionPath));
            DetectedPatterns = new List<DesignPattern>();
            _patternAnalyzer = new PatternAnalyzer(this); // Инициализация здесь
        }

        public async Task AnalyzePatternsAsync()
        {
            try
            {
                if (_patternAnalyzer == null)
                    throw new InvalidOperationException("Pattern analyzer not initialized");

                DetectedPatterns = await Task.Run(() => _patternAnalyzer.Analyze());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pattern analysis failed: {ex.Message}");
                DetectedPatterns = new List<DesignPattern>();
            }
        }

        public IEnumerable<SyntaxTree> GetAllSyntaxTrees()
        {
            if (_compilation == null)
            {
                // Если компиляция еще не создана, создаем ее
                var project = _solution.Projects.First();
                _compilation = project.GetCompilationAsync().Result;
            }
            return _compilation.SyntaxTrees;
        }

        public SemanticModel GetSemanticModel(SyntaxTree tree)
        {
            if (_compilation == null)
            {
                var project = _solution.Projects.First();
                _compilation = project.GetCompilationAsync().Result;
            }
            return _compilation.GetSemanticModel(tree);
        }

        public IEnumerable<ClassDeclarationSyntax> GetAllClassDeclarations()
        {
            return GetAllSyntaxTrees()
                .SelectMany(tree => tree.GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>());
        }

        public IEnumerable<MethodDeclarationSyntax> GetAllMethodDeclarations()
        {
            return GetAllSyntaxTrees()
                .SelectMany(tree => tree.GetRoot()
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>());
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
                _solution = solution;

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
        public async Task AnalyzeMetrics()
        {
            Metrics = new MetricsResult();

            await Task.Run(async () =>
            {
                using var workspace = MSBuildWorkspace.Create();
                var solution = await workspace.OpenSolutionAsync(_solutionPath);

                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync();
                    foreach (var document in project.Documents)
                    {
                        if (!document.FilePath.EndsWith(".cs") || document.FilePath.Contains("\\obj\\")) continue;

                        var tree = await document.GetSyntaxTreeAsync();
                        var root = await tree.GetRootAsync();
                        var model = compilation.GetSemanticModel(tree);

                        foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                        {
                            foreach (var method in cls.DescendantNodes().OfType<MethodDeclarationSyntax>())
                            {
                                var symbol = model.GetDeclaredSymbol(method);
                                var lines = CountLogicalLines(method);
                                var complexity = GetCyclomaticComplexity(method);
                                var halstead = CalculateHalstead(method);
                                var mi = CalculateMaintainabilityIndex(halstead.Volume, complexity, lines);
                                var fanOut = CountFanOut(method, model);
                                var fanIn = CountFanIn(solution, symbol);
                                var npath = CalculateNPathComplexity(method);
                                var nesting = GetMaxNestingDepth(method.Body);

                                Metrics.Methods.Add(new MethodMetrics
                                {
                                    FilePath = document.FilePath,
                                    ClassName = cls.Identifier.Text,
                                    MethodName = method.Identifier.Text,
                                    LogicalLines = lines,
                                    CyclomaticComplexity = complexity,
                                    HalsteadVolume = halstead.Volume,
                                    OperatorCount = halstead.OperatorCount,
                                    OperandCount = halstead.OperandCount,
                                    MaintainabilityIndex = mi,
                                    ParameterCount = method.ParameterList.Parameters.Count,
                                    LocalVariableCount = method.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Count(),
                                    FanIn = fanIn,
                                    FanOut = fanOut,
                                    NPathComplexity = npath,
                                    MaxNestingDepth = nesting
                                });
                            }
                        }
                    }
                }
            });
        }
        #region MetricsMetods
        static private int CountLogicalLines(MethodDeclarationSyntax method)
        {
            return method.Body?.Statements.Count ?? 0;
        }

        static private int GetCyclomaticComplexity(MethodDeclarationSyntax method)
        {
            int count = 1;
            count += method.DescendantNodes().Count(n =>
                n is IfStatementSyntax ||
                n is ForStatementSyntax ||
                n is WhileStatementSyntax ||
                n is DoStatementSyntax ||
                n is CaseSwitchLabelSyntax ||
                n is ConditionalExpressionSyntax ||
                (n is BinaryExpressionSyntax bin && (bin.IsKind(SyntaxKind.LogicalAndExpression) || bin.IsKind(SyntaxKind.LogicalOrExpression))));
            return count;
        }

        static private (int OperatorCount, int OperandCount, double Volume) CalculateHalstead(MethodDeclarationSyntax method)
        {
            var uniqueOperators = new HashSet<string>();
            var uniqueOperands = new HashSet<string>();
            int totalOperators = 0;
            int totalOperands = 0;

            foreach (var node in method.DescendantNodes())
            {
                if (node is BinaryExpressionSyntax binary)
                {
                    uniqueOperators.Add(binary.OperatorToken.Text);
                    totalOperators++;
                    uniqueOperands.Add(binary.Left.ToString());
                    uniqueOperands.Add(binary.Right.ToString());
                    totalOperands += 2;
                }
                else if (node is AssignmentExpressionSyntax assign)
                {
                    uniqueOperators.Add(assign.OperatorToken.Text);
                    totalOperators++;
                    uniqueOperands.Add(assign.Left.ToString());
                    uniqueOperands.Add(assign.Right.ToString());
                    totalOperands += 2;
                }
                else if (node is PrefixUnaryExpressionSyntax prefix)
                {
                    uniqueOperators.Add(prefix.OperatorToken.Text);
                    totalOperators++;
                    uniqueOperands.Add(prefix.Operand.ToString());
                    totalOperands++;
                }
                else if (node is PostfixUnaryExpressionSyntax postfix)
                {
                    uniqueOperators.Add(postfix.OperatorToken.Text);
                    totalOperators++;
                    uniqueOperands.Add(postfix.Operand.ToString());
                    totalOperands++;
                }
                else if (node is InvocationExpressionSyntax invocation)
                {
                    uniqueOperators.Add("call");
                    totalOperators++;
                    foreach (var arg in invocation.ArgumentList.Arguments)
                    {
                        uniqueOperands.Add(arg.ToString());
                        totalOperands++;
                    }
                }
                else if (node is LiteralExpressionSyntax literal)
                {
                    uniqueOperands.Add(literal.ToString());
                    totalOperands++;
                }
            }

            int n1 = uniqueOperators.Count;
            int n2 = uniqueOperands.Count;
            int N = totalOperators + totalOperands;
            int n = n1 + n2;
            double volume = (n > 0 && N > 0) ? N * Math.Log(n, 2) : 0;

            return (totalOperators, totalOperands, volume);
        }

        static private double CalculateMaintainabilityIndex(double volume, int complexity, int lines)
        {
            if (volume <= 0 || lines <= 0) return 0;
            return Math.Max(0, (171 - 5.2 * Math.Log(volume) - 0.23 * complexity - 16.2 * Math.Log(lines)) * 100 / 171);
        }

        static private int CountFanOut(MethodDeclarationSyntax method, SemanticModel model)
        {
            return method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(inv => model.GetSymbolInfo(inv).Symbol?.ToDisplayString())
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .Count();
        }

        static private int CountFanIn(Solution solution, IMethodSymbol targetMethod)
        {
            if (targetMethod == null) return 0;
            int count = 0;

            foreach (var project in solution.Projects)
            {
                var compilation = project.GetCompilationAsync().Result;
                foreach (var doc in project.Documents)
                {
                    var tree = doc.GetSyntaxTreeAsync().Result;
                    var model = compilation.GetSemanticModel(tree);
                    var invocations = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>();
                    foreach (var inv in invocations)
                    {
                        var symbol = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
                        if (SymbolEqualityComparer.Default.Equals(symbol, targetMethod))
                            count++;
                    }
                }
            }
            return count;
        }

        static private int CalculateNPathComplexity(MethodDeclarationSyntax method)
        {
            // Простейшая приближённая формула
            int ifCount = method.DescendantNodes().OfType<IfStatementSyntax>().Count();
            int loopCount = method.DescendantNodes().OfType<ForStatementSyntax>().Count() +
                            method.DescendantNodes().OfType<WhileStatementSyntax>().Count();
            return (int)Math.Pow(2, ifCount + loopCount);
        }

        static private int GetMaxNestingDepth(BlockSyntax block)
        {
            int maxDepth = 0;
            void Traverse(SyntaxNode node, int depth)
            {
                if (node is IfStatementSyntax || node is ForStatementSyntax || node is WhileStatementSyntax || node is DoStatementSyntax)
                    depth++;
                maxDepth = Math.Max(maxDepth, depth);
                foreach (var child in node.ChildNodes())
                    Traverse(child, depth);
            }
            if (block != null) Traverse(block, 0);
            return maxDepth;
        }
        #endregion

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