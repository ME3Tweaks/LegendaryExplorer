#ifndef FLOAT_MATH_H

#define FLOAT_MATH_H

namespace ConvexDecomposition
{

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



// a set of routines that let you do common 3d math
// operations without any vector, matrix, or quaternion
// classes or templates.
//
// a vector (or point) is a 'double *' to 3 doubleing point numbers.
// a matrix is a 'double *' to an array of 16 doubleing point numbers representing a 4x4 transformation matrix compatible with D3D or OGL
// a quaternion is a 'double *' to 4 doubles representing a quaternion x,y,z,w
//
//
//
// Please email bug fixes or improvements to John W. Ratcliff at mailto:jratcliff@infiniplex.net
//
// If you find this source code useful donate a couple of bucks to my kid's fund raising website at
//  www.amillionpixels.us
//
// More snippets at: www.codesuppository.com
//

const double FM_PI = 3.141592654f;
const double FM_DEG_TO_RAD = ((2.0f * FM_PI) / 360.0f);
const double FM_RAD_TO_DEG = (360.0f / (2.0f * FM_PI));

void  fm_identity(double *matrix); // set 4x4 matrix to identity.
void  fm_inverseRT(const double *matrix,const double *pos,double *t); // inverse rotate translate the point.
void  fm_transform(const double *matrix,const double *pos,double *t); // rotate and translate this point.
void  fm_rotate(const double *matrix,const double *pos,double *t); // only rotate the point by a 4x4 matrix, don't translate.
void  fm_eulerMatrix(double ax,double ay,double az,double *matrix); // convert euler (in radians) to a dest 4x4 matrix (translation set to zero)
void  fm_getAABB(unsigned int vcount,const double *points,unsigned int pstride,double *bmin,double *bmax);
void  fm_eulerToQuat(double roll,double pitch,double yaw,double *quat); // convert euler angles to quaternion.
void  fm_quatToMatrix(const double *quat,double *matrix); // convert quaterinion rotation to matrix, translation set to zero.
void  fm_quatRotate(const double *quat,const double *v,double *r); // rotate a vector directly by a quaternion.
void  fm_getTranslation(const double *matrix,double *t);
void  fm_matrixToQuat(const double *matrix,double *quat); // convert the 3x3 portion of a 4x4 matrix into a quaterion as x,y,z,w
double fm_sphereVolume(double radius); // return's the volume of a sphere of this radius (4/3 PI * R cubed )
double fm_cylinderVolume(double radius,double h);
double fm_capsuleVolume(double radius,double h);
double fm_distance(const double *p1,const double *p2);
double fm_distanceSquared(const double *p1,const double *p2);
double fm_computePlane(const double *p1,const double *p2,const double *p3,double *n); // return D
double fm_distToPlane(const double *plane,const double *pos); // computes the distance of this point from the plane.
double fm_dot(const double *p1,const double *p2);
void  fm_cross(double *cross,const double *a,const double *b);
void  fm_computeNormalVector(double *n,const double *p1,const double *p2); // as P2-P1 normalized.
bool  fm_computeWindingOrder(const double *p1,const double *p2,const double *p3); // returns true if the triangle is clockwise.
void  fm_normalize(double *n); // normalize this vector

}; // end of nsamepace

#endif
