#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <assert.h>

#pragma warning(disable:4786)

#include <vector>
#include <map>
#include <set>
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




// CodeSnippet provided by John W. Ratcliff
// on March 23, 2006.
//
// mailto: jratcliff@infiniplex.net
//
// Personal website: http://jratcliffscarab.blogspot.com
// Coding Website:   http://codesuppository.blogspot.com
// FundRaising Blog: http://amillionpixels.blogspot.com
// Fundraising site: http://www.amillionpixels.us
// New Temple Site:  http://newtemple.blogspot.com
//
// This snippet shows how to 'hide' the complexity of
// the STL by wrapping some useful piece of functionality
// around a handful of discrete API calls.
//
// This API allows you to create an indexed triangle list
// from a collection of raw input triangles.  Internally
// it uses an STL set to build the lookup table very rapidly.
//
// Here is how you would use it to build an indexed triangle
// list from a raw list of triangles.
//
// (1) create a 'VertexLookup' interface by calling
//
//     VertexLook vl = Vl_createVertexLookup();
//
// (2) For each vertice in each triangle call:
//
//     unsigned int i1 = Vl_getIndex(vl,p1);
//     unsigned int i2 = Vl_getIndex(vl,p2);
//     unsigned int i3 = Vl_getIndex(vl,p3);
//
//     save the 3 indices into your triangle list array.
//
// (3) Get the vertex array by calling:
//
//     const double *vertices = Vl_getVertices(vl);
//
// (4) Get the number of vertices so you can copy them into
//     your own buffer.
//     unsigned int vcount = Vl_getVcount(vl);
//
// (5) Release the VertexLookup interface when you are done with it.
//     Vl_releaseVertexLookup(vl);
//
// Teaches the following lessons:
//
//    How to wrap the complexity of STL and C++ classes around a
//    simple API interface.
//
//    How to use an STL set and custom comparator operator for
//    a complex data type.
//
//    How to create a template class.
//
//    How to achieve significant performance improvements by
//    taking advantage of built in STL containers in just
//    a few lines of code.
//
//    You could easily modify this code to support other vertex
//    formats with any number of interpolants.




#include "vlookup.h"

namespace ConvexDecomposition
{

class VertexPosition
{
public:
  VertexPosition(void) { };
  VertexPosition(const double *p)
  {
  	mPos[0] = p[0];
  	mPos[1] = p[1];
  	mPos[2] = p[2];
  };

	void Set(int index,const double *pos)
	{
		const double * p = &pos[index*3];

		mPos[0]    = p[0];
		mPos[1]    = p[1];
		mPos[2]    = p[2];

	};

  double GetX(void) const { return mPos[0]; };
  double GetY(void) const { return mPos[1]; };
  double GetZ(void) const { return mPos[2]; };

	double mPos[3];
};


template <class Type> class VertexLess
{
public:
	typedef std::vector< Type > VertexVector;

	bool operator()(int v1,int v2) const;

	static void SetSearch(const Type& match,VertexVector *list)
	{
		mFind = match;
		mList = list;
	};

private:
	const Type& Get(int index) const
	{
		if ( index == -1 ) return mFind;
		VertexVector &vlist = *mList;
		return vlist[index];
	}
	static Type mFind; // vertice to locate.
	static VertexVector  *mList;
};

template <class Type> class VertexPool
{
public:
	typedef std::set<int, VertexLess<Type> > VertexSet;
	typedef std::vector< Type > VertexVector;

	int GetVertex(const Type& vtx)
	{
		VertexLess<Type>::SetSearch(vtx,&mVtxs);
		VertexSet::iterator found;
		found = mVertSet.find( -1 );
		if ( found != mVertSet.end() )
		{
			return *found;
		}
		int idx = (int)mVtxs.size();
		mVtxs.push_back( vtx );
		mVertSet.insert( idx );
		return idx;
	};

	const double * GetPos(int idx) const
	{
		return mVtxs[idx].mPos;
	}

	const Type& Get(int idx) const
	{
		return mVtxs[idx];
	};

	unsigned int GetSize(void) const
	{
		return mVtxs.size();
	};

	void Clear(int reservesize)  // clear the vertice pool.
	{
		mVertSet.clear();
		mVtxs.clear();
		mVtxs.reserve(reservesize);
	};

	const VertexVector& GetVertexList(void) const { return mVtxs; };

	void Set(const Type& vtx)
	{
		mVtxs.push_back(vtx);
	}

	unsigned int GetVertexCount(void) const
	{
		return mVtxs.size();
	};


	Type * GetBuffer(void)
	{
		return &mVtxs[0];
	};

private:
	VertexSet      mVertSet; // ordered list.
	VertexVector   mVtxs;  // set of vertices.
};


VertexPosition VertexLess<VertexPosition>::mFind;
std::vector<VertexPosition > *VertexLess<VertexPosition>::mList=0;

enum RDIFF
{
  RD_EQUAL,
  RD_LESS,
  RD_GREATER
};

static RDIFF relativeDiff(const double *a,const double *b,double magnitude)
{
  RDIFF ret = RD_EQUAL;

  double m2 = magnitude*magnitude;
  double dx = a[0]-b[0];
  double dy = a[1]-b[1];
  double dz = a[2]-b[2];
  double d2 = (dx*dx)+(dy*dy)+(dz*dz);

  if ( d2 > m2 )
  {
         if ( a[0] < b[0] ) ret = RD_LESS;
    else if ( a[0] > b[0] ) ret = RD_GREATER;
    else if ( a[1] < b[1] ) ret = RD_LESS;
    else if ( a[1] > b[1] ) ret = RD_GREATER;
    else if ( a[2] < b[2] ) ret = RD_LESS;
    else if ( a[2] > b[2] ) ret = RD_GREATER;
  }
  return ret;
}


bool VertexLess<VertexPosition>::operator()(int v1,int v2) const
{
  bool ret = false;

	const VertexPosition& a = Get(v1);
	const VertexPosition& b = Get(v2);

  RDIFF d = relativeDiff(a.mPos,b.mPos,0.0001f);
  if ( d == RD_LESS ) ret = true;

  return ret;

};



VertexLookup Vl_createVertexLookup(void)
{
  VertexLookup ret = new VertexPool< VertexPosition >;
  return ret;
}

void          Vl_releaseVertexLookup(VertexLookup vlook)
{
  VertexPool< VertexPosition > *vp = (VertexPool< VertexPosition > *) vlook;
  delete vp;
}

unsigned int  Vl_getIndex(VertexLookup vlook,const double *pos)  // get index.
{
  VertexPool< VertexPosition > *vp = (VertexPool< VertexPosition > *) vlook;
  VertexPosition p(pos);
  return vp->GetVertex(p);
}

const double * Vl_getVertices(VertexLookup vlook)
{
  VertexPool< VertexPosition > *vp = (VertexPool< VertexPosition > *) vlook;
  return vp->GetPos(0);
}


unsigned int  Vl_getVcount(VertexLookup vlook)
{
  VertexPool< VertexPosition > *vp = (VertexPool< VertexPosition > *) vlook;
  return vp->GetVertexCount();
}

}; // end of namespace
