using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.Diagnostics;

namespace Analyzer.src
{
    /// <summary>
    /// Содержит логику обнаружения основных паттернов
    /// </summary>
    internal static class PatternAnalyzer
    {
        #region Factory Method Pattern

        /// <summary>
        /// Анализ компиляции на реализацию паттерна Factory Method
        /// </summary>
        public static void AnalyzeFactoryMethods(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                //Console.WriteLine("\nScanning for Factory Method pattern...");
                var allTypes = SymbolHelper.GetAllTypes(compilation);
                var productTypes = GetProductTypes(allTypes);
                var creatorCandidates = GetCreatorCandidates(allTypes);

                if (!creatorCandidates.Any())
                {
                    //Logger.LogWarning("No potential Factory Method creators found");
                    return;
                }

                foreach (var creator in creatorCandidates)
                {
                    AnalyzeCreatorForFactoryMethods(creator, allTypes, productTypes, compilation, result, projectName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Анализ Factory Method не удался: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static HashSet<INamedTypeSymbol> GetProductTypes(IEnumerable<INamedTypeSymbol> allTypes)
        {
            return new HashSet<INamedTypeSymbol>(
                allTypes.Where(t => t.TypeKind == TypeKind.Class && t.IsAbstract ||
                                   t.TypeKind == TypeKind.Interface)
                       .Where(t => !SymbolHelper.IsSystemType(t)),
                SymbolEqualityComparer.Default);
        }

        private static IEnumerable<INamedTypeSymbol> GetCreatorCandidates(IEnumerable<INamedTypeSymbol> allTypes)
        {
            return allTypes
                .Where(t => t.TypeKind == TypeKind.Class && t.IsAbstract)
                .ToList();
        }

        private static void AnalyzeCreatorForFactoryMethods(
            INamedTypeSymbol creator,
            IEnumerable<INamedTypeSymbol> allTypes,
            HashSet<INamedTypeSymbol> productTypes,
            Compilation compilation, AnalysisResult result, string projectName)
        {
            foreach (var method in creator.GetMembers().OfType<IMethodSymbol>())
            {
                if (IsFactoryMethodCandidate(method, productTypes))
                {
                    AnalyzeFactoryMethodImplementation(method, allTypes, compilation, result, projectName);
                }
            }
        }

        private static bool IsFactoryMethodCandidate(IMethodSymbol method, HashSet<INamedTypeSymbol> productTypes)
        {
            return (method.IsAbstract || method.IsVirtual) &&
                   productTypes.Contains(method.ReturnType, SymbolEqualityComparer.Default);
        }

        private static void AnalyzeFactoryMethodImplementation(
            IMethodSymbol factoryMethod,
            IEnumerable<INamedTypeSymbol> allTypes,
            Compilation compilation, AnalysisResult result, string projectName)
        {
            var derivedCreators = allTypes
                .Where(t => SymbolHelper.IsDerivedFrom(t, factoryMethod.ContainingType))
                .Where(t => !t.IsAbstract);

            foreach (var concreteCreator in derivedCreators)
            {
                var overrideMethod = GetOverrideMethod(concreteCreator, factoryMethod);
                if (overrideMethod != null)
                {
                    AnalyzeConcreteFactoryMethod(overrideMethod, compilation, factoryMethod, concreteCreator, result, projectName);
                }
            }
        }

        private static IMethodSymbol? GetOverrideMethod(INamedTypeSymbol concreteCreator, IMethodSymbol factoryMethod)
        {
            return concreteCreator
                .GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsOverride &&
                    SymbolEqualityComparer.Default.Equals(m.OverriddenMethod, factoryMethod));
        }

        private static void AnalyzeConcreteFactoryMethod(
            IMethodSymbol overrideMethod,
            Compilation compilation,
            IMethodSymbol factoryMethod,
            INamedTypeSymbol concreteCreator, AnalysisResult result, string projectName)
        {
            foreach (var syntaxRef in overrideMethod.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                var semanticModel = compilation.GetSemanticModel(syntax!.SyntaxTree);

                var createdTypes = GetCreatedTypesFromMethod(syntax!, semanticModel);

                foreach (var createdType in createdTypes)
                {
                    if (SymbolHelper.IsDerivedFrom(createdType, (INamedTypeSymbol)factoryMethod.ReturnType))
                    {
                        if (createdType != null)
                        {
                            result.AddPattern(projectName, "Factory Method", new Dictionary<string, string>
                            {
                                { "Метод", factoryMethod.Name },
                                { "Продукт", createdType.Name },
                                { "Фабрика", concreteCreator.Name }
                            });
                        }
                    }
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetCreatedTypesFromMethod(
            MethodDeclarationSyntax methodSyntax,
            SemanticModel semanticModel)
        {
            var objectCreations = methodSyntax.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>();

            foreach (var creation in objectCreations)
            {
                var typeSymbol = semanticModel.GetSymbolInfo(creation.Type).Symbol as INamedTypeSymbol;
                if (typeSymbol != null && !typeSymbol.IsAbstract)
                {
                    yield return typeSymbol;
                }
            }
        }

        #endregion

        #region Abstract Factory Pattern

        /// <summary>
        /// Analyzes compilation for Abstract Factory pattern implementations
        /// </summary>
        public static void AnalyzeAbstractFactories(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                //Console.WriteLine("\nScanning for Abstract Factory pattern...");
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                var factoryCandidates = GetAbstractFactoryCandidates(allTypes);

                if (!factoryCandidates.Any())
                {
                    //Logger.LogWarning("No potential Abstract Factories found");
                    return;
                }

                foreach (var factory in factoryCandidates)
                {
                    AnalyzeAbstractFactoryImplementation(factory, allTypes, compilation, result, projectName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Анализ Abstract Factory не удался: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetAbstractFactoryCandidates(List<INamedTypeSymbol> allTypes)
        {
            return allTypes
                .Where(t => t.TypeKind == TypeKind.Interface ||
                            t.IsAbstract && t.TypeKind == TypeKind.Class)
                .Where(HasFactoryMethods)
                .ToList();
        }

        private static bool HasFactoryMethods(INamedTypeSymbol type)
        {
            return type.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary &&
                            m.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public &&
                            (m.Name.StartsWith("Create") || m.ReturnType.Name.EndsWith("Product")) &&
                            m.Parameters.Length == 0 &&
                            !m.IsStatic &&
                            (m.ReturnType.TypeKind == TypeKind.Interface ||
                            m.ReturnType is INamedTypeSymbol nt && nt.IsAbstract))
                .Count() >= 2;
        }

        private static void AnalyzeAbstractFactoryImplementation(
            INamedTypeSymbol factory,
            List<INamedTypeSymbol> allTypes,
            Compilation compilation, AnalysisResult result, string projectName)
        {
            var concreteFactories = allTypes
                .Where(t => t.TypeKind == TypeKind.Class && !t.IsAbstract)
                .Where(t => SymbolHelper.IsDirectImplementation(t, factory))
                .ToList();

            foreach (var concreteFactory in concreteFactories)
            {
                var products = GetCreatedProducts(concreteFactory, factory, compilation);

                if (products.Count >= 2 && ProductsBelongToSameFamily(products))
                {
                    result.AddPattern(projectName, "Abstract Factory", new Dictionary<string, string>
                    {
                        { "Фабрика", factory.Name },
                        { "Конкретная фабрика", concreteFactory.Name },
                        { "Продукты", string.Join(", ", products.Select(p => p.Name)) }
                    });
                }
            }
        }

        private static List<INamedTypeSymbol> GetCreatedProducts(
            INamedTypeSymbol concreteFactory,
            INamedTypeSymbol abstractFactory,
            Compilation compilation)
        {
            var products = new List<INamedTypeSymbol>();
            var syntaxTree = concreteFactory.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
            if (syntaxTree == null) return products;

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var methods = concreteFactory.GetMembers().OfType<IMethodSymbol>();

            foreach (var method in methods)
            {
                var methodSyntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax;
                if (methodSyntax?.Body == null) continue;

                var createdTypes = GetCreatedTypesFromMethod(methodSyntax, semanticModel);
                products.AddRange(createdTypes);
            }

            return products;
        }

        private static bool ProductsBelongToSameFamily(List<INamedTypeSymbol> products)
        {
            if (products.Count < 2) return false;

            var firstNamespace = products.First().ContainingNamespace?.ToString();
            if (string.IsNullOrEmpty(firstNamespace)) return false;

            return products.All(p =>
                p.ContainingNamespace?.ToString()?.StartsWith(firstNamespace) == true);
        }

        #endregion

        #region Adapter Pattern

        /// <summary>
        /// Анализирует компиляцию на наличие паттерна Adapter
        /// </summary>
        public static void AnalyzeAdapters(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                // 1. Поиск всех интерфейсов (потенциальные Target)
                var allInterfaces = allTypes
                    .Where(t => t.TypeKind == TypeKind.Interface)
                    .ToList();

                foreach (var targetInterface in allInterfaces)
                {
                    // 2. Поиск классов, реализующих интерфейс (Adapter)
                    var adapterCandidates = allTypes
                        .Where(t => t.TypeKind == TypeKind.Class &&
                                    t.Interfaces.Any(i =>
                                        SymbolEqualityComparer.Default.Equals(i, targetInterface)))
                        .ToList();

                    foreach (var adapter in adapterCandidates)
                    {
                        // 3. Поиск полей, ссылающихся на другой класс (Adaptee)
                        var adapteeFields = adapter.GetMembers()
                            .OfType<IFieldSymbol>()
                            .Where(f => f.Type.TypeKind == TypeKind.Class &&
                                       !SymbolEqualityComparer.Default.Equals(f.Type, adapter))
                            .ToList();

                        if (!adapteeFields.Any()) continue;

                        // 4. Проверка делегирования вызовов
                        foreach (var method in adapter.GetMembers().OfType<IMethodSymbol>())
                        {
                            var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
                            if (syntaxRef == null) continue;

                            var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                            if (methodSyntax == null) continue;

                            var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
                            if (semanticModel == null) continue;

                            try
                            {
                                // Поиск вызовов методов Adaptee
                                var invocations = methodSyntax.DescendantNodes()
                                .OfType<InvocationExpressionSyntax>();

                                foreach (var invocation in invocations)
                                {
                                    var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                                    if (methodSymbol == null) continue;

                                    // Проверка, что вызываемый метод принадлежит Adaptee
                                    var containingType = methodSymbol.ContainingType;
                                    if (adapteeFields.Any(f =>
                                        SymbolEqualityComparer.Default.Equals(f.Type, containingType)))
                                    {
                                        result.AddPattern(projectName, "Adapter", new Dictionary<string, string>
                                        {
                                            { "Адаптер", adapter.Name },
                                            { "Целевой интерфейс", targetInterface.Name },
                                            { "Адаптирует", containingType.Name }
                                        });
                                        return; // Найден хотя бы один случай
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"Ошибка при анализе метода {method.Name}: {ex.Message}");
                                Debug.WriteLine(ex.StackTrace);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Adapter: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        #endregion

        #region Bridge Pattern

        public static void AnalyzeBridges(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                // 1. Фильтрация интерфейсов, исключая системные и связанные с другими паттернами
                var implementationInterfaces = allTypes
                    .Where(t => t.TypeKind == TypeKind.Interface &&
                                !SymbolHelper.IsSystemType(t) &&
                                t.Name != "ICommand" &&
                                t.Name != "IStrategy" &&
                                t.Name != "IBuilder")
                    .ToList();

                foreach (var implInterface in implementationInterfaces)
                {
                    // 2. Поиск абстракций с полями интерфейса
                    var abstractionCandidates = allTypes
                        .Where(t => t.TypeKind == TypeKind.Class &&
                                   HasCustomImplementationField(t, implInterface))
                        .ToList();

                    foreach (var abstraction in abstractionCandidates)
                    {
                        // 3. Проверка делегирования через поле
                        if (HasDelegationToCustomImplementation(abstraction, implInterface, compilation))
                        {
                            // 4. Поиск конкретных реализаций
                            var concreteImplementations = allTypes
                                .Where(t => t.TypeKind == TypeKind.Class &&
                                           !t.IsAbstract &&
                                           SymbolHelper.IsDirectImplementation(t, implInterface))
                                .ToList();

                            if (concreteImplementations.Any())
                            {
                                result.AddPattern(projectName, "Bridge", new Dictionary<string, string>
                                {
                                    { "Абстракция", abstraction.Name },
                                    { "Интерфейс реализации", implInterface.Name },
                                    { "Конкретные реализации", string.Join(", ", concreteImplementations.Select(c => c.Name)) }
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Bridge: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static bool HasCustomImplementationField(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
        {
            return type.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f =>
                {
                    if (f.Type is not INamedTypeSymbol fieldType)
                        return false;

                    return SymbolEqualityComparer.Default.Equals(fieldType, interfaceSymbol);
                });
        }

        private static bool HasDelegationToCustomImplementation(
            INamedTypeSymbol abstraction,
            INamedTypeSymbol implementationInterface,
            Compilation compilation)
        {
            var fields = abstraction.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => SymbolEqualityComparer.Default.Equals(f.Type, implementationInterface))
                .ToList();

            foreach (var field in fields)
            {
                foreach (var method in abstraction.GetMembers().OfType<IMethodSymbol>())
                {
                    var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
                    if (syntaxRef == null) continue;

                    var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                    var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

                    var invocations = methodSyntax?.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>();

                    foreach (var invocation in invocations ?? Enumerable.Empty<InvocationExpressionSyntax>())
                    {
                        var invokedSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                        if (invokedSymbol?.ContainingType != null &&
                            SymbolEqualityComparer.Default.Equals(invokedSymbol.ContainingType, implementationInterface) &&
                            IsInvokedThroughField(invocation, field, semanticModel))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool IsInvokedThroughField(
            InvocationExpressionSyntax invocation,
            IFieldSymbol field,
            SemanticModel semanticModel)
        {
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess?.Expression is IdentifierNameSyntax identifier)
            {
                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                return SymbolEqualityComparer.Default.Equals(symbol, field);
            }
            return false;
        }

        #endregion

        #region Singleton Pattern

        /// <summary>
        /// Анализирует компиляцию на наличие паттерна Singleton
        /// </summary>
        public static void AnalyzeSingletons(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allClasses = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => t.TypeKind == TypeKind.Class && !t.IsStatic)
                    .ToList();

                foreach (var classSymbol in allClasses)
                {
                    if (IsSingleton(classSymbol, compilation))
                    {
                        var constructor = classSymbol.Constructors
                            .FirstOrDefault(c => !c.IsStatic);

                        result.AddPattern(projectName, "Singleton", new Dictionary<string, string>
                        {
                            { "Класс", classSymbol.Name },
                            { "Конструктор", constructor?.DeclaredAccessibility.ToString() ?? "N/A" },
                            { "Метод получения", GetInstanceMethodName(classSymbol) },
                            { "Потокобезопасный", HasThreadSafeImplementation(classSymbol, compilation) ? "Да" : "Нет" }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Singleton: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static bool IsSingleton(INamedTypeSymbol classSymbol, Compilation compilation)
        {
            // 1. Проверка наличия статического поля с типом класса
            var instanceField = classSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.IsStatic &&
                                   f.Type.Equals(classSymbol, SymbolEqualityComparer.Default));

            if (instanceField == null) return false;

            // 2. Проверка ограниченного доступа к конструктору
            var instanceConstructors = classSymbol.Constructors
                .Where(c => !c.IsStatic)
                .ToList();

            if (!instanceConstructors.Any(c =>
                c.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private ||
                c.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected))
            {
                return false;
            }

            // 3. Проверка статического метода получения экземпляра
            var getInstanceMethod = classSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic &&
                                   m.ReturnType.Equals(classSymbol, SymbolEqualityComparer.Default) &&
                                   m.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public);

            return getInstanceMethod != null &&
                   HasCorrectInstanceCreationLogic(getInstanceMethod, classSymbol, compilation);
        }

        private static bool HasCorrectInstanceCreationLogic(
            IMethodSymbol method,
            INamedTypeSymbol classSymbol,
            Compilation compilation)
        {
            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
            {
                var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

                // Проверка создания экземпляра
                var creations = methodSyntax?
                    .DescendantNodes()
                    .OfType<ObjectCreationExpressionSyntax>()
                    .Where(oc =>
                    {
                        var typeSymbol = semanticModel.GetSymbolInfo(oc.Type).Symbol;
                        return typeSymbol != null &&
                               typeSymbol.Equals(classSymbol, SymbolEqualityComparer.Default);
                    });

                if (!creations?.Any() ?? true) continue;

                // Поддерживаем три варианта:
                // 1. Простая проверка if (instance == null)
                // 2. Double-checked locking
                // 3. Lazy<T> или аналоги

                // Вариант 1: Простая проверка null
                var nullChecks = methodSyntax?
                    .DescendantNodes()
                    .OfType<IfStatementSyntax>()
                    .Count(IsNullCheckCondition) > 0;

                // Вариант 2: Проверка lock
                var hasLock = methodSyntax?
                    .DescendantNodes()
                    .OfType<LockStatementSyntax>()
                    .Any() ?? false;

                // Вариант 3: Использование Lazy<T>
                var hasLazy = methodSyntax?
                    .DescendantNodes()
                    .OfType<ObjectCreationExpressionSyntax>()
                    .Any(oc =>
                    {
                        var typeSymbol = semanticModel.GetSymbolInfo(oc.Type).Symbol?.ToString();
                        return typeSymbol?.StartsWith("System.Lazy<") == true;
                    }) ?? false;

                if (nullChecks || hasLock || hasLazy)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasThreadSafeImplementation(INamedTypeSymbol classSymbol, Compilation compilation)
        {
            var getInstanceMethod = classSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic &&
                                   m.ReturnType.Equals(classSymbol, SymbolEqualityComparer.Default));

            if (getInstanceMethod == null) return false;

            foreach (var syntaxRef in getInstanceMethod.DeclaringSyntaxReferences)
            {
                var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;

                // Проверка double-checked locking
                var lockStatements = methodSyntax?
                    .DescendantNodes()
                    .OfType<LockStatementSyntax>()
                    .Count() ?? 0;

                var nullChecks = methodSyntax?
                    .DescendantNodes()
                    .OfType<IfStatementSyntax>()
                    .Count(IsNullCheckCondition) ?? 0;

                // Проверка использования Lazy<T>
                var hasLazy = methodSyntax?
                    .DescendantNodes()
                    .OfType<ObjectCreationExpressionSyntax>()
                    .Any(oc => oc.Type.ToString().StartsWith("Lazy<")) ?? false;

                return lockStatements > 0 || nullChecks > 1 || hasLazy;
            }
            return false;
        }

        private static bool IsNullCheckCondition(IfStatementSyntax ifStatement)
        {
            return ifStatement.Condition is BinaryExpressionSyntax binary &&
                   (binary.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken) ||
                    binary.OperatorToken.IsKind(SyntaxKind.ExclamationEqualsToken)) &&
                   (binary.Right is LiteralExpressionSyntax literal &&
                    literal.IsKind(SyntaxKind.NullLiteralExpression) ||
                    binary.Left is LiteralExpressionSyntax leftLiteral &&
                    leftLiteral.IsKind(SyntaxKind.NullLiteralExpression));
        }

        private static string GetInstanceMethodName(INamedTypeSymbol classSymbol)
        {
            return classSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsStatic &&
                                   m.ReturnType.Equals(classSymbol, SymbolEqualityComparer.Default))
                ?.Name ?? "N/A";
        }

        #endregion

        #region Prototype Pattern

        /// <summary>
        /// Анализирует компиляцию на наличие паттерна Prototype
        /// </summary>
        public static void AnalyzePrototypes(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                foreach (var type in allTypes.Where(t => t.TypeKind == TypeKind.Class))
                {
                    AnalyzeTypeForPrototypePattern(type, compilation, result, projectName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Prototype: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static void AnalyzeTypeForPrototypePattern(INamedTypeSymbol type, Compilation compilation, AnalysisResult result, string projectName)
        {
            // 1. Проверка наличия методов копирования
            var copyMethods = type.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Ordinary &&
                            !m.IsStatic &&
                            m.ReturnType.Equals(type, SymbolEqualityComparer.Default) &&
                            m.Parameters.Length == 0)
                .ToList();

            if (copyMethods.Count < 1) return;

            // 2. Проверка реализации копирования
            foreach (var method in copyMethods)
            {
                foreach (var syntaxRef in method.DeclaringSyntaxReferences)
                {
                    var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                    var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

                    // Проверка использования MemberwiseClone или других механизмов копирования
                    var hasCloneLogic = HasCloneLogic(methodSyntax, semanticModel, type);

                    if (hasCloneLogic)
                    {
                        // 3. Проверка использования прототипа в клиентском коде
                        var isUsedAsPrototype = IsTypeUsedAsPrototype(type, compilation);

                        if (isUsedAsPrototype)
                        {
                            result.AddPattern(projectName, "Prototype", new Dictionary<string, string>
                            {
                                { "Тип", type.Name },
                                { "Метод копирования", method.Name },
                                { "Тип копирования", IsDeepCopy(methodSyntax) ? "Deep Copy" : "Shallow Copy" }
                            });

                            return;

                        }
                    }
                }
            }
        }

        private static bool HasCloneLogic(MethodDeclarationSyntax methodSyntax, SemanticModel semanticModel, INamedTypeSymbol type)
        {
            if (methodSyntax == null) return false;

            // Проверка вызова MemberwiseClone
            var memberwiseCloneInvocations = methodSyntax.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression is MemberAccessExpressionSyntax memberAccess &&
                           memberAccess.Name.Identifier.Text == "MemberwiseClone");

            if (memberwiseCloneInvocations.Any()) return true;

            // Проверка создания нового экземпляра с копированием полей
            var objectCreations = methodSyntax.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(oc => semanticModel.GetSymbolInfo(oc.Type).Symbol?.Equals(type, SymbolEqualityComparer.Default) == true);

            if (objectCreations.Any())
            {
                // Проверка, что поля копируются из текущего объекта
                var assignments = methodSyntax.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(a => a.Left is MemberAccessExpressionSyntax leftMember &&
                               a.Right is MemberAccessExpressionSyntax rightMember &&
                               rightMember.Expression is ThisExpressionSyntax);

                if (assignments.Any()) return true;
            }

            return false;
        }

        private static bool IsDeepCopy(MethodDeclarationSyntax methodSyntax)
        {
            // Проверка на глубокое копирование (создание новых объектов для ссылочных полей)
            var newObjectCreations = methodSyntax.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(oc => oc.Parent is AssignmentExpressionSyntax assignment &&
                            assignment.Left is MemberAccessExpressionSyntax);

            return newObjectCreations.Any();
        }

        private static bool IsTypeUsedAsPrototype(INamedTypeSymbol type, Compilation compilation)
        {
            // Проверка, что тип используется для создания новых объектов через копирование
            var references = type.DeclaringSyntaxReferences;
            foreach (var reference in references)
            {
                var syntaxTree = reference.SyntaxTree;
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // Поиск вызовов методов копирования
                var methodInvocations = root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i =>
                    {
                        var methodSymbol = semanticModel.GetSymbolInfo(i).Symbol as IMethodSymbol;
                        return methodSymbol != null &&
                               methodSymbol.ContainingType.Equals(type, SymbolEqualityComparer.Default) &&
                               methodSymbol.ReturnType.Equals(type, SymbolEqualityComparer.Default);
                    });

                if (methodInvocations.Any()) return true;
            }

            return false;
        }

        #endregion

        #region Memento Pattern

        public static void AnalyzeMementos(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                // Поиск Originator по методам Save/SaveState и Restore/RestoreState
                var originatorCandidates = allTypes
                    .Where(t => t.TypeKind == TypeKind.Class &&
                                (HasSaveLikeMethod(t) || HasSaveStateMethod(t)) &&
                                (HasRestoreLikeMethod(t) || HasRestoreStateMethod(t)))
                    .ToList();

                foreach (var originator in originatorCandidates)
                {
                    var mementoSymbol = GetMementoSymbol(originator);
                    if (mementoSymbol == null) continue;

                    var caretaker = FindCaretaker(mementoSymbol, allTypes);
                    result.AddPattern(projectName, "Memento", new Dictionary<string, string>
                    {
                        { "Originator", originator.Name },
                        { "Memento", mementoSymbol.Name },
                        { "Caretaker", caretaker?.Name ?? "Не найден" }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Memento: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static bool HasSaveLikeMethod(INamedTypeSymbol type)
        {
            return type.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.Name == "Save" &&
                          (m.ReturnType.TypeKind == TypeKind.Class ||
                           m.ReturnType.TypeKind == TypeKind.Interface));
        }

        private static bool HasSaveStateMethod(INamedTypeSymbol type)
        {
            return type.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.Name == "SaveState" &&
                          m.ReturnType.TypeKind == TypeKind.Class);
        }

        private static bool HasRestoreLikeMethod(INamedTypeSymbol type)
        {
            return type.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.Name == "Restore" &&
                          m.Parameters.Length == 1 &&
                          (m.Parameters[0].Type.TypeKind == TypeKind.Class ||
                           m.Parameters[0].Type.TypeKind == TypeKind.Interface));
        }

        private static bool HasRestoreStateMethod(INamedTypeSymbol type)
        {
            return type.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.Name == "RestoreState" &&
                          m.Parameters.Length == 1 &&
                          m.Parameters[0].Type.TypeKind == TypeKind.Class);
        }

        private static INamedTypeSymbol? GetMementoSymbol(INamedTypeSymbol originator)
        {
            // Попытка получить Memento из метода Save
            var saveMethod = originator.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Name is "Save" or "SaveState");

            var mementoType = saveMethod?.ReturnType as INamedTypeSymbol;

            // Для интерфейсов проверяем наличие GetState()
            if (mementoType?.TypeKind == TypeKind.Interface &&
                !mementoType.GetMembers().OfType<IMethodSymbol>().Any(m => m.Name == "GetState"))
            {
                return null;
            }

            return mementoType;
        }

        private static INamedTypeSymbol? FindCaretaker(INamedTypeSymbol mementoSymbol, IEnumerable<INamedTypeSymbol> allTypes)
        {
            return allTypes
                .Where(t => t.TypeKind == TypeKind.Class)
                .FirstOrDefault(t =>
                {
                    // Проверка коллекции Memento
                    var hasMementoCollection = t.GetMembers()
                        .OfType<IFieldSymbol>()
                        .Any(f =>
                        {
                            if (f.Type is not INamedTypeSymbol nt) return false;

                            var isGenericCollection = nt.OriginalDefinition.ToString()
                                .StartsWith("System.Collections.Generic.");

                            return isGenericCollection &&
                                   nt.TypeArguments.Length == 1 &&
                                   SymbolEqualityComparer.Default.Equals(nt.TypeArguments[0], mementoSymbol);
                        });

                    // Проверка связи с Originator (для второго примера)
                    var hasOriginatorConnection = t.GetMembers()
                        .OfType<IFieldSymbol>()
                        .Any(f => f.Type.Name.Contains("Originator"));

                    return hasMementoCollection || hasOriginatorConnection;
                });
        }

        #endregion

        #region Proxy Pattern

        /// <summary>
        /// Анализирует компиляцию на наличие паттерна Proxy
        /// </summary>
        public static void AnalyzeProxies(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                var processedProxies = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

                // Ищем абстрактные классы и интерфейсы
                var subjects = allTypes
                    .Where(t => t.TypeKind == TypeKind.Interface ||
                                (t.TypeKind == TypeKind.Class && t.IsAbstract))
                    .ToList();

                foreach (var subject in subjects)
                {
                    var implementations = allTypes
                        .Where(t => t.TypeKind == TypeKind.Class &&
                                    !t.IsAbstract &&
                                    SymbolHelper.IsDerivedFrom(t, subject))
                        .ToList();

                    if (implementations.Count < 2) continue;

                    foreach (var proxyCandidate in implementations)
                    {
                        if (processedProxies.Contains(proxyCandidate)) continue;

                        // Проверка полей, ссылающихся на другую реализацию
                        var realSubjectFields = proxyCandidate.GetMembers()
                            .OfType<IFieldSymbol>()
                            .Where(f => implementations.Any(impl =>
                                SymbolEqualityComparer.Default.Equals(f.Type, impl)))
                            .ToList();

                        if (!realSubjectFields.Any()) continue;

                        bool isProxyDetected = false;

                        foreach (var method in proxyCandidate.GetMembers().OfType<IMethodSymbol>())
                        {
                            if (isProxyDetected) break;

                            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
                            {
                                var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                                var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

                                // Поиск создания объектов и вызовов методов
                                var objectCreations = methodSyntax?
                                    .DescendantNodes()
                                    .OfType<ObjectCreationExpressionSyntax>()
                                    .Select(oc => semanticModel.GetSymbolInfo(oc).Symbol?.ContainingType);

                                var invocations = methodSyntax?
                                    .DescendantNodes()
                                    .OfType<InvocationExpressionSyntax>()
                                    .Select(inv => semanticModel.GetSymbolInfo(inv).Symbol?.ContainingType);

                                var delegations = objectCreations?.Concat(invocations)?
                                    .Where(t => t != null &&
                                               implementations.Contains(t, SymbolEqualityComparer.Default));

                                if (delegations?.Any() == true)
                                {
                                    result.AddPattern(projectName, "Proxy", new Dictionary<string, string>
                                    {
                                        { "Прокси", proxyCandidate.Name },
                                        { "Субъект", subject.Name },
                                        { "Реальный субъект", realSubjectFields.First().Type.Name }
                                    });
                                    processedProxies.Add(proxyCandidate);
                                    isProxyDetected = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Proxy: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        #endregion

        #region State Pattern

        public static void AnalyzeStates(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                // 1. Поиск контекста
                foreach (var context in allTypes.Where(t => t.TypeKind == TypeKind.Class))
                {
                    var (stateField, transitionMethod) = FindStateComponents(context);
                    if (stateField == null || transitionMethod == null) continue;

                    // 2. Поиск абстракции состояния (интерфейс или абстрактный класс)
                    var stateAbstractType = stateField.Type as INamedTypeSymbol;
                    if (stateAbstractType?.TypeKind != TypeKind.Interface &&
                        !stateAbstractType.IsAbstract) continue;

                    // 3. Проверка делегирования методов контекста
                    if (!HasDelegationToState(context, stateAbstractType, compilation)) continue;

                    // и
                    var concreteStates = allTypes
                        .Where(t => t.InheritsFrom(stateAbstractType) &&
                                   t.HasStateTransitionLogic(context, compilation))
                        .ToList();

                    if (concreteStates.Any())
                    {
                        result.AddPattern(projectName, "State", new Dictionary<string, string>
                        {
                            { "Контекст", context.Name },
                            { "Интерфейс состояния", stateAbstractType.Name },
                            { "Конкретные состояния", string.Join(", ", concreteStates.Select(s => s.Name)) }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа State: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static (IFieldSymbol?, IMethodSymbol?) FindStateComponents(INamedTypeSymbol context)
        {
            IFieldSymbol? stateField = null;
            IMethodSymbol? transitionMethod = null;

            // Поиск поля состояния и метода изменения
            foreach (var member in context.GetMembers())
            {
                if (member is IFieldSymbol field &&
                    (field.Type.TypeKind == TypeKind.Interface || field.Type.IsAbstract))
                {
                    stateField = field;
                }

                if (member is IMethodSymbol method &&
                    method.Name.StartsWith("Transition") &&
                    method.Parameters.Length == 1 &&
                    method.Parameters[0].Type.Equals(stateField?.Type, SymbolEqualityComparer.Default))
                {
                    transitionMethod = method;
                }
            }

            return (stateField, transitionMethod);
        }

        private static bool HasDelegationToState(INamedTypeSymbol context,
                                               INamedTypeSymbol stateType,
                                               Compilation compilation)
        {
            return context.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.DelegateCallsTo(stateType, compilation));
        }

        // Расширения для символов
        private static bool InheritsFrom(this ITypeSymbol type, INamedTypeSymbol baseType)
            => SymbolHelper.IsDerivedFrom(type as INamedTypeSymbol, baseType);

        private static bool DelegateCallsTo(this IMethodSymbol method,
                                            INamedTypeSymbol targetType,
                                            Compilation compilation)
        {
            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
            {
                var syntaxTree = syntaxRef.SyntaxTree;
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var syntax = syntaxRef.GetSyntax();

                var invocations = syntax.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    var calledSymbol = semanticModel.GetSymbolInfo(invocation).Symbol;
                    if (calledSymbol?.ContainingType?.Equals(targetType, SymbolEqualityComparer.Default) == true)
                        return true;
                }
            }
            return false;
        }

        private static bool HasStateTransitionLogic(this INamedTypeSymbol state,
                                                 INamedTypeSymbol context,
                                                 Compilation compilation)
        {
            return state.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.CallsContextTransitionMethod(context, compilation));
        }

        private static bool CallsContextTransitionMethod(this IMethodSymbol method,
                                                       INamedTypeSymbol context,
                                                       Compilation compilation)
        {
            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
            {
                var syntaxTree = syntaxRef.SyntaxTree;
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var syntax = syntaxRef.GetSyntax();

                var invocations = syntax.DescendantNodes().OfType<InvocationExpressionSyntax>();
                return invocations.Any(i =>
                    i.Expression.ToString().Contains("TransitionTo") &&
                    semanticModel.GetSymbolInfo(i).Symbol?.ContainingType.Equals(context) == true);
            }
            return false;
        }

        #endregion

        #region Strategy Pattern

        /// <summary>
        /// Анализирует компиляцию на наличие паттерна Strategy
        /// </summary>
        public static void AnalyzeStrategies(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                // 1. Поиск интерфейсов стратегий (методы возвращают результат)
                var strategyInterfaces = allTypes
                    .Where(t => t.TypeKind == TypeKind.Interface &&
                                t.GetMembers().OfType<IMethodSymbol>()
                                    .Any(m => m.ReturnType.SpecialType != SpecialType.System_Void))
                    .ToList();

                foreach (var strategyInterface in strategyInterfaces)
                {
                    // 2. Поиск реализаций стратегии
                    var strategyImplementations = allTypes
                        .Where(t => t.TypeKind == TypeKind.Class &&
                                    !t.IsAbstract &&
                                    SymbolHelper.IsDirectImplementation(t, strategyInterface))
                        .ToList();

                    if (strategyImplementations.Count < 1) continue;

                    // 3. Поиск контекста с динамическим изменением стратегии
                    var contextCandidates = allTypes
                        .Where(t => t.TypeKind == TypeKind.Class &&
                                    HasStrategyFieldOrProperty(t, strategyInterface) &&
                                    HasDynamicStrategySetter(t, strategyInterface))
                        .ToList();

                    foreach (var context in contextCandidates)
                    {
                        // 4. Проверка делегирования вызовов к стратегии
                        if (HasDelegationToStrategy(context, strategyInterface, compilation))
                        {
                            result.AddPattern(projectName, "Strategy", new Dictionary<string, string>
                            {
                                { "Контекст", context.Name },
                                { "Интерфейс стратегии", strategyInterface.Name },
                                { "Реализации", string.Join(", ", strategyImplementations.Select(s => s.Name)) }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Strategy: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static bool HasStrategyFieldOrProperty(INamedTypeSymbol type, INamedTypeSymbol strategyInterface)
        {
            return type.GetMembers().Any(member =>
                (member is IFieldSymbol field &&
                 SymbolEqualityComparer.Default.Equals(field.Type, strategyInterface)) ||
                (member is IPropertySymbol property &&
                 SymbolEqualityComparer.Default.Equals(property.Type, strategyInterface)));
        }

        private static bool HasDynamicStrategySetter(INamedTypeSymbol type, INamedTypeSymbol strategyInterface)
        {
            // Проверка наличия метода для изменения стратегии (например, SetStrategy)
            return type.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.Parameters.Length == 1 &&
                          SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, strategyInterface) &&
                          m.Name.StartsWith("Set"));
        }

        private static bool HasDelegationToStrategy(INamedTypeSymbol context, INamedTypeSymbol strategyInterface, Compilation compilation)
        {
            return context.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.DelegateCallsToStrategy(strategyInterface, compilation));
        }

        #endregion

        #region Template Method Pattern

        /// <summary>
        /// Анализирует компиляцию на наличие паттерна Template Method
        /// </summary>
        public static void AnalyzeTemplateMethods(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                foreach (var abstractClass in allTypes.Where(t => t.IsAbstract && t.TypeKind == TypeKind.Class))
                {
                    // Пропускаем классы, которые являются Factory Method
                    if (IsFactoryMethodClass(abstractClass)) continue;

                    var templateMethod = FindTemplateMethod(abstractClass);
                    if (templateMethod == null) continue;

                    var (requiredMethods, hookMethods) = AnalyzeTemplateStructure(abstractClass, templateMethod);
                    if (!requiredMethods.Any()) continue;

                    var subClasses = allTypes.Where(t =>
                        t.TypeKind == TypeKind.Class &&
                        !t.IsAbstract &&
                        SymbolHelper.IsDerivedFrom(t, abstractClass))
                        .ToList();

                    if (ValidateSubclasses(subClasses, requiredMethods, hookMethods, templateMethod))
                    {
                        result.AddPattern(projectName, "Template Method", new Dictionary<string, string>
                        {
                            { "Абстрактный класс", abstractClass.Name },
                            { "Шаблонный метод", templateMethod.Name },
                            { "Обязательные методы", string.Join(", ", requiredMethods.Select(m => m.Name)) },
                            { "Подклассы", string.Join(", ", subClasses.Select(s => s.Name)) }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Template Method: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static bool IsFactoryMethodClass(INamedTypeSymbol abstractClass)
        {
            return abstractClass.GetMembers()
                .OfType<IMethodSymbol>()
                .Any(m => m.IsAbstract &&
                        m.ReturnType.TypeKind == TypeKind.Interface &&
                        m.Name.Contains("Factory"));
        }

        private static IMethodSymbol? FindTemplateMethod(INamedTypeSymbol abstractClass)
        {
            return abstractClass.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    !m.IsAbstract && // Важно: шаблонный метод имеет реализацию
                    !m.IsVirtual &&
                    m.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Private &&
                    CallsAbstractMethods(m, abstractClass));
        }

        private static bool CallsAbstractMethods(IMethodSymbol method, INamedTypeSymbol abstractClass)
        {
            var syntaxReferences = method.DeclaringSyntaxReferences;
            foreach (var syntaxRef in syntaxReferences)
            {
                var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                var invocations = methodSyntax?.DescendantNodes().OfType<InvocationExpressionSyntax>();

                var abstractMethodNames = abstractClass.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Where(m => m.IsAbstract || m.IsVirtual)
                    .Select(m => m.Name)
                    .ToHashSet();

                foreach (var invocation in invocations ?? Enumerable.Empty<InvocationExpressionSyntax>())
                {
                    var methodName = invocation.Expression.ToString().Split('.').Last();
                    if (abstractMethodNames.Contains(methodName))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static (List<IMethodSymbol>, List<IMethodSymbol>) AnalyzeTemplateStructure(
            INamedTypeSymbol abstractClass,
            IMethodSymbol templateMethod)
        {
            var requiredMethods = new List<IMethodSymbol>();
            var hookMethods = new List<IMethodSymbol>();

            foreach (var member in abstractClass.GetMembers().OfType<IMethodSymbol>())
            {
                if (member.IsAbstract && !member.IsVirtual)
                    requiredMethods.Add(member);
                else if (member.IsVirtual && !member.IsAbstract)
                    hookMethods.Add(member);
            }

            return (requiredMethods, hookMethods);
        }

        private static bool ValidateSubclasses(
                            List<INamedTypeSymbol> subClasses,
                            List<IMethodSymbol> requiredMethods,
                            List<IMethodSymbol> hookMethods,
                            IMethodSymbol templateMethod) // Добавляем параметр templateMethod
        {
            if (!subClasses.Any()) return false;

            foreach (var subClass in subClasses)
            {
                // Проверка реализации обязательных методов
                var implementedRequired = requiredMethods.All(rm =>
                    subClass.GetMembers()
                        .OfType<IMethodSymbol>()
                        .Any(m => m.IsOverride &&
                                  SymbolEqualityComparer.Default.Equals(m.OverriddenMethod, rm)));

                // Проверка, что шаблонный метод не переопределен
                var templateOverride = subClass.GetMembers()
                    .OfType<IMethodSymbol>()
                    .Any(m => m.IsOverride &&
                              SymbolEqualityComparer.Default.Equals(m.OverriddenMethod, templateMethod));

                if (!implementedRequired || templateOverride)
                {
                    Console.WriteLine($"Ошибка в классе {subClass.Name}: Не реализованы обязательные методы или переопределен шаблонный метод.");
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region Visitor Pattern

        /// <summary>
        /// Анализирует компиляцию на наличие паттерна Visitor
        /// </summary>
        public static void AnalyzeVisitors(string projectName, Compilation compilation, AnalysisResult result)
        {
            try
            {
                var allTypes = SymbolHelper.GetAllTypes(compilation)
                    .Where(t => !SymbolHelper.IsSystemType(t))
                    .ToList();

                // 1. Поиск интерфейса посетителя
                var visitorInterfaces = allTypes
                    .Where(t => t.TypeKind == TypeKind.Interface &&
                                t.GetMembers().OfType<IMethodSymbol>()
                                    .Any(m => m.Name.StartsWith("Visit") &&
                                           m.Parameters.Length == 1 &&
                                           m.Parameters[0].Type.TypeKind == TypeKind.Class))
                    .ToList();

                foreach (var visitorInterface in visitorInterfaces)
                {
                    // 2. Поиск компонентов с методом Accept
                    var componentCandidates = allTypes
                        .Where(t => t.GetMembers().OfType<IMethodSymbol>()
                            .Any(m => m.Name == "Accept" &&
                                    m.Parameters.Length == 1 &&
                                    SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, visitorInterface)))
                        .ToList();

                    if (componentCandidates.Count < 1) continue;

                    // 3. Проверка структуры компонентов и посетителей
                    var validComponents = componentCandidates
                        .Where(component => HasValidAcceptMethod(component, visitorInterface, compilation))
                        .ToList();

                    // 4. Поиск конкретных посетителей
                    var concreteVisitors = allTypes
                        .Where(t => t.TypeKind == TypeKind.Class &&
                                    !t.IsAbstract &&
                                    SymbolHelper.IsDirectImplementation(t, visitorInterface))
                        .ToList();

                    if (validComponents.Count >= 2 && concreteVisitors.Any())
                    {
                        result.AddPattern(projectName, "Visitor", new Dictionary<string, string>
                        {
                            { "Интерфейс посетителя", visitorInterface.Name },
                            { "Компоненты", string.Join(", ", validComponents.Select(c => c.Name)) },
                            { "Конкретные посетители", string.Join(", ", concreteVisitors.Select(v => v.Name)) }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка анализа Visitor: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private static bool HasValidAcceptMethod(
            INamedTypeSymbol component,
            INamedTypeSymbol visitorInterface,
            Compilation compilation)
        {
            var acceptMethod = component.GetMembers()
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.Name == "Accept");

            foreach (var syntaxRef in acceptMethod?.DeclaringSyntaxReferences!)
            {
                var syntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;
                var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

                // Проверка вызова метода Visit в Accept
                var invocations = syntax?.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(i =>
                    {
                        var methodSymbol = semanticModel.GetSymbolInfo(i).Symbol as IMethodSymbol;
                        return methodSymbol?.ContainingType?.Equals(visitorInterface, SymbolEqualityComparer.Default) == true &&
                               methodSymbol.Name.StartsWith("Visit");
                    });

                if (invocations?.Any() == true) return true;
            }
            return false;
        }

        #endregion
    }

    /// <summary>
    /// Contains symbol analysis helpers
    /// </summary>
    internal static class SymbolHelper
    {
        public static bool DelegateCallsToStrategy(
               this IMethodSymbol method,
               INamedTypeSymbol strategyInterface,
               Compilation compilation)
        {
            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
            {
                var syntaxTree = syntaxRef.SyntaxTree;
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;

                var invocations = methodSyntax?.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>();

                foreach (var invocation in invocations ?? Enumerable.Empty<InvocationExpressionSyntax>())
                {
                    var calledMethod = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                    if (calledMethod?.ContainingType != null &&
                        SymbolEqualityComparer.Default.Equals(calledMethod.ContainingType, strategyInterface))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Collects all types in the compilation's global namespace
        /// </summary>
        public static IEnumerable<INamedTypeSymbol> GetAllTypes(Compilation compilation)
        {
            var types = new List<INamedTypeSymbol>();
            CollectNamespace(compilation.GlobalNamespace, types);
            return types;
        }

        public static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
        {
            return type.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i, interfaceSymbol));
        }

        private static void CollectNamespace(INamespaceSymbol ns, List<INamedTypeSymbol> results)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    CollectNamespace(childNs, results);
                }
                else if (member is INamedTypeSymbol type && !IsSystemType(type))
                {
                    results.Add(type);
                }
            }
        }

        /// <summary>
        /// Checks if type is derived from specified base type
        /// </summary>
        public static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            // Проверка на null входных параметров
            if (type == null || baseType == null)
                return false;

            // Проверка базовых классов
            var current = type.BaseType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                    return true;
                current = current.BaseType;
            }

            // Проверка реализованных интерфейсов
            return type.AllInterfaces.Any(i =>
                SymbolEqualityComparer.Default.Equals(i, baseType));
        }

        /// <summary>
        /// Checks if type directly implements interface (not through inheritance)
        /// </summary>
        public static bool IsDirectImplementation(INamedTypeSymbol type, INamedTypeSymbol factory)
        {
            return type.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, factory)) ||
                   SymbolEqualityComparer.Default.Equals(type.BaseType, factory);
        }

        /// <summary>
        /// Checks if type belongs to system namespace
        /// </summary>
        public static bool IsSystemType(ITypeSymbol type)
        {
            if (type is not INamedTypeSymbol namedType)
                return false;

            var ns = namedType.ContainingNamespace?.ToString() ?? "";
            return ns.StartsWith("System") ||
                   ns.StartsWith("Microsoft") ||
                   namedType.SpecialType != SpecialType.None;
        }

        public static bool IsParameterlessPrivate(IMethodSymbol ctor)
        {
            return ctor.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private &&
                   ctor.Parameters.IsEmpty;
        }
    }

    internal static class Logger
    {
        public static void LogSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {message}");
            Console.ResetColor();
        }

        public static void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"~ {message}");
            Console.ResetColor();
        }

        public static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"! {message}");
            Console.ResetColor();
        }
    }

    public class AnalysisResult
    {
        public Dictionary<string, List<PatternResult>> ProjectPatterns { get; } = new();

        public void AddPattern(string projectName, string patternName, Dictionary<string, string> details)
        {
            if (!ProjectPatterns.ContainsKey(projectName))
            {
                ProjectPatterns[projectName] = new List<PatternResult>();
            }

            ProjectPatterns[projectName].Add(new PatternResult
            {
                Name = patternName,
                Details = details,
                DetectedAt = DateTime.Now
            });
        }
    }

    public class PatternResult
    {
        public string Name { get; set; }
        public Dictionary<string, string> Details { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }
}