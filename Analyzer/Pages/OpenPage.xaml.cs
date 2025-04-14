using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        private void Search_Click(object sender, RoutedEventArgs e)
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

            // Переход на другую страницу с переданным путём
            var nextPage = new AnalyzePage(filePath);
            NavigationService?.Navigate(nextPage);
        }

        private string CleanPath(string input)
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
