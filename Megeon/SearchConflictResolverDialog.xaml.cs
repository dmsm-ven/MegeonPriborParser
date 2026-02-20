using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Megeon
{
    /// <summary>
    /// Interaction logic for SearchConflictResolverDialog.xaml
    /// </summary>
    public partial class SearchConflictResolverDialog : Window
    {
        public SearchConflictResolverDialog()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
