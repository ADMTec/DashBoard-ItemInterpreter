using System.Windows;
using ItemInterpreter.Data;
using ItemInterpreter.Logic;

namespace ItemInterpreter.UI.Dashboard
{
    public partial class NotificationSettingsWindow : Window
    {
        private readonly NotificationSettingsService _service;
        private NotificationSettings _settings;

        public NotificationSettingsWindow(NotificationSettingsService service)
        {
            InitializeComponent();

            _service = service;
            _settings = _service.Load();

            WebhookEnabledCheckBox.IsChecked = _settings.WebhookEnabled;
            WebhookUrlTextBox.Text = _settings.WebhookUrl ?? string.Empty;
            UpdateControls();
        }

        public NotificationSettings GetUpdatedSettings()
        {
            return _settings;
        }

        private void Salvar_Click(object sender, RoutedEventArgs e)
        {
            _settings.WebhookEnabled = WebhookEnabledCheckBox.IsChecked == true;
            _settings.WebhookUrl = string.IsNullOrWhiteSpace(WebhookUrlTextBox.Text)
                ? null
                : WebhookUrlTextBox.Text.Trim();

            _service.Save(_settings);
            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void WebhookEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateControls();
        }

        private void WebhookEnabledCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateControls();
        }

        private void UpdateControls()
        {
            var enabled = WebhookEnabledCheckBox.IsChecked == true;
            WebhookUrlTextBox.IsEnabled = enabled;
            WebhookUrlTextBox.Opacity = enabled ? 1.0 : 0.5;
        }
    }
}
