#ifndef CONVEX_DECOMPOSITION_H

#define CONVEX_DECOMPOSITION_H
#include <cstring>
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



class ConvexResult
{
public:
  ConvexResult(void)
  {
    mHullVcount = 0;
    mHullVertices = 0;
    mHullTcount = 0;
    mHullIndices = 0;
  }

  ConvexResult(unsigned int hvcount,const double *hvertices,unsigned int htcount,const unsigned int *hindices)
  {
    mHullVcount = hvcount;
    if ( mHullVcount )
    {
      mHullVertices = new double[mHullVcount*sizeof(double)*3];
      memcpy(mHullVertices, hvertices, sizeof(double)*3*mHullVcount );
    }
    else
    {
      mHullVertices = 0;
    }

    mHullTcount = htcount;

    if ( mHullTcount )
    {
      mHullIndices = new unsigned int[sizeof(unsigned int)*mHullTcount*3];
      memcpy(mHullIndices,hindices, sizeof(unsigned int)*mHullTcount*3 );
    }
    else
    {
      mHullIndices = 0;
    }

  }

  ConvexResult(const ConvexResult &r) // copy constructor, perform a deep copy of the data.
  {
    mHullVcount = r.mHullVcount;
    if ( mHullVcount )
    {
      mHullVertices = new double[mHullVcount*sizeof(double)*3];
      memcpy(mHullVertices, r.mHullVertices, sizeof(double)*3*mHullVcount );
    }
    else
    {
      mHullVertices = 0;
    }
    mHullTcount = r.mHullTcount;
    if ( mHullTcount )
    {
      mHullIndices = new unsigned int[sizeof(unsigned int)*mHullTcount*3];
      memcpy(mHullIndices, r.mHullIndices, sizeof(unsigned int)*mHullTcount*3 );
    }
    else
    {
      mHullIndices = 0;
    }
  }

  ~ConvexResult(void)
  {
    delete mHullVertices;
    delete mHullIndices;
  }

// the convex hull.
  unsigned int		    mHullVcount;
  double *						  mHullVertices;
  unsigned  int       mHullTcount;
  unsigned int			 *mHullIndices;

  double               mHullVolume;		    // the volume of the convex hull.

};

// convert from doubles back down to floats.
class FConvexResult
{
public:
  FConvexResult(const ConvexResult &r)
  {
    mHullVcount = r.mHullVcount;
    mHullVertices = 0;
    if ( mHullVcount )
    {
      mHullVertices = new float[mHullVcount*3];

      const double *src = r.mHullVertices;
      float *     dest  = mHullVertices;
      for (unsigned int i=0; i<mHullVcount; i++)
      {
        dest[0] = (float) src[0];
        dest[1] = (float) src[1];
        dest[2] = (float) src[2];
        src+=3;
        dest+=3;
      }
    }
    mHullTcount = r.mHullTcount;
    if ( mHullTcount )
    {
      mHullIndices = new unsigned int[mHullTcount*3];
      memcpy(mHullIndices,r.mHullIndices,sizeof(unsigned int)*mHullTcount*3);
    }
    else
    {
      mHullIndices = 0;
    }
    mHullVolume = (float)r.mHullVolume;
  }

  ~FConvexResult(void)
  {
    delete mHullVertices;
    delete mHullIndices;
  }

  unsigned int		    mHullVcount;
  float  *						  mHullVertices;
  unsigned  int       mHullTcount;
  unsigned int			 *mHullIndices;
  float                mHullVolume;		    // the volume of the convex hull.
};

class ConvexDecompInterface
{
public:

	virtual void ConvexDebugTri(const double *p1,const double *p2,const double *p3,unsigned int color) { };
	virtual void ConvexDebugPoint(const double *p,double dist,unsigned int color) { };
  virtual void ConvexDebugBound(const double *bmin,const double *bmax,unsigned int color) { };
  virtual void ConvexDebugOBB(const double *sides, const double *matrix,unsigned int color) { };

  virtual void ConvexDecompResult(ConvexResult &result) = 0;



};

// just to avoid passing a zillion parameters to the method the
// options are packed into this descriptor.
class DecompDesc
{
public:
  DecompDesc(void)
  {
  	mVcount = 0;
  	mVertices = 0;
  	mTcount   = 0;
  	mIndices  = 0;
  	mDepth    = 5;
  	mCpercent = 5;
  	mPpercent = 5;
  	mMaxVertices = 32;
  	mSkinWidth   = 0;
  	mCallback    = 0;
  }

// describes the input triangle.
  unsigned	int	mVcount;   // the number of vertices in the source mesh.
  const double  *mVertices; // start of the vertex position array.  Assumes a stride of 3 doubles.
  unsigned int  mTcount;   // the number of triangles in the source mesh.
  unsigned int *mIndices;  // the indexed triangle list array (zero index based)

// options
  unsigned int  mDepth;    // depth to split, a maximum of 10, generally not over 7.
  double         mCpercent; // the concavity threshold percentage.  0=20 is reasonable.
	double         mPpercent; // the percentage volume conservation threshold to collapse hulls. 0-30 is reasonable.

// hull output limits.
  unsigned int  mMaxVertices; // maximum number of vertices in the output hull. Recommended 32 or less.
  double         mSkinWidth;   // a skin width to apply to the output hulls.

	ConvexDecompInterface *mCallback; // the interface to receive back the results.

};

// perform approximate convex decomposition on a mesh.
unsigned int performConvexDecomposition(const DecompDesc &desc); // returns the number of hulls produced.

};

#endif
