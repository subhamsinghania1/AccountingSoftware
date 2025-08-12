using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;

namespace AccountingApp
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;
            StatusTextBlock.Text = string.Empty;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                StatusTextBlock.Text = "Please enter your username and password.";
                return;
            }
            LoginButton.IsEnabled = false;
            try
            {
                bool authenticated = await AuthenticateAsync(username, password);
                if (authenticated)
                {
                    var dashboard = new DashboardWindow { Owner = this.Owner };
                    this.Hide();
                    dashboard.ShowDialog();
                    this.Show();
                    this.DialogResult = true;
                }
                else
                {
                    StatusTextBlock.Text = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Error: " + ex.Message;
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Authenticate the user by calling the server API.
        /// Replace the stub with real HTTP call.
        /// Returns true for demo if username == "admin" and password == "password".
        /// </summary>
        private async Task<bool> AuthenticateAsync(string username, string password)
        {
            var apiUrl = "http://localhost:5000/api/auth/login";

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    // NOTE: Capitalized property names
                    var payload = new { Username = username, Password = password };
                    string json = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                    // Optional: read the server's error message for debugging
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"Server error: {errorBody}", "Login failed");
                    }

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Network error: {ex.Message}", "Login failed");
                return false;
            }
        }
    }
    }