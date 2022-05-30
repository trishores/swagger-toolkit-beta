using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
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
        private string _previousApiTag;
        private string _previousApiOperation;
        private static readonly string _cmbApiTagText = "Select an API tag";
        private static readonly string _cmbApiOperationText = "Select an API operation";
        private static readonly string _hintSummary = "Path summary";
        private static readonly string _hintDescription = "Path description";
        private static readonly string _hintFile = "Drag and drop a JSON swagger file here";
        private string SummaryText => string.Equals(TxtSummary.Text, _hintSummary) ? "" : TxtSummary.Text;
        private string DescriptionText => string.Equals(TxtDescription.Text, _hintDescription) ? "" : TxtDescription.Text;

        public MainWindow()
        {
            InitializeComponent();

            // Defaults.
            ShowInTaskbar = true;
            Title = $"{s_appDisplayName} v{s_appVersion}";
            CmbApiTags.Items.Add(_cmbApiTagText);
            CmbApiTags.SelectedIndex = 0;
            CmbApiOperations.Items.Add(_cmbApiOperationText);
            CmbApiOperations.SelectedIndex = 0;
            TxtSummary.Text = _hintSummary;
            TxtDescription.Text = _hintDescription;
            TxtSummary.Foreground = Brushes.Gray;
            TxtDescription.Foreground = Brushes.Gray;

            // Events.
            Loaded += MainWindow_Loaded;
            CmbApiTags.SelectionChanged += CmbApiTags_SelectionChanged;
            CmbApiOperations.SelectionChanged += CmbApiOperations_SelectionChanged;
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
            CmbApiTags.Items.Clear();
            CmbApiTags.Items.Add(_cmbApiTagText);
            CmbApiOperations.Items.Clear();
            CmbApiOperations.Items.Add(_cmbApiOperationText);
            // Wipe swagger file path.
            Properties.Settings.Default.SwaggerFilePath = "";
            Properties.Settings.Default.Save();

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
            CmbApiTags.SelectedIndex = -1;
            CmbApiOperations.SelectedIndex = -1;

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
            List<string> ApiCategories = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).SelectMany(y => y.ApiTag).Distinct().OrderBy(x => x).ToList();
            ApiCategories.ForEach(x => CmbApiTags.Items.Add(x));
            CmbApiTags.SelectedIndex = 0;
            CmbApiOperations.SelectedIndex = 0;

            return (true, null);
        }

        #endregion

        #region Navigate API

        private void CmbApiTags_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_objectModel == null ||
                CmbApiTags.SelectedItem == null ||
                CmbApiTags.SelectedIndex == 0)
            {
                CmbApiOperations.SelectedIndex = 0;
                TxtSummary.Clear();
                TxtDescription.Clear();
                return;
            }

            // Unless undoing page edits, check whether to save previous summary & description:
            if (sender != null) CheckWhetherToSavePreviousPage();

            // Lock control.
            CmbApiTags.IsEditable = false;

            // Populate API names.
            string apiTag = CmbApiTags.SelectedItem.ToString();
            string[] apiOperations = GetApiOperationsForTag(apiTag);

            CmbApiOperations.SelectionChanged -= CmbApiOperations_SelectionChanged;
            CmbApiOperations.Items.Clear();
            CmbApiOperations.Items.Add(_cmbApiOperationText);
            apiOperations.ToList().ForEach(x => CmbApiOperations.Items.Add(x));
            CmbApiOperations.SelectedIndex = 0;
            CmbApiOperations.SelectionChanged += CmbApiOperations_SelectionChanged;

            TxtSummary.Text = _hintSummary;
            TxtSummary.Foreground = Brushes.Gray;
            TxtDescription.Text = _hintDescription;
            TxtDescription.Foreground = Brushes.Gray;
        }

        private void CmbApiOperations_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_objectModel == null ||
                CmbApiTags.SelectedItem == null ||
                CmbApiTags.SelectedIndex == 0 ||
                CmbApiOperations.SelectedItem == null ||
                CmbApiOperations.SelectedIndex == 0)
            {
                TxtSummary.Clear();
                TxtDescription.Clear();
                return;
            }

            // Unless undoing page edits, check whether to save previous summary & description:
            if (sender != null) CheckWhetherToSavePreviousPage();

            // Lock control.
            CmbApiOperations.IsEditable = false;

            string apiTag = CmbApiTags.SelectedItem.ToString();
            string apiOperation = CmbApiOperations.SelectedItem.ToString();
            _previousApiTag = apiTag;
            _previousApiOperation = apiOperation;

            // Populate summary.
            string summary = GetSummary(apiTag, apiOperation);
            TxtSummary.Text = RawSummaryToUi(summary ?? "");
            TxtSummary.Foreground = Brushes.Black;

            // Populate description.
            string description = GetDescription(apiTag, apiOperation);
            TxtDescription.Text = RawDescriptionToUi(description ?? "");
            TxtDescription.Foreground = Brushes.Black;
        }

        private string[] GetApiOperationsForTag(string apiTag)
        {
            string[] apiOperations = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiTag.Any(y => string.Equals(y, apiTag) && x.ApiOperation.IsNotEmpty()))?.Select(y => y.ApiOperation)?.Distinct()?.OrderBy(x => x)?.ToArray();
            return apiOperations;
        }

        private string GetSummary(string apiTag, string apiOperation)
        {
            string summary = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiTag.Any(y => string.Equals(y, apiTag)) && x.ApiOperation == apiOperation).Select(y => y.Summary).SingleOrDefault();
            return summary;
        }

        private string GetDescription(string apiTag, string apiOperation)
        {
            string description = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).Where(x => x.ApiTag.Any(y => string.Equals(y, apiTag)) && string.Equals(x.ApiOperation, apiOperation)).Select(y => y.Description).SingleOrDefault();
            return description;
        }

        #endregion

        #region Insert snippet

        private void BtnInsertAlert_Click(object sender, RoutedEventArgs e)
        {
            // Insert an alert construct at cursor position in description field.
            string alertName = ((Button)sender).Name.Replace("Btn", "").ToUpper();
            string alertText = $"> [!{alertName}]\n> {alertName.ToLower()} text here";
            int caretIndex = TxtDescription.CaretIndex;

            if (DescriptionText.IsEmpty())
            {
                TxtDescription.Text = alertText;
                TxtDescription.Foreground = Brushes.Black;
            }
            else
            {
                TxtDescription.Text = TxtDescription.Text.Insert(TxtDescription.CaretIndex, alertText);
            }

            try { TxtDescription.CaretIndex = caretIndex + alertText.Length; }
            catch { }

            TxtDescription.Focus();
        }

        private void BtnInsertTable_Click(object sender, RoutedEventArgs e)
        {
            // Insert a table at cursor position in description field.
            string tableText = $"| Header A | Header B |\n|-|-|\n| A1 | B1 |\n| A2 | B2 |";
            int caretIndex = TxtDescription.CaretIndex;

            if (DescriptionText.IsEmpty())
            {
                TxtDescription.Text = tableText;
                TxtDescription.Foreground = Brushes.Black;
            }
            else
            {
                TxtDescription.Text = TxtDescription.Text.Insert(TxtDescription.CaretIndex, tableText);
            }

            try { TxtDescription.CaretIndex = caretIndex + tableText.Length; }
            catch { }

            TxtDescription.Focus();
        }

        #endregion

        #region Undo page edits

        private void BtnDiscardPageEdits_Click(object sender, RoutedEventArgs e)
        {
            if (CmbApiTags.SelectedItem == null || CmbApiTags.SelectedIndex == 0) return;
            if (CmbApiOperations.SelectedItem == null || CmbApiOperations.SelectedIndex == 0) return;
            if (_objectModel == null) return;

            string apiTag = CmbApiTags.SelectedItem.ToString();
            string apiOperation = CmbApiOperations.SelectedItem.ToString();

            // Get object model summary and description.
            string summary = GetSummary(apiTag, apiOperation);
            string description = GetDescription(apiTag, apiOperation);

            // Check whether any changes have been made.
            if (string.Equals(SummaryText, RawSummaryToUi(summary)) && string.Equals(DescriptionText, RawDescriptionToUi(description)))
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
            CmbApiOperations_SelectionChanged(null, null);
        }

        #endregion

        #region Check whether to save previous page

        private async void CheckWhetherToSavePreviousPage()
        {
            if (_previousApiTag.IsEmpty() || _previousApiOperation.IsEmpty()) return;

            try
            {
                // Get object model summary and description.
                string prevSummary = GetSummary(_previousApiTag, _previousApiOperation);
                string prevDescription = GetDescription(_previousApiTag, _previousApiOperation)?.Replace("\r", "");

                // Check whether any changes have been made.
                if (string.Equals(UiSummaryToRaw(SummaryText), prevSummary) && string.Equals(UiDescriptionToRaw(DescriptionText), prevDescription))
                {
                    return;
                }
                else
                {
                    MessageBoxResult dr = MessageBox.Show("Save your changes to this page?", s_appDisplayName, MessageBoxButton.OKCancel, MessageBoxImage.Question);
                    if (dr != MessageBoxResult.OK)
                    {
                        return;
                    }
                }

                // Save summary and description to object model.
                HttpMethod httpMethod = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).SingleOrDefault(x => x.ApiTag.Any(y => string.Equals(y, _previousApiTag)) && x.ApiOperation == _previousApiOperation);
                httpMethod.Summary = UiSummaryToRaw(SummaryText);
                httpMethod.Description = UiDescriptionToRaw(DescriptionText).Replace("\r", "");

                // Write to disc.
                await _objectModel.SaveAsync();
            }
            finally
            {
                _previousApiTag = null;
                _previousApiOperation = null;
            }
        }

        #endregion

        #region Save page

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (CmbApiTags.SelectedItem == null || CmbApiTags.SelectedIndex == 0 ||
                CmbApiOperations.SelectedItem == null || CmbApiOperations.SelectedIndex == 0 ||
                _objectModel == null)
            {
                MessageBox.Show("Nothing to save.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string apiTag = CmbApiTags.SelectedItem.ToString();
            string apiOperation = CmbApiOperations.SelectedItem.ToString();

            // Get object model summary and description.
            string summary = GetSummary(apiTag, apiOperation);
            string description = GetDescription(apiTag, apiOperation).Replace("\r", "");

            // Check whether any changes have been made.
            if (string.Equals(UiSummaryToRaw(SummaryText), summary) && string.Equals(UiDescriptionToRaw(DescriptionText), description))
            {
                MessageBoxResult dr = MessageBox.Show("No changes found on page, but saving will fix any inconsistent formatting in swagger.json. Save?", s_appDisplayName, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (dr != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            // Save summary and description to object model.
            HttpMethod httpMethod = _objectModel.ApiPaths.SelectMany(x => x.HttpMethods).SingleOrDefault(x => x.ApiTag.Any(y => string.Equals(y, apiTag)) && x.ApiOperation == apiOperation);
            httpMethod.Summary = UiSummaryToRaw(SummaryText);
            httpMethod.Description = UiDescriptionToRaw(DescriptionText).Replace("\r", "");

            // Write to disc.
            await _objectModel.SaveAsync();

            MessageBox.Show("Page saved and any inconsistent formatting in swagger.json is fixed.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Copy JSON to clipboard

        private void BtnSummary_Click(object sender, RoutedEventArgs e)
        {
            if (SummaryText.IsEmpty())
            {
                MessageBox.Show("No summary to convert.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Convert to JSON with escaping.
            JsonNode jsonNode = UiSummaryToRaw(SummaryText);
            JsonSerializerOptions serializerOptions = new()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            string summary = jsonNode.ToJsonString(serializerOptions);

            Clipboard.SetText($"\"summary\": {summary}");

            MessageBox.Show("Copied JSON summary to clipboard.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDescription_Click(object sender, RoutedEventArgs e)
        {
            if (DescriptionText.IsEmpty())
            {
                MessageBox.Show("No description to convert.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Convert to JSON with escaping.
            JsonNode jsonNode = UiDescriptionToRaw(DescriptionText).Replace("\r", "");
            JsonSerializerOptions serializerOptions = new()
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            string description = jsonNode.ToJsonString(serializerOptions);

            Clipboard.SetText($"\"description\": {description}");

            MessageBox.Show("Copied JSON description to clipboard.", s_appDisplayName, MessageBoxButton.OK, MessageBoxImage.Information);
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

        private string RawDescriptionToUi(string rawDescription)
        {
            // Remove formatting applicable to the Power BI REST API swagger file.
            rawDescription = _objectModel.IsPowerBiClient && rawDescription.IsNotEmpty() ? rawDescription.TrimEnd(new[] { " ", "\r", "\n", "<br>", "</br>" }) : rawDescription;

            string uiDescription = rawDescription.TrimStart(new[] { ' ', '\r', '\n' }).TrimEnd(new[] { ' ', '\r', '\n', '#' });
            uiDescription = _objectModel.IsPowerBiClient && uiDescription.IsNotEmpty() ? uiDescription.TrimEnd("\n<br><br>").TrimEnd("<br><br>").TrimEnd("<br><br>") : uiDescription;
            return uiDescription;
        }

        private string UiDescriptionToRaw(string uiDescription)
        {
            // Remove formatting applicable to the Power BI REST API swagger file.
            uiDescription = _objectModel.IsPowerBiClient && uiDescription.IsNotEmpty() ? uiDescription.TrimEnd(new[] { " ", "\r", "\n", "<br>", "</br>" }) : uiDescription;

            // Apply a special formatting to the Power BI REST API swagger file.
            string preSpacing = _objectModel.IsPowerBiClient && uiDescription.IsNotEmpty() ? "\n" : "";
            string postSpacing = _objectModel.IsPowerBiClient && uiDescription.IsNotEmpty() ? "\n<br><br>" : "";

            string rawDescription = preSpacing + uiDescription.Trim(new[] { ' ', '\r', '\n' }) + postSpacing;
            return rawDescription;
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
