#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
#include <math.h>

#include "float_math.h"

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

namespace ConvexDecomposition
{

void fm_inverseRT(const double *matrix,const double *pos,double *t) // inverse rotate translate the point.
{

	double _x = pos[0] - matrix[3*4+0];
	double _y = pos[1] - matrix[3*4+1];
	double _z = pos[2] - matrix[3*4+2];

	// Multiply inverse-translated source vector by inverted rotation transform

	t[0] = (matrix[0*4+0] * _x) + (matrix[0*4+1] * _y) + (matrix[0*4+2] * _z);
	t[1] = (matrix[1*4+0] * _x) + (matrix[1*4+1] * _y) + (matrix[1*4+2] * _z);
	t[2] = (matrix[2*4+0] * _x) + (matrix[2*4+1] * _y) + (matrix[2*4+2] * _z);

}


void fm_identity(double *matrix) // set 4x4 matrix to identity.
{
	matrix[0*4+0] = 1;
	matrix[1*4+1] = 1;
	matrix[2*4+2] = 1;
	matrix[3*4+3] = 1;

	matrix[1*4+0] = 0;
	matrix[2*4+0] = 0;
	matrix[3*4+0] = 0;

	matrix[0*4+1] = 0;
	matrix[2*4+1] = 0;
	matrix[3*4+1] = 0;

	matrix[0*4+2] = 0;
	matrix[1*4+2] = 0;
	matrix[3*4+2] = 0;

	matrix[0*4+3] = 0;
	matrix[1*4+3] = 0;
	matrix[2*4+3] = 0;

}

void fm_eulerMatrix(double ax,double ay,double az,double *matrix) // convert euler (in radians) to a dest 4x4 matrix (translation set to zero)
{
  double quat[4];
  fm_eulerToQuat(ax,ay,az,quat);
  fm_quatToMatrix(quat,matrix);
}

void fm_getAABB(unsigned int vcount,const double *points,unsigned int pstride,double *bmin,double *bmax)
{

  const unsigned char *source = (const unsigned char *) points;

	bmin[0] = points[0];
	bmin[1] = points[1];
	bmin[2] = points[2];

	bmax[0] = points[0];
	bmax[1] = points[1];
	bmax[2] = points[2];


  for (unsigned int i=1; i<vcount; i++)
  {
  	source+=pstride;
  	const double *p = (const double *) source;

  	if ( p[0] < bmin[0] ) bmin[0] = p[0];
  	if ( p[1] < bmin[1] ) bmin[1] = p[1];
  	if ( p[2] < bmin[2] ) bmin[2] = p[2];

		if ( p[0] > bmax[0] ) bmax[0] = p[0];
		if ( p[1] > bmax[1] ) bmax[1] = p[1];
		if ( p[2] > bmax[2] ) bmax[2] = p[2];

  }
}


void fm_eulerToQuat(double roll,double pitch,double yaw,double *quat) // convert euler angles to quaternion.
{
	roll  *= 0.5f;
	pitch *= 0.5f;
	yaw   *= 0.5f;

	double cr = cos(roll);
	double cp = cos(pitch);
	double cy = cos(yaw);

	double sr = sin(roll);
	double sp = sin(pitch);
	double sy = sin(yaw);

	double cpcy = cp * cy;
	double spsy = sp * sy;
	double spcy = sp * cy;
	double cpsy = cp * sy;

	quat[0]   = ( sr * cpcy - cr * spsy);
	quat[1]   = ( cr * spcy + sr * cpsy);
	quat[2]   = ( cr * cpsy - sr * spcy);
	quat[3]   = cr * cpcy + sr * spsy;
}

void fm_quatToMatrix(const double *quat,double *matrix) // convert quaterinion rotation to matrix, zeros out the translation component.
{

	double xx = quat[0]*quat[0];
	double yy = quat[1]*quat[1];
	double zz = quat[2]*quat[2];
	double xy = quat[0]*quat[1];
	double xz = quat[0]*quat[2];
	double yz = quat[1]*quat[2];
	double wx = quat[3]*quat[0];
	double wy = quat[3]*quat[1];
	double wz = quat[3]*quat[2];

	matrix[0*4+0] = 1 - 2 * ( yy + zz );
	matrix[1*4+0] =     2 * ( xy - wz );
	matrix[2*4+0] =     2 * ( xz + wy );

	matrix[0*4+1] =     2 * ( xy + wz );
	matrix[1*4+1] = 1 - 2 * ( xx + zz );
	matrix[2*4+1] =     2 * ( yz - wx );

	matrix[0*4+2] =     2 * ( xz - wy );
	matrix[1*4+2] =     2 * ( yz + wx );
	matrix[2*4+2] = 1 - 2 * ( xx + yy );

	matrix[3*4+0] =(double) matrix[3*4+1] = matrix[3*4+2] = 0.0f;
	matrix[0*4+3] =(double) matrix[1*4+3] = matrix[2*4+3] = 0.0f;
	matrix[3*4+3] =(double) 1.0f;

}


void fm_quatRotate(const double *quat,const double *v,double *r) // rotate a vector directly by a quaternion.
{
  double left[4];

	left[0] =   quat[3]*v[0] + quat[1]*v[2] - v[1]*quat[2];
	left[1] =   quat[3]*v[1] + quat[2]*v[0] - v[2]*quat[0];
	left[2] =   quat[3]*v[2] + quat[0]*v[1] - v[0]*quat[1];
	left[3] = - quat[0]*v[0] - quat[1]*v[1] - quat[2]*v[2];

	r[0] = (left[3]*-quat[0]) + (quat[3]*left[0]) + (left[1]*-quat[2]) - (-quat[1]*left[2]);
	r[1] = (left[3]*-quat[1]) + (quat[3]*left[1]) + (left[2]*-quat[0]) - (-quat[2]*left[0]);
	r[2] = (left[3]*-quat[2]) + (quat[3]*left[2]) + (left[0]*-quat[1]) - (-quat[0]*left[1]);

}


void fm_getTranslation(const double *matrix,double *t)
{
	t[0] = matrix[3*4+0];
	t[1] = matrix[3*4+1];
	t[2] = matrix[3*4+2];
}

void fm_matrixToQuat(const double *matrix,double *quat) // convert the 3x3 portion of a 4x4 matrix into a quaterion as x,y,z,w
{

	double tr = matrix[0*4+0] + matrix[1*4+1] + matrix[2*4+2];

	// check the diagonal

	if (tr > 0.0f )
	{
		double s = (double) sqrt ( (double) (tr + 1.0f) );
		quat[3] = s * 0.5f;
		s = 0.5f / s;
		quat[0] = (matrix[1*4+2] - matrix[2*4+1]) * s;
		quat[1] = (matrix[2*4+0] - matrix[0*4+2]) * s;
		quat[2] = (matrix[0*4+1] - matrix[1*4+0]) * s;

	}
	else
	{
		// diagonal is negative
		int nxt[3] = {1, 2, 0};
		double  qa[4];

		int i = 0;

		if (matrix[1*4+1] > matrix[0*4+0]) i = 1;
		if (matrix[2*4+2] > matrix[i*4+i]) i = 2;

		int j = nxt[i];
		int k = nxt[j];

		double s = sqrt ( ((matrix[i*4+i] - (matrix[j*4+j] + matrix[k*4+k])) + 1.0f) );

		qa[i] = s * 0.5f;

		if (s != 0.0f ) s = 0.5f / s;

		qa[3] = (matrix[j*4+k] - matrix[k*4+j]) * s;
		qa[j] = (matrix[i*4+j] + matrix[j*4+i]) * s;
		qa[k] = (matrix[i*4+k] + matrix[k*4+i]) * s;

		quat[0] = qa[0];
		quat[1] = qa[1];
		quat[2] = qa[2];
		quat[3] = qa[3];
	}


}


double fm_sphereVolume(double radius) // return's the volume of a sphere of this radius (4/3 PI * R cubed )
{
	return (4.0f / 3.0f ) * FM_PI * radius * radius * radius;
}


double fm_cylinderVolume(double radius,double h)
{
	return FM_PI * radius * radius *h;
}

double fm_capsuleVolume(double radius,double h)
{
	double volume = fm_sphereVolume(radius); // volume of the sphere portion.
	double ch = h-radius*2; // this is the cylinder length
	if ( ch > 0 )
	{
		volume+=fm_cylinderVolume(radius,ch);
	}
	return volume;
}

void  fm_transform(const double *matrix,const double *v,double *t) // rotate and translate this point
{
  t[0] = (matrix[0*4+0] * v[0]) +  (matrix[1*4+0] * v[1]) + (matrix[2*4+0] * v[2]) + matrix[3*4+0];
  t[1] = (matrix[0*4+1] * v[0]) +  (matrix[1*4+1] * v[1]) + (matrix[2*4+1] * v[2]) + matrix[3*4+1];
  t[2] = (matrix[0*4+2] * v[0]) +  (matrix[1*4+2] * v[1]) + (matrix[2*4+2] * v[2]) + matrix[3*4+2];
}

void  fm_rotate(const double *matrix,const double *v,double *t) // rotate and translate this point
{
  t[0] = (matrix[0*4+0] * v[0]) +  (matrix[1*4+0] * v[1]) + (matrix[2*4+0] * v[2]);
  t[1] = (matrix[0*4+1] * v[0]) +  (matrix[1*4+1] * v[1]) + (matrix[2*4+1] * v[2]);
  t[2] = (matrix[0*4+2] * v[0]) +  (matrix[1*4+2] * v[1]) + (matrix[2*4+2] * v[2]);
}


double fm_distance(const double *p1,const double *p2)
{
	double dx = p1[0] - p2[0];
	double dy = p1[1] - p2[1];
	double dz = p1[2] - p2[2];

	return sqrt( dx*dx + dy*dy + dz *dz );
}

double fm_distanceSquared(const double *p1,const double *p2)
{
	double dx = p1[0] - p2[0];
	double dy = p1[1] - p2[1];
	double dz = p1[2] - p2[2];

	return dx*dx + dy*dy + dz *dz;
}


double fm_computePlane(const double *A,const double *B,const double *C,double *n) // returns D
{
	double vx = (B[0] - C[0]);
	double vy = (B[1] - C[1]);
	double vz = (B[2] - C[2]);

	double wx = (A[0] - B[0]);
	double wy = (A[1] - B[1]);
	double wz = (A[2] - B[2]);

	double vw_x = vy * wz - vz * wy;
	double vw_y = vz * wx - vx * wz;
	double vw_z = vx * wy - vy * wx;

	double mag = sqrt((vw_x * vw_x) + (vw_y * vw_y) + (vw_z * vw_z));

	if ( mag < 0.000001f )
	{
		mag = 0;
	}
	else
	{
		mag = 1.0f/mag;
	}

	double x = vw_x * mag;
	double y = vw_y * mag;
	double z = vw_z * mag;


	double D = 0.0f - ((x*A[0])+(y*A[1])+(z*A[2]));

  n[0] = x;
  n[1] = y;
  n[2] = z;

	return D;
}

double fm_distToPlane(const double *plane,const double *p) // computes the distance of this point from the plane.
{
  return p[0]*plane[0]+p[1]*plane[1]+p[2]*plane[2]+plane[3];
}

double fm_dot(const double *p1,const double *p2)
{
  return p1[0]*p2[0]+p1[1]*p2[2]+p1[2]*p2[2];
}

void fm_cross(double *cross,const double *a,const double *b)
{
	cross[0] = a[1]*b[2] - a[2]*b[1];
	cross[1] = a[2]*b[0] - a[0]*b[2];
	cross[2] = a[0]*b[1] - a[1]*b[0];
}

void fm_computeNormalVector(double *n,const double *p1,const double *p2)
{
  n[0] = p2[0] - p1[0];
  n[1] = p2[1] - p1[1];
  n[2] = p2[2] - p1[2];
  fm_normalize(n);
}

bool  fm_computeWindingOrder(const double *p1,const double *p2,const double *p3) // returns true if the triangle is clockwise.
{
  bool ret = false;

  double v1[3];
  double v2[3];

  fm_computeNormalVector(v1,p1,p2); // p2-p1 (as vector) and then normalized
  fm_computeNormalVector(v2,p1,p3); // p3-p1 (as vector) and then normalized

  double cross[3];

  fm_cross(cross, v1, v2 );
  double ref[3] = { 1, 0, 0 };

  double d = fm_dot( cross, ref );


  if ( d <= 0 )
    ret = false;
  else
    ret = true;

  return ret;
}

void  fm_normalize(double *n) // normalize this vector
{

  double dist = n[0]*n[0] + n[1]*n[1] + n[2]*n[2];
  double mag = 0;

  if ( dist > 0.0000001f )
    mag = 1.0f / sqrt(dist);

  n[0]*=mag;
  n[1]*=mag;
  n[2]*=mag;

}

}; // end of namespace
