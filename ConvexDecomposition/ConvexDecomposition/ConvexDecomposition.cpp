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



#include <algorithm>
#include <vector>

#include "ConvexDecomposition.h"
#include "cd_vector.h"
#include "cd_hull.h"
#include "bestfit.h"
#include "planetri.h"
#include "vlookup.h"
#include "splitplane.h"
#include "meshvolume.h"
#include "concavity.h"
#include "bestfitobb.h"
#include "fitsphere.h"
#include "triangulate.h"
#include "float_math.h"

#define MAKE_MESH 1
#define CLOSE_FACE 0

static unsigned int MAXDEPTH=8;
static double        CONCAVE_PERCENT=1.0f;
static double        MERGE_PERCENT=2.0f;


using namespace ConvexDecomposition;

typedef std::vector< unsigned int > UintVector;

namespace ConvexDecomposition
{

class Edge
{
public:

  Edge(unsigned int i1,unsigned int i2)
  {
    mE1 = i1;
    mE2 = i2;
    mUsed = false;
  }

  unsigned int  mE1;
  unsigned int  mE2;
  bool          mUsed;
};

typedef std::vector< Edge > EdgeVector;

class FaceTri
{
public:
	FaceTri(void) { };

  FaceTri(const double *vertices,unsigned int i1,unsigned int i2,unsigned int i3)
  {
  	mP1.Set( &vertices[i1*3] );
  	mP2.Set( &vertices[i2*3] );
  	mP3.Set( &vertices[i3*3] );
  }

  Vector3d<double>	mP1;
  Vector3d<double>	mP2;
  Vector3d<double>	mP3;
  Vector3d<double> mNormal;

};



class CHull
{
public:
  CHull(const ConvexResult &result)
  {
    mResult = new ConvexResult(result);
    mVolume = computeMeshVolume( result.mHullVertices, result.mHullTcount, result.mHullIndices );

    mDiagonal = getBoundingRegion( result.mHullVcount, result.mHullVertices, sizeof(double)*3, mMin, mMax );

    double dx = mMax[0] - mMin[0];
    double dy = mMax[1] - mMin[1];
    double dz = mMax[2] - mMin[2];

    dx*=0.1f; // inflate 1/10th on each edge
    dy*=0.1f; // inflate 1/10th on each edge
    dz*=0.1f; // inflate 1/10th on each edge

    mMin[0]-=dx;
    mMin[1]-=dy;
    mMin[2]-=dz;

    mMax[0]+=dx;
    mMax[1]+=dy;
    mMax[2]+=dz;


  }

  ~CHull(void)
  {
    delete mResult;
  }

  bool overlap(const CHull &h) const
  {
    return overlapAABB(mMin,mMax, h.mMin, h.mMax );
  }

  double          mMin[3];
  double          mMax[3];
	double          mVolume;
  double          mDiagonal; // long edge..
  ConvexResult  *mResult;
};

// Usage: std::sort( list.begin(), list.end(), StringSortRef() );
class CHullSort
{
	public:

	 bool operator()(const CHull *a,const CHull *b) const
	 {
		 return a->mVolume < b->mVolume;
	 }
};


typedef std::vector< CHull * > CHullVector;


class ConvexBuilder : public ConvexDecompInterface
{
public:
  ConvexBuilder(ConvexDecompInterface *callback)
  {
    mCallback = callback;
  };

  ~ConvexBuilder(void)
  {
    CHullVector::iterator i;
    for (i=mChulls.begin(); i!=mChulls.end(); ++i)
    {
      CHull *cr = (*i);
      delete cr;
    }
  }

	bool isDuplicate(unsigned int i1,unsigned int i2,unsigned int i3,
		               unsigned int ci1,unsigned int ci2,unsigned int ci3)
	{
		unsigned int dcount = 0;

		assert( i1 != i2 && i1 != i3 && i2 != i3 );
		assert( ci1 != ci2 && ci1 != ci3 && ci2 != ci3 );

		if ( i1 == ci1 || i1 == ci2 || i1 == ci3 ) dcount++;
		if ( i2 == ci1 || i2 == ci2 || i2 == ci3 ) dcount++;
		if ( i3 == ci1 || i3 == ci2 || i3 == ci3 ) dcount++;

		return dcount == 3;
	}

