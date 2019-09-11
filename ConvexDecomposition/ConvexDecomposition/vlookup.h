#ifndef VLOOKUP_H

#define VLOOKUP_H

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
//
//    Hide C++ classes from the rest of your application by
//    keeping them in the CPP and wrapping them in a namespace
// Uses an STL set to create an index table for a bunch of vertex positions
// used typically to re-index a collection of raw triangle data.

namespace ConvexDecomposition
{

typedef void * VertexLookup;

VertexLookup  Vl_createVertexLookup(void);
void          Vl_releaseVertexLookup(VertexLookup vlook);

unsigned int  Vl_getIndex(VertexLookup vlook,const double *pos);  // get index.
const double * Vl_getVertices(VertexLookup vlook);

unsigned int  Vl_getVcount(VertexLookup vlook);

};


#endif
