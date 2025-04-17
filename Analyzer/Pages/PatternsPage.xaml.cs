using Analyzer.src;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Analyzer.Pages
{
    /// <summary>
    /// Логика взаимодействия для PatternsPage.xaml
    /// </summary>
    public partial class PatternsPage : Page
    {
        private readonly SolutionLoader _loader;

        public PatternsPage(SolutionLoader loader)
        {
            InitializeComponent();
            _loader = loader;

            PatternListView.ItemsSource = _loader.DetectedPatterns;
        }

        private void PatternListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Пока оставим пустым, добавим позже визуализацию схемы
            if (PatternListView.SelectedItem is DesignPattern selectedPattern)
            {
                // Здесь будем рисовать схему паттерна
            }
        }
    }
}
