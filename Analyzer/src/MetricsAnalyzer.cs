using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Analyzer.src
{
    public class MethodMetrics
    {
        public string FilePath { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; }

        public int LogicalLines { get; set; }
        public int CyclomaticComplexity { get; set; }
        public int OperatorCount { get; set; }
        public int OperandCount { get; set; }
        public double HalsteadVolume { get; set; }
        public double MaintainabilityIndex { get; set; }
        public int ParameterCount { get; set; }
        public int LocalVariableCount { get; set; }
        public int FanIn { get; set; }
        public int FanOut { get; set; }
        public int NPathComplexity { get; set; }
        public int MaxNestingDepth { get; set; }

        public override string ToString()
        {
            return $"Файл: {FilePath}\nКласс: {ClassName}\nМетод: {MethodName}\n" +
                   $"- Строк: {LogicalLines}, Параметров: {ParameterCount}, Переменных: {LocalVariableCount}\n" +
                   $"- Сложность: {CyclomaticComplexity}, NPath: {NPathComplexity}, Вложенность: {MaxNestingDepth}\n" +
                   $"- Halstead: Volume={HalsteadVolume:F2}, Op={OperatorCount}, Opr={OperandCount}\n" +
                   $"- MI: {MaintainabilityIndex:F2}, Fan-In: {FanIn}, Fan-Out: {FanOut}\n";
        }
    }


    public class MetricsResult
    {
        public List<MethodMetrics> Methods { get; } = new();
    }
}