	void getMesh(const ConvexResult &cr,VertexLookup vc)
	{
		unsigned int *src = cr.mHullIndices;

		for (unsigned int i=0; i<cr.mHullTcount; i++)
		{
			unsigned int i1 = *src++;
			unsigned int i2 = *src++;
			unsigned int i3 = *src++;

			const double *p1 = &cr.mHullVertices[i1*3];
			const double *p2 = &cr.mHullVertices[i2*3];
			const double *p3 = &cr.mHullVertices[i3*3];

			i1 = Vl_getIndex(vc,p1);
			i2 = Vl_getIndex(vc,p2);
			i3 = Vl_getIndex(vc,p3);


		}
	}

	CHull * canMerge(CHull *a,CHull *b)
	{

    if ( !a->overlap(*b) ) return 0; // if their AABB's (with a little slop) don't overlap, then return.

    if ( MERGE_PERCENT < 0 ) return 0;

		assert( a->mVolume > 0 );
		assert( b->mVolume > 0 );

		CHull *ret = 0;

		// ok..we are going to combine both meshes into a single mesh
		// and then we are going to compute the concavity...

    VertexLookup vc = Vl_createVertexLookup();

    getMesh( *a->mResult, vc);
    getMesh( *b->mResult, vc);

		unsigned int vcount = Vl_getVcount(vc);
		const double *vertices = Vl_getVertices(vc);

    HullResult hresult;
    HullLibrary hl;
    HullDesc   desc;

  	desc.SetHullFlag(QF_TRIANGLES);

    desc.mVcount       = vcount;
    desc.mVertices     = vertices;
    desc.mVertexStride = sizeof(double)*3;

    HullError hret = hl.CreateConvexHull(desc,hresult);

    if ( hret == QE_OK )
    {

      double combineVolume  = computeMeshVolume( hresult.mOutputVertices, hresult.mNumFaces, hresult.mIndices );
			double sumVolume      = a->mVolume + b->mVolume;

      double percent = (sumVolume*100) / combineVolume;

      if ( percent >= (100.0f-MERGE_PERCENT)  )
      {
  			ConvexResult cr(hresult.mNumOutputVertices, hresult.mOutputVertices, hresult.mNumFaces, hresult.mIndices);
    		ret = new CHull(cr);
    	}
		}


		Vl_releaseVertexLookup(vc);

		return ret;
	}

  bool combineHulls(void)
  {

  	bool combine = false;

		sortChulls(mChulls); // sort the convex hulls, largest volume to least...

		CHullVector output; // the output hulls...


    CHullVector::iterator i;

    for (i=mChulls.begin(); i!=mChulls.end() && !combine; ++i)
    {
      CHull *cr = (*i);

      CHullVector::iterator j;
      for (j=mChulls.begin(); j!=mChulls.end(); ++j)
      {
        CHull *match = (*j);

        if ( cr != match ) // don't try to merge a hull with itself, that be stoopid
        {

					CHull *merge = canMerge(cr,match); // if we can merge these two....

					if ( merge )
					{

						output.push_back(merge);


						++i;
						while ( i != mChulls.end() )
						{
							CHull *cr = (*i);
							if ( cr != match )
							{
  							output.push_back(cr);
  						}
							i++;
						}

						delete cr;
						delete match;
						combine = true;
						break;
					}
        }
      }

      if ( combine )
      {
      	break;
      }
      else
      {
      	output.push_back(cr);
      }

    }

		if ( combine )
		{
			mChulls.clear();
			mChulls = output;
			output.clear();
		}


    return combine;
  }

