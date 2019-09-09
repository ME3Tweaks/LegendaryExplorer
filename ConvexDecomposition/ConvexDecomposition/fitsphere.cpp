#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
#include <math.h>

#include "fitsphere.h"


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




/*
An Efficient Bounding Sphere
by Jack Ritter
from "Graphics Gems", Academic Press, 1990
*/

/* Routine to calculate tight bounding sphere over    */
/* a set of points in 3D */
/* This contains the routine find_bounding_sphere(), */
/* the struct definition, and the globals used for parameters. */
/* The abs() of all coordinates must be < BIGNUMBER */
/* Code written by Jack Ritter and Lyle Rains. */

namespace ConvexDecomposition
{

#define BIGNUMBER 100000000.0  		/* hundred million */

static inline void Set(double *n,double x,double y,double z)
{
	n[0] = x;
	n[1] = y;
	n[2] = z;
};

static inline void Copy(double *dest,const double *source)
{
	dest[0] = source[0];
	dest[1] = source[1];
	dest[2] = source[2];
}

double computeBoundingSphere(unsigned int vcount,const double *points,double *center)
{

  double mRadius;
  double mRadius2;

	double xmin[3];
	double xmax[3];
	double ymin[3];
	double ymax[3];
	double zmin[3];
	double zmax[3];
	double dia1[3];
	double dia2[3];

  /* FIRST PASS: find 6 minima/maxima points */
  Set(xmin,BIGNUMBER,BIGNUMBER,BIGNUMBER);
  Set(xmax,-BIGNUMBER,-BIGNUMBER,-BIGNUMBER);
  Set(ymin,BIGNUMBER,BIGNUMBER,BIGNUMBER);
  Set(ymax,-BIGNUMBER,-BIGNUMBER,-BIGNUMBER);
  Set(zmin,BIGNUMBER,BIGNUMBER,BIGNUMBER);
  Set(zmax,-BIGNUMBER,-BIGNUMBER,-BIGNUMBER);

  for (unsigned i=0; i<vcount; i++)
	{
		const double *caller_p = &points[i*3];

   	if (caller_p[0]<xmin[0])
  	  Copy(xmin,caller_p); /* New xminimum point */
  	if (caller_p[0]>xmax[0])
  	  Copy(xmax,caller_p);
  	if (caller_p[1]<ymin[1])
  	  Copy(ymin,caller_p);
  	if (caller_p[1]>ymax[1])
  	  Copy(ymax,caller_p);
  	if (caller_p[2]<zmin[2])
  	  Copy(zmin,caller_p);
  	if (caller_p[2]>zmax[2])
  	  Copy(zmax,caller_p);
	}

  /* Set xspan = distance between the 2 points xmin & xmax (squared) */
  double dx = xmax[0] - xmin[0];
  double dy = xmax[1] - xmin[1];
  double dz = xmax[2] - xmin[2];
  double xspan = dx*dx + dy*dy + dz*dz;

  /* Same for y & z spans */
  dx = ymax[0] - ymin[0];
  dy = ymax[1] - ymin[1];
  dz = ymax[2] - ymin[2];
  double yspan = dx*dx + dy*dy + dz*dz;

  dx = zmax[0] - zmin[0];
  dy = zmax[1] - zmin[1];
  dz = zmax[2] - zmin[2];
  double zspan = dx*dx + dy*dy + dz*dz;

  /* Set points dia1 & dia2 to the maximally separated pair */
  Copy(dia1,xmin);
  Copy(dia2,xmax); /* assume xspan biggest */
  double maxspan = xspan;

  if (yspan>maxspan)
	{
	  maxspan = yspan;
  	Copy(dia1,ymin);
  	Copy(dia2,ymax);
	}

  if (zspan>maxspan)
	{
	  Copy(dia1,zmin);
	  Copy(dia2,zmax);
	}


  /* dia1,dia2 is a diameter of initial sphere */
  /* calc initial center */
  center[0] = (dia1[0]+dia2[0])*0.5f;
  center[1] = (dia1[1]+dia2[1])*0.5f;
  center[2] = (dia1[2]+dia2[2])*0.5f;

  /* calculate initial radius**2 and radius */

  dx = dia2[0]-center[0]; /* x component of radius vector */
  dy = dia2[1]-center[1]; /* y component of radius vector */
  dz = dia2[2]-center[2]; /* z component of radius vector */

  mRadius2 = dx*dx + dy*dy + dz*dz;
  mRadius = double(sqrt(mRadius2));

  /* SECOND PASS: increment current sphere */
  if ( 1 )
  {
	  for (unsigned i=0; i<vcount; i++)
		{
			const double *caller_p = &points[i*3];

  		dx = caller_p[0]-center[0];
		  dy = caller_p[1]-center[1];
  		dz = caller_p[2]-center[2];

		  double old_to_p_sq = dx*dx + dy*dy + dz*dz;

  		if (old_to_p_sq > mRadius2) 	/* do r**2 test first */
			{ 	/* this point is outside of current sphere */
	  		double old_to_p = double(sqrt(old_to_p_sq));
			  /* calc radius of new sphere */
  			mRadius = (mRadius + old_to_p) * 0.5f;
	  		mRadius2 = mRadius*mRadius; 	/* for next r**2 compare */
  			double old_to_new = old_to_p - mRadius;

	  		/* calc center of new sphere */

		  double recip = 1.0f /old_to_p;

  			double cx = (mRadius*center[0] + old_to_new*caller_p[0]) * recip;
	  		double cy = (mRadius*center[1] + old_to_new*caller_p[1]) * recip;
			  double cz = (mRadius*center[2] + old_to_new*caller_p[2]) * recip;

		  Set(center,cx,cy,cz);
			}
		}
  }

  return mRadius;
}

static inline void Set(float *n,float x,float y,float z)
{
	n[0] = x;
	n[1] = y;
	n[2] = z;
};

static inline void Copy(float *dest,const float *source)
{
	dest[0] = source[0];
	dest[1] = source[1];
	dest[2] = source[2];
}



float  computeBoundingSphere(unsigned int vcount,const float *points,float *center)
{
  float mRadius;
  float mRadius2;

	float xmin[3];
	float xmax[3];
	float ymin[3];
	float ymax[3];
	float zmin[3];
	float zmax[3];
	float dia1[3];
	float dia2[3];

  /* FIRST PASS: find 6 minima/maxima points */
  Set(xmin,BIGNUMBER,BIGNUMBER,BIGNUMBER);
  Set(xmax,-BIGNUMBER,-BIGNUMBER,-BIGNUMBER);
  Set(ymin,BIGNUMBER,BIGNUMBER,BIGNUMBER);
  Set(ymax,-BIGNUMBER,-BIGNUMBER,-BIGNUMBER);
  Set(zmin,BIGNUMBER,BIGNUMBER,BIGNUMBER);
  Set(zmax,-BIGNUMBER,-BIGNUMBER,-BIGNUMBER);

  for (unsigned i=0; i<vcount; i++)
	{
		const float *caller_p = &points[i*3];

   	if (caller_p[0]<xmin[0])
  	  Copy(xmin,caller_p); /* New xminimum point */
  	if (caller_p[0]>xmax[0])
  	  Copy(xmax,caller_p);
  	if (caller_p[1]<ymin[1])
  	  Copy(ymin,caller_p);
  	if (caller_p[1]>ymax[1])
  	  Copy(ymax,caller_p);
  	if (caller_p[2]<zmin[2])
  	  Copy(zmin,caller_p);
  	if (caller_p[2]>zmax[2])
  	  Copy(zmax,caller_p);
	}

  /* Set xspan = distance between the 2 points xmin & xmax (squared) */
  float dx = xmax[0] - xmin[0];
  float dy = xmax[1] - xmin[1];
  float dz = xmax[2] - xmin[2];
  float xspan = dx*dx + dy*dy + dz*dz;

  /* Same for y & z spans */
  dx = ymax[0] - ymin[0];
  dy = ymax[1] - ymin[1];
  dz = ymax[2] - ymin[2];
  float yspan = dx*dx + dy*dy + dz*dz;

  dx = zmax[0] - zmin[0];
  dy = zmax[1] - zmin[1];
  dz = zmax[2] - zmin[2];
  float zspan = dx*dx + dy*dy + dz*dz;

  /* Set points dia1 & dia2 to the maximally separated pair */
  Copy(dia1,xmin);
  Copy(dia2,xmax); /* assume xspan biggest */
  float maxspan = xspan;

  if (yspan>maxspan)
	{
	  maxspan = yspan;
  	Copy(dia1,ymin);
  	Copy(dia2,ymax);
	}

  if (zspan>maxspan)
	{
	  Copy(dia1,zmin);
	  Copy(dia2,zmax);
	}


  /* dia1,dia2 is a diameter of initial sphere */
  /* calc initial center */
  center[0] = (dia1[0]+dia2[0])*0.5f;
  center[1] = (dia1[1]+dia2[1])*0.5f;
  center[2] = (dia1[2]+dia2[2])*0.5f;

  /* calculate initial radius**2 and radius */

  dx = dia2[0]-center[0]; /* x component of radius vector */
  dy = dia2[1]-center[1]; /* y component of radius vector */
  dz = dia2[2]-center[2]; /* z component of radius vector */

  mRadius2 = dx*dx + dy*dy + dz*dz;
  mRadius = float(sqrt(mRadius2));

  /* SECOND PASS: increment current sphere */
  if ( 1 )
  {
	  for (unsigned i=0; i<vcount; i++)
		{
			const float *caller_p = &points[i*3];

  		dx = caller_p[0]-center[0];
		  dy = caller_p[1]-center[1];
  		dz = caller_p[2]-center[2];

		  float old_to_p_sq = dx*dx + dy*dy + dz*dz;

  		if (old_to_p_sq > mRadius2) 	/* do r**2 test first */
			{ 	/* this point is outside of current sphere */
	  		float old_to_p = float(sqrt(old_to_p_sq));
			  /* calc radius of new sphere */
  			mRadius = (mRadius + old_to_p) * 0.5f;
	  		mRadius2 = mRadius*mRadius; 	/* for next r**2 compare */
  			float old_to_new = old_to_p - mRadius;

	  		/* calc center of new sphere */

		  float recip = 1.0f /old_to_p;

  			float cx = (mRadius*center[0] + old_to_new*caller_p[0]) * recip;
	  		float cy = (mRadius*center[1] + old_to_new*caller_p[1]) * recip;
			  float cz = (mRadius*center[2] + old_to_new*caller_p[2]) * recip;

		  Set(center,cx,cy,cz);
			}
		}
  }

  return mRadius;
}


double computeSphereVolume(double r)
{
	return (4.0f*3.141592654f*r*r*r)/3.0f;  // 4/3 PI R cubed
}

};
