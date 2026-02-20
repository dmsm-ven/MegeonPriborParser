using System.Collections.Generic;
using System.ComponentModel;

namespace Megeon
{
    public class SearchConflictResolverViewModel : INotifyPropertyChanged
    {
        
        public List<string> Items { get; set; }
        public string ModelToFind { get; set; }

        string selectedItem;
        public string SelectedItem
        {
            get => selectedItem;
            set
            {
                selectedItem = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItem)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}