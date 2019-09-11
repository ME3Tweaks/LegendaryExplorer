#ifndef BEST_FIT_OBB_H

#define BEST_FIT_OBB_H

/*!  
** 
** Copyright (c) 2007 by John W. Ratcliff mailto:jratcliff@infiniplex.net
**
** Portions of this source has been released with the PhysXViewer application, as well as 
** Rocket, CreateDynamics, ODF, and as a number of sample code snippets.
**
** If you find this code useful or you are feeling particularily generous I would
** ask that you please go to http://www.amillionpixels.us and make a donation
** to Troy DeMolay.
**
** DeMolay is a youth group for young men between the ages of 12 and 21.  
** It teaches strong moral principles, as well as leadership skills and 
** public speaking.  The donations page uses the 'pay for pixels' paradigm
** where, in this case, a pixel is only a single penny.  Donations can be
** made for as small as $4 or as high as a $100 block.  Each person who donates
** will get a link to their own site as well as acknowledgement on the
** donations blog located here http://www.amillionpixels.blogspot.com/
**
** If you wish to contact me you can use the following methods:
**
** Skype Phone: 636-486-4040 (let it ring a long time while it goes through switches)
** Skype ID: jratcliff63367
** Yahoo: jratcliff63367
** AOL: jratcliff1961
** email: jratcliff@infiniplex.net
** Personal website: http://jratcliffscarab.blogspot.com
** Coding Website:   http://codesuppository.blogspot.com
** FundRaising Blog: http://amillionpixels.blogspot.com
** Fundraising site: http://www.amillionpixels.us
** New Temple Site:  http://newtemple.blogspot.com
**
**
** The MIT license:
**
** Permission is hereby granted, free of charge, to any person obtaining a copy 
** of this software and associated documentation files (the "Software"), to deal 
** in the Software without restriction, including without limitation the rights 
** to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
** copies of the Software, and to permit persons to whom the Software is furnished 
** to do so, subject to the following conditions:
**
** The above copyright notice and this permission notice shall be included in all 
** copies or substantial portions of the Software.

** THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
** IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
** FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
** AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
** WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN 
** CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

*/



// compute the 'best fit' oriented bounding box of an input point cloud by doing an exhaustive search.
// it spins the point cloud around searching for the minimal volume.  It keeps narrowing down until
// it fails to find a better fit.  The only dependency is on 'double_math'
//
// The inputs are:
//
//         vcount    : number of input vertices in the point cloud.
//         points    : a pointer to the first vertex.
//         pstride   : The stride between each point measured in bytes.
//
// The outputs are:
//
//         sides     : The length of the sides of the OBB as X, Y, Z distance.
//         matrix    : A pointer to a 4x4 matrix.  This will contain the 3x3 rotation and the translation component.
//         pos       : The center of the OBB
//         quat      : The orientation of the OBB expressed as quaternion in the form of X,Y,Z,W
//
//
// Please email bug fixes or improvements to John W. Ratcliff at mailto:jratcliff@infiniplex.net
//
// If you find this source code useful donate a couple of bucks to my kid's fund raising website at
//  www.amillionpixels.us
//
// More snippets at: www.codesuppository.com
//

namespace ConvexDecomposition
{

void computeBestFitOBB(unsigned int vcount,const double *points,unsigned int pstride,double *sides,double *matrix);
void computeBestFitOBB(unsigned int vcount,const double *points,unsigned int pstride,double *sides,double *pos,double *quat);
void computeBestFitABB(unsigned int vcount,const double *points,unsigned int pstride,double *sides,double *pos);


void computeBestFitOBB(unsigned int vcount,const float *points,unsigned int pstride,float *sides,float *pos,float *quat); // the float version of the routine.
void computeBestFitABB(unsigned int vcount,const float *points,unsigned int pstride,float *sides,float *pos);

};

#endif
