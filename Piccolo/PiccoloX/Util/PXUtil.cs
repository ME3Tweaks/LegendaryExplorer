using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace UMD.HCIL.PiccoloX.Util {
	/// <summary>
	/// <b>PXUtil</b> provides utility methods for the PiccoloX module.
	/// </summary>
	public class PXUtil {
		#region Screen Grab
		/// <summary>
		/// Gets an image snapshot of the specified control, which must be mapped to a
		/// <see cref="System.Windows.Forms.Form"/> and must be fully visible.
		/// </summary>
		/// <param name="control">The control to get an image snapshot of.</param>
		/// <returns>An image snapshot of the control.</returns>
		public static Image GrabControl(Control control) {
			Bitmap bmp = bmp = new Bitmap(control.Width, control.Height);;

			if (control.IsHandleCreated) {
				System.IntPtr srcDC=GetDC(control.Handle); 
				Bitmap bm=new Bitmap(control.Width, control.Height); 
				Graphics g=Graphics.FromImage(bmp);
				System.IntPtr bmDC=g.GetHdc();

				BitBlt(bmDC,0,0,bm.Width,bm.Height,srcDC,0,0,0x00CC0020); //SRCCOPY

				ReleaseDC(control.Handle, srcDC); 
				g.ReleaseHdc(bmDC);
				g.Dispose();
			}

			return bmp;
		}

		/// <summary>
		/// Gets an image snapshot of the specified control, which does not have to be
		/// mapped to a <see cref="System.Windows.Forms.Form"/> and does not have to be
		/// fully visible.
		/// </summary>
		/// <param name="control">The control to get an image snapshot of.</param>
		/// <returns>An image snapshot of the control.</returns>
		public static Image OffscreenGrab(Control control) {
			// Save the old parent and location.
			Control oldParent = control.Parent;
			Point oldLocation = control.Location;

			// Create a form offscreen
			Form transpForm = new Form();
			transpForm.StartPosition = FormStartPosition.Manual;
			transpForm.Location = new Point(Screen.PrimaryScreen.Bounds.Right + 10, 0);
			transpForm.ClientSize = new Size(control.Width + 10, control.Height + 10);

			// Make the form semi-transparent so windows turns on layering.
			transpForm.Opacity = .8;

			// Add the control.
			transpForm.Controls.Add(control);
			control.Location = new Point(0, 0);

			// Show the form (offscreen).
			transpForm.Show();
			transpForm.Visible = true;
			transpForm.Refresh();
			transpForm.Update();

			// Grab the control.
			Image img = GrabControl(control);

			// Put the control back on its original form.
			control.Parent = oldParent;
			control.Location = oldLocation;

			// Get rid of our temporary form.
			transpForm.Close();

			// Return the image.
			return img;
		}

		/// <summary>
		/// Imports the GDI BitBlt function that enables the background of the window 
		/// to be captured.
		/// </summary>
		/// <param name="hdcDest">Handle to the destination device context.</param>
		/// <param name="nXDest">
		/// Specifies the x-coordinate, in logical units, of the upper-left corner of
		/// the destination rectangle. 
		/// </param>
		/// <param name="nYDest">
		/// Specifies the y-coordinate, in logical units, of the upper-left corner of
		/// the destination rectangle.
		/// </param>
		/// <param name="nWidth">
		/// Specifies the width, in logical units, of the source and destination
		/// rectangles. 
		/// </param>
		/// <param name="nHeight">
		/// Specifies the height, in logical units, of the source and the destination
		/// rectangles.
		/// </param>
		/// <param name="hdcSrc">Handle to the source device context.</param>
		/// <param name="nXSrc">
		/// the x-coordinate, in logical units, of the upper-left corner of the source
		/// rectangle.
		/// </param>
		/// <param name="nYSrc">
		/// Specifies the y-coordinate, in logical units, of the upper-left corner of
		/// the source rectangle.
		/// </param>
		/// <param name="dwRop">
		/// Specifies a raster-operation code. These codes define how the color data
		/// for the source rectangle is to be combined with the color data for the
		/// destination rectangle to achieve the final color. </param>
		/// <returns>Nonzero if the function succeeds; otherwise, zero.</returns>
		[DllImport("gdi32.dll")] 
		private static extern bool BitBlt( 
			IntPtr hdcDest, // handle to destination DC 
			int nXDest, // x-coord of destination upper-left corner 
			int nYDest, // y-coord of destination upper-left corner 
			int nWidth, // width of destination rectangle 
			int nHeight, // height of destination rectangle 
			IntPtr hdcSrc, // handle to source DC 
			int nXSrc, // x-coordinate of source upper-left corner 
			int nYSrc, // y-coordinate of source upper-left corner 
			System.Int32 dwRop // raster operation code 
			);
		#endregion

		#region Device Contexts
		/// <summary>
		/// Imports the GDI GetDC function that retrieves a handle to a display device
		/// context (DC) for the client area of a specified window or for the entire screen. 
		/// </summary>
		/// <param name="hWnd">
		/// Handle to the window whose DC is to be retrieved. If this value is NULL, GetDC
		/// retrieves the DC for the entire screen.
		/// </param>
		/// <returns>
		/// A handle to the DC for the specified window's client area if the function succeeds;
		/// otherwise, <c>null</c>.
		/// </returns>
		[DllImport("User32.dll")] 
		public extern static System.IntPtr GetDC(System.IntPtr hWnd); 

		/// <summary>
		/// Imports the GDI ReleaseDC function that releases a device context (DC), freeing it
		/// for use by other applications.
		/// </summary>
		/// <param name="hWnd">Handle to the window whose DC is to be released.</param>
		/// <param name="hDC">Handle to the DC to be released.</param>
		/// <returns><c>1</c> if the DC was released; otherwise, zero.</returns>
		[DllImport("User32.dll")] 
		public extern static int ReleaseDC(System.IntPtr hWnd, System.IntPtr hDC); //modified to include hWnd
		#endregion
	}
}
