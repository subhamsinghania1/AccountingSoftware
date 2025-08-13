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
using System.Windows.Input;
using Newtonsoft.Json;

namespace AccountingApp
{
    /// <summary>
    /// Interaction logic for DashboardWindow.xaml
    /// </summary>
    public partial class DashboardWindow : Window
    {

        // Observable collections to hold vendors, transactions and users
        private ObservableCollection<Vendor> Vendors { get; set; } = new ObservableCollection<Vendor>();
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

            // Bind the vendors, transactions and users lists to the DataGrids
            VendorsDataGrid.ItemsSource = Vendors;
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
                if (RoznamchaDatePicker != null)
                {
                    RoznamchaDatePicker.SelectedDate = DateTime.Now;
                }
                await LoadVendorsAsync();
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

        // Refresh vendors list
        private async void RefreshVendors_Click(object sender, RoutedEventArgs e)
        {
            await LoadVendorsAsync();
        }

        // Add a new vendor
        private async void AddVendor_Click(object sender, RoutedEventArgs e)
        {
            var name = (VendorNameTextBox.Text ?? string.Empty).Trim();
            var address = (VendorAddressTextBox.Text ?? string.Empty).Trim();
            var phone = (VendorPhoneTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Vendor name is required", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var vendorObj = new { Name = name, Address = address, Phone = phone };
            string json = JsonConvert.SerializeObject(vendorObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("http://localhost:5000/api/vendors", content);
                if (response.IsSuccessStatusCode)
                {
                    // Clear inputs and reload vendors
                    VendorNameTextBox.Text = string.Empty;
                    VendorAddressTextBox.Text = string.Empty;
                    VendorPhoneTextBox.Text = string.Empty;
                    await LoadVendorsAsync();
                }
                else
                {
                    string error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Failed to add vendor: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding vendor: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Load vendors from the API and update UI
        private async Task LoadVendorsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("http://localhost:5000/api/vendors");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var list = JsonConvert.DeserializeObject<List<Vendor>>(json) ?? new List<Vendor>();
                    Vendors.Clear();
                    foreach (var v in list)
                    {
                        Vendors.Add(v);
                    }

                    // Update vendor combo boxes for filtering and adding transactions
                    LedgerVendorFilterComboBox.ItemsSource = Vendors;
                    AddTransactionVendorComboBox.ItemsSource = Vendors;
                    UpdateDashboardSummary();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vendors: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        var vendor = Vendors.FirstOrDefault(v => v.Id == t.VendorId);
                        AllTransactions.Add(new TransactionViewModel
                        {
                            Id = t.Id,
                            VendorId = t.VendorId,
                            VendorName = vendor?.Name ?? string.Empty,
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
            AddTransactionErrorTextBlock.Text = string.Empty;

            if (AddTransactionVendorComboBox.SelectedValue == null)
            {
                AddTransactionErrorTextBlock.Text = "Please select a vendor.";
                return;
            }
            var vendorId = (int)AddTransactionVendorComboBox.SelectedValue;
            var date = AddTransactionDatePicker.SelectedDate ?? DateTime.Now;
            var typeItem = AddTransactionTypeComboBox.SelectedItem as ComboBoxItem;
            if (typeItem == null)
            {
                AddTransactionErrorTextBlock.Text = "Please select a transaction type.";
                return;
            }
            string type = typeItem.Content.ToString() ?? string.Empty;
            if (!decimal.TryParse((AddTransactionAmountTextBox.Text ?? string.Empty).Trim(), out decimal amount))
            {
                AddTransactionErrorTextBlock.Text = "Please enter a valid amount.";
                return;
            }
            var description = (AddTransactionDescriptionTextBox.Text ?? string.Empty).Trim();
            var transObj = new { VendorId = vendorId, Amount = amount, Type = type, Date = date, Description = description };
            string json = JsonConvert.SerializeObject(transObj);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await _httpClient.PostAsync("http://localhost:5000/api/transactions", content);
                if (response.IsSuccessStatusCode)
                {
                    // Clear inputs and reload transactions
                    AddTransactionVendorComboBox.SelectedIndex = -1;
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

        private void AddTransactionPanel_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddTransaction_Click(AddTransactionButton, new RoutedEventArgs());
            }
        }

        // Enable editing on a transaction row
        private void EditTransaction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var row = TransactionsDataGrid.ItemContainerGenerator.ContainerFromItem(btn.DataContext) as DataGridRow;
                if (row != null)
                {
                    TransactionsDataGrid.SelectedItem = row.Item;
                    TransactionsDataGrid.CurrentCell = new DataGridCellInfo(row.Item, TransactionsDataGrid.Columns[5]);
                    TransactionsDataGrid.BeginEdit();
                }
            }
        }

        // Refresh a transaction from the server
        private async void UpdateTransaction_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is int transactionId)
            {
                try
                {
                    var response = await _httpClient.GetAsync($"http://localhost:5000/api/transactions/{transactionId}");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var t = JsonConvert.DeserializeObject<Transaction>(json);
                        if (t != null)
                        {
                            var tvm = AllTransactions.FirstOrDefault(x => x.Id == t.Id);
                            if (tvm != null)
                            {
                                var vendor = Vendors.FirstOrDefault(v => v.Id == t.VendorId);
                                tvm.VendorId = t.VendorId;
                                tvm.VendorName = vendor?.Name ?? string.Empty;
                                tvm.Amount = t.Amount;
                                tvm.Type = t.Type;
                                tvm.Date = t.Date;
                                tvm.Description = t.Description;
                                TransactionsDataGrid.Items.Refresh();
                            }
                        }
                    }
                    else
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"Failed to fetch transaction: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error fetching transaction: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

        // Persist inline edits to transactions
        private async void TransactionsDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }

            if (e.Row.Item is TransactionViewModel tvm)
            {
                var transObj = new
                {
                    VendorId = tvm.VendorId,
                    Amount = tvm.Amount,
                    Type = tvm.Type,
                    Date = tvm.Date,
                    Description = tvm.Description
                };
                string json = JsonConvert.SerializeObject(transObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                try
                {
                    var response = await _httpClient.PutAsync($"http://localhost:5000/api/transactions/{tvm.Id}", content);
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"Failed to update transaction: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating transaction: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // Filter ledger by vendor and date range
        private void LoadLedger_Click(object sender, RoutedEventArgs e)
        {
            int? vendorId = null;
            if (LedgerVendorFilterComboBox.SelectedValue != null)
            {
                try
                {
                    vendorId = Convert.ToInt32(LedgerVendorFilterComboBox.SelectedValue);
                }
                catch
                {
                    vendorId = null;
                }
            }
            DateTime? from = FromDatePicker.SelectedDate;
            DateTime? to = ToDatePicker.SelectedDate;

            var filtered = AllTransactions.Where(t =>
                (!vendorId.HasValue || t.VendorId == vendorId.Value) &&
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
            if (VendorCountTextBlock != null)
            {
                VendorCountTextBlock.Text = Vendors.Count.ToString();
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

        private void LoadRoznamcha_Click(object sender, RoutedEventArgs e)
        {
            if (RoznamchaDatePicker == null)
            {
                return;
            }

            DateTime? selectedDate = RoznamchaDatePicker.SelectedDate;
            if (!selectedDate.HasValue)
            {
                RoznamchaDataGrid.ItemsSource = null;
                RoznamchaCreditTextBlock.Text = "0.00";
                RoznamchaDebitTextBlock.Text = "0.00";
                return;
            }

            var filtered = AllTransactions
                .Where(t => t.Date.Date == selectedDate.Value.Date)
                .ToList();

            RoznamchaDataGrid.ItemsSource = new ObservableCollection<TransactionViewModel>(filtered);

            decimal totalCredit = filtered
                .Where(t => string.Equals(t.Type, "Credit", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);
            decimal totalDebit = filtered
                .Where(t => string.Equals(t.Type, "Debit", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            RoznamchaCreditTextBlock.Text = totalCredit.ToString("0.00");
            RoznamchaDebitTextBlock.Text = totalDebit.ToString("0.00");
        }

        // Vendor model
        private class Vendor
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
        }

        // Transaction model returned from API
        private class Transaction
        {
            public int Id { get; set; }
            public int VendorId { get; set; }
            public decimal Amount { get; set; }
            public string Type { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public string Description { get; set; } = string.Empty;
        }

        // View model for displaying transactions with vendor name
        private class TransactionViewModel
        {
            public int Id { get; set; }
            public int VendorId { get; set; }
            public string VendorName { get; set; } = string.Empty;
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