#include "ConvexDecomposition/ConvexDecomposition.h"

using uint = unsigned int;
using convexDecompCallback = void __stdcall (uint, double*, uint, int*);

extern "C" 
{
	extern __declspec(dllexport) void CreateConvexHull(int vertexCount, const double vertices[], int triangleCount,
	                                                    uint indices[], uint depth,
	                                                    double conservationThreshold, int maxVerts,
	                                                    convexDecompCallback callback);
}

using namespace ConvexDecomposition;

class DecompCallback : public ConvexDecompInterface
{
public:
	convexDecompCallback* callback;
	void ConvexDecompResult(ConvexResult& result) override
	{
		callback(result.mHullVcount * 3, result.mHullVertices, result.mHullTcount * 3, reinterpret_cast<int*>(result.mHullIndices));
	}
};

void CreateConvexHull(const int vertexCount, const double vertices[], const int triangleCount, uint indices[],
                       const uint depth, const double conservationThreshold, const int maxVerts,
                       convexDecompCallback callback)
{
	DecompCallback callbackClass;
	callbackClass.callback = callback;

	DecompDesc desc;
	desc.mVcount = vertexCount;
	desc.mVertices = vertices;
	desc.mTcount = triangleCount;
	desc.mIndices = indices;
	desc.mCpercent = 10.0;
	desc.mPpercent = conservationThreshold;
	desc.mDepth = depth;
	desc.mMaxVertices = maxVerts;
	desc.mCallback = &callbackClass;

	performConvexDecomposition(desc);
}
