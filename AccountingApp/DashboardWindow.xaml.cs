using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace AccountingApp
{
    /// <summary>
    /// Interaction logic for DashboardWindow.xaml
    /// </summary>
    public partial class DashboardWindow : Window
    {

        // Observable collections to hold parties, transactions and users
        private ObservableCollection<Party> Parties { get; set; } = new ObservableCollection<Party>();
        private ObservableCollection<TransactionViewModel> AllTransactions { get; set; } = new ObservableCollection<TransactionViewModel>();
        private ObservableCollection<User> Users { get; set; } = new ObservableCollection<User>();

        // HTTP client for API calls
        private readonly HttpClient _httpClient;
        private readonly bool _isAdmin;

        public DashboardWindow(bool isAdmin)
        {
            _isAdmin = isAdmin;
            InitializeComponent();

            // Initialize HttpClient
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Bind the parties, transactions and users lists to the DataGrids
            PartiesDataGrid.ItemsSource = Parties;
            UsersDataGrid.ItemsSource = Users;

            // Hide admin-only features for non-admin users
            if (!_isAdmin)
            {
                UsersTab.Visibility = Visibility.Collapsed;
                KillButton.Visibility = Visibility.Collapsed;
            }

            // Load data asynchronously after the window is loaded
            this.Loaded += async (_, __) =>
            {
                // Set default date for adding transactions to today
                AddTransactionDatePicker.SelectedDate = DateTime.Now;
                await LoadPartiesAsync();
                await LoadTransactionsAsync();
                if (_isAdmin)
                {
                    await LoadUsersAsync();
                }
            };
        }

        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the dashboard to return to login
            this.Close();
        }

        // Refresh parties list
        private async void RefreshParties_Click(object sender, RoutedEventArgs e)
        {
            await LoadPartiesAsync();
        }

        // Add a new party
        private async void AddParty_Click(object sender, RoutedEventArgs e)
        {
            var name = (PartyNameTextBox.Text ?? string.Empty).Trim();
            var address = (PartyAddressTextBox.Text ?? string.Empty).Trim();
            var phone = (PartyPhoneTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Party name is required", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var partyObj = new { Name = name, Address = address, Phone = phone };
            string json = JsonConvert.SerializeObject(partyObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("http://localhost:5000/api/parties", content);
                if (response.IsSuccessStatusCode)
                {
                    // Clear inputs and reload parties
                    PartyNameTextBox.Text = string.Empty;
                    PartyAddressTextBox.Text = string.Empty;
                    PartyPhoneTextBox.Text = string.Empty;
                    await LoadPartiesAsync();
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to add party: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding party: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Load parties from the API and update UI
        private async Task LoadPartiesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("http://localhost:5000/api/parties");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var list = JsonConvert.DeserializeObject<List<Party>>(json) ?? new List<Party>();
                    Parties.Clear();
                    foreach (var p in list)
                    {
                        Parties.Add(p);
                    }

                    // Update party combo boxes for filtering and adding transactions
                    LedgerPartyFilterComboBox.ItemsSource = Parties;
                    AddTransactionPartyComboBox.ItemsSource = Parties;
                    UpdateDashboardSummary();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading parties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Load transactions from the API and update UI
        private async Task LoadTransactionsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("http://localhost:5000/api/transactions");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var list = JsonConvert.DeserializeObject<List<Transaction>>(json) ?? new List<Transaction>();
                    AllTransactions.Clear();

                    foreach (var t in list)
                    {
                        var party = Parties.FirstOrDefault(p => p.Id == t.PartyId);
                        AllTransactions.Add(new TransactionViewModel
                        {
                            Id = t.Id,
                            PartyId = t.PartyId,
                            PartyName = party?.Name ?? string.Empty,
                            Amount = t.Amount,
                            Type = t.Type,
                            Date = t.Date,
                            Description = t.Description
                        });
                    }

                    // Display all transactions by default
                    TransactionsDataGrid.ItemsSource = new ObservableCollection<TransactionViewModel>(AllTransactions);
                    UpdateDashboardSummary();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading transactions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Add a new transaction
        private async void AddTransaction_Click(object sender, RoutedEventArgs e)
        {
            if (AddTransactionPartyComboBox.SelectedValue == null)
            {
                MessageBox.Show("Please select a party", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var partyId = (int)AddTransactionPartyComboBox.SelectedValue;
            var date = AddTransactionDatePicker.SelectedDate ?? DateTime.Now;
            var typeItem = AddTransactionTypeComboBox.SelectedItem as ComboBoxItem;
            if (typeItem == null)
            {
                MessageBox.Show("Please select a transaction type", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string type = typeItem.Content.ToString() ?? string.Empty;
            if (!decimal.TryParse((AddTransactionAmountTextBox.Text ?? string.Empty).Trim(), out decimal amount))
            {
                MessageBox.Show("Please enter a valid amount", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var description = (AddTransactionDescriptionTextBox.Text ?? string.Empty).Trim();
            var transObj = new { PartyId = partyId, Amount = amount, Type = type, Date = date, Description = description };
            string json = JsonConvert.SerializeObject(transObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await _httpClient.PostAsync("http://localhost:5000/api/transactions", content);
                if (response.IsSuccessStatusCode)
                {
                    // Clear inputs and reload transactions
                    AddTransactionPartyComboBox.SelectedIndex = -1;
                    AddTransactionDatePicker.SelectedDate = DateTime.Now;
                    AddTransactionTypeComboBox.SelectedIndex = -1;
                    AddTransactionAmountTextBox.Text = string.Empty;
                    AddTransactionDescriptionTextBox.Text = string.Empty;
                    await LoadTransactionsAsync();
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to add transaction: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding transaction: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Refresh transactions list
        private async void RefreshTransactions_Click(object sender, RoutedEventArgs e)
        {
            await LoadTransactionsAsync();
        }

        // Delete a transaction
        private async void DeleteTransaction_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int transactionId)
            {
                if (MessageBox.Show("Delete this transaction?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        var response = await _httpClient.DeleteAsync($"http://localhost:5000/api/transactions/{transactionId}");
                        if (response.IsSuccessStatusCode)
                        {
                            await LoadTransactionsAsync();
                        }
                        else
                        {
                            string error = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Failed to delete transaction: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting transaction: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Refresh users list
        private async void RefreshUsers_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsersAsync();
        }

        // Revoke a user's access
        private async void RevokeUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int userId)
            {
                if (MessageBox.Show("Revoke this user's access?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        var response = await _httpClient.PutAsync($"http://localhost:5000/api/users/{userId}/revoke", null);
                        if (response.IsSuccessStatusCode)
                        {
                            await LoadUsersAsync();
                        }
                        else
                        {
                            string error = await response.Content.ReadAsStringAsync();
                            MessageBox.Show($"Failed to revoke user: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error revoking user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // Load users from the API
        private async Task LoadUsersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("http://localhost:5000/api/users");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var list = JsonConvert.DeserializeObject<List<User>>(json) ?? new List<User>();
                    Users.Clear();
                    foreach (var u in list)
                    {
                        Users.Add(u);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading users: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Kill switch to delete all server data
        private async void KillButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("This will delete all data. Are you sure?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    var response = await _httpClient.DeleteAsync("http://localhost:5000/api/admin/kill");
                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show("All data deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"Failed to delete data: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error invoking kill switch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Filter ledger by party and date range
        private void LoadLedger_Click(object sender, RoutedEventArgs e)
        {
            int? partyId = null;
            if (LedgerPartyFilterComboBox.SelectedValue != null)
            {
                try
                {
                    partyId = Convert.ToInt32(LedgerPartyFilterComboBox.SelectedValue);
                }
                catch
                {
                    partyId = null;
                }
            }
            DateTime? from = FromDatePicker.SelectedDate;
            DateTime? to = ToDatePicker.SelectedDate;

            var filtered = AllTransactions.Where(t =>
                (!partyId.HasValue || t.PartyId == partyId.Value) &&
                (!from.HasValue || t.Date.Date >= from.Value.Date) &&
                (!to.HasValue || t.Date.Date <= to.Value.Date)
            ).ToList();

            LedgerDataGrid.ItemsSource = new ObservableCollection<TransactionViewModel>(filtered);
            CalculateLedgerTotals(filtered);
        }

        // Calculate totals for credit, debit and balance
        private void CalculateLedgerTotals(IEnumerable<TransactionViewModel> transactions)
        {
            decimal totalCredit = transactions.Where(t => string.Equals(t.Type, "Credit", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            decimal totalDebit = transactions.Where(t => string.Equals(t.Type, "Debit", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            decimal balance = totalCredit - totalDebit;

            TotalCreditTextBlock.Text = totalCredit.ToString("0.00");
            TotalDebitTextBlock.Text = totalDebit.ToString("0.00");
            BalanceTextBlock.Text = balance.ToString("0.00");
        }

        // Update summary values shown on the Home tab
        private void UpdateDashboardSummary()
        {
            if (PartyCountTextBlock != null)
            {
                PartyCountTextBlock.Text = Parties.Count.ToString();
            }
            if (TransactionCountTextBlock != null)
            {
                TransactionCountTextBlock.Text = AllTransactions.Count.ToString();
            }

            decimal totalCredit = AllTransactions.Where(t => string.Equals(t.Type, "Credit", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            decimal totalDebit = AllTransactions.Where(t => string.Equals(t.Type, "Debit", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            decimal balance = totalCredit - totalDebit;

            if (SummaryCreditTextBlock != null)
            {
                SummaryCreditTextBlock.Text = totalCredit.ToString("0.00");
            }
            if (SummaryDebitTextBlock != null)
            {
                SummaryDebitTextBlock.Text = totalDebit.ToString("0.00");
            }
            if (SummaryBalanceTextBlock != null)
            {
                SummaryBalanceTextBlock.Text = balance.ToString("0.00");
            }
        }

        // Party model
        private class Party
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
        }

        // Transaction model returned from API (no GST or tax fields)
        private class Transaction
        {
            public int Id { get; set; }
            public int PartyId { get; set; }
            public decimal Amount { get; set; }
            public string Type { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public string Description { get; set; } = string.Empty;
        }

        // View model for displaying transactions with party name
        private class TransactionViewModel
        {
            public int Id { get; set; }
            public int PartyId { get; set; }
            public string PartyName { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string Type { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public string Description { get; set; } = string.Empty;
        }
        private class User
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }
    }
}