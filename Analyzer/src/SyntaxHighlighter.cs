using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Analyzer.src
{
    public class SyntaxHighlighter
    {
        public List<TextRange> HighlightCode(string code)
        {
            // Анализируем код с использованием Roslyn
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();

            var highlightedRanges = new List<TextRange>();

            // Ищем все узлы с определенными типами (например, классы, методы и т.д.)
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDeclaration in classDeclarations)
            {
                var span = classDeclaration.Span;
                highlightedRanges.Add(new TextRange(span.Start, span.Length));
            }

            var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var methodDeclaration in methodDeclarations)
            {
                var span = methodDeclaration.Span;
                highlightedRanges.Add(new TextRange(span.Start, span.Length));
            }

            // И так далее для других узлов, например, переменных, ключевых слов и т.д.

            return highlightedRanges;
        }
    }

    public class TextRange
    {
        public int Start { get; set; }
        public int Length { get; set; }

        public TextRange(int start, int length)
        {
            Start = start;
            Length = length;
        }
    }
}
