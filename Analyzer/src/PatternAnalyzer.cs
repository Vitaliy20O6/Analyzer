// Analysis/PatternAnalyzer.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

                // Группировка по уникальным паттернам
                return patterns
                    .GroupBy(p => p.GetHashCode())
                    .Select(g => g.First())
                    .ToList();
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
            var factoryMethods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => IsFactoryMethod(m, semanticModel));

            foreach (var method in factoryMethods)
            {
                var factoryClass = method.Parent as ClassDeclarationSyntax;
                if (factoryClass == null) continue;

                // Ищем возвращаемый тип
                var returnType = semanticModel.GetTypeInfo(method.ReturnType).Type;
                if (returnType == null) continue;

                // Создаем или находим существующий паттерн
                var pattern = patterns.FirstOrDefault(p =>
                    p.PatternName == "Factory Method" &&
                    p.Classes.Any(c => c.Name == factoryClass.Identifier.Text));

                if (pattern == null)
                {
                    pattern = new DesignPattern
                    {
                        PatternName = "Factory Method",
                        Description = GetPatternDescription("Factory Method"),
                        Category = GetPatternCategory("Factory Method"),
                        IconPath = "/Images/Patterns/factorymethod.png"
                    };
                    pattern.Classes.Add(new PatternClass
                    {
                        Name = factoryClass.Identifier.Text,
                        Type = "Factory",
                        Methods = { method.Identifier.Text }
                    });
                    patterns.Add(pattern);
                }

                // Добавляем продукт
                var productClass = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.Text == returnType.Name);

                if (productClass != null && !pattern.Classes.Any(c => c.Name == productClass.Identifier.Text))
                {
                    pattern.Classes.Add(new PatternClass
                    {
                        Name = productClass.Identifier.Text,
                        Type = "Product"
                    });

                    pattern.Relations.Add(new PatternRelation
                    {
                        Type = "Creates",
                        FromClass = factoryClass.Identifier.Text,
                        ToClass = productClass.Identifier.Text
                    });
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
            // Убрали проверку на ParameterList.Parameters.Count == 0
            var returnType = semanticModel.GetTypeInfo(method.ReturnType).Type;
            return method.Identifier.Text.StartsWith("Create") &&
                   (returnType?.TypeKind == TypeKind.Interface ||
                    returnType?.IsAbstract == true);
        }
        #endregion

        #region Helpers
        private DesignPattern CreatePattern(string name, ClassDeclarationSyntax cls)
        {
            // Защита от null
            if (cls == null)
            {
                return new DesignPattern
                {
                    PatternName = name,
                    Description = "Некорректный паттерн: класс не определён",
                    Category = "Ошибка",
                    IconPath = "/Images/error.png"
                };
            }

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