using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Analyzer
{
    /// <summary>
    /// Логика взаимодействия для OpenPage.xaml
    /// </summary>
    public partial class OpenPage : Page
    {
        public OpenPage()
        {
            InitializeComponent();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            string filePath = CleanPath(Path.Text);

            // Если путь пустой или файл не существует — открываем проводник
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Solution or Project (*.sln;*.csproj)|*.sln;*.csproj",
                    Title = "Выберите файл решения или проекта"
                };

                if (dialog.ShowDialog() == true)
                {
                    filePath = dialog.FileName;
                    Path.Text = filePath;
                }
                else
                {
                    return; // Отменили выбор
                }
            }

            LoadingBar.Visibility = Visibility.Visible;
            LoadingInfo.Visibility = Visibility.Visible;
            Search.IsEnabled = false;

            try
            {
                var loader = new Analyzer.src.SolutionLoader(filePath);

                LoadingInfo.Content = "Анализ структуры решения...";
                await loader.LoadSolution();

                LoadingInfo.Content = "Анализ метрик...";
                await loader.AnalyzeMetrics();

                LoadingInfo.Content = "Поиск паттернов...";
                await loader.AnalyzePatternsAsync();

                LoadingInfo.Content = "";

                // Переход в AnalyzePage с уже загруженным SolutionLoader
                var analyzePage = new AnalyzePage(loader);
                NavigationService?.Navigate(analyzePage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}");
            }
            finally
            {
                LoadingBar.Visibility = Visibility.Collapsed;
                LoadingInfo.Visibility = Visibility.Collapsed;
                Search.IsEnabled = true;
            }
        }

        private static string CleanPath(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var trimmed = input.Trim();

            // Убираем кавычки в начале/конце
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                trimmed = trimmed.Substring(1, trimmed.Length - 2);

            // Заменяем слэши
            trimmed = trimmed.Replace('/', '\\');

            return trimmed;
        }

    }
}
