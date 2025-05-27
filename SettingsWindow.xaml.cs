using System;
using System.Windows;
using explaina.Services; // 添加这个引用
using System.Collections.Generic; // Dictionary需要这个

namespace explaina
{
    public partial class SettingsWindow : Window
    {
        public string ApiKey { get; private set; }
        public string ModelName { get; private set; }
        public string PromptSuffix { get; private set; }
        public bool AutoStart { get; private set; }

        public SettingsWindow(string currentApiKey, string currentModelName, string currentPromptSuffix, bool currentAutoStart)
        {
            InitializeComponent();
            ApiKeyTextBox.Text = currentApiKey;
            ModelNameTextBox.Text = currentModelName;
            PromptSuffixTextBox.Text = currentPromptSuffix;
            AutoStartCheckBox.IsChecked = currentAutoStart;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ApiKey = ApiKeyTextBox.Text.Trim();
            ModelName = ModelNameTextBox.Text.Trim();
            PromptSuffix = PromptSuffixTextBox.Text.Trim();
            AutoStart = AutoStartCheckBox.IsChecked ?? false;
            
            SettingsService.SaveApiKey(ApiKey);
            SettingsService.SaveModelName(ModelName);
            SettingsService.SavePromptSuffix(PromptSuffix);
            SettingsService.SaveAutoStart(AutoStart);
            
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ResetModelButton_Click(object sender, RoutedEventArgs e)
        {
            ModelNameTextBox.Text = "google/gemini-2.0-flash-exp:free";
        }

        private void ResetPromptSuffixButton_Click(object sender, RoutedEventArgs e)
        {
            PromptSuffixTextBox.Text = "请用中文解释上面的概念。";
        }
    }
}