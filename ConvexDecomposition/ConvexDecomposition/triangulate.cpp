#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>
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



#include <vector>  // Include STL vector class.

#include "triangulate.h"


namespace ConvexDecomposition
{


class Vec2d
{
public:
	Vec2d(const double *v)
	{
		mX = v[0];
		mY = v[1];
	}
  Vec2d(double x,double y)
  {
    Set(x,y);
  };
  double GetX(void) const { return mX; };
  double GetY(void) const { return mY; };

  void  Set(double x,double y)
  {
    mX = x;
    mY = y;
  };

private:
  double mX;
  double mY;
};// Typedef an STL vector of vertices which are used to represent
// a polygon/contour and a series of triangles.

typedef std::vector< Vec2d > Vec2dVector;

static bool Process(const Vec2dVector &contour,Vec2dVector &result);  // compute area of a contour/polygon
static double Area(const Vec2dVector &contour);  // decide if point Px/Py is inside triangle defined by (Ax,Ay) (Bx,By) (Cx,Cy)
static bool InsideTriangle(double Ax, double Ay,double Bx, double By,double Cx, double Cy,double Px, double Py);
static bool Snip(const Vec2dVector &contour,int u,int v,int w,int n,int *V);

static const double EPSILON=0.0000000001f;

double Area(const Vec2dVector &contour)
{
  int n = contour.size();
  double A=0.0f;
  for(int p=n-1,q=0; q<n; p=q++)
  {
    A+= contour[p].GetX()*contour[q].GetY() - contour[q].GetX()*contour[p].GetY();
  }
  return A*0.5f;
}
/*
  InsideTriangle decides if a point P is Inside of the triangle
  defined by A, B, C.
*/
bool InsideTriangle(double Ax, double Ay,double Bx, double By,double Cx, double Cy,double Px, double Py)
{
  double ax, ay, bx, by, cx, cy, apx, apy, bpx, bpy, cpx, cpy;
  double cCROSSap, bCROSScp, aCROSSbp;  ax = Cx - Bx;  ay = Cy - By;
  bx = Ax - Cx;  by = Ay - Cy;
  cx = Bx - Ax;  cy = By - Ay;
  apx= Px - Ax;  apy= Py - Ay;
  bpx= Px - Bx;  bpy= Py - By;
  cpx= Px - Cx;  cpy= Py - Cy;  aCROSSbp = ax*bpy - ay*bpx;
  cCROSSap = cx*apy - cy*apx;
  bCROSScp = bx*cpy - by*cpx;  return ((aCROSSbp >= 0.0f) && (bCROSScp >= 0.0f) && (cCROSSap >= 0.0f));
};

bool Snip(const Vec2dVector &contour,int u,int v,int w,int n,int *V)
{
  int p;
  double Ax, Ay, Bx, By, Cx, Cy, Px, Py;
  Ax = contour[V[u]].GetX();
  Ay = contour[V[u]].GetY();
  Bx = contour[V[v]].GetX();
  By = contour[V[v]].GetY();
  Cx = contour[V[w]].GetX();
  Cy = contour[V[w]].GetY();
  if ( EPSILON > (((Bx-Ax)*(Cy-Ay)) - ((By-Ay)*(Cx-Ax))) ) return false;  for (p=0;p<n;p++)
  {
    if( (p == u) || (p == v) || (p == w) ) continue;
    Px = contour[V[p]].GetX();
    Py = contour[V[p]].GetY();
    if (InsideTriangle(Ax,Ay,Bx,By,Cx,Cy,Px,Py)) return false;
  }  return true;
}

bool Process(const Vec2dVector &contour,Vec2dVector &result)
{
  /* allocate and initialize list of Vertices in polygon */
  int n = contour.size();
  if ( n < 3 ) return false;  int *V = new int[n];  /* we want a counter-clockwise polygon in V */  if ( 0.0f < Area(contour) )
    for (int v=0; v<n; v++) V[v] = v;
  else
    for(int v=0; v<n; v++) V[v] = (n-1)-v;  int nv = n;  /*  remove nv-2 Vertices, creating 1 triangle every time */

  int count = 2*nv;   /* error detection */

  for(int m=0, v=nv-1; nv>2; )
  {
    /* if we loop, it is probably a non-simple polygon */
    if (0 >= (count--))
    {
      //** Triangulate: ERROR - probable bad polygon!
      return false;
    }    /* three consecutive vertices in current polygon, <u,v,w> */

    int u = v  ; 
		if (nv <= u) u = 0;     /* previous */
    v = u+1; if (nv <= v) v = 0;     /* new v    */
    int w = v+1;
    if (nv <= w) w = 0;     /* next     */

    if ( Snip(contour,u,v,w,nv,V) )
    {
      int a,b,c,s,t;      /* true names of the vertices */

      a = V[u]; 
			b = V[v];
			c = V[w];      /* output Triangle */

      result.push_back( contour[a] );
      result.push_back( contour[b] );
      result.push_back( contour[c] );

      m++;      /* remove v from remaining polygon */
      for(s=v,t=v+1;t<nv;s++,t++) V[s] = V[t]; nv--;      /* resest error detection counter */
      count = 2*nv;
    }

  }
  delete V;
  return true;
}


unsigned int triangulate3d(unsigned int pcount,     // number of points in the polygon
                           const double *vertices,   // array of 3d vertices.
                           double *triangles,        // memory to store output triangles
                           unsigned int maxTri,    // maximum triangles we are allowed to output.
                           const double *plane)

{
  unsigned int ret = 0;

  FILE *fph = fopen("debug.obj", "wb");
  if ( fph )
  {
    fprintf(fph,"v 10 10 0\r\n");
    for (unsigned int i=0; i<pcount; i++)
    {
      fprintf(fph,"v %f %f %f\r\n", vertices[i*3+0], vertices[i*3+1], vertices[i*3+2]);
    }
    for (unsigned int i=0; i<pcount; i++)
    {
      unsigned int next = i+1;
      if ( next == pcount ) next = 0;
      fprintf(fph,"f %d %d %d\r\n", i+2, 1, next+2 );
    }
    fclose(fph);
  }

  if ( pcount >= 3 )
  {
    double normal[3];

    normal[0] = plane[0];
    normal[1] = plane[1];
    normal[2] = plane[2];
    double D = plane[3];

    unsigned int i0 = 0;
    unsigned int i1 = 1;
    unsigned int i2 = 2;
    unsigned int axis = 0;


    // find the dominant axis.
    double dx = fabs(normal[0]);
    double dy = fabs(normal[1]);
    double dz = fabs(normal[2]);

    if ( dx > dy && dx > dz )
    {
      axis = 0;
      i0   = 1;
      i1   = 2;
      i2   = 0;
    }
    else if ( dy > dx && dy > dz )
    {
      i0   = 0;
      i1   = 2;
      i2   = 1;
      axis = 1;
    }
    else if ( dz > dx && dz > dy )
    {
      i0 = 0;
      i1 = 1;
      i2 = 2;
      axis = 2;
    }

    double *ptemp = new double[pcount*2];
    double *ptri  = new double[maxTri*2*3];
    const double *source = vertices;
    double *dest = ptemp;

    for (unsigned int i=0; i<pcount; i++)
    {

      dest[0] = source[i0];
      dest[1] = source[i1];

      dest+=2;
      source+=3;
    }

    ret = triangulate2d(pcount, ptemp, ptri, maxTri );

    // ok..now we have to copy it back and project the 3d component.
    if ( ret )
    {
      const double *source = ptri;
      double *dest = triangles;

			double inverseZ = -1.0f / normal[i2];

      for (unsigned int i=0; i<ret*3; i++)
      {

        dest[i0] = source[0];
        dest[i1] = source[1];

        dest[i2] = (normal[i0]*source[0] + normal[i1]*source[1] + D ) * inverseZ; // solve for projected component

        dest+=3;
        source+=2;
      }


     if ( 1 )
     {
      FILE *fph = fopen("test.obj","wb");
      const double *source = triangles;
      for (unsigned int i=0; i<ret*3; i++)
      {
        fprintf(fph,"v %f %f %f\r\n", source[0], source[1], source[2] );
        source+=3;
      }
      int index = 1;
      for (unsigned int i=0; i<ret; i++)
      {
        fprintf(fph,"f %d %d %d\r\n", index, index+1, index+2 );
        index+=3;
      }
      fclose(fph);
		}
		}

    delete ptri;
    delete ptemp;

  }

  return ret;
}

unsigned int triangulate3d(unsigned int pcount,     // number of points in the polygon
                           const unsigned int *indices, // polygon points using indices
                           const double *vertices,   // base address for array indexing
                           double *triangles,        // buffer to store output 3d triangles.
                           unsigned int maxTri,    // maximum triangles we can output.
                           const double *plane)
{
  unsigned int ret = 0;

  if ( pcount )
  {
    // copy the indexed polygon out as a flat array of vertices.
    double *ptemp = new double[pcount*3];
    double *dest = ptemp;

    for (unsigned int i=0; i<pcount; i++)
    {
      unsigned int index = indices[i];
      const double *source = &vertices[index*3];
      *dest++ = *source++;
      *dest++ = *source++;
      *dest++ = *source++;
    }

    ret = triangulate3d(pcount,ptemp,triangles,maxTri,plane);

    delete ptemp;
  }

  return ret;
}

unsigned int triangulate2d(unsigned int pcount,     // number of points in the polygon
                           const double *vertices,   // address of input points (2d)
                           double *triangles,        // destination buffer for output triangles.
                           unsigned int maxTri)    // maximum number of triangles we can store.
{
  unsigned int ret = 0;

  const double *source = vertices;
  Vec2dVector vlist;

  for (unsigned int i=0; i<pcount; i++)
  {
    Vec2d v(source);
    vlist.push_back(v);
    source+=2;
  }

  Vec2dVector result;

  bool ok = Process(vlist,result);
  if ( ok )
  {
    ret = result.size()/3;
    if ( ret < maxTri )
    {
      double *dest = triangles;
      for (unsigned int i=0; i<ret*3; i++)
      {
        dest[0] = result[i].GetX();
        dest[1] = result[i].GetY();
        dest+=2;
      }
    }
    else
    {
      ret = 0;
    }
  }

  return ret;
}

}; // end of namespace
