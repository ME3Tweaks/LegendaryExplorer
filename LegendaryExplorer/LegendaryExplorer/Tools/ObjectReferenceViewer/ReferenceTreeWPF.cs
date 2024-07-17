using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools.ObjectReferenceViewer
{
    public class ReferenceTreeWPF : ReferenceTreeBase<ReferenceTreeWPF>, INotifyPropertyChanged
    {
        // This is a hack. For poor WPF TreeView stuff.
        public static bool AllowSelection = true;

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (AllowSelection)
                    SetProperty(ref _isSelected, value);
            }
        }


        private bool isExpanded;
        public bool IsExpanded
        {
            get => this.isExpanded;
            set => SetProperty(ref isExpanded, value);
        }

        public void ExpandParents()
        {
            if (Parent is not null)
            {
                Parent.ExpandParents();
                Parent.IsExpanded = true;
            }
        }


        /// <summary>
        /// Flattens the tree into depth first order. Use this method for searching the list.
        /// </summary>
        /// <returns></returns>
        public List<ReferenceTreeWPF> FlattenTree()
        {
            var nodes = new List<ReferenceTreeWPF> { this };
            foreach (ReferenceTreeWPF tve in Children)
            {
                nodes.AddRange(tve.FlattenTree());
            }

            return nodes;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
