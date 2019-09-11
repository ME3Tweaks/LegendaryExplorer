#include <stdio.h>
#include <stdlib.h>
#include <string.h>
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



#include "planetri.h"

namespace ConvexDecomposition
{


static inline double DistToPt(const double *p,const double *plane)
{
	double x = p[0];
	double y = p[1];
	double z = p[2];
	double d = x*plane[0] + y*plane[1] + z*plane[2] + plane[3];
	return d;
}


static PlaneTriResult getSidePlane(const double *p,const double *plane,double epsilon)
{

  double d = DistToPt(p,plane);

  if ( (d+epsilon) > 0 )
		return PTR_FRONT; // it is 'in front' within the provided epsilon value.

  return PTR_BACK;
}

static void add(const double *p,double *dest,unsigned int tstride,unsigned int &pcount)
{
  char *d = (char *) dest;
  d = d + pcount*tstride;
  dest = (double *) d;
  dest[0] = p[0];
  dest[1] = p[1];
  dest[2] = p[2];
  pcount++;
	assert( pcount <= 4 );
}


// assumes that the points are on opposite sides of the plane!
static void intersect(const double *p1,const double *p2,double *split,const double *plane)
{

  double dp1 = DistToPt(p1,plane);
  double dp2 = DistToPt(p2,plane);

  double dir[3];

  dir[0] = p2[0] - p1[0];
  dir[1] = p2[1] - p1[1];
  dir[2] = p2[2] - p1[2];

  double dot1 = dir[0]*plane[0] + dir[1]*plane[1] + dir[2]*plane[2];
  double dot2 = dp1 - plane[3];

  double    t = -(plane[3] + dot2 ) / dot1;

  split[0] = (dir[0]*t)+p1[0];
  split[1] = (dir[1]*t)+p1[1];
  split[2] = (dir[2]*t)+p1[2];

}

#define MAXPTS 256

class point
{
public:

  void set(const double *p)
  {
    x = p[0];
    y = p[1];
    z = p[2];
  }

  double x;
  double y;
  double z;
};
class polygon
{
public:
  polygon(void)
  {
    mVcount = 0;
  }

  polygon(const double *p1,const double *p2,const double *p3)
  {
    mVcount = 3;
    mVertices[0].set(p1);
    mVertices[1].set(p2);
    mVertices[2].set(p3);
  }


  int NumVertices(void) const { return mVcount; };

  const point& Vertex(int index)
  {
    if ( index < 0 ) index+=mVcount;
    return mVertices[index];
  };


  void set(const point *pts,int count)
  {
    for (int i=0; i<count; i++)
    {
      mVertices[i] = pts[i];
    }
    mVcount = count;
  }

  int   mVcount;
  point mVertices[MAXPTS];
};

class plane
{
public:
  plane(const double *p)
  {
    normal.x = p[0];
    normal.y = p[1];
    normal.z = p[2];
    D        = p[3];
  }

  double Classify_Point(const point &p)
  {
    return p.x*normal.x + p.y*normal.y + p.z*normal.z + D;
  }

  point normal;
  double D;
};

void Split_Polygon(polygon *poly, plane *part, polygon &front, polygon &back)
{
  int   count = poly->NumVertices ();
  int   out_c = 0, in_c = 0;
  point ptA, ptB,outpts[MAXPTS],inpts[MAXPTS];
  double sideA, sideB;
  ptA = poly->Vertex (count - 1);
  sideA = part->Classify_Point (ptA);
  for (int i = -1; ++i < count;)
  {
    ptB = poly->Vertex(i);
    sideB = part->Classify_Point(ptB);
    if (sideB > 0)
    {
      if (sideA < 0)
      {
			  point v;
        intersect(&ptB.x, &ptA.x, &v.x, &part->normal.x );
        outpts[out_c++] = inpts[in_c++] = v;
      }
      outpts[out_c++] = ptB;
    }
    else if (sideB < 0)
    {
      if (sideA > 0)
      {
        point v;
        intersect(&ptB.x, &ptA.x, &v.x, &part->normal.x );
        outpts[out_c++] = inpts[in_c++] = v;
      }
      inpts[in_c++] = ptB;
    }
    else
       outpts[out_c++] = inpts[in_c++] = ptB;
    ptA = ptB;
    sideA = sideB;
  }

  front.set(&outpts[0], out_c);
  back.set(&inpts[0], in_c);
}

PlaneTriResult planeTriIntersection(const double *_plane,    // the plane equation in Ax+By+Cz+D format
                                    const double *triangle, // the source triangle.
                                    unsigned int tstride,  // stride in bytes of the input and output *vertices*
                                    double        epsilon,  // the co-planer epsilon value.
                                    double       *front,    // the triangle in front of the
                                    unsigned int &fcount,  // number of vertices in the 'front' triangle
                                    double       *back,     // the triangle in back of the plane
                                    unsigned int &bcount) // the number of vertices in the 'back' triangle.
{

  fcount = 0;
  bcount = 0;

  const char *tsource = (const char *) triangle;

  // get the three vertices of the triangle.
  const double *p1     = (const double *) (tsource);
  const double *p2     = (const double *) (tsource+tstride);
  const double *p3     = (const double *) (tsource+tstride*2);


  PlaneTriResult r1   = getSidePlane(p1,_plane,epsilon); // compute the side of the plane each vertex is on
  PlaneTriResult r2   = getSidePlane(p2,_plane,epsilon);
  PlaneTriResult r3   = getSidePlane(p3,_plane,epsilon);

  if ( r1 == r2 && r1 == r3 ) // if all three vertices are on the same side of the plane.
  {
    if ( r1 == PTR_FRONT ) // if all three are in front of the plane, then copy to the 'front' output triangle.
    {
      add(p1,front,tstride,fcount);
      add(p2,front,tstride,fcount);
      add(p3,front,tstride,fcount);
    }
    else
    {
      add(p1,back,tstride,bcount); // if all three are in 'back' then copy to the 'back' output triangle.
      add(p2,back,tstride,bcount);
      add(p3,back,tstride,bcount);
    }
    return r1; // if all three points are on the same side of the plane return result
  }


  polygon pi(p1,p2,p3);
  polygon  pfront,pback;

  plane    part(_plane);
  Split_Polygon(&pi,&part,pfront,pback);

  for (int i=0; i<pfront.mVcount; i++)
  {
    add( &pfront.mVertices[i].x, front, tstride, fcount );
  }

  for (int i=0; i<pback.mVcount; i++)
  {
    add( &pback.mVertices[i].x, back, tstride, bcount );
  }

  PlaneTriResult ret = PTR_SPLIT;

  if ( fcount == 0 && bcount )
    ret = PTR_BACK;

  if ( bcount == 0 && fcount )
    ret = PTR_FRONT;

  return ret;
}

};
