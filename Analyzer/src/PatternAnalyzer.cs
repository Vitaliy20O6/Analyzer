// Analysis/PatternAnalyzer.cs
using Analyzer.src;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Analyzer.src
{
    public class PatternAnalyzer
    {
        private readonly SolutionLoader _solutionLoader;

        public PatternAnalyzer(SolutionLoader solutionLoader)
        {
            _solutionLoader = solutionLoader ?? throw new ArgumentNullException(nameof(solutionLoader));
        }

        public List<DesignPattern> Analyze()
        {
            try
            {
                var syntaxTrees = _solutionLoader.GetAllSyntaxTrees()?.ToList();
                if (syntaxTrees == null || !syntaxTrees.Any())
                    return new List<DesignPattern>();

                var patterns = new ConcurrentBag<DesignPattern>();

                Parallel.ForEach(syntaxTrees, tree =>
                {
                    if (tree?.GetRoot() is not CompilationUnitSyntax root) return;

                    var semanticModel = _solutionLoader.GetSemanticModel(tree);
                    if (semanticModel == null) return;

                    AnalyzeSingletonPatterns(root, semanticModel, patterns);
                    AnalyzeFactoryPatterns(root, semanticModel, patterns);
                    // Другие анализаторы...
                });

                return patterns.ToList();
            }
            catch
            {
                return new List<DesignPattern>();
            }
        }
        private void AnalyzeSingletonPatterns(CompilationUnitSyntax root, SemanticModel semanticModel, ConcurrentBag<DesignPattern> patterns)
        {
            var singletonClasses = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(cls => IsSingletonClass(cls, semanticModel));

            foreach (var cls in singletonClasses)
            {
                patterns.Add(CreatePatternInfo("Singleton", cls));
            }
        }

        private void AnalyzeFactoryPatterns(CompilationUnitSyntax root, SemanticModel semanticModel, ConcurrentBag<DesignPattern> patterns)
        {
            var factoryMethods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(m => IsFactoryMethod(m, semanticModel));

            foreach (var method in factoryMethods)
            {
                patterns.Add(CreatePatternInfo("Factory Method", method.Parent as ClassDeclarationSyntax));
            }
        }

        private bool IsFactoryMethod(MethodDeclarationSyntax method, SemanticModel semanticModel)
        {
            return method.Identifier.Text.StartsWith("Create") &&
                   method.ReturnType is IdentifierNameSyntax returnType &&
                   semanticModel.GetTypeInfo(returnType).Type?.TypeKind == TypeKind.Interface;
        }

        private bool IsSingletonClass(ClassDeclarationSyntax classDecl, SemanticModel semanticModel)
        {
            var typeSymbol = semanticModel.GetDeclaredSymbol(classDecl);
            if (typeSymbol == null) return false;

            // 1. Проверка наличия статического поля, содержащего экземпляр
            bool hasInstanceField = typeSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f => f.IsStatic &&
                         (f.Name == "Instance" || f.Name == "_instance" || f.Name == "instance") &&
                         f.Type.Equals(typeSymbol, SymbolEqualityComparer.Default));

            // 2. Проверка наличия статического свойства, возвращающего экземпляр
            bool hasInstanceProperty = typeSymbol.GetMembers()
                .OfType<IPropertySymbol>()
                .Any(p => p.IsStatic &&
                         (p.Name == "Instance" || p.Name == "Current") &&
                         p.Type.Equals(typeSymbol, SymbolEqualityComparer.Default));

            // 3. Проверка приватного конструктора (исправленная часть)
            bool hasPrivateConstructor = typeSymbol.Constructors
                .Any(c =>
                    // Проверка через синтаксис
                    c.DeclaringSyntaxReferences.Any(r =>
                        r.GetSyntax() is ConstructorDeclarationSyntax ctor &&
                        ctor.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))) ||
                    // Или через символы
                    c.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private);

            // 4. Проверка статического метода получения экземпляра
            bool hasGetInstanceMethod = typeSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.IsStatic &&
                         (m.Name == "GetInstance" || m.Name == "Instance" || m.Name == "Current") &&
                         m.ReturnType.Equals(typeSymbol, SymbolEqualityComparer.Default) &&
                         m.Parameters.IsEmpty);

            // 5. Проверка Lazy-инициализации
            bool hasLazyInitialization = classDecl.DescendantNodes()
                .OfType<FieldDeclarationSyntax>()
                .Any(f => f.Declaration.Variables
                    .Any(v => v.Initializer?.Value is ObjectCreationExpressionSyntax creation &&
                             creation.Type is IdentifierNameSyntax typeName &&
                             typeName.Identifier.Text == classDecl.Identifier.Text));

            return (hasInstanceField || hasInstanceProperty || hasGetInstanceMethod || hasLazyInitialization) &&
                   hasPrivateConstructor;
        }

        private DesignPattern CreatePatternInfo(string patternName, ClassDeclarationSyntax cls)
        {
            return new DesignPattern
            {
                PatternName = patternName,
                Description = GetPatternDescription(patternName),
                IconPath = $"Images/Patterns/{patternName.ToLower()}.png",
                Category = GetPatternCategory(patternName),
                // Позже добавим: RelatedClasses = { cls.Identifier.Text }
            };
        }

        // В классе PatternAnalyzer добавим следующие методы:

        private string GetPatternDescription(string patternName)
        {
            // Словарь с описаниями паттернов
            var patternDescriptions = new Dictionary<string, string>
            {
                ["Singleton"] = "Гарантирует, что у класса есть только один экземпляр",
                ["Factory Method"] = "Определяет интерфейс для создания объектов, но оставляет подклассам решение о том, экземпляры какого класса создавать",
                ["Abstract Factory"] = "Предоставляет интерфейс для создания семейств связанных объектов",
                ["Builder"] = "Позволяет создавать сложные объекты пошагово",
                ["Prototype"] = "Позволяет копировать объекты без привязки к их конкретным классам",
                ["Adapter"] = "Позволяет объектам с несовместимыми интерфейсами работать вместе",
                ["Bridge"] = "Разделяет абстракцию и реализацию, позволяя изменять их независимо",
                ["Composite"] = "Позволяет сгруппировать объекты в древовидную структуру",
                ["Decorator"] = "Позволяет динамически добавлять объектам новую функциональность",
                ["Facade"] = "Предоставляет простой интерфейс к сложной системе классов",
                ["Flyweight"] = "Позволяет эффективно поддерживать множество мелких объектов",
                ["Proxy"] = "Позволяет подставлять вместо реальных объектов специальные объекты-заменители",
                ["Observer"] = "Определяет зависимость \"один - ко - многим\" между объектами",
                ["Strategy"] = "Определяет семейство алгоритмов, инкапсулирует каждый из них",
                ["Command"] = "Инкапсулирует запрос как объект",
                ["State"] = "Позволяет объекту изменять свое поведение при изменении состояния",
                ["Template Method"] = "Определяет скелет алгоритма, перекладывая ответственность",
                ["Visitor"] = "Позволяет добавлять новые операции без изменения классов"
            };

            return patternDescriptions.TryGetValue(patternName, out var description)
                ? description
                : "Описание паттерна отсутствует";
        }

        private string GetPatternCategory(string patternName)
        {
            // Словарь с категориями паттернов
            var patternCategories = new Dictionary<string, string>
            {
                ["Singleton"] = "Порождающий",
                ["Factory Method"] = "Порождающий",
                ["Abstract Factory"] = "Порождающий",
                ["Builder"] = "Порождающий",
                ["Prototype"] = "Порождающий",
                ["Adapter"] = "Структурный",
                ["Bridge"] = "Структурный",
                ["Composite"] = "Структурный",
                ["Decorator"] = "Структурный",
                ["Facade"] = "Структурный",
                ["Flyweight"] = "Структурный",
                ["Proxy"] = "Структурный",
                ["Observer"] = "Поведенческий",
                ["Strategy"] = "Поведенческий",
                ["Command"] = "Поведенческий",
                ["State"] = "Поведенческий",
                ["Template Method"] = "Поведенческий",
                ["Visitor"] = "Поведенческий"
            };

            return patternCategories.TryGetValue(patternName, out var category)
                ? category
                : "Неизвестная категория";
        }

        private void FindSingletonPatterns(List<DesignPattern> patterns)
        {
            foreach (var classDecl in _solutionLoader.GetAllClassDeclarations())
            {
                // Простейшая проверка на Singleton
                if (classDecl.Members.OfType<FieldDeclarationSyntax>()
                    .Any(f => f.Declaration.Variables
                        .Any(v => v.Identifier.Text == "Instance")))
                {
                    patterns.Add(new DesignPattern
                    {
                        PatternName = "Singleton",
                        Description = "Гарантирует, что у класса есть только один экземпляр",
                        IconPath = "Images/Patterns/singleton.png",
                        Category = "Порождающий"
                    });
                }
            }
        }

        private void FindFactoryPatterns(List<DesignPattern> patterns)
        {
            // Упрощенная логика поиска Factory
            foreach (var method in _solutionLoader.GetAllMethodDeclarations())
            {
                if (method.ReturnType is IdentifierNameSyntax returnType &&
                    method.Identifier.Text.StartsWith("Create") &&
                    returnType.Identifier.Text.EndsWith("Factory"))
                {
                    patterns.Add(new DesignPattern
                    {
                        PatternName = "Factory Method",
                        Description = "Определяет интерфейс для создания объекта",
                        IconPath = "Images/Patterns/factory.png",
                        Category = "Порождающий"
                    });
                }
            }
        }

        private void FindObserverPatterns(List<DesignPattern> patterns)
        {
            // Упрощенная логика поиска Observer
        }
    }
}