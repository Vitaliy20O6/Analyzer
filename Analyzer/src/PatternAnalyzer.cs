// Analysis/PatternAnalyzer.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Analyzer.src
{
    public class PatternAnalyzer
    {
        private readonly SolutionLoader _solutionLoader;

        public PatternAnalyzer(SolutionLoader solutionLoader)
        {
            _solutionLoader = solutionLoader;
        }

        #region Main Analysis
        public List<DesignPattern> Analyze()
        {
            try
            {
                var patterns = new ConcurrentBag<DesignPattern>();
                var syntaxTrees = _solutionLoader.GetAllSyntaxTrees()?.ToList();

                Parallel.ForEach(syntaxTrees, tree =>
                {
                    if (tree?.GetRoot() is CompilationUnitSyntax root &&
                        _solutionLoader.GetSemanticModel(tree) is SemanticModel semanticModel)
                    {
                        AnalyzeSingleton(root, semanticModel, patterns);
                        AnalyzeFactory(root, semanticModel, patterns);
                        AnalyzeAbstractFactory(root, semanticModel, patterns);
                        AnalyzeDecorator(root, semanticModel, patterns);
                        AnalyzeObserver(root, semanticModel, patterns);
                    }
                });

                return patterns.Distinct().ToList();
            }
            catch
            {
                return new List<DesignPattern>();
            }
        }
        #endregion

        #region Pattern Detection
        private void AnalyzeSingleton(CompilationUnitSyntax root, SemanticModel semanticModel, ConcurrentBag<DesignPattern> patterns)
        {
            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (IsSingletonClass(cls, semanticModel))
                {
                    patterns.Add(CreatePattern("Singleton", cls));
                }
            }
        }

        private void AnalyzeFactory(CompilationUnitSyntax root, SemanticModel semanticModel, ConcurrentBag<DesignPattern> patterns)
        {
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (IsFactoryMethod(method, semanticModel))
                {
                    patterns.Add(CreatePattern("Factory Method", method.Parent as ClassDeclarationSyntax));
                }
            }
        }

        private void AnalyzeAbstractFactory(CompilationUnitSyntax root, SemanticModel semanticModel, ConcurrentBag<DesignPattern> patterns)
        {
            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (cls.BaseList?.Types.Any(t => t.Type.ToString().Contains("AbstractFactory")) == true)
                {
                    patterns.Add(CreatePattern("Abstract Factory", cls));
                }
            }
        }

        private void AnalyzeDecorator(CompilationUnitSyntax root, SemanticModel semanticModel, ConcurrentBag<DesignPattern> patterns)
        {
            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (cls.BaseList?.Types.Any(t => t.Type.ToString().EndsWith("Decorator")) == true &&
                    cls.DescendantNodes().OfType<FieldDeclarationSyntax>()
                        .Any(f => f.Declaration.Type.ToString().Contains("Component")))
                {
                    patterns.Add(CreatePattern("Decorator", cls));
                }
            }
        }

        private void AnalyzeObserver(CompilationUnitSyntax root, SemanticModel semanticModel, ConcurrentBag<DesignPattern> patterns)
        {
            foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (cls.DescendantNodes().OfType<EventFieldDeclarationSyntax>().Any() &&
                    cls.BaseList?.Types.Any(t => t.Type.ToString().Contains("IObservable")) == true)
                {
                    patterns.Add(CreatePattern("Observer", cls));
                }
            }
        }
        #endregion

        #region Pattern Validation
        private bool IsSingletonClass(ClassDeclarationSyntax cls, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(cls);
            return symbol?.GetMembers().OfType<IFieldSymbol>()
                       .Any(f => f.IsStatic && f.Type.Equals(symbol)) == true &&
                   symbol.Constructors.Any(c => c.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private);
        }

        private bool IsFactoryMethod(MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            var returnType = semanticModel.GetTypeInfo(method.ReturnType).Type;
            return method.Identifier.Text.StartsWith("Create") &&
                   (returnType?.TypeKind == TypeKind.Interface || returnType?.IsAbstract == true);
        }
        #endregion

        #region Helpers
        private DesignPattern CreatePattern(string name, ClassDeclarationSyntax cls)
        {
            return new DesignPattern
            {
                PatternName = name,
                Description = GetPatternDescription(name),
                Category = GetPatternCategory(name),
                IconPath = $"/Images/Patterns/{name.ToLower()}.png",
                Classes = { new PatternClass { Name = cls.Identifier.Text } }
            };
        }

        private string GetPatternDescription(string name) => name switch
        {
            "Singleton" => "Ensures a class has only one instance",
            "Factory Method" => "Creates objects without specifying the exact class",
            "Abstract Factory" => "Produces families of related objects",
            "Decorator" => "Adds responsibilities to objects dynamically",
            "Observer" => "Defines a dependency between objects",
            _ => "No description available"
        };

        private string GetPatternCategory(string name) => name switch
        {
            "Singleton" or "Factory Method" or "Abstract Factory" => "Creational",
            "Decorator" => "Structural",
            "Observer" => "Behavioral",
            _ => "Other"
        };
        #endregion
    }
}