  unsigned int process(const DecompDesc &desc)
  {

  	unsigned int ret = 0;

		MAXDEPTH        = desc.mDepth;
		CONCAVE_PERCENT = desc.mCpercent;
		MERGE_PERCENT   = desc.mPpercent;


    doConvexDecomposition(desc.mVcount, desc.mVertices, desc.mTcount, desc.mIndices,this,0,0);


		while ( combineHulls() ); // keep combinging hulls until I can't combine any more...

    CHullVector::iterator i;
    for (i=mChulls.begin(); i!=mChulls.end(); ++i)
    {
      CHull *cr = (*i);

			// before we hand it back to the application, we need to regenerate the hull based on the
			// limits given by the user.

			const ConvexResult &c = *cr->mResult; // the high resolution hull...

      HullResult result;
      HullLibrary hl;
      HullDesc   hdesc;

    	hdesc.SetHullFlag(QF_TRIANGLES);

      hdesc.mVcount       = c.mHullVcount;
      hdesc.mVertices     = c.mHullVertices;
      hdesc.mVertexStride = sizeof(double)*3;
      hdesc.mMaxVertices  = desc.mMaxVertices; // maximum number of vertices allowed in the output

      if ( desc.mSkinWidth > 0 )
      {
      	hdesc.mSkinWidth = desc.mSkinWidth;
      	hdesc.SetHullFlag(QF_SKIN_WIDTH); // do skin width computation.
      }

      HullError ret = hl.CreateConvexHull(hdesc,result);

      if ( ret == QE_OK )
      {
  			ConvexResult r(result.mNumOutputVertices, result.mOutputVertices, result.mNumFaces, result.mIndices);

				r.mHullVolume = computeMeshVolume( result.mOutputVertices, result.mNumFaces, result.mIndices ); // the volume of the hull.

        mCallback->ConvexDecompResult(r);
      }


      delete cr;
    }

		ret = mChulls.size();

    mChulls.clear();

    return ret;
  }


	virtual void ConvexDebugTri(const double *p1,const double *p2,const double *p3,unsigned int color)
  {
    mCallback->ConvexDebugTri(p1,p2,p3,color);
  }

  virtual void ConvexDebugOBB(const double *sides, const double *matrix,unsigned int color)
  {
    mCallback->ConvexDebugOBB(sides,matrix,color);
  }
	virtual void ConvexDebugPoint(const double *p,double dist,unsigned int color)
  {
    mCallback->ConvexDebugPoint(p,dist,color);
  }

  virtual void ConvexDebugBound(const double *bmin,const double *bmax,unsigned int color)
  {
    mCallback->ConvexDebugBound(bmin,bmax,color);
  }

  virtual void ConvexDecompResult(ConvexResult &result)
  {
    CHull *ch = new CHull(result);
		mChulls.push_back(ch);
  }

	void sortChulls(CHullVector &hulls)
	{
		std::sort( hulls.begin(), hulls.end(), CHullSort() );
	}

#define EPSILON 0.001f

  bool isEdge(const Vector3d<double> &p,const double *plane)
  {
    bool ret = false;

  	double dist = fabs(fm_distToPlane(plane,p.Ptr()));

  	if ( dist < EPSILON )
  	{
  		ret = true;
  	}


    return ret;
  }

  void addEdge(const Vector3d<double> &p1,const Vector3d<double> &p2,EdgeVector &edges,VertexLookup split,const double *plane)
  {
    if ( isEdge(p1,plane) && isEdge(p2,plane) )
    {
      unsigned int i1 = Vl_getIndex(split,p1.Ptr());
      unsigned int i2 = Vl_getIndex(split,p2.Ptr());

      bool found = false;

      for (unsigned int i=0; i<edges.size(); i++)
      {
        Edge &e = edges[i];
        if ( e.mE1 == i1 && e.mE2 == i2 )
        {
          found = true;
          break;
        }
        if ( e.mE1 == i2 && e.mE2 == i1 )
        {
          found = true;
          break;
        }
      }
      if ( !found )
      {
        Edge e(i1,i2);
        edges.push_back(e);
      }
    }
  }

  bool addTri(VertexLookup vl,
              UintVector &list,
              const Vector3d<double> &p1,
              const Vector3d<double> &p2,
              const Vector3d<double> &p3,
              EdgeVector &edges,
              VertexLookup split,
              const double *plane)
  {
    bool ret = false;

    unsigned int i1 = Vl_getIndex(vl, p1.Ptr() );
    unsigned int i2 = Vl_getIndex(vl, p2.Ptr() );
    unsigned int i3 = Vl_getIndex(vl, p3.Ptr() );

    // do *not* process degenerate triangles!

    if ( i1 != i2 && i1 != i3 && i2 != i3 )
    {

      list.push_back(i1);
      list.push_back(i2);
      list.push_back(i3);
#if CLOSE_FACE
      addEdge(p1,p2,edges,split,plane);
      addEdge(p2,p3,edges,split,plane);
      addEdge(p3,p1,edges,split,plane);
#endif
      ret = true;
    }
    return ret;
  }

