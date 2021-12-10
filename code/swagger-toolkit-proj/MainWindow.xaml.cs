using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Swagger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Initialization

        internal static readonly string s_appDisplayName;
        private static readonly string s_appVersion;
        private ObjectModel _objectModel;
        private static DragDropEffects _dragDropEffects = DragDropEffects.None;
        private static readonly string _hintSummary = "Path summary";
        private static readonly string _hintDescription = "Path description";
        private static readonly string _hintFile = "Drag and drop a JSON swagger file here";

        public MainWindow()
        {
            InitializeComponent();

            // Defaults.
            ShowInTaskbar = true;
            Title = $"{s_appDisplayName} v{s_appVersion}";
            CmbApiCategory.Items.Add("Select an API category");
            CmbApiCategory.SelectedIndex = 0;
            CmbApiOperation.Items.Add("Select an API operation");
            CmbApiOperation.SelectedIndex = 0;
            TxtSummary.Text = _hintSummary;
            TxtSummary.Foreground = Brushes.Gray;
            TxtDescription.Text = _hintDescription;
            TxtDescription.Foreground = Brushes.Gray;

            // Events.
            Loaded += MainWindow_Loaded;
            CmbApiCategory.SelectionChanged += CmbApiCategory_SelectionChanged;
            CmbApiOperation.SelectionChanged += CmbApiOperation_SelectionChanged;
        }

        static MainWindow()
        {
            s_appDisplayName = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;
            s_appVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Check for a previous swagger file path.
            string filePath = Properties.Settings.Default.SwaggerFilePath;
            if (!File.Exists(filePath))
            {
                TxtFile.Text = _hintFile;
                TxtFile.Foreground = Brushes.Gray;
                return;
            }

            (bool isValidJson, string jsonContent, string err) = await ValidateJsonFileAsync(filePath);

            if (!isValidJson || jsonContent == null)
            {
                TxtFile.Text = err;
                return;
            }

            (bool isValidSwagger, err) = await ValidateSwaggerContentAsync(jsonContent);
            
            if (!isValidSwagger)
            {
                TxtFile.Text = err;
                return;
            }

            // Store file path.
            _objectModel.FilePath = filePath;

            RunAnimation();
        }

        #endregion

        #region Drag-drop file

        private void TxtFile_DragEnter(object sender, DragEventArgs e)
        {
            TxtFile.Clear();
            TxtSummary.Clear();
            TxtDescription.Clear();
            CmbApiCategory.Items.Clear();
            CmbApiCategory.Items.Add("Select an API category");
            CmbApiOperation.Items.Clear();
            CmbApiOperation.Items.Add("Select an API operation");

            // Validate drop file.
            (bool isValidDrop, string filePath, string err) = ValidateDropFileAsync(e);
            if (isValidDrop)
            {
                TxtFile.Text = $"{filePath}";
                TxtFile.Foreground = Brushes.Black;
                TxtFile.Background = Brushes.LightGreen;
                _dragDropEffects = DragDropEffects.Copy;
            }
            else
            {
                TxtFile.Text = $"{err}";
                TxtFile.Foreground = Brushes.Black;
                TxtFile.Background = Brushes.Tomato;
                _dragDropEffects = DragDropEffects.None;
            }
        }

        private void TxtFile_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = _dragDropEffects;
            e.Handled = true;
        }

        private void TxtFile_DragLeave(object sender, EventArgs e)
        {
            TxtFile.Text = _hintFile;
            TxtFile.Foreground = Brushes.Gray;
            TxtFile.Background = Brushes.White;
        }

        private async void TxtFile_Drop(object sender, DragEventArgs e)
        {
            TxtFile.Background = Brushes.White;
            CmbApiCategory.SelectedIndex = -1;
            CmbApiOperation.SelectedIndex = -1;

            // Validate drop file.
            (bool isValidDrop, string filePath, string err) = ValidateDropFileAsync(e);
            if (!isValidDrop || filePath == null)
            {
                TxtFile.Text = err;
                return;
            }

            (bool isValidJson, string jsonContent, err) = await ValidateJsonFileAsync(filePath);

            if (!isValidJson || jsonContent == null)
            {
                TxtFile.Text = err;
                return;
            }

            (bool isValidSwagger, err) = await ValidateSwaggerContentAsync(jsonContent);

            if (!isValidSwagger)
            {
                TxtFile.Text = err;
                return;
            }

            // Store file path.
            _objectModel.FilePath = filePath;

            // Save swagger file path.
            Properties.Settings.Default.SwaggerFilePath = filePath;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region Validation

        private static (bool isValidDrop, string filePath, string err) ValidateDropFileAsync(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return (false, null, "Invalid item.");
            }

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length != 1)
            {
                return (false, null, "Multiple items detected.");
            }

            string extension = Path.GetExtension(files[0]);
            if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                return (false, null, "Incorrect file type.");
            }

            if (!File.Exists(files[0]))
            {
                return (false, null, "Invalid path.");
            }

            return (true, files[0], null);
        }

        private async Task<(bool isValidJson, string content, string err)> ValidateJsonFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return (false, null, "Invalid path.");
            }

            string extension = Path.GetExtension(filePath);
            if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                return (false, null, "Incorrect file type.");
            }

            string content;
            try
            {
                content = await File.ReadAllTextAsync(filePath);
                JsonNode jsonNode = JsonNode.Parse(content);
                if (jsonNode == null)
                {
                    return (false, null, "Invalid JSON.");
                }
            }
            catch (Exception ex)
            {
                return (false, null, $"JSON parser error:\r\n{ex.Message}");
            }

            TxtFile.Text = filePath;

            return (true, content, null);
        }

        private async Task<(bool isValidSwagger, string err)> ValidateSwaggerContentAsync(string jsonContent)
        {
            _objectModel = null;
            string err = null;

            // Run I/O task asynchronously.
            await Task.Run(() =>
            {
                try
                {
                    // Parse JSON:
                    JsonNode rootJsonNode = JsonNode.Parse(jsonContent);
                    if (rootJsonNode == null)
                    {
                        err = "Invalid JSON.";
                        return;
                    }
                    // Parse swagger:
                    _objectModel = new(rootJsonNode);
                    if (_objectModel == null)
                    {
                        err = "Invalid JSON.";
                        return;
                    }
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }
            });

            if (_objectModel == null || err.IsNotEmpty())
            {
                return (false, err);
            }

            // Populate API categories.
            List<string> ApiCategories = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Select(y => y.ApiCategory).Distinct().OrderBy(x => x).ToList();
            ApiCategories.ForEach(x => CmbApiCategory.Items.Add(x));
            CmbApiCategory.SelectedIndex = 0;
            CmbApiOperation.SelectedIndex = 0;

            return (true, null);
        }

        #endregion

        #region Navigate API

        private void CmbApiCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbApiCategory.SelectedItem == null || CmbApiCategory.SelectedIndex == 0) return;
            if (_objectModel == null) return;

            // Lock control.
            CmbApiCategory.IsEditable = false;

            // Populate API names.
            string apiCategory = CmbApiCategory.SelectedItem.ToString();
            List<string> apiNames = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiCategory == apiCategory)?.Select(y => y.ApiName)?.Distinct()?.OrderBy(x => x)?.ToList();
            
            CmbApiOperation.SelectionChanged -= CmbApiOperation_SelectionChanged;
            CmbApiOperation.Items.Clear();
            CmbApiOperation.Items.Add("Select an API operation");
            apiNames.ForEach(x => CmbApiOperation.Items.Add(x));
            CmbApiOperation.SelectedIndex = 0;
            CmbApiOperation.SelectionChanged += CmbApiOperation_SelectionChanged;

            TxtSummary.Text = _hintSummary;
            TxtSummary.Foreground = Brushes.Gray;
            TxtDescription.Text = _hintDescription;
            TxtDescription.Foreground = Brushes.Gray;
        }

        private void CmbApiOperation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbApiCategory.SelectedItem == null || CmbApiCategory.SelectedIndex == 0) return;
            if (CmbApiOperation.SelectedItem == null || CmbApiOperation.SelectedIndex == 0) return;
            if (_objectModel == null) return;

            // Lock control.
            CmbApiOperation.IsEditable = false;

            string apiCategory = CmbApiCategory.SelectedItem.ToString();
            string apiName = CmbApiOperation.SelectedItem.ToString();

            // Populate summary.
            string summary = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiCategory == apiCategory && x.ApiName == apiName).Select(y => y.Summary).SingleOrDefault();
            if (summary == null) throw new Exception("Summary not found");
            TxtSummary.Text = RawSummaryToUi(summary);
            TxtSummary.Foreground = Brushes.Black;

            // Populate description.
            string description = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiCategory == apiCategory && x.ApiName == apiName).Select(y => y.Description).SingleOrDefault();
            if (description == null) throw new Exception("Description not found");
            TxtDescription.Text = RawDescriptionToUi(description);
            TxtDescription.Foreground = Brushes.Black;
        }

        #endregion

        #region Discard page edits

        private void BtnDiscardPageEdits_Click(object sender, RoutedEventArgs e)
        {
            if (CmbApiCategory.SelectedItem == null || CmbApiCategory.SelectedIndex == 0) return;
            if (CmbApiOperation.SelectedItem == null || CmbApiOperation.SelectedIndex == 0) return;
            if (_objectModel == null) return;

            string apiCategory = CmbApiCategory.SelectedItem.ToString();
            string apiName = CmbApiOperation.SelectedItem.ToString();

            // Get object model summary and description.
            string summary = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiCategory == apiCategory && x.ApiName == apiName).Select(y => y.Summary).SingleOrDefault();
            string description = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiCategory == apiCategory && x.ApiName == apiName).Select(y => y.Description).SingleOrDefault();

            // Check whether any changes have been made.
            if (string.Equals(TxtSummary.Text, RawSummaryToUi(summary)) && string.Equals(TxtDescription.Text, RawDescriptionToUi(description)))
            {
                MessageBox.Show("No changes found on page.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Prompt user.
            MessageBoxResult dr = MessageBox.Show("Discard changes to this page?", s_appDisplayName, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (dr != MessageBoxResult.OK)
            {
                return;
            }

            // Reset page.
            CmbApiOperation_SelectionChanged(null, null);
        }

        #endregion

        #region Save page

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (CmbApiCategory.SelectedItem == null || CmbApiCategory.SelectedIndex == 0) return;
            if (CmbApiOperation.SelectedItem == null || CmbApiOperation.SelectedIndex == 0) return;
            if (_objectModel == null) return;

            string apiCategory = CmbApiCategory.SelectedItem.ToString();
            string apiName = CmbApiOperation.SelectedItem.ToString();

            // Get object model summary and description.
            string summary = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiCategory == apiCategory && x.ApiName == apiName).Select(y => y.Summary).SingleOrDefault();
            string description = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiCategory == apiCategory && x.ApiName == apiName).Select(y => y.Description).SingleOrDefault();

            // Check whether any changes have been made.
            //if (string.Equals(UiSummaryToRaw(TxtSummary.Text), summary.Replace("\n", @"\n")) && string.Equals(UiDescriptionToRaw(TxtDescription.Text), description.Replace("\n", @"\n")))
            if (string.Equals(UiSummaryToRaw(TxtSummary.Text), summary) && string.Equals(UiDescriptionToRaw(TxtDescription.Text), description))
            {
                MessageBox.Show("No changes found on page.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Prompt user.
            MessageBoxResult dr = MessageBox.Show("Save your changes to this page?", s_appDisplayName, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (dr != MessageBoxResult.OK)
            {
                return;
            }

            // Save summary and description to object model.
            HttpMethod httpMethod = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).SingleOrDefault(x => x.ApiCategory == apiCategory && x.ApiName == apiName);
            httpMethod.Summary = UiSummaryToRaw(TxtSummary.Text);
            httpMethod.Description = UiDescriptionToRaw(TxtDescription.Text);

            // Write to disc.
            await _objectModel.SaveAsync();
        }

        #endregion

        #region Copy JSON to clipboard

        private void BtnSummary_Click(object sender, RoutedEventArgs e)
        {
            string summary = UiSummaryToRaw(TxtSummary.Text).Replace("\n", @"\n"); ;
            Clipboard.SetText(summary);

            MessageBox.Show("JSON summary copied to clipboard.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDescription_Click(object sender, RoutedEventArgs e)
        {
            string description = UiDescriptionToRaw(TxtDescription.Text).Replace("\r", "").Replace("\n", @"\n");
            Clipboard.SetText(description);

            MessageBox.Show("JSON description copied to clipboard.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Text transforms

        private static string RawSummaryToUi(string rawSummary)
        {
            return rawSummary.TrimStart(new[] { ' ', '\r', '\n' }).TrimEnd(new[] { ' ', '\r', '\n', '#' });
        }

        private static string UiSummaryToRaw(string uiSummary)
        {
            return uiSummary.Trim(new[] { ' ', '\r', '\n' }).Replace("\n", "");
        }

        private static string RawDescriptionToUi(string rawDescription)
        {
            return rawDescription.TrimStart(new[] { ' ', '\r', '\n' }).TrimEnd(new[] { ' ', '\r', '\n', '#' });
        }

        private static string UiDescriptionToRaw(string uiDescription)
        {
            //return @"\n" + uiDescription.Trim(new[] { ' ', '\r', '\n' }).Replace("\n", @"\n") + @"\n\n######\n";
            return "\n" + uiDescription.Trim(new[] { ' ', '\r', '\n' }) + "\n\n######\n";
        }

        #endregion

        #region Textbox hints

        private void TxtSummary_GotFocus(object sender, RoutedEventArgs e)
        {
            TxtSummary.Foreground = Brushes.Black;

            if (string.Equals(TxtSummary.Text, _hintSummary))
            {
                TxtSummary.Text = "";
            }
        }

        private void TxtSummary_LostFocus(object sender, RoutedEventArgs e)
        {
            TxtSummary.Foreground = TxtSummary.Text == "" ? Brushes.Gray : Brushes.Black;

            if (TxtSummary.Text.IsEmpty())
            {
                TxtSummary.Text = _hintSummary;
            }
        }

        private void TxtDescription_GotFocus(object sender, RoutedEventArgs e)
        {
            TxtDescription.Foreground = Brushes.Black;

            if (string.Equals(TxtDescription.Text, _hintDescription))
            {
                TxtDescription.Text = "";
            }
        }

        private void TxtDescription_LostFocus(object sender, RoutedEventArgs e)
        {
            TxtDescription.Foreground = TxtDescription.Text == "" ? Brushes.Gray : Brushes.Black;

            if (TxtDescription.Text.IsEmpty())
            {
                TxtDescription.Text = _hintDescription;
            }
        }

        private void TxtFile_GotFocus(object sender, RoutedEventArgs e)
        {
            TxtFile.Foreground = Brushes.Black;

            if (string.Equals(TxtFile.Text, _hintFile))
            {
                TxtFile.Text = "";
            }
        }

        private void TxtFile_LostFocus(object sender, RoutedEventArgs e)
        {
            TxtFile.Foreground = TxtFile.Text == "" ? Brushes.Gray : Brushes.Black;

            if (TxtFile.Text.IsEmpty())
            {
                TxtFile.Text = _hintFile;
            }
        }

        #endregion

        #region Animation

        private void RunAnimation()
        {
            SolidColorBrush brush = FindName("AnimBrush") as SolidColorBrush;
            ColorAnimation animation = new();
            animation.From = Colors.LightGreen;
            animation.To = Colors.White;
            animation.FillBehavior = FillBehavior.Stop;
            animation.AutoReverse = false;
            animation.Duration = TimeSpan.FromSeconds(1);
            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        #endregion
    }
}
