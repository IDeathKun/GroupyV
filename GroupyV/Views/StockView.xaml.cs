using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GroupyV.ViewModels;

namespace GroupyV.Views
{
    public partial class StockView : UserControl
    {
        public StockView()
        {
            InitializeComponent();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && DataContext is StockViewModel vm)
            {
                vm.SelectedFilter = rb.Content?.ToString() ?? "Tous";
            }
        }

        private void QuantityTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void QuantityTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var text = (string)e.DataObject.GetData(typeof(string))!;
                if (!int.TryParse(text, out _))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void QuantityPlus_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is StockViewModel vm)
            {
                int current = int.TryParse(vm.DialogQuantite, out int v) ? v : 0;
                vm.DialogQuantite = (current + 1).ToString();
            }
        }

        private void QuantityMinus_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is StockViewModel vm)
            {
                int current = int.TryParse(vm.DialogQuantite, out int v) ? v : 0;
                if (current > 1)
                    vm.DialogQuantite = (current - 1).ToString();
            }
        }

        private void QuickQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag && DataContext is StockViewModel vm)
            {
                int add = int.TryParse(tag, out int a) ? a : 0;
                int current = int.TryParse(vm.DialogQuantite, out int v) ? v : 0;
                vm.DialogQuantite = (current + add).ToString();
            }
        }
    }
}
