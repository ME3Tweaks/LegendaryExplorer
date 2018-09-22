using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ME3Explorer.SharedUI
{
    /// <summary>
    /// Interaction logic for LoadingSpinner.xaml
    /// From https://stackoverflow.com/questions/6359848/wpf-loading-spinner/6369137#6369137
    /// </summary>
    public partial class LoadingSpinner : UserControl
    {
        public int EllipseSize { get; set; } = 8;
        public int SpinnerHeight { get; set; } = 0;
        public int SpinnerWidth { get; set; } = 0;


        // start positions
        public EllipseStartPosition EllipseN { get; private set; }
        public EllipseStartPosition EllipseNE { get; private set; }
        public EllipseStartPosition EllipseE { get; private set; }
        public EllipseStartPosition EllipseSE { get; private set; }
        public EllipseStartPosition EllipseS { get; private set; }
        public EllipseStartPosition EllipseSW { get; private set; }
        public EllipseStartPosition EllipseW { get; private set; }
        public EllipseStartPosition EllipseNW { get; private set; }

        public LoadingSpinner()
        {
            InitializeComponent();
        }

        private void initialSetup()
        {
            float horizontalCenter = (float)(SpinnerWidth / 2);
            float verticalCenter = (float)(SpinnerHeight / 2);
            float distance = (float)Math.Min(SpinnerHeight, SpinnerWidth) / 2;

            double angleInRadians = 44.8;
            float cosine = (float)Math.Cos(angleInRadians);
            float sine = (float)Math.Sin(angleInRadians);

            EllipseN = newPos(left: horizontalCenter, top: verticalCenter - distance);
            EllipseNE = newPos(left: horizontalCenter + (distance * cosine), top: verticalCenter - (distance * sine));
            EllipseE = newPos(left: horizontalCenter + distance, top: verticalCenter);
            EllipseSE = newPos(left: horizontalCenter + (distance * cosine), top: verticalCenter + (distance * sine));
            EllipseS = newPos(left: horizontalCenter, top: verticalCenter + distance);
            EllipseSW = newPos(left: horizontalCenter - (distance * cosine), top: verticalCenter + (distance * sine));
            EllipseW = newPos(left: horizontalCenter - distance, top: verticalCenter);
            EllipseNW = newPos(left: horizontalCenter - (distance * cosine), top: verticalCenter - (distance * sine));
        }

        private EllipseStartPosition newPos(float left, float top)
        {
            return new EllipseStartPosition() { Left = left, Top = top };
        }


        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Height")
            {
                SpinnerHeight = Convert.ToInt32(e.NewValue);
            }

            if (e.Property.Name == "Width")
            {
                SpinnerWidth = Convert.ToInt32(e.NewValue);
            }

            if (SpinnerHeight > 0 && SpinnerWidth > 0)
            {
                initialSetup();
            }

            base.OnPropertyChanged(e);
        }
    }

    public struct EllipseStartPosition
    {
        public float Left { get; set; }
        public float Top { get; set; }
    }
}
