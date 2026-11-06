using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the open building import options window can be built and that it works on a copy of the options it is given, without a database or a running application (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#38).
        /// <para>The window is the only way a tray run is scoped, so one whose markup failed to parse, or whose constructor threw on the county list or on a bounding box, would not be found until an operator opened it to import a dataset. The three construction paths are covered: a county scope with a county list, a bounding-box scope with no county list to hand, and no options at all - the last reaching the window through a task constructed by hand.</para>
        /// <para><b>It does not check what the window looks like.</b> A window is laid out by the handle it gets when it is shown, so measuring one that is never shown reports nothing, and showing one during a test run would put a dialog on screen. The controls are private to the window, so the scope radio, the dataset caption and the field values they ended up holding cannot be read from here either - that the dialog behaves correctly on screen is still something a person has to look at once.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImportOptionsWindow_Construction()
        {
            PostgreSQLOpenBuildingImportTaskOptions options = new()
            {
                DatasetKey = "microsoft",
                Version = "TST-1",
                DownloadDate = new DateTime(2026, 11, 6),
                Quadkey = "03210",
                SourcePaths = ["C:/source/chunk.geojsonl"],
                CountyIds = [4816]
            };

            Exception? exception = null;
            bool copied = false;
            bool versionCarried = false;
            bool quadkeyCarried = false;
            bool countyIdsCarried = false;
            bool sourcePathsCarried = false;
            bool untouched = false;

            Thread thread = new(() =>
            {
                try
                {
                    List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                    [
                        new() { Id = 55417, Code = "1465", Name = "m. St. Warszawa", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                        // Two pieces of one multi-part county: same code and name, told apart by the identifier.
                        new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 22139, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
                    ];

                    OpenBuildingImportOptionsWindow openBuildingImportOptionsWindow = new(options, administrativeAreal2DReferences);

                    PostgreSQLOpenBuildingImportTaskOptions held = openBuildingImportOptionsWindow.PostgreSQLOpenBuildingImportTaskOptions;

                    // Until OK is pressed the window holds what it was given.
                    copied = !ReferenceEquals(options, held);
                    versionCarried = held.Version == "TST-1";
                    quadkeyCarried = held.Quadkey == "03210";
                    countyIdsCarried = held.CountyIds is HashSet<int> countyIds && countyIds.Count == 1 && countyIds.Contains(4816);
                    sourcePathsCarried = held.SourcePaths is HashSet<string> sourcePaths && sourcePaths.Count == 1;

                    // A cancelled dialog has to leave the caller's options alone, which only holds if the window
                    // took a copy rather than a reference.
                    held.Version = "changed";

                    // The bounding-box construction branch, with no county list to hand at all.
                    OpenBuildingImportOptionsWindow openBuildingImportOptionsWindow_Box = new(
                        new PostgreSQLOpenBuildingImportTaskOptions
                        {
                            DatasetKey = "osm",
                            BoundingBox2D = new BoundingBox2D(new Point2D(629000, 519000), new Point2D(631000, 521000))
                        },
                        null);
                    _ = openBuildingImportOptionsWindow_Box.PostgreSQLOpenBuildingImportTaskOptions;

                    // No options and no counties: the window opens reporting that no dataset was chosen rather
                    // than throwing, because a task built by hand can reach it.
                    OpenBuildingImportOptionsWindow openBuildingImportOptionsWindow_Empty = new(null, null);
                    _ = openBuildingImportOptionsWindow_Empty.PostgreSQLOpenBuildingImportTaskOptions;

                    untouched = options.Version == "TST-1";
                }
                catch (Exception exception_Temp)
                {
                    exception = exception_Temp;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(exception);
            Assert.True(copied);
            Assert.True(versionCarried);
            Assert.True(quadkeyCarried);
            Assert.True(countyIdsCarried);
            Assert.True(sourcePathsCarried);

            // The caller's own instance is untouched by the edit made to the window's copy above.
            Assert.True(untouched);
        }
    }
}
