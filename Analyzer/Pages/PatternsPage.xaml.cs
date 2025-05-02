using Analyzer.src;
using Microsoft.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Analyzer.Pages
{
    public partial class PatternsPage : Page
    {
        public PatternsPage(SolutionLoader loader)
        {
            InitializeComponent();
            PrintResults(loader.Result);
        }

        void PrintResults(AnalysisResult result)
        {
            // Получаем документ
            FlowDocument flowDoc = new FlowDocument();
            PatternsRichTextBox.Document = flowDoc;

            // Создаем параграф
            Paragraph paragraph = new Paragraph();
            flowDoc.Blocks.Add(paragraph);

            // Новые контрастные цвета
            var projectColor = Brushes.DodgerBlue; 
            var patternColor = Brushes.Red;       
            var detailColor = Brushes.Black;  
            var separatorColor = Brushes.DimGray;  
            var highlightColor = Brushes.Green;

            // Настройки шрифта для лучшей читаемости
            double mainFontSize = 14;
            double headerFontSize = 16;

            foreach (var project in result.ProjectPatterns)
            {
                // Вывод разделителя проекта
                paragraph.Inlines.Add(new Run("\n" + new string('=', 50) + "\n")
                {
                    Foreground = separatorColor,
                    FontWeight = FontWeights.Bold,
                    FontSize = mainFontSize
                });

                // Вывод названия проекта
                paragraph.Inlines.Add(new Run($"=== Проект: {project.Key} ===\n")
                {
                    Foreground = projectColor,
                    FontWeight = FontWeights.Bold,
                    FontSize = headerFontSize
                });

                paragraph.Inlines.Add(new Run(new string('=', 50) + "\n")
                {
                    Foreground = separatorColor,
                    FontWeight = FontWeights.Bold,
                    FontSize = mainFontSize
                });

                foreach (var pattern in project.Value)
                {
                    // Вывод названия паттерна
                    paragraph.Inlines.Add(new Run($"\n{pattern.Name}:\n")
                    {
                        Foreground = patternColor,
                        FontWeight = FontWeights.Bold,
                        FontSize = mainFontSize,
                    });

                    // Вывод деталей паттерна
                    foreach (var detail in pattern.Details)
                    {
                        paragraph.Inlines.Add(new Run($"   {detail.Key}: ")
                        {
                            Foreground = highlightColor,
                            FontWeight = FontWeights.Bold,
                            FontSize = mainFontSize
                        });

                        paragraph.Inlines.Add(new Run($"{detail.Value}\n")
                        {
                            Foreground = detailColor,
                            FontWeight = FontWeights.Normal,
                            FontSize = mainFontSize
                        });
                    }
                }
            }
        }
    }
}