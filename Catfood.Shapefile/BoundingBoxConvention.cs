/* ------------------------------------------------------------------------
 * (c)copyright Robert Ellison and contributors 
 * Provided under the ms-PL license, see LICENSE.txt
 * https://github.com/abfo/shapefile
 * ------------------------------------------------------------------------ */

namespace Catfood.Shapefile
{
    /// <summary>Controls how shapefile Y extents map to rectangle edges.</summary>
    public enum BoundingBoxConvention
    {
        /// <summary>Preserves the original mapping: Top is YMin and Bottom is YMax.</summary>
        Legacy = 0,

        /// <summary>Uses Y-up coordinates: Top is YMax and Bottom is YMin.</summary>
        YUp = 1
    }
}
