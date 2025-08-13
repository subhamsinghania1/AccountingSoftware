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
                var result = await AuthenticateAsync(username, password);
                if (result.Success)
                {
                    var dashboard = new DashboardWindow(result.Role == "Admin") { Owner = this.Owner };
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
        /// Authenticate the user by calling the server API and return role information.
        /// </summary>
        private async Task<LoginResult> AuthenticateAsync(string username, string password)
        {
            // Allow an offline demo login without contacting the server.
            if (username == "admin" && password == "password")
            {
                return true;
            }

            var apiUrl = "http://localhost:5000/api/auth/login";

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    var payload = new { Username = username, Password = password };
                    string json = JsonConvert.SerializeObject(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        var loginResult = JsonConvert.DeserializeObject<LoginResult>(body);
                        if (loginResult != null)
                        {
                            return loginResult;
                        }
                    }
                    else
                    {
                        string errorBody = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"Server error: {errorBody}", "Login failed");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Network error: {ex.Message}", "Login failed");
            }

            return new LoginResult { Success = false };
        }

        private class LoginResult
        {
            public bool Success { get; set; }
            public string Role { get; set; } = string.Empty;
        }
    }
}
