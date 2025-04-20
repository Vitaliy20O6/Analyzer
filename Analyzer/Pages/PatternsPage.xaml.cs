using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Analyzer.src;

namespace Analyzer.Pages
{
    public partial class PatternsPage : Page
    {
        private readonly SolutionLoader _loader;
        public List<DesignPattern> DetectedPatterns { get; private set; }
        private DesignPattern _selectedPattern;
        public DesignPattern SelectedPattern
        {
            get => _selectedPattern;
            set
            {
                if (_selectedPattern != value)
                {
                    _selectedPattern = value;
                    OnPropertyChanged(nameof(SelectedPattern));

                    // Обновляем диаграмму при изменении выбора
                    DiagramCanvas.Children.Clear();
                    if (_selectedPattern != null)
                    {
                        DrawPatternDiagram(_selectedPattern);
                    }
                }
            }
        }

        public PatternsPage(SolutionLoader loader)
        {
            InitializeComponent();
            _loader = loader;
            DataContext = this; // Устанавливаем DataContext для привязок

            LoadPatternsAsync();
        }

        private async void LoadPatternsAsync()
        {
            try
            {
                // Показываем индикатор загрузки
                LoadingIndicator.Visibility = Visibility.Visible;
                PatternListView.Visibility = Visibility.Collapsed;

                // Загружаем паттерны асинхронно
                await _loader.AnalyzePatternsAsync();
                DetectedPatterns = _loader.DetectedPatterns;

                // Обновляем привязку данных
                PatternListView.ItemsSource = DetectedPatterns;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при анализе паттернов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Скрываем индикатор загрузки
                LoadingIndicator.Visibility = Visibility.Collapsed;
                PatternListView.Visibility = Visibility.Visible;
            }
        }

        private void PatternListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PatternListView.SelectedItem is DesignPattern selectedPattern)
            {
                SelectedPattern = selectedPattern;
                OnPropertyChanged(nameof(SelectedPattern)); // Уведомляем об изменении

                // Очищаем область визуализации
                DiagramCanvas.Children.Clear();

                // Рисуем схему паттерна
                DrawPatternDiagram(selectedPattern);
            }
        }

        private void DrawPatternDiagram(DesignPattern pattern)
        {
            const int startX = 50;
            const int startY = 50;
            const int stepX = 200;
            const int stepY = 100;

            // Рисуем классы
            for (int i = 0; i < pattern.Classes.Count; i++)
            {
                var cls = pattern.Classes[i];
                int x = startX + (i % 2) * stepX;
                int y = startY + (i / 2) * stepY;

                DrawClass(cls, x, y);
            }

            // Рисуем связи
            foreach (var relation in pattern.Relations)
            {
                var fromClass = pattern.Classes.FirstOrDefault(c => c.Name == relation.FromClass);
                var toClass = pattern.Classes.FirstOrDefault(c => c.Name == relation.ToClass);

                if (fromClass != null && toClass != null)
                {
                    DrawRelation(relation, fromClass, toClass);
                }
            }
        }

        private void DrawClass(PatternClass cls, int x, int y)
        {
            // Создаем прямоугольник для класса
            var rect = new Rectangle
            {
                Width = 150,
                Height = 60,
                Fill = Brushes.LightBlue,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Tag = cls // Сохраняем ссылку на класс
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            DiagramCanvas.Children.Add(rect);

            // Добавляем текст с именем класса
            var text = new TextBlock
            {
                Text = cls.Name,
                TextWrapping = TextWrapping.Wrap,
                Width = 140,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Bold
            };

            Canvas.SetLeft(text, x + 5);
            Canvas.SetTop(text, y + 20);
            DiagramCanvas.Children.Add(text);
        }

        private void DrawRelation(PatternRelation relation, PatternClass from, PatternClass to)
        {
            // Находим элементы на canvas по тегам
            var fromRect = DiagramCanvas.Children
                .OfType<Rectangle>()
                .FirstOrDefault(r => r.Tag == from);

            var toRect = DiagramCanvas.Children
                .OfType<Rectangle>()
                .FirstOrDefault(r => r.Tag == to);

            if (fromRect == null || toRect == null) return;

            // Вычисляем координаты
            double fromX = Canvas.GetLeft(fromRect) + fromRect.Width / 2;
            double fromY = Canvas.GetTop(fromRect) + fromRect.Height;
            double toX = Canvas.GetLeft(toRect) + toRect.Width / 2;
            double toY = Canvas.GetTop(toRect);

            // Создаем линию
            var line = new Line
            {
                X1 = fromX,
                Y1 = fromY,
                X2 = toX,
                Y2 = toY,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            // Добавляем стрелку для некоторых типов связей
            if (relation.Type == "Inheritance" || relation.Type == "Creation")
            {
                line.StrokeThickness = 2;

                // Треугольник для стрелки
                var arrow = new Polygon
                {
                    Points = new PointCollection(new[] { new Point(-5, -10), new Point(5, -10), new Point(0, 0) }),
                    Fill = Brushes.Black
                };

                Canvas.SetLeft(arrow, toX);
                Canvas.SetTop(arrow, toY);
                DiagramCanvas.Children.Add(arrow);
            }

            DiagramCanvas.Children.Add(line);
        }

        // Реализация INotifyPropertyChanged для привязки данных
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}