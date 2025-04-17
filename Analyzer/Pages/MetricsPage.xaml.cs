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

            SetInfoText();

            var metrics = loader.Metrics.Methods;
            DisplayMetricsGrouped(metrics);
        }

        private void DisplayMetricsGrouped(List<MethodMetrics> metrics)
        {
            MetricsRichTextBox.Document.Blocks.Clear();

            var grouped = metrics
                .GroupBy(m => new { m.FilePath, m.ClassName })
                .OrderBy(g => g.Key.FilePath)
                .ThenBy(g => g.Key.ClassName);

            foreach (var group in grouped)
            {
                var paragraph = new Paragraph();

                // Файл
                paragraph.Inlines.Add(new Run($"Файл: {group.Key.FilePath}\n")
                {
                    Foreground = Brushes.DarkRed,
                    FontWeight = FontWeights.Bold
                });

                // Класс
                paragraph.Inlines.Add(new Run($"Класс: {group.Key.ClassName}\n")
                {
                    Foreground = Brushes.DarkCyan,
                    FontWeight = FontWeights.SemiBold
                });

                foreach (var method in group)
                {
                    // Метод
                    paragraph.Inlines.Add(new Run($"  Метод: {method.MethodName}\n")
                    {
                        Foreground = Brushes.Navy,
                        FontWeight = FontWeights.SemiBold
                    });

                    // Общая информация
                    paragraph.Inlines.Add(new Run($"    Строк: {method.LogicalLines}, Параметров: {method.ParameterCount}, Переменных: {method.LocalVariableCount}\n")
                    {
                        Foreground = Brushes.Black
                    });

                    // Сложности
                    paragraph.Inlines.Add(new Run($"    Сложность: {method.CyclomaticComplexity}, NPath: {method.NPathComplexity}, Вложенность: {method.MaxNestingDepth}\n")
                    {
                        Foreground = Brushes.SteelBlue
                    });

                    // Halstead
                    paragraph.Inlines.Add(new Run($"    Halstead Volume: {method.HalsteadVolume:F2}, Операторы: {method.OperatorCount}, Операнды: {method.OperandCount}\n")
                    {
                        Foreground = Brushes.DarkGreen
                    });

                    // MI
                    Brush miColor = method.MaintainabilityIndex switch
                    {
                        >= 80 => Brushes.Green,
                        >= 60 => Brushes.Orange,
                        _ => Brushes.Red
                    };
                    paragraph.Inlines.Add(new Run($"    Maintainability Index: {method.MaintainabilityIndex:F2}\n")
                    {
                        Foreground = miColor,
                        FontWeight = FontWeights.Bold
                    });

                    // Fan-in/out
                    paragraph.Inlines.Add(new Run($"    Fan-In: {method.FanIn}, Fan-Out: {method.FanOut}\n\n")
                    {
                        Foreground = Brushes.Purple
                    });
                }

                // Разделитель только один раз на группу
                paragraph.Inlines.Add(new Run(new string('-', 27) + "\n")
                {
                    Foreground = Brushes.Gray
                });

                MetricsRichTextBox.Document.Blocks.Add(paragraph);
            }
        }


        void SetInfoText()
        {
            string text = "\t📏 Общие метрики\r\n" +
                "✅ Длина метода\r\nЧто это: Количество строк кода в методе.\r\n" +
                "Зачем нужно: Слишком длинные методы трудно читать и сопровождать. " +
                "Обычно стараются держать методы до 20–30 строк.\r\n\r\n✅ Количество параметров\r\n" +
                "Что это: Сколько параметров принимает метод.\r\nЗачем нужно: Чем больше параметров" +
                " — тем сложнее использовать метод. Более 3–4 параметров могут указывать на" +
                " необходимость рефакторинга (например, объединить в объект).\r\n\r\n✅" +
                " Количество локальных переменных\r\nЧто это: Сколько переменных объявлено" +
                " внутри метода.\r\nЗачем нужно: Много переменных может усложнять понимание логики.\r\n\r\n" +
                "---------------------------\r\n\r\n\t" +
                "🔄 Сложность и структура\r\n✅ Цикломатическая сложность (Cyclomatic Complexity)\r\n" +
                "Что это: Количество независимых путей в методе.\r\nКак считается: Базовое значение 1 +" +
                " количество управляющих конструкций (if, for, while, switch, логические операторы и т. д.).\r\n" +
                "Зачем нужно:\r\n\r\n≤ 10 — допустимо\r\n\r\n10–20 — высокая сложность\r\n\r\n" +
                "20 — потенциальный кандидат на переработку\r\n\r\n✅ NPath Complexity\r\n" +
                "Что это: Количество всех возможных путей исполнения (вложенные if/else, циклы и т. д.).\r\n" +
                "Зачем нужно: Показывает, насколько сложно протестировать все ветви выполнения.\r\n\r\n" +
                "✅ Максимальная глубина вложенности\r\nЧто это: Насколько глубоко вложены блоки кода " +
                "(например, if в if в for).\r\nЗачем нужно: Глубокая вложенность затрудняет понимание кода." +
                " Обычно стараются не превышать 3–4 уровня.\r\n\r\n" +
                "---------------------------\r\n\r\n" +
                "\t\U0001f9ee Метрики Halstead\r\nМетрики Халстеда основаны на анализе операторов и" +
                " операндов в коде.\r\n\r\n✅ Операторы и операнды\r\nОператоры — символы и конструкции," +
                " выполняющие действия (+, -, =, if, return, for, и т. д.)\r\n\r\nОперанды — имена переменных," +
                " литералы, выражения и т. д.\r\n\r\n✅ Halstead Volume (объём)\r\nЧто это: Объем информации," +
                " содержащейся в коде.\r\nКак считается: Volume = (n1 + n2) * log2(n1 + n2)\r\nГде:\r\n\r\nn1" +
                " — количество уникальных операторов\r\n\r\nn2 — количество уникальных операндов\r\n" +
                "Зачем нужно:\r\n\r\nМеньше — проще для понимания\r\n\r\nБольше — более \"нагруженный\"" +
                " по смыслу код\r\n\r\n" +
                "---------------------------\r\n\r\n\t" +
                "📉 Поддерживаемость\r\n✅ Maintainability Index (MI)\r\nЧто это: " +
                "Индекс удобства сопровождения. Комбинирует длину, Halstead Volume и сложность.\r\nФормула:" +
                " MI = 171 - 5.2 * log(HalsteadVolume) - 0.23 * CyclomaticComplexity - 16.2 * log(LinesOfCode)\r\n" +
                "Диапазон значений:\r\n\r\n85 — отличная поддерживаемость\r\n\r\n65–85 — нормальная\r\n\r\n" +
                "< 65 — низкая, стоит упростить\r\n\r\n" +
                "---------------------------\r\n\r\n\t" +
                "🔗 Взаимосвязь методов\r\n✅ Fan-In\r\nЧто это: Сколько других методов вызывает данный метод.\r\n" +
                "Зачем нужно: Чем выше Fan-In, тем выше важность и переиспользуемость метода." +
                " Такие методы следует тестировать особенно тщательно.\r\n\r\n✅ Fan-Out\r\nЧто это: " +
                "Сколько методов вызывает текущий метод.\r\nЗачем нужно: Высокое значение говорит о сильной " +
                "связанности — изменения в других методах могут затронуть этот. Иногда это признак нарушенного" +
                " SRP (Single Responsibility Principle).\r\n\r\n\r\n\r\n\r\n\r\n";

            Paragraph paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run(text));
            MetricsInfoRichTextBox.Document.Blocks.Clear();
            MetricsInfoRichTextBox.Document.Blocks.Add(paragraph);
        }
    }
}
