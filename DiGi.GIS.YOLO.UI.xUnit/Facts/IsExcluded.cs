using DiGi.Geometry.Planar.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the exclusion test a training dataset build drops buildings with: a footprint inside an exclusion box and one within the buffer are excluded, one beyond the buffer is kept, an edge that only touches counts as inside, and an empty box list excludes nothing.
        /// <para>The box is [1000, 1100] x [2000, 2100]. The boundary is inclusive in both directions, so a footprint grown exactly onto an edge is excluded while one a hundredth of a metre clear of it is kept - a crop must never slip past a boundary it lands exactly on.</para>
        /// </summary>
        [Fact]
        public void IsExcluded()
        {
            List<Classes.YOLOExclusionBoundingBox> yOLOExclusionBoundingBoxes = [new Classes.YOLOExclusionBoundingBox(1000, 2000, 1100, 2100)];

            // Inside the box: excluded with no buffer at all, and a footprint enclosing the whole box as well.
            BoundingBox2D boundingBox2D_Inside = new(1010, 2010, 20, 30);
            Assert.True(Query.IsExcluded(boundingBox2D_Inside, yOLOExclusionBoundingBoxes, 0));
            Assert.True(Query.IsExcluded(new BoundingBox2D(900, 1900, 400, 400), yOLOExclusionBoundingBoxes, 0));

            // Outside the box but within the buffer: the grown envelope reaches it.
            BoundingBox2D boundingBox2D_Near = new(940, 2010, 20, 30);
            Assert.True(Query.IsExcluded(boundingBox2D_Near, yOLOExclusionBoundingBoxes, 100));
            Assert.False(Query.IsExcluded(boundingBox2D_Near, yOLOExclusionBoundingBoxes, 30));

            // Beyond the buffer: kept - and exactly at the buffer the grown edge touches the box, which counts.
            BoundingBox2D boundingBox2D_Far = new(920, 2010, 20, 30);
            Assert.False(Query.IsExcluded(boundingBox2D_Far, yOLOExclusionBoundingBoxes, 50));
            Assert.True(Query.IsExcluded(boundingBox2D_Far, yOLOExclusionBoundingBoxes, 60));

            // Edge-touching with no buffer: the right edge lands exactly on the box, a hundredth of a metre clear does not.
            BoundingBox2D boundingBox2D_Touching = new(980, 2010, 20, 30);
            Assert.True(Query.IsExcluded(boundingBox2D_Touching, yOLOExclusionBoundingBoxes, 0));
            Assert.False(Query.IsExcluded(new BoundingBox2D(979.99, 2010, 20, 30), yOLOExclusionBoundingBoxes, 0));

            // The same rule on the Y axis: clear of the box is kept, within the buffer is excluded, just beyond it is kept.
            Assert.False(Query.IsExcluded(new BoundingBox2D(1050, 2130, 20, 30), yOLOExclusionBoundingBoxes, 20));
            Assert.True(Query.IsExcluded(new BoundingBox2D(1050, 2130, 20, 30), yOLOExclusionBoundingBoxes, 50));
            Assert.False(Query.IsExcluded(new BoundingBox2D(1050, 2400, 20, 30), yOLOExclusionBoundingBoxes, 50));

            // An empty list, or none at all, excludes nothing - the behaviour before the option existed.
            Assert.False(Query.IsExcluded(boundingBox2D_Inside, [], 100));
            Assert.False(Query.IsExcluded(boundingBox2D_Inside, null, 100));

            // No footprint: nothing to test, nothing excluded - the build counts the reference as without a footprint.
            Assert.False(Query.IsExcluded(null, yOLOExclusionBoundingBoxes, 100));

            // A box whose corners were given the wrong way round is read as the area between them.
            Assert.True(Query.IsExcluded(boundingBox2D_Inside, [new Classes.YOLOExclusionBoundingBox(1100, 2100, 1000, 2000)], 0));

            // A negative buffer is read as 0 - the box exactly as drawn - so an inside footprint stays inside.
            Assert.True(Query.IsExcluded(new BoundingBox2D(1005, 2005, 20, 30), yOLOExclusionBoundingBoxes, -50));
        }
    }
}
