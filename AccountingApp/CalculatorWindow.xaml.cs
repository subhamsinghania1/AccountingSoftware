using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace AccountingApp
{
    /// <summary>
    /// Interaction logic for CalculatorWindow.xaml
    /// A fully functional calculator that reveals the login screen when a secret code is entered.
    /// Supports basic operations: addition, subtraction, multiplication, division, percentage and sign toggle.
    /// </summary>
    public partial class CalculatorWindow : Window
    {
        private double? _previousValue = null;
        private string _currentOperator = null;
        private bool _waitingForNextValue = false;
        private readonly StringBuilder _secretBuffer = new StringBuilder();
        private const string SecretCode = "2+2+102";

        public CalculatorWindow()
        {
            InitializeComponent();
            DisplayTextBox.Text = "0";
        }

        private void NumberButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string value)
            {
                AppendSecret(value);
                if (_waitingForNextValue || DisplayTextBox.Text == "0")
                {
                    // Start new number
                    DisplayTextBox.Text = value;
                    _waitingForNextValue = false;
                }
                else
                {
                    DisplayTextBox.Text += value;
                }
            }
        }

        private void DecimalButton_Click(object sender, RoutedEventArgs e)
        {
            AppendSecret(".");
            if (_waitingForNextValue)
            {
                DisplayTextBox.Text = "0.";
                _waitingForNextValue = false;
            }
            else if (!DisplayTextBox.Text.Contains("."))
            {
                DisplayTextBox.Text += ".";
            }
        }

        private void OperatorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string op)
            {
                AppendSecret(op);
                double currentValue;
                if (!double.TryParse(DisplayTextBox.Text, out currentValue))
                    return;
                if (_previousValue == null)
                {
                    _previousValue = currentValue;
                }
                else if (!_waitingForNextValue)
                {
                    _previousValue = Compute(_previousValue.Value, _currentOperator, currentValue);
                    DisplayTextBox.Text = _previousValue.ToString();
                }
                _currentOperator = op;
                _waitingForNextValue = true;
            }
        }

        private void EqualButton_Click(object sender, RoutedEventArgs e)
        {
            AppendSecret("=");
            if (_previousValue != null && _currentOperator != null && !_waitingForNextValue)
            {
                double currentValue;
                if (!double.TryParse(DisplayTextBox.Text, out currentValue))
                    return;
                var result = Compute(_previousValue.Value, _currentOperator, currentValue);
                DisplayTextBox.Text = result.ToString();
                _previousValue = result;
                _waitingForNextValue = true;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear current entry
            DisplayTextBox.Text = "0";
            _waitingForNextValue = true;
            // Do not reset previousValue or operator
        }

        private void AllClearButton_Click(object sender, RoutedEventArgs e)
        {
            ResetCalculator();
        }

        private void SignButton_Click(object sender, RoutedEventArgs e)
        {
            AppendSecret("±");
            double value;
            if (double.TryParse(DisplayTextBox.Text, out value))
            {
                value = -value;
                DisplayTextBox.Text = value.ToString();
            }
        }

        private void PercentButton_Click(object sender, RoutedEventArgs e)
        {
            AppendSecret("%");
            double value;
            if (double.TryParse(DisplayTextBox.Text, out value))
            {
                value = value / 100.0;
                DisplayTextBox.Text = value.ToString();
            }
        }

        private double Compute(double left, string op, double right)
        {
            switch (op)
            {
                case "+": return left + right;
                case "−": return left - right;
                case "×": return left * right;
                case "÷": return right != 0 ? left / right : 0;
                default: return right;
            }
        }

        private void ResetCalculator()
        {
            DisplayTextBox.Text = "0";
            _previousValue = null;
            _currentOperator = null;
            _waitingForNextValue = false;
            _secretBuffer.Clear();
        }

        private void AppendSecret(string value)
        {
            _secretBuffer.Append(value);
            // Keep only the last SecretCode length characters
            if (_secretBuffer.Length > SecretCode.Length)
            {
                _secretBuffer.Remove(0, _secretBuffer.Length - SecretCode.Length);
            }
            // Check for secret code match (exact)
            if (_secretBuffer.ToString() == SecretCode)
            {
                // Reset secret buffer and open login
                _secretBuffer.Clear();
                OpenLoginWindow();
            }
        }

        private void OpenLoginWindow()
        {
            var login = new LoginWindow { Owner = this };
            this.Hide();
            login.ShowDialog();
            this.Show();
            // After returning, you can choose to reset the calculator or leave state
            ResetCalculator();
        }
    }
}