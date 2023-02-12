using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Catfood.Shapefile.UnitTests
{
    [TestClass]
    public class PolyLineUnitTests
    {
        [TestMethod]
        public void ParseFirstPointSuccess()
        {
            string shapefilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "..\\..\\..\\TestData\\PAN_water_lines_dcw.shp");
            
            using (Shapefile shapefile = new Shapefile(shapefilePath))
            {
                Assert.AreEqual(ShapeType.PolyLine, shapefile.Type);
                Assert.AreEqual(757, shapefile.Count);
                Assert.AreEqual(-83.032531823292, shapefile.BoundingBox.Left, 0.001);
                Assert.AreEqual(7.23685458952989, shapefile.BoundingBox.Top, 0.001);
                Assert.AreEqual(-77.2360076059741, shapefile.BoundingBox.Right, 0.001);
                Assert.AreEqual(9.61545560060634, shapefile.BoundingBox.Bottom, 0.001);
                
                foreach (Shape shape in shapefile)
                {
                    Assert.AreEqual(ShapeType.PolyLine, shape.Type);
                    ShapePolyLine shapePolyLine = shape as ShapePolyLine;
                    Assert.IsNotNull(shapePolyLine);
                    Assert.AreEqual(5, shapePolyLine.GetMetadataNames().Length);
                    Assert.AreEqual("Panama", shapePolyLine.GetMetadata("name_0"));
                    Assert.AreEqual(1, shapePolyLine.Parts.Count);
                    Assert.AreEqual(3, shapePolyLine.Parts[0].Length);
                    Assert.AreEqual(-80.5127334422155, shapePolyLine.Parts[0][0].X, 0.001);
                    Assert.AreEqual(8.22265632942063, shapePolyLine.Parts[0][0].Y, 0.001);
                    break;
                }
            }
        }
    }
}
