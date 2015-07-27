using System;
using System.Windows.Forms;
using Transitions;

namespace TestSample
{
    /// <summary>
    /// This is a simple user-control that hosts two picture-boxes (one showing
    /// a kitten and the other showing a puppy). The transitionPictures method
    /// performs a random animated transition between the two pictures.
    /// </summary>
    public partial class KittenPuppyControl : UserControl
    {
        #region Public methods

        /// <summary>
        /// Constructor.
        /// </summary>
        public KittenPuppyControl()
        {
            InitializeComponent();
            m_ActivePicture = ctrlPuppy;
            m_InactivePicture = ctrlKitten;
        }

        /// <summary>
        /// Performs a random tarnsition between the two pictures.
        /// </summary>
        public void transitionPictures()
        {
            // We randomly choose where the current image is going to 
            // slide off to (and where we are going to slide the inactive
            // image in from)...
            int iDestinationLeft = (m_Random.Next(2) == 0) ? Width : -Width;
            int iDestinationTop = (m_Random.Next(3) - 1) * Height;

            // We move the inactive image to this location...
            SuspendLayout();
            m_InactivePicture.Top = iDestinationTop;
            m_InactivePicture.Left = iDestinationLeft;
            m_InactivePicture.BringToFront();
            ResumeLayout();

            // We perform the transition which moves the active image off the
            // screen, and the inactive one onto the screen...
            Transition t = new Transition(new TransitionType_EaseInEaseOut(1000));
            t.add(m_InactivePicture, "Left", 0);
            t.add(m_InactivePicture, "Top", 0);
            t.add(m_ActivePicture, "Left", iDestinationLeft);
            t.add(m_ActivePicture, "Top", iDestinationTop);
            t.run();

            // We swap over which image is active and inactive for next time
            // the function is called...
            PictureBox tmp = m_ActivePicture;
            m_ActivePicture = m_InactivePicture;
            m_InactivePicture = tmp;
        }

        #endregion

        #region Private data

        private PictureBox m_ActivePicture = null;
        private PictureBox m_InactivePicture = null;
        private Random m_Random = new Random();

        #endregion
    }
}
