using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Walks the validation chain of <see cref="OrtoAreaController"/> with a valid user token, so every refusal observed here is the guard under test rather than the session gate.
        /// <para>The bounding-box guard refuses a non-finite corner, an unordered box and a side over <see cref="Constants.OrtoArea.MaximumAreaSide"/> on every GET route carrying it, while a side of exactly the maximum passes; the <c>label</c> chain then runs body, box, state (0/1/2 each accepted past the state check), epoch, exactly-one identity (reference XOR objectId - neither the county part nor a polygon is part of the identity), and finally the area-exists check - and <c>objects</c> rejects a degenerate or unreadable polygon before it reads the store.</para>
        /// <para>All converters sit on null ConnectionData, which answers null without opening a connection, so no assertion here depends on a database: the checks fire in the documented order before any store read, and the store reads that are reached answer null (400 "initialize first") rather than throwing.</para>
        /// </summary>
        [Fact]
        public async Task OrtoAreaController_Validation()
        {
            string path = OrtoAreaConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OrtoAreaController controller = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher, supplyToken: true);

                // Bounding-box guard wired on every GET route: a non-finite corner refuses before the store is read,
                // and on the image route before any cache lookup or WMS fetch could run.
                Assert.IsType<BadRequestObjectResult>(await controller.AvailableYearsByBoundingBoxAsync(double.NaN, 0, 100, 100));
                Assert.IsType<BadRequestObjectResult>(await controller.AvailableYearsByBoundingBoxAsync(0, 0, 100, double.PositiveInfinity));
                Assert.IsType<BadRequestObjectResult>(await controller.ImageByBoundingBoxAsync(double.NaN, 0, 100, 100, 2020));
                Assert.IsType<BadRequestObjectResult>(await controller.AreaByBoundingBoxAsync(double.NaN, 0, 100, 100));
                Assert.IsType<BadRequestObjectResult>(await controller.LabelsByBoundingBoxAsync(double.NaN, 0, 100, 100));

                // min > max refuses on both axes.
                Assert.IsType<BadRequestObjectResult>(await controller.AvailableYearsByBoundingBoxAsync(100, 0, 0, 100));
                Assert.IsType<BadRequestObjectResult>(await controller.AvailableYearsByBoundingBoxAsync(0, 100, 100, 0));

                // A side one metre over the maximum refuses on both axes; a side of exactly the maximum passes the
                // guard (the null sheet read behind it answers 200 with the empty epoch list, never 400).
                Assert.IsType<BadRequestObjectResult>(await controller.AvailableYearsByBoundingBoxAsync(0, 0, Constants.OrtoArea.MaximumAreaSide + 1, 100));
                Assert.IsType<BadRequestObjectResult>(await controller.AvailableYearsByBoundingBoxAsync(0, 0, 100, Constants.OrtoArea.MaximumAreaSide + 1));
                Assert.IsType<OkObjectResult>(await controller.AvailableYearsByBoundingBoxAsync(0, 0, Constants.OrtoArea.MaximumAreaSide, 100));

                // label, body then box: a non-finite box on a non-null body refuses with the box message, ahead of
                // every label-specific check.
                BadRequestObjectResult badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.OrtoAreaLabelParameter { X_1 = double.NaN, Y_1 = 0, X_2 = 100, Y_2 = 100, State = 0, EpochYear = 2024, ObjectId = "object-id" }));
                Assert.Contains("finite", Assert.IsType<string>(badRequestObjectResult.Value));

                // label, state: an omitted or out-of-range state refuses with the state message itself, so the 400
                // is the state guard and not a later check sharing the status.
                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_StateOmitted = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, EpochYear = 2024, ObjectId = "object-id" };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_StateOmitted));
                Assert.Contains("State must be", Assert.IsType<string>(badRequestObjectResult.Value));

                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_State3 = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, State = 3, EpochYear = 2024, ObjectId = "object-id" };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_State3));
                Assert.Contains("State must be", Assert.IsType<string>(badRequestObjectResult.Value));

                // label, epoch: EpochYear must be present once the state passes.
                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_EpochOmitted = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, State = 0, ObjectId = "object-id" };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_EpochOmitted));
                Assert.Contains("EpochYear is required", Assert.IsType<string>(badRequestObjectResult.Value));

                // label, identity: both identities given and neither given are each refused with the exactly-one
                // message, so no identity combination slips through to a store read.
                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_BothIdentities = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, CountyId = 1, Reference = "reference", ObjectId = "object-id", EpochYear = 2024, State = 0 };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_BothIdentities));
                Assert.Contains("Exactly one object identity", Assert.IsType<string>(badRequestObjectResult.Value));

                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_NoIdentity = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, EpochYear = 2024, State = 0 };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_NoIdentity));
                Assert.Contains("Exactly one object identity", Assert.IsType<string>(badRequestObjectResult.Value));

                // label, drawn identity carries no polygon: the body's polygon left the contract - the stored object
                // under the objectId resolves the dates - so the label runs past identity and every other guard
                // straight to the area-exists check.
                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_NoPolygon = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, EpochYear = 2024, State = 0, ObjectId = "object-id" };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_NoPolygon));
                Assert.Contains("initialize it first", Assert.IsType<string>(badRequestObjectResult.Value));

                // label, footprint identity is the reference alone: the bbox payload carries no county id, so a
                // reference without CountyId must pass identity validation and stop at the same area-exists check -
                // the CountyId is resolved from building_2d server-side, which needs an initialized area.
                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_ReferenceOnly = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, Reference = "reference", EpochYear = 2024, State = 0 };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_ReferenceOnly));
                Assert.Contains("initialize it first", Assert.IsType<string>(badRequestObjectResult.Value));

                // label, state 0/1/2 accepted: each legal state passes the state guard and the rest of the chain
                // down to the area-exists check, which answers "initialize first" for the never-created area -
                // reaching that message (not the state message) is what proves the state was accepted.
                short[] states = [0, 1, 2];
                foreach (short state in states)
                {
                    Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_StateAccepted = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, EpochYear = 2024, State = state, ObjectId = "object-id" };
                    badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_StateAccepted));
                    Assert.Contains("initialize it first", Assert.IsType<string>(badRequestObjectResult.Value));
                }

                // label, unknown area (footprint identity): the area-exists check answers 400 before the building_2d
                // lookup, so an uninitialized area is 400 here and the 404 can only follow an existing area.
                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter_UnknownArea = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, CountyId = 1, Reference = "reference", EpochYear = 2024, State = 0 };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(ortoAreaLabelParameter_UnknownArea));
                Assert.Contains("initialize it first", Assert.IsType<string>(badRequestObjectResult.Value));

                // objects, bounding box: an over-limit side refuses before the polygon is parsed or the store read.
                Classes.Parameter.OrtoAreaObjectParameter ortoAreaObjectParameter_OverLimit = new() { X_1 = 0, Y_1 = 0, X_2 = Constants.OrtoArea.MaximumAreaSide + 1, Y_2 = 100, Polygon = OrtoAreaPolygonJson([0, 0], [10, 0], [10, 10], [0, 10]) };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.CreateObjectAsync(ortoAreaObjectParameter_OverLimit));
                Assert.Contains("finite", Assert.IsType<string>(badRequestObjectResult.Value));

                // objects, degenerate polygon: two vertices are readable but fewer than three, refused with the
                // vertex-count message before any area read.
                Classes.Parameter.OrtoAreaObjectParameter ortoAreaObjectParameter_TwoVertices = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, Polygon = OrtoAreaPolygonJson([0, 0], [10, 10]) };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.CreateObjectAsync(ortoAreaObjectParameter_TwoVertices));
                Assert.Contains("at least 3 vertices", Assert.IsType<string>(badRequestObjectResult.Value));

                // objects, unreadable vertex: one vertex that is not a number disqualifies the whole polygon rather
                // than silently dropping the vertex.
                JsonArray jsonArray_Point_1 = [];
                jsonArray_Point_1.Add(0);
                jsonArray_Point_1.Add(0);
                JsonArray jsonArray_Point_Unreadable = [];
                jsonArray_Point_Unreadable.Add("not a number");
                jsonArray_Point_Unreadable.Add(10);
                JsonArray jsonArray_Polygon_Unreadable = [jsonArray_Point_1, jsonArray_Point_Unreadable];

                Classes.Parameter.OrtoAreaObjectParameter ortoAreaObjectParameter_Unreadable = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, Polygon = jsonArray_Polygon_Unreadable };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.CreateObjectAsync(ortoAreaObjectParameter_Unreadable));
                Assert.Contains("Polygon is missing or unreadable", Assert.IsType<string>(badRequestObjectResult.Value));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
