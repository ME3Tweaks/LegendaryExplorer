#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
#include <float.h>
#include <math.h>

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



#include "splitplane.h"
#include "ConvexDecomposition.h"
#include "cd_vector.h"
#include "cd_hull.h"
#include "cd_wavefront.h"
#include "bestfit.h"
#include "PlaneTri.h"
#include "vlookup.h"
#include "meshvolume.h"
#include "bestfitobb.h"
#include "float_math.h"

namespace ConvexDecomposition
{

static void computePlane(const double *A,const double *B,const double *C,double *plane)
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

  plane[0] = x;
  plane[1] = y;
  plane[2] = z;
  plane[3] = D;

}

class Rect3d
{
public:
  Rect3d(void) { };

  Rect3d(const double *bmin,const double *bmax)
  {

    mMin[0] = bmin[0];
    mMin[1] = bmin[1];
    mMin[2] = bmin[2];

    mMax[0] = bmax[0];
    mMax[1] = bmax[1];
    mMax[2] = bmax[2];

  }

  void SetMin(const double *bmin)
  {
    mMin[0] = bmin[0];
    mMin[1] = bmin[1];
    mMin[2] = bmin[2];
  }

  void SetMax(const double *bmax)
  {
    mMax[0] = bmax[0];
    mMax[1] = bmax[1];
    mMax[2] = bmax[2];
  }

	void SetMin(double x,double y,double z)
	{
		mMin[0] = x;
		mMin[1] = y;
		mMin[2] = z;
	}

	void SetMax(double x,double y,double z)
	{
		mMax[0] = x;
		mMax[1] = y;
		mMax[2] = z;
	}

  double mMin[3];
  double mMax[3];
};

void splitRect(unsigned int axis,
						   const Rect3d &source,
							 Rect3d &b1,
							 Rect3d &b2,
							 const double *midpoint)
{
	switch ( axis )
	{
		case 0:
			b1.SetMin(source.mMin);
			b1.SetMax( midpoint[0], source.mMax[1], source.mMax[2] );

			b2.SetMin( midpoint[0], source.mMin[1], source.mMin[2] );
			b2.SetMax(source.mMax);

			break;
		case 1:
			b1.SetMin(source.mMin);
			b1.SetMax( source.mMax[0], midpoint[1], source.mMax[2] );

			b2.SetMin( source.mMin[0], midpoint[1], source.mMin[2] );
			b2.SetMax(source.mMax);

			break;
		case 2:
			b1.SetMin(source.mMin);
			b1.SetMax( source.mMax[0], source.mMax[1], midpoint[2] );

			b2.SetMin( source.mMin[0], source.mMin[1], midpoint[2] );
			b2.SetMax(source.mMax);

			break;
	}
}

bool computeSplitPlane(unsigned int vcount,
                       const double *vertices,
                       unsigned int tcount,
                       const unsigned int *indices,
                       ConvexDecompInterface *callback,
                       double *plane)
{
  bool cret = false;


  double sides[3];
  double matrix[16];

  computeBestFitOBB( vcount, vertices, sizeof(double)*3, sides, matrix );

  double bmax[3];
  double bmin[3];

  bmax[0] = sides[0]*0.5f;
  bmax[1] = sides[1]*0.5f;
  bmax[2] = sides[2]*0.5f;

  bmin[0] = -bmax[0];
  bmin[1] = -bmax[1];
  bmin[2] = -bmax[2];


  double dx = sides[0];
  double dy = sides[1];
  double dz = sides[2];


	double laxis = dx;

	unsigned int axis = 0;

	if ( dy > dx )
	{
		axis = 1;
		laxis = dy;
	}

	if ( dz > dx && dz > dy )
	{
		axis = 2;
		laxis = dz;
	}

  double p1[3];
  double p2[3];
  double p3[3];

  p3[0] = p2[0] = p1[0] = bmin[0] + dx*0.5f;
  p3[1] = p2[1] = p1[1] = bmin[1] + dy*0.5f;
  p3[2] = p2[2] = p1[2] = bmin[2] + dz*0.5f;

  Rect3d b(bmin,bmax);

  Rect3d b1,b2;

  splitRect(axis,b,b1,b2,p1);


//  callback->ConvexDebugBound(b1.mMin,b1.mMax,0x00FF00);
//  callback->ConvexDebugBound(b2.mMin,b2.mMax,0xFFFF00);

  switch ( axis )
  {
    case 0:
      p2[1] = bmin[1];
      p2[2] = bmin[2];

      if ( dz > dy )
      {
        p3[1] = bmax[1];
        p3[2] = bmin[2];
      }
      else
      {
        p3[1] = bmin[1];
        p3[2] = bmax[2];
      }

      break;
    case 1:
      p2[0] = bmin[0];
      p2[2] = bmin[2];

      if ( dx > dz )
      {
        p3[0] = bmax[0];
        p3[2] = bmin[2];
      }
      else
      {
        p3[0] = bmin[0];
        p3[2] = bmax[2];
      }

      break;
    case 2:
      p2[0] = bmin[0];
      p2[1] = bmin[1];

      if ( dx > dy )
      {
        p3[0] = bmax[0];
        p3[1] = bmin[1];
      }
      else
      {
        p3[0] = bmin[0];
        p3[1] = bmax[1];
      }

      break;
  }

  double tp1[3];
  double tp2[3];
  double tp3[3];

  fm_transform(matrix,p1,tp1);
  fm_transform(matrix,p2,tp2);
  fm_transform(matrix,p3,tp3);

//  callback->ConvexDebugTri(p1,p2,p3,0xFF0000);

	computePlane(tp1,tp2,tp3,plane);

  return true;

}


};