  void saveEdges(VertexLookup vl,const EdgeVector &edges,bool front)
  {
    char scratch[512];
    if ( front )
    {
      static int fcount=1;
      sprintf(scratch,"CD_Front%d.obj", fcount++);
    }
    else
    {
      static int bcount=1;
      sprintf(scratch,"CD_Back%d.obj", bcount++);
    }

    FILE *fph = fopen(scratch,"wb");
    if (fph)
    {
      unsigned int vcount = Vl_getVcount(vl);
      const double *vertices = Vl_getVertices(vl);
      fprintf(fph,"v 10 10 0\r\n");
      for (unsigned int i=0; i<vcount; i++)
      {
        fprintf(fph,"v %0.9f %0.9f %0.9f\r\n", vertices[0], vertices[1], vertices[2] );
        vertices+=3;
      }
      for (unsigned int i=0; i<edges.size(); i++)
      {
        const Edge &e = edges[i];
        fprintf(fph,"f %d %d %d\r\n", e.mE1+2, 1, e.mE2+2);
      }
      fclose(fph);
    }

  }

  void saveObj(VertexLookup vl,const UintVector &indices,bool front)
  {
    char scratch[512];
    if ( front )
    {
      static int fcount=1;
      sprintf(scratch,"CD_Front%d.obj", fcount++);
    }
    else
    {
      static int bcount=1;
      sprintf(scratch,"CD_Back%d.obj", bcount++);
    }

    FILE *fph = fopen(scratch,"wb");
    if (fph)
    {
      unsigned int vcount = Vl_getVcount(vl);
      const double *vertices = Vl_getVertices(vl);
      for (unsigned int i=0; i<vcount; i++)
      {
        fprintf(fph,"v %0.9f %0.9f %0.9f\r\n", vertices[0], vertices[1], vertices[2] );
        vertices+=3;
      }
      for (unsigned int i=0; i<indices.size()/3; i++)
      {
        unsigned int i1 = indices[i*3+0];
        unsigned int i2 = indices[i*3+1];
        unsigned int i3 = indices[i*3+2];
        fprintf(fph,"f %d %d %d\r\n", i1+1, i2+1, i3+1);
      }
      fclose(fph);
    }

  };

  void doConvexDecomposition(unsigned int           vcount,
                                  const double           *vertices,
                                  unsigned int           tcount,
                                  const unsigned int    *indices,
                                  ConvexDecompInterface *callback,
                                  double                  masterVolume,
                                  unsigned int           depth)

