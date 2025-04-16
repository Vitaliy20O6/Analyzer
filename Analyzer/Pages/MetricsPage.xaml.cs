using Analyzer.src;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Analyzer.Pages
{
    /// <summary>
    /// Логика взаимодействия для MetricsPage.xaml
    /// </summary>
    public partial class MetricsPage : Page
    {
        private readonly SolutionLoader _loader;

        public MetricsPage(SolutionLoader loader)
        {
            InitializeComponent();
            _loader = loader;

            var metrics = loader.Metrics.Methods;
            DisplayMetrics(metrics);
        }

        private void LoadMetricsToRichTextBox()
        {
            var doc = new FlowDocument();

            foreach (var method in _loader.Metrics.Methods)
            {
                var para = new Paragraph(new Run(method.ToString()))
                {
                    Margin = new Thickness(0, 0, 0, 10)
                };
                doc.Blocks.Add(para);
            }

            MetricsRichTextBox.Document = doc;
        }

        public void DisplayMetrics(List<MethodMetrics> metricsList)
        {
            MetricsRichTextBox.Document.Blocks.Clear();

            foreach (var metric in metricsList)
            {
                var paragraph = new Paragraph();

                // Заголовок файла
                paragraph.Inlines.Add(new Run($"Файл: {metric.FilePath}\n")
                {
                    Foreground = Brushes.DarkRed,
                    FontWeight = FontWeights.Bold
                });

                // Имя класса
                paragraph.Inlines.Add(new Run($"Класс: {metric.ClassName}\n")
                {
                    Foreground = Brushes.DarkCyan,
                    FontWeight = FontWeights.SemiBold
                });

                // Имя метода
                paragraph.Inlines.Add(new Run($"Метод: {metric.MethodName}\n")
                {
                    Foreground = Brushes.Navy,
                    FontWeight = FontWeights.SemiBold
                });

                // Общая информация
                paragraph.Inlines.Add(new Run($"  Строк: {metric.LogicalLines}, Параметров: {metric.ParameterCount}, Переменных: {metric.LocalVariableCount}\n")
                {
                    Foreground = Brushes.Black
                });

                // Сложности
                paragraph.Inlines.Add(new Run($"  Сложность: {metric.CyclomaticComplexity}, NPath: {metric.NPathComplexity}, Вложенность: {metric.MaxNestingDepth}\n")
                {
                    Foreground = Brushes.SteelBlue
                });

                // Halstead
                paragraph.Inlines.Add(new Run($"  Halstead Volume: {metric.HalsteadVolume:F2}, Операторы: {metric.OperatorCount}, Операнды: {metric.OperandCount}\n")
                {
                    Foreground = Brushes.DarkGreen
                });

                // Maintainability Index
                Brush miColor = metric.MaintainabilityIndex switch
                {
                    >= 80 => Brushes.Green,
                    >= 60 => Brushes.Orange,
                    _ => Brushes.Red
                };
                paragraph.Inlines.Add(new Run($"  Maintainability Index: {metric.MaintainabilityIndex:F2}\n")
                {
                    Foreground = miColor,
                    FontWeight = FontWeights.Bold
                });

                // Fan-in/out
                paragraph.Inlines.Add(new Run($"  Fan-In: {metric.FanIn}, Fan-Out: {metric.FanOut}\n")
                {
                    Foreground = Brushes.Purple
                });

                // Разделитель
                paragraph.Inlines.Add(new Run(new string('-', 80) + "\n")
                {
                    Foreground = Brushes.Gray
                });

                MetricsRichTextBox.Document.Blocks.Add(paragraph);
            }
        }
    }
}
