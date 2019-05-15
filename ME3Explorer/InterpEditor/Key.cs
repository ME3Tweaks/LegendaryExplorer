using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace ME3Explorer.Matinee
{
    class Key : Thumb
    {
        public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
            nameof(Time), typeof(float), typeof(Key), new PropertyMetadata(default(float)));

        public float Time
        {
            get => (float) GetValue(TimeProperty);
            set => SetValue(TimeProperty, value);
        }

        public Key(float time = 0)
        {
            Time = time;
        }
    }
}