  {

    double plane[4];

    bool split = false;


    if ( depth < MAXDEPTH )
    {
      if ( CONCAVE_PERCENT >= 0 )
      {
    		double volume;

    		double c = computeConcavity( vcount, vertices, tcount, indices, callback, plane, volume );

        if ( depth == 0 )
        {
          masterVolume = volume;
        }

    		double percent = (c*100.0f)/masterVolume;

    		if ( percent > CONCAVE_PERCENT ) // if great than 5% of the total volume is concave, go ahead and keep splitting.
    		{
          split = true;
        }
      }
      else
      {
        split = computeSplitPlane(vcount,vertices,tcount,indices,callback,plane);
      }

    }

    if ( depth >= MAXDEPTH || !split )
    {

      HullResult result;
      HullLibrary hl;
      HullDesc   desc;

    	desc.SetHullFlag(QF_TRIANGLES);

      desc.mVcount       = vcount;
      desc.mVertices     = vertices;
      desc.mVertexStride = sizeof(double)*3;

      HullError ret = hl.CreateConvexHull(desc,result);

      if ( ret == QE_OK )
      {

  			ConvexResult r(result.mNumOutputVertices, result.mOutputVertices, result.mNumFaces, result.mIndices);


  			callback->ConvexDecompResult(r);
      }


      return;
    }

    UintVector ifront;
    UintVector iback;

    EdgeVector frontEdges;
    EdgeVector backEdges;

    VertexLookup vfront = Vl_createVertexLookup();
    VertexLookup vback  = Vl_createVertexLookup();

    VertexLookup splitFront = Vl_createVertexLookup();
    VertexLookup splitBack  = Vl_createVertexLookup();



  	if ( 1 )
  	{

  		// ok..now we are going to 'split' all of the input triangles against this plane!

  		const unsigned int *source = indices;

  		for (unsigned int i=0; i<tcount; i++)
  		{
  			unsigned int i1 = *source++;
  			unsigned int i2 = *source++;
  			unsigned int i3 = *source++;

  			FaceTri t(vertices, i1, i2, i3 );

  			Vector3d<double> front[4];
  			Vector3d<double> back[4];

  			unsigned int fcount=0;
  			unsigned int bcount=0;

  			PlaneTriResult result;

  		  result = planeTriIntersection(plane,t.mP1.Ptr(),sizeof(Vector3d<double>),0.00001f,front[0].Ptr(),fcount,back[0].Ptr(),bcount );

  			if( fcount > 4 || bcount > 4 )
  			{
  		    result = planeTriIntersection(plane,t.mP1.Ptr(),sizeof(Vector3d<double>),0.00001f,front[0].Ptr(),fcount,back[0].Ptr(),bcount );
  			}

  			switch ( result )
  			{
  				case PTR_FRONT:

  					assert( fcount == 3 );

            #if MAKE_MESH
            addTri( vfront, ifront, front[0], front[1], front[2], frontEdges, splitFront, plane );
            #endif

  					break;
  				case PTR_BACK:
  					assert( bcount == 3 );

            #if MAKE_MESH
            addTri( vback, iback, back[0], back[1], back[2], backEdges, splitBack, plane );
            #endif

  					break;
  				case PTR_SPLIT:

  					assert( fcount >= 3 && fcount <= 4);
  					assert( bcount >= 3 && bcount <= 4);

            #if MAKE_MESH
            addTri( vfront, ifront, front[0], front[1], front[2], frontEdges, splitFront, plane );
            addTri( vback, iback, back[0], back[1], back[2], backEdges, splitBack, plane );

            if ( fcount == 4 )
            {
              addTri( vfront, ifront, front[0], front[2], front[3], frontEdges, splitFront, plane );
            }

            if ( bcount == 4  )
            {
              addTri( vback, iback, back[0], back[2], back[3], backEdges, splitBack, plane );
            }
            #endif

  					break;
  			}
  		}


//      saveEdges(vfront,frontEdges,true);
//      saveEdges(vback,backEdges,false);

      // Triangulate the front surface...
      if ( frontEdges.size() ) // extract polygons for the front
      {
        UintVector polygon;

        bool ok = extractPolygon(frontEdges,polygon,splitFront);

        while ( ok )
        {

          const double *vertices = Vl_getVertices(splitFront);
          unsigned int pcount   = polygon.size();
          unsigned int maxTri   = pcount*3;
          double *tris     = new double[maxTri*9];

          unsigned int tcount = triangulate3d(pcount,(const unsigned int *) &polygon[0], vertices, tris, maxTri, plane );

          if ( tcount )
          {
            // cool! now add these triangles to the frong..
            const double *source = tris;
            for (unsigned int i=0; i<tcount; i++)
            {
              unsigned int i1 = Vl_getIndex(vfront, &source[0] );
              unsigned int i2 = Vl_getIndex(vfront, &source[3] );
              unsigned int i3 = Vl_getIndex(vfront, &source[6] );

              ifront.push_back(i1);
              ifront.push_back(i2);
              ifront.push_back(i3);

              ifront.push_back(i3);
              ifront.push_back(i2);
              ifront.push_back(i1);


              source+=9;
            }
          }
    			delete tris;
  		    ok = extractPolygon(frontEdges,polygon,splitFront);
        }
      }

      // Triangulate the back surface...
      if ( backEdges.size() ) // extract polygons for the front
      {
        UintVector polygon;

        bool ok = extractPolygon(backEdges,polygon,splitBack);

        while ( ok )
        {

          const double *vertices = Vl_getVertices(splitBack);
          unsigned int pcount   = polygon.size();
          unsigned int maxTri   = pcount*3;
          double *tris     = new double[maxTri*9];

          unsigned int tcount = triangulate3d(pcount,(const unsigned int *) &polygon[0], vertices, tris, maxTri, plane );

          if ( tcount )
          {
            // cool! now add these triangles to the frong..
            const double *source = tris;
            for (unsigned int i=0; i<tcount; i++)
            {
              unsigned int i1 = Vl_getIndex(vback, &source[0] );
              unsigned int i2 = Vl_getIndex(vback, &source[3] );
              unsigned int i3 = Vl_getIndex(vback, &source[6] );

              iback.push_back(i1);
              iback.push_back(i2);
              iback.push_back(i3);

              iback.push_back(i3);
              iback.push_back(i2);
              iback.push_back(i1);


              source+=9;
            }
          }
    			delete tris;
  		    ok = extractPolygon(backEdges,polygon,splitBack);
        }
      }

#if CLOSE_FACE
      saveObj(vfront,ifront,true);
      saveObj(vback,iback,false);
#endif

      Vl_releaseVertexLookup(splitFront);
      Vl_releaseVertexLookup(splitBack);

  		unsigned int fsize = ifront.size()/3;
  		unsigned int bsize = iback.size()/3;

      // ok... here we recursively call
      if ( ifront.size() )
      {
        unsigned int vcount   = Vl_getVcount(vfront);
        const double *vertices = Vl_getVertices(vfront);
        unsigned int tcount   = ifront.size()/3;

        doConvexDecomposition(vcount, vertices, tcount, &ifront[0], callback, masterVolume, depth+1);

      }

      ifront.clear();

      Vl_releaseVertexLookup(vfront);

      if ( iback.size() )
      {
        unsigned int vcount   = Vl_getVcount(vback);
        const double *vertices = Vl_getVertices(vback);
        unsigned int tcount   = iback.size()/3;

        doConvexDecomposition(vcount, vertices, tcount, &iback[0], callback, masterVolume, depth+1);

      }

      iback.clear();
      Vl_releaseVertexLookup(vback);

  	}
  }

