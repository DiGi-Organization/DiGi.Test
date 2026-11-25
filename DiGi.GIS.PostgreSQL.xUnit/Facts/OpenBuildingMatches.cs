using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The origin of the synthetic footprints of the matching facts, in EPSG:2180. Any position works: the matching runs in memory.
        /// </summary>
        private const double openBuildingMatch_X = 500000;

        /// <summary>
        /// The origin of the synthetic footprints of the matching facts, in EPSG:2180.
        /// </summary>
        private const double openBuildingMatch_Y = 500000;

        /// <summary>
        /// Verifies that two identical footprints form one one-to-one component with an intersection over union of one, an area ratio of one and no offset (DiGi.GIS.PostgreSQL#127).
        /// <para>The row names both footprints by county part and key, carries the dataset footprint's primary source, and its representative point is the BDOT10k footprint's centroid - the component's owner.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_OneToOne()
        {
            OpenBuildingMatchItem openBuildingMatchItem_B = OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10);
            OpenBuildingMatchItem openBuildingMatchItem_D = OpenBuildingMatch_Rectangle(1, "d1", true, 0, 0, 10, 10);

            List<OpenBuildingMatch>? openBuildingMatches = Query.OpenBuildingMatches([openBuildingMatchItem_D], [openBuildingMatchItem_B], new OpenBuildingMatchParameters(), new HashSet<int>() { 1 });
            Assert.NotNull(openBuildingMatches);

            OpenBuildingMatch openBuildingMatch = Assert.Single(openBuildingMatches);
            Assert.Equal(OpenBuildingMatchClass.OneToOne, openBuildingMatch.OpenBuildingMatchClass);
            Assert.Equal(1, openBuildingMatch.CountyId);
            Assert.Equal(1L, openBuildingMatch.ComponentId);
            Assert.Equal(1, openBuildingMatch.BDOTCountyId);
            Assert.Equal("b1", openBuildingMatch.BDOTReference);
            Assert.Equal(1, openBuildingMatch.SourceCountyId);
            Assert.Equal("d1", openBuildingMatch.SourceId);
            Assert.Equal("register", openBuildingMatch.PrimarySource);
            Assert.Equal(1.0, openBuildingMatch.IoU!.Value, 9);
            Assert.Equal(1.0, openBuildingMatch.AreaRatio!.Value, 9);
            Assert.Equal(0.0, openBuildingMatch.CentroidDistance!.Value, 9);
            Assert.Equal(0.0, openBuildingMatch.Dx!.Value, 9);
            Assert.Equal(0.0, openBuildingMatch.Dy!.Value, 9);
            Assert.Equal(openBuildingMatch_X + 5, openBuildingMatch.PointX, 6);
            Assert.Equal(openBuildingMatch_Y + 5, openBuildingMatch.PointY, 6);

            // Nothing is returned for a county group that does not own the component.
            List<OpenBuildingMatch>? openBuildingMatches_Other = Query.OpenBuildingMatches([openBuildingMatchItem_D], [openBuildingMatchItem_B], new OpenBuildingMatchParameters(), new HashSet<int>() { 2 });
            Assert.NotNull(openBuildingMatches_Other);
            Assert.Empty(openBuildingMatches_Other);

            Assert.Null(Query.OpenBuildingMatches(null, [openBuildingMatchItem_B], new OpenBuildingMatchParameters(), new HashSet<int>() { 1 }));
            Assert.Null(Query.OpenBuildingMatches([openBuildingMatchItem_D], [openBuildingMatchItem_B], null, new HashSet<int>() { 1 }));
        }

        /// <summary>
        /// Verifies the IoU threshold just inside and just outside: a one-and-one component whose intersection over union is 1e-6 above the threshold is one-to-one, 1e-6 below it is one-to-one weak (DiGi.GIS.PostgreSQL#127).
        /// <para>Two 10 m squares, the dataset one shifted by <c>s</c> along X, have an IoU of <c>(10 - s) / (10 + s)</c>; the shift is solved for the target IoU, so the two cases straddle the default threshold of 0.5 by a margin far above the overlay's rounding and far below anything a real footprint shows.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_IoUThreshold_JustInsideAndOutside()
        {
            foreach ((double ioU, OpenBuildingMatchClass openBuildingMatchClass) in new (double, OpenBuildingMatchClass)[] { (0.5 + 1e-6, OpenBuildingMatchClass.OneToOne), (0.5 - 1e-6, OpenBuildingMatchClass.OneToOneWeak) })
            {
                double shift = 10 * (1 - ioU) / (1 + ioU);

                OpenBuildingMatchItem openBuildingMatchItem_B = OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10);
                OpenBuildingMatchItem openBuildingMatchItem_D = OpenBuildingMatch_Rectangle(1, "d1", true, shift, 0, 10, 10);

                List<OpenBuildingMatch>? openBuildingMatches = Query.OpenBuildingMatches([openBuildingMatchItem_D], [openBuildingMatchItem_B], new OpenBuildingMatchParameters(), new HashSet<int>() { 1 });
                Assert.NotNull(openBuildingMatches);

                OpenBuildingMatch openBuildingMatch = Assert.Single(openBuildingMatches);
                Assert.Equal(openBuildingMatchClass, openBuildingMatch.OpenBuildingMatchClass);
                Assert.Equal(ioU, openBuildingMatch.IoU!.Value, 9);
            }
        }

        /// <summary>
        /// Verifies the edge rule just inside and just outside the minimum overlap share: an overlap covering 0.3 + 1e-7 of the smaller footprint joins the pair (a weak one-to-one at this IoU), 0.3 - 1e-7 leaves an omission and a commission (DiGi.GIS.PostgreSQL#127).
        /// <para>Two 10 m squares shifted by <c>s</c> overlap on <c>(10 - s) / 10</c> of either; <c>s = 7 ∓ 1e-6</c> straddles the default share of 0.3. Below it, the BDOT10k footprint comes first and is component one.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_MinOverlapShare_JustInsideAndOutside()
        {
            OpenBuildingMatchItem openBuildingMatchItem_B = OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10);

            List<OpenBuildingMatch>? openBuildingMatches_Inside = Query.OpenBuildingMatches([OpenBuildingMatch_Rectangle(1, "d1", true, 7 - 1e-6, 0, 10, 10)], [openBuildingMatchItem_B], new OpenBuildingMatchParameters(), new HashSet<int>() { 1 });
            Assert.NotNull(openBuildingMatches_Inside);
            OpenBuildingMatch openBuildingMatch_Inside = Assert.Single(openBuildingMatches_Inside);
            Assert.Equal(OpenBuildingMatchClass.OneToOneWeak, openBuildingMatch_Inside.OpenBuildingMatchClass);

            List<OpenBuildingMatch>? openBuildingMatches_Outside = Query.OpenBuildingMatches([OpenBuildingMatch_Rectangle(1, "d1", true, 7 + 1e-6, 0, 10, 10)], [openBuildingMatchItem_B], new OpenBuildingMatchParameters(), new HashSet<int>() { 1 });
            Assert.NotNull(openBuildingMatches_Outside);
            Assert.Equal(2, openBuildingMatches_Outside.Count);

            Assert.Equal(OpenBuildingMatchClass.Omission, openBuildingMatches_Outside[0].OpenBuildingMatchClass);
            Assert.Equal("b1", openBuildingMatches_Outside[0].BDOTReference);
            Assert.Null(openBuildingMatches_Outside[0].SourceId);
            Assert.Null(openBuildingMatches_Outside[0].PrimarySource);
            Assert.Null(openBuildingMatches_Outside[0].IoU);
            Assert.Equal(1L, openBuildingMatches_Outside[0].ComponentId);

            Assert.Equal(OpenBuildingMatchClass.Commission, openBuildingMatches_Outside[1].OpenBuildingMatchClass);
            Assert.Equal("d1", openBuildingMatches_Outside[1].SourceId);
            Assert.Null(openBuildingMatches_Outside[1].BDOTReference);
            Assert.Equal("register", openBuildingMatches_Outside[1].PrimarySource);
            Assert.Equal(2L, openBuildingMatches_Outside[1].ComponentId);
        }

        /// <summary>
        /// Verifies the many-member classes (DiGi.GIS.PostgreSQL#127): a register building split in two by the dataset is many-to-one, two register buildings merged by the dataset are one-to-many, and a chain of overlaps across two footprints on each side is many-to-many - each one component, one row per edge.
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_SplitMergeAndChain()
        {
            OpenBuildingMatchParameters openBuildingMatchParameters = new();
            HashSet<int> countyIds_Owned = [1];

            // Split: one BDOT10k building, two dataset halves.
            List<OpenBuildingMatch>? openBuildingMatches_Split = Query.OpenBuildingMatches(
                [OpenBuildingMatch_Rectangle(1, "d1", true, 0, 0, 10, 10), OpenBuildingMatch_Rectangle(1, "d2", true, 10, 0, 10, 10)],
                [OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 20, 10)],
                openBuildingMatchParameters,
                countyIds_Owned);
            Assert.NotNull(openBuildingMatches_Split);
            Assert.Equal(2, openBuildingMatches_Split.Count);
            Assert.All(openBuildingMatches_Split, x => Assert.Equal(OpenBuildingMatchClass.ManyToOne, x.OpenBuildingMatchClass));
            Assert.All(openBuildingMatches_Split, x => Assert.Equal(1L, x.ComponentId));
            Assert.Equal(["d1", "d2"], openBuildingMatches_Split.ConvertAll(x => x.SourceId));
            Assert.Equal(0.5, openBuildingMatches_Split[0].AreaRatio!.Value, 9);

            // Merge: two BDOT10k buildings, one dataset footprint.
            List<OpenBuildingMatch>? openBuildingMatches_Merge = Query.OpenBuildingMatches(
                [OpenBuildingMatch_Rectangle(1, "d1", true, 0, 0, 20, 10)],
                [OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10), OpenBuildingMatch_Rectangle(1, "b2", false, 10, 0, 10, 10)],
                openBuildingMatchParameters,
                countyIds_Owned);
            Assert.NotNull(openBuildingMatches_Merge);
            Assert.Equal(2, openBuildingMatches_Merge.Count);
            Assert.All(openBuildingMatches_Merge, x => Assert.Equal(OpenBuildingMatchClass.OneToMany, x.OpenBuildingMatchClass));
            Assert.Equal(["b1", "b2"], openBuildingMatches_Merge.ConvertAll(x => x.BDOTReference));

            // Chain: d1 covers b1 and half of b2, d2 the other half of b2.
            List<OpenBuildingMatch>? openBuildingMatches_Chain = Query.OpenBuildingMatches(
                [OpenBuildingMatch_Rectangle(1, "d1", true, 0, 0, 15, 10), OpenBuildingMatch_Rectangle(1, "d2", true, 15, 0, 5, 10)],
                [OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10), OpenBuildingMatch_Rectangle(1, "b2", false, 10, 0, 10, 10)],
                openBuildingMatchParameters,
                countyIds_Owned);
            Assert.NotNull(openBuildingMatches_Chain);
            Assert.Equal(3, openBuildingMatches_Chain.Count);
            Assert.All(openBuildingMatches_Chain, x => Assert.Equal(OpenBuildingMatchClass.ManyToMany, x.OpenBuildingMatchClass));
            Assert.Single(openBuildingMatches_Chain.Select(x => x.ComponentId).Distinct());

            // Rows ordered by their BDOT10k member, then their dataset member.
            Assert.Equal(["b1:d1", "b2:d1", "b2:d2"], openBuildingMatches_Chain.ConvertAll(x => $"{x.BDOTReference}:{x.SourceId}"));
        }

        /// <summary>
        /// Verifies the minimum area on both sides (DiGi.GIS.PostgreSQL#127): a footprint below it takes no part in the matching and is returned as excluded, so a small dataset footprint on a register building leaves that building an omission - and with the minimum switched off the same pair is matched.
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_MinAreaExcluded()
        {
            OpenBuildingMatchItem openBuildingMatchItem_BLarge = OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10);
            OpenBuildingMatchItem openBuildingMatchItem_BSmall = OpenBuildingMatch_Rectangle(1, "b2", false, 100, 0, 2, 2);
            OpenBuildingMatchItem openBuildingMatchItem_DSmall = OpenBuildingMatch_Rectangle(1, "d1", true, 4, 4, 2, 2);

            List<OpenBuildingMatch>? openBuildingMatches = Query.OpenBuildingMatches([openBuildingMatchItem_DSmall], [openBuildingMatchItem_BLarge, openBuildingMatchItem_BSmall], new OpenBuildingMatchParameters(minArea: 10), new HashSet<int>() { 1 });
            Assert.NotNull(openBuildingMatches);
            Assert.Equal(3, openBuildingMatches.Count);

            Assert.Equal(OpenBuildingMatchClass.Omission, openBuildingMatches.Single(x => x.BDOTReference == "b1").OpenBuildingMatchClass);
            Assert.Equal(OpenBuildingMatchClass.Excluded, openBuildingMatches.Single(x => x.BDOTReference == "b2").OpenBuildingMatchClass);
            Assert.Equal(OpenBuildingMatchClass.Excluded, openBuildingMatches.Single(x => x.SourceId == "d1").OpenBuildingMatchClass);

            List<OpenBuildingMatch>? openBuildingMatches_NoMinimum = Query.OpenBuildingMatches([openBuildingMatchItem_DSmall], [openBuildingMatchItem_BLarge], new OpenBuildingMatchParameters(minArea: 0), new HashSet<int>() { 1 });
            Assert.NotNull(openBuildingMatches_NoMinimum);
            Assert.Equal(OpenBuildingMatchClass.OneToOneWeak, Assert.Single(openBuildingMatches_NoMinimum).OpenBuildingMatchClass);
        }

        /// <summary>
        /// Verifies the ownership rule across a county edge (DiGi.GIS.PostgreSQL#127): a component belongs to the county part of its smallest BDOT10k key, or of its smallest dataset key when it has no BDOT10k member, so two groups that both read it return it exactly once between them.
        /// <para>A register building of part 1 split by two dataset footprints filed under parts 1 and 2; a commission filed under part 2; and a dataset footprint spanning two register buildings of parts 2 and 1. Run as group 1 and as group 2 over the same input, the rows of the two runs are disjoint and together cover every component.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_OwnedOnceAcrossCountyEdge()
        {
            List<OpenBuildingMatchItem> openBuildingMatchItems_Dataset =
            [
                OpenBuildingMatch_Rectangle(1, "d1", true, 0, 0, 5, 10),
                OpenBuildingMatch_Rectangle(2, "d2", true, 5, 0, 5, 10),
                OpenBuildingMatch_Rectangle(2, "d3", true, 100, 0, 10, 10),
                OpenBuildingMatch_Rectangle(2, "d4", true, 200, 0, 20, 10),
            ];

            List<OpenBuildingMatchItem> openBuildingMatchItems_BDOT =
            [
                OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10),
                OpenBuildingMatch_Rectangle(2, "b2", false, 200, 0, 10, 10),
                OpenBuildingMatch_Rectangle(1, "b3", false, 210, 0, 10, 10),
            ];

            OpenBuildingMatchParameters openBuildingMatchParameters = new();

            List<OpenBuildingMatch>? openBuildingMatches_1 = Query.OpenBuildingMatches(openBuildingMatchItems_Dataset, openBuildingMatchItems_BDOT, openBuildingMatchParameters, new HashSet<int>() { 1 });
            List<OpenBuildingMatch>? openBuildingMatches_2 = Query.OpenBuildingMatches(openBuildingMatchItems_Dataset, openBuildingMatchItems_BDOT, openBuildingMatchParameters, new HashSet<int>() { 2 });
            Assert.NotNull(openBuildingMatches_1);
            Assert.NotNull(openBuildingMatches_2);

            // Group 1 owns the split (smallest BDOT10k key 1:b1) and the merge (1:b3 sorts before 2:b2).
            Assert.Equal(4, openBuildingMatches_1.Count);
            Assert.All(openBuildingMatches_1, x => Assert.Equal(1, x.CountyId));
            Assert.Equal(2, openBuildingMatches_1.Count(x => x.OpenBuildingMatchClass == OpenBuildingMatchClass.ManyToOne));
            Assert.Equal(2, openBuildingMatches_1.Count(x => x.OpenBuildingMatchClass == OpenBuildingMatchClass.OneToMany));

            // Group 2 owns only the commission of its own dataset footprint.
            OpenBuildingMatch openBuildingMatch_2 = Assert.Single(openBuildingMatches_2);
            Assert.Equal(OpenBuildingMatchClass.Commission, openBuildingMatch_2.OpenBuildingMatchClass);
            Assert.Equal("d3", openBuildingMatch_2.SourceId);
            Assert.Equal(2, openBuildingMatch_2.CountyId);

            // Both groups together: every footprint once.
            HashSet<string> keys = [];
            foreach (OpenBuildingMatch openBuildingMatch in openBuildingMatches_1.Concat(openBuildingMatches_2))
            {
                Assert.True(keys.Add($"{openBuildingMatch.BDOTCountyId}:{openBuildingMatch.BDOTReference}|{openBuildingMatch.SourceCountyId}:{openBuildingMatch.SourceId}"));
            }

            Assert.Equal(5, keys.Count);
        }

        /// <summary>
        /// Verifies the offset of a footprint shifted by the ETRS89 versus WGS84 residual (about 0.9 m, DiGi.GIS#21): dx is 0.9 m, dy zero, and the centroid distance 0.9 m - the figures the systematic offset is aggregated from (DiGi.GIS.PostgreSQL#127).
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_DatumOffset()
        {
            List<OpenBuildingMatch>? openBuildingMatches = Query.OpenBuildingMatches([OpenBuildingMatch_Rectangle(1, "d1", true, 0.9, 0, 12, 8)], [OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 12, 8)], new OpenBuildingMatchParameters(), new HashSet<int>() { 1 });
            Assert.NotNull(openBuildingMatches);

            OpenBuildingMatch openBuildingMatch = Assert.Single(openBuildingMatches);
            Assert.Equal(OpenBuildingMatchClass.OneToOne, openBuildingMatch.OpenBuildingMatchClass);
            Assert.Equal(0.9, openBuildingMatch.Dx!.Value, 9);
            Assert.Equal(0.0, openBuildingMatch.Dy!.Value, 9);
            Assert.Equal(0.9, openBuildingMatch.CentroidDistance!.Value, 9);
            Assert.Equal((12 - 0.9) / (12 + 0.9), openBuildingMatch.IoU!.Value, 9);
        }

        /// <summary>
        /// Verifies how the matching's footprint factory treats geometry it cannot use as stored (DiGi.GIS.PostgreSQL#127): a self-intersecting ring is repaired and flagged, a centroid, a missing geometry and a degenerate ring leave the item without a geometry, and a missing key builds nothing. An item without a geometry is never returned - not as an omission, not as a commission.
        /// </summary>
        [Fact]
        public void OpenBuildingMatchItem_UnusableGeometry()
        {
            Polygon2D polygon2D_BowTie = new([new Point2D(openBuildingMatch_X, openBuildingMatch_Y), new Point2D(openBuildingMatch_X + 10, openBuildingMatch_Y + 10), new Point2D(openBuildingMatch_X + 10, openBuildingMatch_Y), new Point2D(openBuildingMatch_X, openBuildingMatch_Y + 10)]);
            PolygonalFace2D? polygonalFace2D_BowTie = Geometry.Planar.Create.PolygonalFace2D(polygon2D_BowTie);
            Assert.NotNull(polygonalFace2D_BowTie);

            OpenBuildingMatchItem? openBuildingMatchItem_BowTie = Create.OpenBuildingMatchItem(1, "bow-tie", 1, "osm", polygonalFace2D_BowTie);
            Assert.NotNull(openBuildingMatchItem_BowTie);
            Assert.True(openBuildingMatchItem_BowTie.Repaired, "the self-intersecting ring was not flagged as repaired.");
            Assert.NotNull(openBuildingMatchItem_BowTie.Geometry);
            Assert.True(openBuildingMatchItem_BowTie.Geometry.IsValid);

            // The repaired bow-tie is two triangles of 25 m² each.
            Assert.Equal(50.0, openBuildingMatchItem_BowTie.Area, 6);

            OpenBuildingMatchItem? openBuildingMatchItem_Point = Create.OpenBuildingMatchItem(1, "point", 1, "osm", new Point2D(openBuildingMatch_X, openBuildingMatch_Y));
            Assert.NotNull(openBuildingMatchItem_Point);
            Assert.Null(openBuildingMatchItem_Point.Geometry);

            OpenBuildingMatchItem? openBuildingMatchItem_Null = Create.OpenBuildingMatchItem(1, "null", null, null, null);
            Assert.NotNull(openBuildingMatchItem_Null);
            Assert.Null(openBuildingMatchItem_Null.Geometry);

            Assert.Null(Create.OpenBuildingMatchItem(1, null, null, null, null));
            Assert.Null(Create.OpenBuildingMatchItem(1, "  ", null, null, null));

            List<OpenBuildingMatch>? openBuildingMatches = Query.OpenBuildingMatches([openBuildingMatchItem_Point], [openBuildingMatchItem_Null], new OpenBuildingMatchParameters(), new HashSet<int>() { 1 });
            Assert.NotNull(openBuildingMatches);
            Assert.Empty(openBuildingMatches);
        }

        /// <summary>
        /// Verifies that the matching's output - rows, their order and their component numbers - is the same under the invariant culture and under <c>pl-PL</c>, and whatever order the footprints arrive in (DiGi.GIS.PostgreSQL#127).
        /// <para>Keys mix cases and Polish letters, whose culture-aware order differs from the ordinal one; the rows are rendered with the invariant culture and compared as one text.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_DeterministicUnderPolishCulture()
        {
            List<OpenBuildingMatchItem> openBuildingMatchItems_Dataset =
            [
                OpenBuildingMatch_Rectangle(1, "ź-1", true, 0, 0, 10, 10),
                OpenBuildingMatch_Rectangle(1, "Z-2", true, 30, 0, 10, 10),
                OpenBuildingMatch_Rectangle(1, "a-3", true, 60, 0, 5, 10),
                OpenBuildingMatch_Rectangle(1, "ą-4", true, 65, 0, 5, 10),
                OpenBuildingMatch_Rectangle(1, "B-5", true, 500, 0, 10, 10),
            ];

            List<OpenBuildingMatchItem> openBuildingMatchItems_BDOT =
            [
                OpenBuildingMatch_Rectangle(1, "ł", false, 0, 0, 10, 10),
                OpenBuildingMatch_Rectangle(1, "L", false, 30, 0, 10, 10),
                OpenBuildingMatch_Rectangle(1, "ś", false, 60, 0, 10, 10),
                OpenBuildingMatch_Rectangle(1, "S", false, 300, 0, 10, 10),
            ];

            static string Render(List<OpenBuildingMatch>? openBuildingMatches)
            {
                Assert.NotNull(openBuildingMatches);

                StringBuilder stringBuilder = new();
                foreach (OpenBuildingMatch openBuildingMatch in openBuildingMatches)
                {
                    stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4:R}", openBuildingMatch.ComponentId, openBuildingMatch.OpenBuildingMatchClass, openBuildingMatch.BDOTReference, openBuildingMatch.SourceId, openBuildingMatch.IoU));
                }

                return stringBuilder.ToString();
            }

            CultureInfo cultureInfo_Current = CultureInfo.CurrentCulture;
            List<string> renders = [];
            try
            {
                foreach (string cultureName in new string[] { string.Empty, "pl-PL" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

                    renders.Add(Render(Query.OpenBuildingMatches(openBuildingMatchItems_Dataset, openBuildingMatchItems_BDOT, new OpenBuildingMatchParameters(), new HashSet<int>() { 1 })));

                    List<OpenBuildingMatchItem> openBuildingMatchItems_DatasetReversed = [.. openBuildingMatchItems_Dataset];
                    openBuildingMatchItems_DatasetReversed.Reverse();
                    List<OpenBuildingMatchItem> openBuildingMatchItems_BDOTReversed = [.. openBuildingMatchItems_BDOT];
                    openBuildingMatchItems_BDOTReversed.Reverse();

                    renders.Add(Render(Query.OpenBuildingMatches(openBuildingMatchItems_DatasetReversed, openBuildingMatchItems_BDOTReversed, new OpenBuildingMatchParameters(), new HashSet<int>() { 1 })));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo_Current;
            }

            Assert.Single(renders.Distinct());

            // Ordinal order: "L" < "S" < "ł" < "ś" - the uppercase ASCII keys own the first components.
            Assert.StartsWith("1|OneToOne|L|Z-2|", renders[0]);
        }

        /// <summary>
        /// Verifies the areas the link rows carry (DiGi.GIS.PostgreSQL#127): each footprint's own area, and the part of it the other side covers - every overlapping footprint counted, linked or not, and several partners unioned so an overlap between them is not counted twice.
        /// <para>Two dataset footprints of 80 and 60 m2 overlapping each other by 40 m2 on one 100 m2 register building cover it whole, not 140 m2. A dataset footprint overlapping a register building by 25 % of either - below the minimum share, so an omission and a commission - still reports the 25 m2 it covers, which is what makes the area-based measures differ from the object-level ones. An excluded footprint carries its area and no covered area.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatches_CoveredAreas()
        {
            List<OpenBuildingMatch>? openBuildingMatches = Query.OpenBuildingMatches(
                [
                    OpenBuildingMatch_Rectangle(1, "d1", true, 0, 0, 8, 10),
                    OpenBuildingMatch_Rectangle(1, "d2", true, 4, 0, 6, 10),
                    OpenBuildingMatch_Rectangle(1, "d3", true, 107.5, 0, 10, 10),
                    OpenBuildingMatch_Rectangle(1, "d4", true, 300, 0, 1, 1),
                ],
                [
                    OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10),
                    OpenBuildingMatch_Rectangle(1, "b2", false, 100, 0, 10, 10),
                ],
                new OpenBuildingMatchParameters(minArea: 2),
                new HashSet<int>() { 1 });
            Assert.NotNull(openBuildingMatches);

            OpenBuildingMatch openBuildingMatch_D1 = openBuildingMatches.Single(x => x.SourceId == "d1");
            OpenBuildingMatch openBuildingMatch_D2 = openBuildingMatches.Single(x => x.SourceId == "d2");
            Assert.Equal(OpenBuildingMatchClass.ManyToOne, openBuildingMatch_D1.OpenBuildingMatchClass);
            Assert.Equal(100.0, openBuildingMatch_D1.BDOTArea!.Value, 9);
            Assert.Equal(100.0, openBuildingMatch_D1.BDOTCoveredArea!.Value, 9);
            Assert.Equal(100.0, openBuildingMatch_D2.BDOTCoveredArea!.Value, 9);
            Assert.Equal(80.0, openBuildingMatch_D1.SourceArea!.Value, 9);
            Assert.Equal(80.0, openBuildingMatch_D1.SourceCoveredArea!.Value, 9);
            Assert.Equal(60.0, openBuildingMatch_D2.SourceCoveredArea!.Value, 9);

            OpenBuildingMatch openBuildingMatch_Omission = openBuildingMatches.Single(x => x.BDOTReference == "b2");
            Assert.Equal(OpenBuildingMatchClass.Omission, openBuildingMatch_Omission.OpenBuildingMatchClass);
            Assert.Equal(25.0, openBuildingMatch_Omission.BDOTCoveredArea!.Value, 9);
            Assert.Null(openBuildingMatch_Omission.SourceArea);

            OpenBuildingMatch openBuildingMatch_Commission = openBuildingMatches.Single(x => x.SourceId == "d3");
            Assert.Equal(OpenBuildingMatchClass.Commission, openBuildingMatch_Commission.OpenBuildingMatchClass);
            Assert.Equal(25.0, openBuildingMatch_Commission.SourceCoveredArea!.Value, 9);

            OpenBuildingMatch openBuildingMatch_Excluded = openBuildingMatches.Single(x => x.SourceId == "d4");
            Assert.Equal(OpenBuildingMatchClass.Excluded, openBuildingMatch_Excluded.OpenBuildingMatchClass);
            Assert.Equal(1.0, openBuildingMatch_Excluded.SourceArea!.Value, 9);
            Assert.Null(openBuildingMatch_Excluded.SourceCoveredArea);

            // Area level differs from object level: b2 is an omission yet a quarter covered.
            List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults = Query.OpenBuildingMatchSummaries(openBuildingMatches, "osm", 1, new OpenBuildingMatchParameters(minArea: 2), null);
            Assert.NotNull(openBuildingMatchSummaryResults);
            Assert.Equal(0.5, openBuildingMatchSummaryResults[0].Completeness!.Value, 12);
            Assert.Equal(125.0 / 200.0, openBuildingMatchSummaryResults[0].AreaCompleteness!.Value, 9);
        }

        /// <summary>
        /// Verifies the in-memory summary of a hand-computed case (DiGi.GIS.PostgreSQL#127): counts, rates, the one-to-one geometry measures and their linear-interpolated percentiles, a total row and one row per primary source.
        /// <para>Four register buildings and six dataset footprints: two one-to-one pairs (identical, register; shifted 1 m, osm), an omission, a commission (osm), a register building split by a register and an osm footprint, and a 4 m² register footprint excluded by a 5 m² minimum. The per-source rows carry no BDOT10k figure, and their dataset counts and commissions sum to the total's - the check DiGi.GIS.WebAPI#108 makes on E4.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatchSummaries_HandComputed()
        {
            OpenBuildingMatchParameters openBuildingMatchParameters = new(minArea: 5);

            List<OpenBuildingMatch>? openBuildingMatches = Query.OpenBuildingMatches(
                [
                    OpenBuildingMatch_Rectangle(1, "d1", true, 0, 0, 10, 10, "register"),
                    OpenBuildingMatch_Rectangle(1, "d2", true, 101, 0, 10, 10, "osm"),
                    OpenBuildingMatch_Rectangle(1, "d3", true, 400, 0, 10, 10, "osm"),
                    OpenBuildingMatch_Rectangle(1, "d4a", true, 300, 0, 10, 10, "register"),
                    OpenBuildingMatch_Rectangle(1, "d4b", true, 310, 0, 10, 10, "osm"),
                    OpenBuildingMatch_Rectangle(1, "d5", true, 600, 0, 2, 2, "register"),
                ],
                [
                    OpenBuildingMatch_Rectangle(1, "b1", false, 0, 0, 10, 10),
                    OpenBuildingMatch_Rectangle(1, "b2", false, 100, 0, 10, 10),
                    OpenBuildingMatch_Rectangle(1, "b3", false, 200, 0, 10, 10),
                    OpenBuildingMatch_Rectangle(1, "b4", false, 300, 0, 20, 10),
                ],
                openBuildingMatchParameters,
                new HashSet<int>() { 1 });

            List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults = Query.OpenBuildingMatchSummaries(openBuildingMatches, "eubucco", 7, openBuildingMatchParameters, null);
            Assert.NotNull(openBuildingMatchSummaryResults);
            Assert.Equal([null, "osm", "register"], openBuildingMatchSummaryResults.ConvertAll(x => x.PrimarySource));

            OpenBuildingMatchSummaryResult total = openBuildingMatchSummaryResults[0];
            Assert.Equal("eubucco", total.DatasetKey);
            Assert.Equal(7, total.DatasetId);
            Assert.Equal(4L, total.BDOTCount);
            Assert.Equal(5L, total.DatasetCount);
            Assert.Equal(0L, total.BDOTExcludedCount);
            Assert.Equal(1L, total.DatasetExcludedCount);
            Assert.Equal(2L, total.OneToOneCount);
            Assert.Equal(0L, total.OneToOneWeakCount);
            Assert.Equal(0L, total.OneToManyCount);
            Assert.Equal(1L, total.ManyToOneCount);
            Assert.Equal(0L, total.ManyToManyCount);
            Assert.Equal(1L, total.OmissionCount);
            Assert.Equal(1L, total.CommissionCount);
            Assert.Equal(0.25, total.OmissionRate!.Value, 12);
            Assert.Equal(0.2, total.CommissionRate!.Value, 12);

            double ioU_Shifted = 9.0 / 11.0;
            Assert.Equal((1 + ioU_Shifted) / 2, total.IoUMean!.Value, 9);
            Assert.Equal((1 + ioU_Shifted) / 2, total.IoUMedian!.Value, 9);
            Assert.Equal(ioU_Shifted + (0.1 * (1 - ioU_Shifted)), total.IoUP10!.Value, 9);
            Assert.Equal(1.0, total.AreaRatioMedian!.Value, 9);
            Assert.Equal(0.5, total.CentroidDistanceMedian!.Value, 9);
            Assert.Equal(0.5, total.DxMean!.Value, 9);
            Assert.Equal(0.5, total.DxMedian!.Value, 9);
            Assert.Equal(0.0, total.DyMean!.Value, 9);
            Assert.NotNull(total.OpenBuildingMatchParameters);
            Assert.Equal(5.0, total.OpenBuildingMatchParameters.MinArea);

            OpenBuildingMatchSummaryResult osm = openBuildingMatchSummaryResults[1];
            Assert.Null(osm.BDOTCount);
            Assert.Null(osm.BDOTExcludedCount);
            Assert.Null(osm.OmissionCount);
            Assert.Null(osm.OmissionRate);
            Assert.Equal(3L, osm.DatasetCount);
            Assert.Equal(1L, osm.CommissionCount);
            Assert.Equal(1.0 / 3.0, osm.CommissionRate!.Value, 12);
            Assert.Equal(1L, osm.OneToOneCount);
            Assert.Equal(1L, osm.ManyToOneCount);
            Assert.Equal(1.0, osm.DxMean!.Value, 9);

            OpenBuildingMatchSummaryResult register = openBuildingMatchSummaryResults[2];
            Assert.Equal(2L, register.DatasetCount);
            Assert.Equal(1L, register.DatasetExcludedCount);
            Assert.Equal(0L, register.CommissionCount);
            Assert.Equal(0.0, register.DxMean!.Value, 9);

            Assert.Equal(total.DatasetCount, osm.DatasetCount + register.DatasetCount);
            Assert.Equal(total.CommissionCount, osm.CommissionCount + register.CommissionCount);
            Assert.Equal(total.DatasetExcludedCount, osm.DatasetExcludedCount + register.DatasetExcludedCount);

            // Object level (ISPRS benchmark): completeness 3/4, correctness 4/5, quality C*R / (C + R - C*R).
            Assert.Equal(0.75, total.Completeness!.Value, 12);
            Assert.Equal(0.8, total.Correctness!.Value, 12);
            Assert.Equal(0.6 / 0.95, total.Quality!.Value, 12);
            Assert.Null(osm.Completeness);
            Assert.Null(osm.Quality);
            Assert.Equal(2.0 / 3.0, osm.Correctness!.Value, 12);
            Assert.Equal(1.0, register.Correctness!.Value, 12);

            // Area level: BDOT10k 100 + 100 + 100 + 200 m2, covered 100 + 90 + 0 + 200; the dataset (d5 excluded)
            // 5 x 100 m2, covered 100 + 90 + 0 + 100 + 100.
            Assert.Equal(390.0 / 500.0, total.AreaCompleteness!.Value, 9);
            Assert.Equal(390.0 / 500.0, total.AreaCorrectness!.Value, 9);
            Assert.Null(osm.AreaCompleteness);
            Assert.Equal(190.0 / 300.0, osm.AreaCorrectness!.Value, 9);
            Assert.Equal(1.0, register.AreaCorrectness!.Value, 9);

            // Offsets of the two one-to-one pairs: dx 0 and 1 m, dy 0.
            Assert.Equal(Math.Sqrt(0.5), total.DxRms!.Value, 9);
            Assert.Equal(0.0, total.DyRms!.Value, 9);
            Assert.Equal(Math.Sqrt(0.5), total.PositionRms!.Value, 9);

            // Size classes: every counted footprint is 50 m2 or more; d5 (4 m2) is excluded, so no class counts it.
            Assert.NotNull(total.SizeClasses);
            Assert.Equal(3, total.SizeClasses.Count);
            Assert.Equal([0.0, 10.0, 50.0], total.SizeClasses.ConvertAll(x => x.MinArea));
            Assert.Equal([10.0, 50.0, null], total.SizeClasses.ConvertAll(x => x.MaxArea));
            Assert.Equal([0L, 0L, 4L], total.SizeClasses.ConvertAll(x => x.BDOTCount));
            Assert.Equal([0L, 0L, 1L], total.SizeClasses.ConvertAll(x => x.OmissionCount));
            Assert.Equal([0L, 0L, 5L], total.SizeClasses.ConvertAll(x => x.DatasetCount));
            Assert.Equal([0L, 0L, 1L], total.SizeClasses.ConvertAll(x => x.CommissionCount));
            Assert.Equal(0.25, total.SizeClasses[2].OmissionRate!.Value, 12);
            Assert.Null(total.SizeClasses[0].OmissionRate);
            Assert.NotNull(osm.SizeClasses);
            Assert.Null(osm.SizeClasses[2].BDOTCount);
            Assert.Equal(3L, osm.SizeClasses[2].DatasetCount);
            Assert.Equal(1L, osm.SizeClasses[2].CommissionCount);

            // No rows: one total row of zeros and no measure.
            List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_Empty = Query.OpenBuildingMatchSummaries([], "eubucco", 7, openBuildingMatchParameters, null);
            Assert.NotNull(openBuildingMatchSummaryResults_Empty);
            OpenBuildingMatchSummaryResult empty = Assert.Single(openBuildingMatchSummaryResults_Empty);
            Assert.Equal(0L, empty.BDOTCount);
            Assert.Null(empty.OmissionRate);
            Assert.Null(empty.IoUMedian);

            Assert.Null(Query.OpenBuildingMatchSummaries(null, "eubucco", 7, openBuildingMatchParameters, null));
        }

        /// <summary>
        /// Verifies that the matching's serializable types survive the JSON round trip and their copy constructors member by member (DiGi.GIS.PostgreSQL#127; Coding - Serialization.md section 9): the rule parameters, the task options, the run result and the summary.
        /// <para>Every member is set to a value other than its default, so a member a constructor forgets to copy, or a field the JSON leaves out, fails its own assertion.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatch_SerializationAndCopy()
        {
            OpenBuildingMatchParameters openBuildingMatchParameters = new(0.6, 0.25, 12.5, 3, 150);
            Core.xUnit.Query.SerializationCheck(openBuildingMatchParameters);

            foreach (OpenBuildingMatchParameters openBuildingMatchParameters_Copy in new OpenBuildingMatchParameters[] { new(openBuildingMatchParameters), new(openBuildingMatchParameters.ToJsonObject()) })
            {
                Assert.Equal(0.6, openBuildingMatchParameters_Copy.IoUThreshold);
                Assert.Equal(0.25, openBuildingMatchParameters_Copy.MinOverlapShare);
                Assert.Equal(12.5, openBuildingMatchParameters_Copy.MinArea);
                Assert.Equal(3.0, openBuildingMatchParameters_Copy.MaxCentroidDistance);
                Assert.Equal(150.0, openBuildingMatchParameters_Copy.BoundaryMargin);
            }

            OpenBuildingMatchParameters openBuildingMatchParameters_Default = new();
            Assert.Equal(0.5, openBuildingMatchParameters_Default.IoUThreshold);
            Assert.Equal(0.3, openBuildingMatchParameters_Default.MinOverlapShare);
            Assert.Equal(0.0, openBuildingMatchParameters_Default.MinArea);
            Assert.Equal(0.0, openBuildingMatchParameters_Default.MaxCentroidDistance);
            Assert.Equal(100.0, openBuildingMatchParameters_Default.BoundaryMargin);

            PostgreSQLOpenBuildingMatchTaskOptions postgreSQLOpenBuildingMatchTaskOptions = new()
            {
                BoundingBox2D = new BoundingBox2D(new Point2D(630000, 480000), new Point2D(631000, 481000)),
                CountyIds = [55417, 29861],
                DatasetKeys = ["eubucco", "osm"],
                OpenBuildingMatchParameters = openBuildingMatchParameters,
                Persist = false,
            };
            Core.xUnit.Query.SerializationCheck(postgreSQLOpenBuildingMatchTaskOptions);

            JsonObjectOrFail(postgreSQLOpenBuildingMatchTaskOptions.ToJsonObject(), out System.Text.Json.Nodes.JsonObject jsonObject_Options);
            foreach (PostgreSQLOpenBuildingMatchTaskOptions postgreSQLOpenBuildingMatchTaskOptions_Copy in new PostgreSQLOpenBuildingMatchTaskOptions[] { new(postgreSQLOpenBuildingMatchTaskOptions), new(jsonObject_Options) })
            {
                Assert.NotNull(postgreSQLOpenBuildingMatchTaskOptions_Copy.BoundingBox2D);
                Assert.Equal(630000, postgreSQLOpenBuildingMatchTaskOptions_Copy.BoundingBox2D.Min.X);
                Assert.Equal(481000, postgreSQLOpenBuildingMatchTaskOptions_Copy.BoundingBox2D.Max.Y);
                Assert.NotSame(postgreSQLOpenBuildingMatchTaskOptions.BoundingBox2D, postgreSQLOpenBuildingMatchTaskOptions_Copy.BoundingBox2D);
                Assert.Equal(new HashSet<int>() { 55417, 29861 }, postgreSQLOpenBuildingMatchTaskOptions_Copy.CountyIds);
                Assert.NotSame(postgreSQLOpenBuildingMatchTaskOptions.CountyIds, postgreSQLOpenBuildingMatchTaskOptions_Copy.CountyIds);
                Assert.Equal(new HashSet<string>() { "eubucco", "osm" }, postgreSQLOpenBuildingMatchTaskOptions_Copy.DatasetKeys);
                Assert.NotNull(postgreSQLOpenBuildingMatchTaskOptions_Copy.OpenBuildingMatchParameters);
                Assert.NotSame(openBuildingMatchParameters, postgreSQLOpenBuildingMatchTaskOptions_Copy.OpenBuildingMatchParameters);
                Assert.Equal(12.5, postgreSQLOpenBuildingMatchTaskOptions_Copy.OpenBuildingMatchParameters.MinArea);
                Assert.False(postgreSQLOpenBuildingMatchTaskOptions_Copy.Persist);
            }

            PostgreSQLOpenBuildingMatchTaskOptions postgreSQLOpenBuildingMatchTaskOptions_Default = new();
            Assert.Null(postgreSQLOpenBuildingMatchTaskOptions_Default.DatasetKeys);
            Assert.True(postgreSQLOpenBuildingMatchTaskOptions_Default.Persist);
            Assert.NotNull(postgreSQLOpenBuildingMatchTaskOptions_Default.OpenBuildingMatchParameters);

            OpenBuildingMatchResult openBuildingMatchResult = new("osm", 3, [55417, 29861], 1000, 900, 2, 5, 7, 1500, 1, true);
            Core.xUnit.Query.SerializationCheck(openBuildingMatchResult);

            JsonObjectOrFail(openBuildingMatchResult.ToJsonObject(), out System.Text.Json.Nodes.JsonObject jsonObject_Result);
            foreach (OpenBuildingMatchResult openBuildingMatchResult_Copy in new OpenBuildingMatchResult[] { new(openBuildingMatchResult), new(jsonObject_Result) })
            {
                Assert.Equal("osm", openBuildingMatchResult_Copy.DatasetKey);
                Assert.Equal(3, openBuildingMatchResult_Copy.DatasetId);
                Assert.Equal([55417, 29861], openBuildingMatchResult_Copy.CountyIds);
                Assert.Equal(1000L, openBuildingMatchResult_Copy.BDOTCount);
                Assert.Equal(900L, openBuildingMatchResult_Copy.DatasetCount);
                Assert.Equal(2L, openBuildingMatchResult_Copy.BDOTRejectedCount);
                Assert.Equal(5L, openBuildingMatchResult_Copy.DatasetRejectedCount);
                Assert.Equal(7L, openBuildingMatchResult_Copy.RepairedCount);
                Assert.Equal(1500L, openBuildingMatchResult_Copy.RowCount);
                Assert.Equal(1L, openBuildingMatchResult_Copy.MarginReachCount);
                Assert.True(openBuildingMatchResult_Copy.Persisted);
            }

            DateTimeOffset computedAt = new(2026, 11, 25, 10, 30, 0, TimeSpan.FromHours(1));
            OpenBuildingMatchSummaryResult openBuildingMatchSummaryResult = new("eubucco", 4, "register", 10, 11, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0.8, 0.9, 0.71, 0.72, 0.41, 1.05, 0.93, 0.85, 0.86, -0.12, -0.11, 0.9, 0.2, 0.95, 0.75, 0.82, 0.64, 0.8, 0.86, [new OpenBuildingMatchSizeClassResult(0, 10, 3, 1, 4, 2)], openBuildingMatchParameters, computedAt);
            Core.xUnit.Query.SerializationCheck(openBuildingMatchSummaryResult);

            OpenBuildingMatchSizeClassResult openBuildingMatchSizeClassResult = new(50, null, null, null, 7, 1);
            Core.xUnit.Query.SerializationCheck(openBuildingMatchSizeClassResult);
            JsonObjectOrFail(openBuildingMatchSizeClassResult.ToJsonObject(), out System.Text.Json.Nodes.JsonObject jsonObject_SizeClass);
            foreach (OpenBuildingMatchSizeClassResult openBuildingMatchSizeClassResult_Trip in new OpenBuildingMatchSizeClassResult[] { new(openBuildingMatchSizeClassResult), new(jsonObject_SizeClass) })
            {
                Assert.Equal(50.0, openBuildingMatchSizeClassResult_Trip.MinArea);
                Assert.Null(openBuildingMatchSizeClassResult_Trip.MaxArea);
                Assert.Null(openBuildingMatchSizeClassResult_Trip.BDOTCount);
                Assert.Null(openBuildingMatchSizeClassResult_Trip.OmissionCount);
                Assert.Null(openBuildingMatchSizeClassResult_Trip.OmissionRate);
                Assert.Equal(7L, openBuildingMatchSizeClassResult_Trip.DatasetCount);
                Assert.Equal(1L, openBuildingMatchSizeClassResult_Trip.CommissionCount);
                Assert.Equal(1.0 / 7.0, openBuildingMatchSizeClassResult_Trip.CommissionRate!.Value, 12);
            }

            JsonObjectOrFail(openBuildingMatchSummaryResult.ToJsonObject(), out System.Text.Json.Nodes.JsonObject jsonObject_Summary);
            foreach (OpenBuildingMatchSummaryResult openBuildingMatchSummaryResult_Copy in new OpenBuildingMatchSummaryResult[] { new(openBuildingMatchSummaryResult), new(jsonObject_Summary) })
            {
                Assert.Equal("eubucco", openBuildingMatchSummaryResult_Copy.DatasetKey);
                Assert.Equal(4, openBuildingMatchSummaryResult_Copy.DatasetId);
                Assert.Equal("register", openBuildingMatchSummaryResult_Copy.PrimarySource);
                Assert.Equal(10L, openBuildingMatchSummaryResult_Copy.BDOTCount);
                Assert.Equal(11L, openBuildingMatchSummaryResult_Copy.DatasetCount);
                Assert.Equal(1L, openBuildingMatchSummaryResult_Copy.BDOTExcludedCount);
                Assert.Equal(2L, openBuildingMatchSummaryResult_Copy.DatasetExcludedCount);
                Assert.Equal(3L, openBuildingMatchSummaryResult_Copy.OneToOneCount);
                Assert.Equal(4L, openBuildingMatchSummaryResult_Copy.OneToOneWeakCount);
                Assert.Equal(5L, openBuildingMatchSummaryResult_Copy.OneToManyCount);
                Assert.Equal(6L, openBuildingMatchSummaryResult_Copy.ManyToOneCount);
                Assert.Equal(7L, openBuildingMatchSummaryResult_Copy.ManyToManyCount);
                Assert.Equal(8L, openBuildingMatchSummaryResult_Copy.OmissionCount);
                Assert.Equal(9L, openBuildingMatchSummaryResult_Copy.CommissionCount);
                Assert.Equal(0.8, openBuildingMatchSummaryResult_Copy.OmissionRate);
                Assert.Equal(0.9, openBuildingMatchSummaryResult_Copy.CommissionRate);
                Assert.Equal(0.71, openBuildingMatchSummaryResult_Copy.IoUMean);
                Assert.Equal(0.72, openBuildingMatchSummaryResult_Copy.IoUMedian);
                Assert.Equal(0.41, openBuildingMatchSummaryResult_Copy.IoUP10);
                Assert.Equal(1.05, openBuildingMatchSummaryResult_Copy.AreaRatioMedian);
                Assert.Equal(0.93, openBuildingMatchSummaryResult_Copy.CentroidDistanceMedian);
                Assert.Equal(0.85, openBuildingMatchSummaryResult_Copy.DxMean);
                Assert.Equal(0.86, openBuildingMatchSummaryResult_Copy.DxMedian);
                Assert.Equal(-0.12, openBuildingMatchSummaryResult_Copy.DyMean);
                Assert.Equal(-0.11, openBuildingMatchSummaryResult_Copy.DyMedian);
                Assert.NotNull(openBuildingMatchSummaryResult_Copy.OpenBuildingMatchParameters);
                Assert.NotSame(openBuildingMatchSummaryResult.OpenBuildingMatchParameters, openBuildingMatchSummaryResult_Copy.OpenBuildingMatchParameters);
                Assert.Equal(0.6, openBuildingMatchSummaryResult_Copy.OpenBuildingMatchParameters.IoUThreshold);
                Assert.Equal(computedAt, openBuildingMatchSummaryResult_Copy.ComputedAt);
                Assert.Equal(0.9, openBuildingMatchSummaryResult_Copy.DxRms);
                Assert.Equal(0.2, openBuildingMatchSummaryResult_Copy.DyRms);
                Assert.Equal(0.95, openBuildingMatchSummaryResult_Copy.PositionRms);
                Assert.Equal(0.75, openBuildingMatchSummaryResult_Copy.Completeness);
                Assert.Equal(0.82, openBuildingMatchSummaryResult_Copy.Correctness);
                Assert.Equal(0.64, openBuildingMatchSummaryResult_Copy.Quality);
                Assert.Equal(0.8, openBuildingMatchSummaryResult_Copy.AreaCompleteness);
                Assert.Equal(0.86, openBuildingMatchSummaryResult_Copy.AreaCorrectness);
                Assert.NotNull(openBuildingMatchSummaryResult_Copy.SizeClasses);
                OpenBuildingMatchSizeClassResult openBuildingMatchSizeClassResult_Copy = Assert.Single(openBuildingMatchSummaryResult_Copy.SizeClasses);
                Assert.NotSame(openBuildingMatchSummaryResult.SizeClasses![0], openBuildingMatchSizeClassResult_Copy);
                Assert.Equal(0.0, openBuildingMatchSizeClassResult_Copy.MinArea);
                Assert.Equal(10.0, openBuildingMatchSizeClassResult_Copy.MaxArea);
                Assert.Equal(3L, openBuildingMatchSizeClassResult_Copy.BDOTCount);
                Assert.Equal(1L, openBuildingMatchSizeClassResult_Copy.OmissionCount);
                Assert.Equal(4L, openBuildingMatchSizeClassResult_Copy.DatasetCount);
                Assert.Equal(2L, openBuildingMatchSizeClassResult_Copy.CommissionCount);
            }
        }

        /// <summary>
        /// Asserts that a serialization produced a JSON object and hands it out non-null.
        /// </summary>
        /// <param name="jsonObject">The serialized object.</param>
        /// <param name="jsonObject_NotNull">The same object, asserted non-null.</param>
        private static void JsonObjectOrFail(System.Text.Json.Nodes.JsonObject? jsonObject, out System.Text.Json.Nodes.JsonObject jsonObject_NotNull)
        {
            Assert.NotNull(jsonObject);
            jsonObject_NotNull = jsonObject;
        }

        /// <summary>
        /// Builds one axis-aligned rectangular footprint for the matching facts, offset from the facts' origin, through the same factory the reads use.
        /// </summary>
        /// <param name="countyId">The county part the footprint is filed under.</param>
        /// <param name="key">The key of the footprint.</param>
        /// <param name="isDataset">True for a dataset footprint (register row 1), false for a BDOT10k footprint.</param>
        /// <param name="x">The X of the lower-left corner relative to the origin.</param>
        /// <param name="y">The Y of the lower-left corner relative to the origin.</param>
        /// <param name="width">The width in metres.</param>
        /// <param name="height">The height in metres.</param>
        /// <param name="primarySource">The primary source of a dataset footprint.</param>
        /// <returns>The footprint, with its geometry.</returns>
        private static OpenBuildingMatchItem OpenBuildingMatch_Rectangle(
            int countyId,
            string key,
            bool isDataset,
            double x,
            double y,
            double width,
            double height,
            string primarySource = "register")
        {
            double x_Min = openBuildingMatch_X + x;
            double y_Min = openBuildingMatch_Y + y;

            Polygon2D polygon2D = new([new Point2D(x_Min, y_Min), new Point2D(x_Min + width, y_Min), new Point2D(x_Min + width, y_Min + height), new Point2D(x_Min, y_Min + height)]);
            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(polygonalFace2D);

            OpenBuildingMatchItem? openBuildingMatchItem = Create.OpenBuildingMatchItem(countyId, key, isDataset ? 1 : null, isDataset ? primarySource : null, polygonalFace2D);
            Assert.NotNull(openBuildingMatchItem);
            Assert.NotNull(openBuildingMatchItem.Geometry);

            return openBuildingMatchItem;
        }
    }
}
