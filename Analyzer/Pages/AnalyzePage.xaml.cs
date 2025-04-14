using Analyzer.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// Логика взаимодействия для AnalyzePage.xaml
    /// </summary>
    public partial class AnalyzePage : Page
    {
        private string _solutionPath;

        public AnalyzePage(string solutionPath)
        {
            InitializeComponent();
            _solutionPath = solutionPath;
            StructureButton.IsChecked = true;
            MenuNavigate();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            MenuNavigate();
        }
        void MenuNavigate()
        {
            if (StructureButton.IsChecked == true)
            {
                ContentFrame.Navigate(new StructurePage(_solutionPath));
            }
            else if (MethodsButton.IsChecked == true)
            {
                //ContentFrame.Navigate(new MethodsPage(_solutionPath));
            }
            else if (InfographButton.IsChecked == true)
            {
                //ContentFrame.Navigate(new InfographPage(_solutionPath));
            }
            else if (PatternsButton.IsChecked == true)
            {
                //ContentFrame.Navigate(new PatternsPage(_solutionPath));
            }
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new OpenPage());
        }
    }
}