  int findFirstUnused(EdgeVector &edges) const
  {
    int ret = -1;

    for (int i=0; i<(int)edges.size(); i++)
    {
      if ( !edges[i].mUsed )
      {
				edges[i].mUsed = true;
        ret = i;
        printf("%d edges, found root at %d\r\n", edges.size(), ret );
        break;
      }
    }

    for (int i=0; i<(int)edges.size(); i++)
    {
      const char *used = "false";
      if ( edges[i].mUsed ) used = "true";
      printf("Edge%d : %d to %d   used: %s\r\n", i, edges[i].mE1, edges[i].mE2, used );
    }


    return ret;
  }

  int findEdge(EdgeVector &edges,unsigned int index) const
  {
    int ret = -1;

    for (int i=0; i<(int)edges.size(); i++)
    {
      if ( !edges[i].mUsed && edges[i].mE1 == index )
      {
				edges[i].mUsed = true;
				printf("Found matching unused edge %d matching (%d)\r\n", i, index );
        ret = i;
        break;
      }
    }

    if ( ret == -1 )
    {
      printf("Failed to find a match for edge '%d'\r\n", index );
    }

    return ret;
  }

  int findNearestEdge(EdgeVector &edges,unsigned int index,VertexLookup verts) const
  {
    int ret = -1;


    const double *vertices = Vl_getVertices(verts);
    const double *pos = &vertices[index*3];
    double closest = 0.2f*0.2f;

    for (int i=0; i<(int)edges.size(); i++)
    {
      Edge &e = edges[i];

      if ( !e.mUsed )
      {
        const double *dpos = &vertices[ e.mE1*3 ];
        double dx = pos[0] - dpos[0];
        double dy = pos[1] - dpos[1];
        double dz = pos[2] - dpos[2];
        double dist = dx*dx+dy*dy+dz*dz;
        if ( dist < closest )
        {
          closest = dist;
          ret = i;
        }
      }
    }

    if ( ret == -1 )
    {
      printf("Failed to find a match for edge '%d'\r\n", index );
    }
    else
    {
      edges[ret].mUsed = true;
    }

    return ret;
  }

  bool extractPolygon(EdgeVector &edges,UintVector &polygon,VertexLookup split)
  {
    bool ret = false;


		polygon.clear();

    int root = findFirstUnused(edges);

    if ( root >= 0 )
    {
      Edge &e = edges[root];
      polygon.push_back(e.mE1);
			int link;

      do
      {
        link = findEdge(edges,e.mE2);
        if ( link < 0 )
          link = findNearestEdge(edges,e.mE2,split);

        if ( link >= 0 )
        {
					e = edges[link];
          polygon.push_back(e.mE1 );
        }
      } while ( link >= 0 );


      if ( polygon.size() >= 3 )
      {
        ret = true;
      }

    }

    return ret;
  }

CHullVector     mChulls;
ConvexDecompInterface *mCallback;

};

unsigned int performConvexDecomposition(const DecompDesc &desc)
{
	unsigned int ret = 0;

  if ( desc.mCallback )
  {
    ConvexBuilder cb(desc.mCallback);

    ret = cb.process(desc);
  }

  return ret;
}



};
