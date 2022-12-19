using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Painter
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<double> Thicknesses { get { return thickness; } set { thickness = value; OnPropertyChanged(); } }
        private ObservableCollection<double> thickness = new ();

        internal MainViewModel() 
        {
            for (int i = 1; i < 30; i++)
                Thicknesses.Add(i);
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }


    }
}
