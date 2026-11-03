using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the orthophoto usability rubric to its version and checks the score on reference sheets.
        /// <para>The table of expected values is keyed by <see cref="Constants.OrtoUsability.Version"/>: changing a weight or factor without bumping the version fails the pinned comparison, and bumping it fails until the new version has its own row. Shown to fail by changing <see cref="Constants.OrtoUsability.ResolutionWeight"/> alone.</para>
        /// </summary>
        [Fact]
        public void OrtoSheetUsability()
        {
            Dictionary<int, double[]> pinned = new()
            {
                [1] = [0.35, 0.15, 0.15, 0.10, 0.25, 0.5, 1.00, 0.90, 0.60, 0.35, 0.15, 1.00, 0.70, 0.50, 1.00, 0.90, 0.50, 0.60, 1.00, 0.60, 0.80, 0.05, 0.05],
            };

            Assert.True(pinned.ContainsKey(Constants.OrtoUsability.Version), "OrtoUsability.Version has no pinned row: add the new rubric values to this fact.");

            double[] values =
            [
                Constants.OrtoUsability.ResolutionWeight,
                Constants.OrtoUsability.OrtoTypeWeight,
                Constants.OrtoUsability.ColorModeWeight,
                Constants.OrtoUsability.SourceWeight,
                Constants.OrtoUsability.ClarityWeight,
                Constants.OrtoUsability.SheetIncompleteCap,
                .. Constants.OrtoUsability.ResolutionValues,
                Constants.OrtoUsability.TrueOrthoValue,
                Constants.OrtoUsability.StandardOrthoValue,
                Constants.OrtoUsability.UndefinedOrthoValue,
                Constants.OrtoUsability.CIRValue,
                Constants.OrtoUsability.RGBValue,
                Constants.OrtoUsability.BlackAndWhiteValue,
                Constants.OrtoUsability.UndefinedColorModeValue,
                Constants.OrtoUsability.DigitalSourceValue,
                Constants.OrtoUsability.AnalogSourceValue,
                Constants.OrtoUsability.UndefinedSourceValue,
                Constants.OrtoUsability.LeafOnPrior,
                Constants.OrtoUsability.SnowRiskPrior,
            ];

            Assert.Equal(pinned[Constants.OrtoUsability.Version], values);
            Assert.Equal([0.10, 0.25, 0.50, 1.00, 2.00], Constants.OrtoUsability.ResolutionKnots);
            Assert.Equal(1, Constants.OrtoUsability.ResolutionWeight + Constants.OrtoUsability.OrtoTypeWeight + Constants.OrtoUsability.ColorModeWeight + Constants.OrtoUsability.SourceWeight + Constants.OrtoUsability.ClarityWeight, 9);

            // Best case: 0.05 m true CIR digital, probed with no obstacles.
            Assert.Equal(1, Query.OrtoSheetUsability(Sheet(0.05, OrtoColorMode.CIR, "Zdj. cyfrowe", OrtoType.TrueOrtho, true, new DateTimeOffset(2023, 4, 22, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day), 0)!.Value, 9);

            // 2005 analog B/W 0.5 m, unprobed, year-only date (leaf-on prior applies): 0.35*0.6 + 0.15*0.5 + 0.15*0.5 + 0.1*0.6 + 0.25*0.95.
            Classes.OrtoSheet ortoSheet_2005 = Sheet(0.5, OrtoColorMode.BlackAndWhite, "Zdj. analogowe", OrtoType.Undefined, true, new DateTimeOffset(2005, 1, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Year);
            Assert.Equal(0.6575, Query.OrtoSheetUsability(ortoSheet_2005)!.Value, 9);
            Assert.Equal(OrtoObstacle.Unprobed | OrtoObstacle.DateYearPrecision | OrtoObstacle.TrueOrthoUnknown, Query.OrtoObstacles(ortoSheet_2005));

            // Interpolation between 0.25 m (0.9) and 0.5 m (0.6): 0.375 m scores 0.75 on resolution.
            double usability_375 = Query.OrtoSheetUsability(Sheet(0.375, OrtoColorMode.RGB, "Zdj. cyfrowe", OrtoType.Standard, true, new DateTimeOffset(2020, 11, 5, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day))!.Value;
            Assert.Equal((0.35 * 0.75) + (0.15 * 0.7) + (0.15 * 0.9) + (0.10 * 1) + (0.25 * 1), usability_375, 9);

            // A snow-risk month costs the prior; a measured obstacle fraction replaces the priors.
            Classes.OrtoSheet ortoSheet_January = Sheet(0.25, OrtoColorMode.RGB, "Zdj. cyfrowe", OrtoType.Standard, true, new DateTimeOffset(2020, 1, 15, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day);
            Assert.Equal((0.35 * 0.9) + (0.15 * 0.7) + (0.15 * 0.9) + 0.10 + (0.25 * 0.95), Query.OrtoSheetUsability(ortoSheet_January)!.Value, 9);
            Assert.Equal((0.35 * 0.9) + (0.15 * 0.7) + (0.15 * 0.9) + 0.10 + (0.25 * 0.6), Query.OrtoSheetUsability(ortoSheet_January, 0.4)!.Value, 9);
            Assert.Equal(OrtoObstacle.Snow, Query.OrtoObstacles(ortoSheet_January, OrtoObstacle.Snow));

            // An incomplete sheet is capped.
            Classes.OrtoSheet ortoSheet_Incomplete = Sheet(0.05, OrtoColorMode.CIR, "Zdj. cyfrowe", OrtoType.TrueOrtho, false, new DateTimeOffset(2023, 4, 22, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day);
            Assert.Equal(Constants.OrtoUsability.SheetIncompleteCap, Query.OrtoSheetUsability(ortoSheet_Incomplete, 0)!.Value, 9);
            Assert.True(Query.OrtoObstacles(ortoSheet_Incomplete, OrtoObstacle.None).HasFlag(OrtoObstacle.SheetIncomplete));

            Assert.Null(Query.OrtoSheetUsability(null));

            static Classes.OrtoSheet Sheet(double pixelSize, OrtoColorMode ortoColorMode, string source, OrtoType ortoType, bool sheetFilled, DateTimeOffset date, OrtoDatePrecision ortoDatePrecision)
            {
                return new Classes.OrtoSheet("S", "SkorowidzOrtofomapy" + date.Year, date.Year, date, ortoDatePrecision, pixelSize, ortoColorMode, source, null, null, null, sheetFilled, null, null, null, null, ortoType, null);
            }
        }
    }
}
