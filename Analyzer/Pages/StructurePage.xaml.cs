using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using Analyzer.src;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Analyzer.Pages
{
    public partial class StructurePage : Page
    {
        private readonly SolutionLoader _viewModel;
        private const string DefaultHighlightingResource = "Analyzer.Resources.CSharp-Dark.xshd";

        public StructurePage(SolutionLoader loader)
        {
            InitializeComponent();
            _viewModel = loader;
            DataContext = _viewModel;

            CodeEditor.SyntaxHighlighting = LoadHighlightingDefinition();
        }

        private static IHighlightingDefinition LoadHighlightingDefinition()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(DefaultHighlightingResource);
                if (stream == null) throw new FileNotFoundException(DefaultHighlightingResource);

                using var reader = new XmlTextReader(stream);
                return HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load custom highlighting: {ex.Message}");
                return HighlightingManager.Instance.GetDefinition("C#");
            }
        }

        // В конструкторе:
        

        private async Task LoadSolutionAsync()
        {
            try
            {
                await _viewModel.LoadSolution();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Failed to load solution: {ex.Message}");
            }
        }

        private async void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not TreeViewNode { IsFile: true } selectedNode)
                return;

            await LoadFileContentAsync(selectedNode);
        }

        private async Task LoadFileContentAsync(TreeViewNode node)
        {
            CodeEditor.Text = "Loading...";

            try
            {
                var fileContent = await Task.Run(() =>
                    File.Exists(node.FullPath) ? File.ReadAllText(node.FullPath) : null);

                CodeEditor.Text = fileContent ?? $"// File not found: {node.FullPath}";
            }
            catch (Exception ex)
            {
                CodeEditor.Text = $"// Error loading file:\n// {ex.Message}";
            }
        }

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}