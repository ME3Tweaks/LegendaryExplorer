#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include <assert.h>

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


#include "bestfitobb.h"
#include "float_math.h"

namespace ConvexDecomposition
{

// computes the OBB for this set of points relative to this transform matrix.
void computeOBB(unsigned int vcount,const double *points,unsigned int pstride,double *sides,double *matrix)
{
  const char *src = (const char *) points;

  double bmin[3] = { 1e9, 1e9, 1e9 };
  double bmax[3] = { -1e9, -1e9, -1e9 };

  for (unsigned int i=0; i<vcount; i++)
  {
    const double *p = (const double *) src;
    double t[3];

    fm_inverseRT(matrix, p, t ); // inverse rotate translate

    if ( t[0] < bmin[0] ) bmin[0] = t[0];
    if ( t[1] < bmin[1] ) bmin[1] = t[1];
    if ( t[2] < bmin[2] ) bmin[2] = t[2];

    if ( t[0] > bmax[0] ) bmax[0] = t[0];
    if ( t[1] > bmax[1] ) bmax[1] = t[1];
    if ( t[2] > bmax[2] ) bmax[2] = t[2];

    src+=pstride;
  }

  double center[3];

  sides[0] = bmax[0]-bmin[0];
  sides[1] = bmax[1]-bmin[1];
  sides[2] = bmax[2]-bmin[2];

  center[0] = sides[0]*0.5f+bmin[0];
  center[1] = sides[1]*0.5f+bmin[1];
  center[2] = sides[2]*0.5f+bmin[2];

  double ocenter[3];

  fm_rotate(matrix,center,ocenter);

  matrix[12]+=ocenter[0];
  matrix[13]+=ocenter[1];
  matrix[14]+=ocenter[2];

}

void computeBestFitOBB(unsigned int vcount,const double *points,unsigned int pstride,double *sides,double *matrix)
{

  double bmin[3];
  double bmax[3];

  fm_getAABB(vcount,points,pstride,bmin,bmax);

  double center[3];

  center[0] = (bmax[0]-bmin[0])*0.5f + bmin[0];
  center[1] = (bmax[1]-bmin[1])*0.5f + bmin[1];
  center[2] = (bmax[2]-bmin[2])*0.5f + bmin[2];

  double ax = 0;
  double ay = 0;
  double az = 0;

  double sweep =  45.0f; // 180 degree sweep on all three axes.
  double steps =  7.0f; // 7 steps on each axis)

  double bestVolume = 1e9;
  double angle[3];

  while ( sweep >= 1 )
  {

    bool found = false;

    double stepsize = sweep / steps;

    for (double x=ax-sweep; x<=ax+sweep; x+=stepsize)
    {
      for (double y=ay-sweep; y<=ay+sweep; y+=stepsize)
      {
        for (double z=az-sweep; z<=az+sweep; z+=stepsize)
        {
          double pmatrix[16];

          fm_eulerMatrix( x*FM_DEG_TO_RAD, y*FM_DEG_TO_RAD, z*FM_DEG_TO_RAD, pmatrix );

          pmatrix[3*4+0] = center[0];
          pmatrix[3*4+1] = center[1];
          pmatrix[3*4+2] = center[2];

          double psides[3];

          computeOBB( vcount, points, pstride, psides, pmatrix );

          double volume = psides[0]*psides[1]*psides[2]; // the volume of the cube

          if ( volume < bestVolume )
          {
            bestVolume = volume;

            sides[0] = psides[0];
            sides[1] = psides[1];
            sides[2] = psides[2];

            angle[0] = ax;
            angle[1] = ay;
            angle[2] = az;

            memcpy(matrix,pmatrix,sizeof(double)*16);
            found = true; // yes, we found an improvement.
          }
        }
      }
    }

    if ( found )
    {

      ax = angle[0];
      ay = angle[1];
      az = angle[2];

      sweep*=0.5f; // sweep 1/2 the distance as the last time.
    }
    else
    {
      break; // no improvement, so just
    }

  }

}


void computeBestFitOBB(unsigned int vcount,const double *points,unsigned int pstride,double *sides,double *pos,double *quat)
{
  double matrix[16];

  computeBestFitOBB(vcount,points,pstride,sides,matrix);
  fm_getTranslation(matrix,pos);
  fm_matrixToQuat(matrix,quat);

}


void computeBestFitABB(unsigned int vcount,const double *points,unsigned int pstride,double *sides,double *pos)
{
	double bmin[3];
	double bmax[3];

  bmin[0] = points[0];
  bmin[1] = points[1];
  bmin[2] = points[2];

  bmax[0] = points[0];
  bmax[1] = points[1];
  bmax[2] = points[2];

	const char *cp = (const char *) points;
	for (unsigned int i=0; i<vcount; i++)
	{
		const double *p = (const double *) cp;

		if ( p[0] < bmin[0] ) bmin[0] = p[0];
		if ( p[1] < bmin[1] ) bmin[1] = p[1];
		if ( p[2] < bmin[2] ) bmin[2] = p[2];

    if ( p[0] > bmax[0] ) bmax[0] = p[0];
    if ( p[1] > bmax[1] ) bmax[1] = p[1];
    if ( p[2] > bmax[2] ) bmax[2] = p[2];

    cp+=pstride;
	}


	sides[0] = bmax[0] - bmin[0];
	sides[1] = bmax[1] - bmin[1];
	sides[2] = bmax[2] - bmin[2];

	pos[0] = bmin[0]+sides[0]*0.5f;
	pos[1] = bmin[1]+sides[1]*0.5f;
	pos[2] = bmin[2]+sides[2]*0.5f;

}


void computeBestFitOBB(unsigned int vcount,const float *points,unsigned int pstride,float *sides,float *pos,float *quat) // the float version of the routine.
{
  double *temp = new double[vcount*3];
  const char *src = (const char *)points;
  double *dest     = temp;
  for (unsigned int i=0; i<vcount; i++)
  {
    const float *s = (const float *) src;
    temp[0] = s[0];
    temp[1] = s[1];
    temp[2] = s[2];
    temp+=3;
    s+=pstride;
  }

  double dsides[3];
  double dpos[3];
  double dquat[3];

  computeBestFitOBB(vcount,temp,sizeof(double)*3,dsides,dpos,dquat);

  if ( sides )
  {
    sides[0] = (float) dsides[0];
    sides[1] = (float) dsides[1];
    sides[2] = (float) dsides[2];
  }
  if ( pos )
  {
    pos[0] = (float) dpos[0];
    pos[1] = (float) dpos[1];
    pos[2] = (float) dpos[2];
  }
  if ( quat )
  {
    quat[0] = (float) dquat[0];
    quat[1] = (float) dquat[1];
    quat[2] = (float) dquat[2];
    quat[3] = (float) dquat[3];
  }

  delete temp;

}

void computeBestFitABB(unsigned int vcount,const float *points,unsigned int pstride,float *sides,float *pos)
{
	float bmin[3];
	float bmax[3];

  bmin[0] = points[0];
  bmin[1] = points[1];
  bmin[2] = points[2];

  bmax[0] = points[0];
  bmax[1] = points[1];
  bmax[2] = points[2];

	const char *cp = (const char *) points;
	for (unsigned int i=0; i<vcount; i++)
	{
		const float *p = (const float *) cp;

		if ( p[0] < bmin[0] ) bmin[0] = p[0];
		if ( p[1] < bmin[1] ) bmin[1] = p[1];
		if ( p[2] < bmin[2] ) bmin[2] = p[2];

    if ( p[0] > bmax[0] ) bmax[0] = p[0];
    if ( p[1] > bmax[1] ) bmax[1] = p[1];
    if ( p[2] > bmax[2] ) bmax[2] = p[2];

    cp+=pstride;
	}


	sides[0] = bmax[0] - bmin[0];
	sides[1] = bmax[1] - bmin[1];
	sides[2] = bmax[2] - bmin[2];

	pos[0] = bmin[0]+sides[0]*0.5f;
	pos[1] = bmin[1]+sides[1]*0.5f;
	pos[2] = bmin[2]+sides[2]*0.5f;

}

};
