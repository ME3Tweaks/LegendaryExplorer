using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using UsefulThings.WPF;

namespace ME3Explorer
{
    public class PeriodicUpdater : DependencyObject, INotifyPropertyChanged
    {
        DispatcherTimer timer;

        public double Interval
        {
            get { return (double)GetValue(IntervalProperty); }
            set { SetValue(IntervalProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Interval.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IntervalProperty =
            DependencyProperty.Register("Interval", typeof(double), typeof(PeriodicUpdater), new PropertyMetadata(0.0, IntervalChanged));

        private static void IntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PeriodicUpdater p = d as PeriodicUpdater;
            p?.Start((double)e.NewValue);
        }

        public DateTime Now
        {
            get { return DateTime.Now; }
        }

        public void Start(double interval)
        {
            Stop();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(interval);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public void Stop()
        {
            timer?.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Now"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
