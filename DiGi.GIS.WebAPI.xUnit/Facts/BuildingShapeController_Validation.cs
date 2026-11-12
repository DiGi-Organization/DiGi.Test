using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the labelling gate of <c>gis/buildingshape</c> (DiGi.GIS.WebAPI#80): with <c>AllowUpdateBuildingShapeLabel=false</c> a fully valid label body and an authorized sample load both answer the gate's bare 400 (a message-less <see cref="BadRequestResult"/>), so the flag fires before body validation and before any store read.
        /// </summary>
        [Fact]
        public async Task BuildingShapeController_Gate()
        {
            string path = BuildingShapeConfigurationFilePath(allowUpdateBuildingShapeLabel: false, open: false);

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingShapeController controller = CreateBuildingShapeController(gISWebAPIConfigurationFileWatcher, supplyToken: true);

                Classes.Parameter.BuildingShapeLabelParameter buildingShapeLabelParameter = new() { SampleKey = "gis12-2026", CountyId = 1465, Reference = "REF", Shape = 4 };
                Assert.IsType<BadRequestResult>(await controller.SetLabelAsync(buildingShapeLabelParameter));
                Assert.IsType<BadRequestResult>(await controller.SetLabelAsync(null));

                Assert.IsType<BadRequestResult>(await controller.CreateSampleAsync("gis12-2026", [1465], 500, 12, key: buildingShapeKey));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies the parameter validation of <c>gis/buildingshape</c> with a valid token and the gate open, and that a store that cannot answer is reported as a failure, never as an empty result (DiGi.GIS.WebAPI#80).
        /// <para>A missing sample key answers a message-bearing 400 on every read; a label body missing its sample key, county id, reference or shape, or carrying a shape outside <c>BuildingShape</c> (99, -1), answers 400; the sample load refuses a missing key, empty county ids, a size outside 1…10 000 and a missing seed. With converters on no database, <c>nextsample</c> finds nothing (404), while <c>progress</c>, a valid label and the exports answer 500 - an unreachable store is never read as zero, stored or empty.</para>
        /// </summary>
        [Fact]
        public async Task BuildingShapeController_Validation()
        {
            string path = BuildingShapeConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingShapeController controller = CreateBuildingShapeController(gISWebAPIConfigurationFileWatcher, supplyToken: true);

                Assert.IsType<BadRequestObjectResult>(await controller.NextSampleAsync(" "));
                Assert.IsType<BadRequestObjectResult>(await controller.ProgressAsync(null));
                Assert.IsType<BadRequestObjectResult>(await controller.LabelsAsync(null));
                Assert.IsType<BadRequestObjectResult>(await controller.SampleItemsAsync(null));

                Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(null));
                Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { CountyId = 1465, Reference = "REF", Shape = 4 }));
                Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { SampleKey = "gis12-2026", Reference = "REF", Shape = 4 }));
                Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { SampleKey = "gis12-2026", CountyId = 1465, Shape = 4 }));
                Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { SampleKey = "gis12-2026", CountyId = 1465, Reference = "REF" }));
                Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { SampleKey = "gis12-2026", CountyId = 1465, Reference = "REF", Shape = 99 }));
                Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { SampleKey = "gis12-2026", CountyId = 1465, Reference = "REF", Shape = -1 }));

                Assert.IsType<BadRequestObjectResult>(await controller.CreateSampleAsync(null, [1465], 500, 12));
                Assert.IsType<BadRequestObjectResult>(await controller.CreateSampleAsync("gis12-2026", [], 500, 12));
                Assert.IsType<BadRequestObjectResult>(await controller.CreateSampleAsync("gis12-2026", [1465], 0, 12));
                Assert.IsType<BadRequestObjectResult>(await controller.CreateSampleAsync("gis12-2026", [1465], 10001, 12));
                Assert.IsType<BadRequestObjectResult>(await controller.CreateSampleAsync("gis12-2026", [1465], 500, null));

                // No database: nothing to label is a 404, but an unreadable count, an unstored label and an unreadable export are failures.
                Assert.IsType<NotFoundResult>(await controller.NextSampleAsync("gis12-2026"));
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.ProgressAsync("gis12-2026")).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { SampleKey = "gis12-2026", CountyId = 1465, Reference = "REF", Shape = 0 })).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.LabelsAsync("gis12-2026")).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.SampleItemsAsync("gis12-2026")).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.CreateSampleAsync("gis12-2026", [1465], 500, 12)).StatusCode);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
