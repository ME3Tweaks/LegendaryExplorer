#include "meshvolume.h"

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


namespace ConvexDecomposition
{


inline double det(const double *p1,const double *p2,const double *p3)
{
  return  p1[0]*p2[1]*p3[2] + p2[0]*p3[1]*p1[2] + p3[0]*p1[1]*p2[2] -p1[0]*p3[1]*p2[2] - p2[0]*p1[1]*p3[2] - p3[0]*p2[1]*p1[2];
}

double computeMeshVolume(const double *vertices,unsigned int tcount,const unsigned int *indices)
{
	double volume = 0;

	const double *p0 = vertices;
	for (unsigned int i=0; i<tcount; i++,indices+=3)
	{

		const double *p1 = &vertices[ indices[0]*3 ];
		const double *p2 = &vertices[ indices[1]*3 ];
		const double *p3 = &vertices[ indices[2]*3 ];

		volume+=det(p1,p2,p3); // compute the volume of the tetrahedran relative to the origin.
	}

	volume*=(1.0f/6.0f);
	if ( volume < 0 )
		volume*=-1;
	return volume;
}


inline void CrossProduct(const double *a,const double *b,double *cross)
{
	cross[0] = a[1]*b[2] - a[2]*b[1];
	cross[1] = a[2]*b[0] - a[0]*b[2];
	cross[2] = a[0]*b[1] - a[1]*b[0];
}

inline double DotProduct(const double *a,const double *b)
{
	return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
}

inline double tetVolume(const double *p0,const double *p1,const double *p2,const double *p3)
{
	double a[3];
	double b[3];
	double c[3];

  a[0] = p1[0] - p0[0];
  a[1] = p1[1] - p0[1];
  a[2] = p1[2] - p0[2];

	b[0] = p2[0] - p0[0];
	b[1] = p2[1] - p0[1];
	b[2] = p2[2] - p0[2];

  c[0] = p3[0] - p0[0];
  c[1] = p3[1] - p0[1];
  c[2] = p3[2] - p0[2];

  double cross[3];

  CrossProduct( b, c, cross );

	double volume = DotProduct( a, cross );

  if ( volume < 0 )
   return -volume;

  return volume;
}

inline double det(const double *p0,const double *p1,const double *p2,const double *p3)
{
  return  p1[0]*p2[1]*p3[2] + p2[0]*p3[1]*p1[2] + p3[0]*p1[1]*p2[2] -p1[0]*p3[1]*p2[2] - p2[0]*p1[1]*p3[2] - p3[0]*p2[1]*p1[2];
}

double computeMeshVolume2(const double *vertices,unsigned int tcount,const unsigned int *indices)
{
	double volume = 0;

	const double *p0 = vertices;
	for (unsigned int i=0; i<tcount; i++,indices+=3)
	{

		const double *p1 = &vertices[ indices[0]*3 ];
		const double *p2 = &vertices[ indices[1]*3 ];
		const double *p3 = &vertices[ indices[2]*3 ];

		volume+=tetVolume(p0,p1,p2,p3); // compute the volume of the tetrahdren relative to the root vertice
	}

  return volume * (1.0f / 6.0f );
}


//** Float versions

inline float det(const float *p1,const float *p2,const float *p3)
{
  return  p1[0]*p2[1]*p3[2] + p2[0]*p3[1]*p1[2] + p3[0]*p1[1]*p2[2] -p1[0]*p3[1]*p2[2] - p2[0]*p1[1]*p3[2] - p3[0]*p2[1]*p1[2];
}

float computeMeshVolume(const float *vertices,unsigned int tcount,const unsigned int *indices)
{
	float volume = 0;

	const float *p0 = vertices;
	for (unsigned int i=0; i<tcount; i++,indices+=3)
	{

		const float *p1 = &vertices[ indices[0]*3 ];
		const float *p2 = &vertices[ indices[1]*3 ];
		const float *p3 = &vertices[ indices[2]*3 ];

		volume+=det(p1,p2,p3); // compute the volume of the tetrahedran relative to the origin.
	}

	volume*=(1.0f/6.0f);
	if ( volume < 0 )
		volume*=-1;
	return volume;
}


inline void CrossProduct(const float *a,const float *b,float *cross)
{
	cross[0] = a[1]*b[2] - a[2]*b[1];
	cross[1] = a[2]*b[0] - a[0]*b[2];
	cross[2] = a[0]*b[1] - a[1]*b[0];
}

inline float DotProduct(const float *a,const float *b)
{
	return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
}

inline float tetVolume(const float *p0,const float *p1,const float *p2,const float *p3)
{
	float a[3];
	float b[3];
	float c[3];

  a[0] = p1[0] - p0[0];
  a[1] = p1[1] - p0[1];
  a[2] = p1[2] - p0[2];

	b[0] = p2[0] - p0[0];
	b[1] = p2[1] - p0[1];
	b[2] = p2[2] - p0[2];

  c[0] = p3[0] - p0[0];
  c[1] = p3[1] - p0[1];
  c[2] = p3[2] - p0[2];

  float cross[3];

  CrossProduct( b, c, cross );

	float volume = DotProduct( a, cross );

  if ( volume < 0 )
   return -volume;

  return volume;
}

inline float det(const float *p0,const float *p1,const float *p2,const float *p3)
{
  return  p1[0]*p2[1]*p3[2] + p2[0]*p3[1]*p1[2] + p3[0]*p1[1]*p2[2] -p1[0]*p3[1]*p2[2] - p2[0]*p1[1]*p3[2] - p3[0]*p2[1]*p1[2];
}

float computeMeshVolume2(const float *vertices,unsigned int tcount,const unsigned int *indices)
{
	float volume = 0;

	const float *p0 = vertices;
	for (unsigned int i=0; i<tcount; i++,indices+=3)
	{

		const float *p1 = &vertices[ indices[0]*3 ];
		const float *p2 = &vertices[ indices[1]*3 ];
		const float *p3 = &vertices[ indices[2]*3 ];

		volume+=tetVolume(p0,p1,p2,p3); // compute the volume of the tetrahdren relative to the root vertice
	}

  return volume * (1.0f / 6.0f );
}


};

