/* ------------------------------------------------------------------------
 * (c)copyright Robert Ellison and contributors
 * Provided under the ms-PL license, see LICENSE.txt
 * https://github.com/abfo/shapefile
 * ------------------------------------------------------------------------ */

namespace Catfood.Shapefile
{
    /// <summary>
    /// Describes how the vertices of a MultiPatch part form a surface.
    /// </summary>
    public enum MultiPatchPartType
    {
        /// <summary>Each vertex after the first two forms a triangle with its two predecessors.</summary>
        TriangleStrip = 0,
        /// <summary>Each vertex after the first two forms a triangle with its predecessor and the first vertex.</summary>
        TriangleFan = 1,
        /// <summary>The outer boundary of a polygonal surface patch.</summary>
        OuterRing = 2,
        /// <summary>A hole following its outer ring.</summary>
        InnerRing = 3,
        /// <summary>The first ring of a surface patch with unspecified ring types.</summary>
        FirstRing = 4,
        /// <summary>A subsequent ring of a surface patch with unspecified ring types.</summary>
        Ring = 5
    }
}
