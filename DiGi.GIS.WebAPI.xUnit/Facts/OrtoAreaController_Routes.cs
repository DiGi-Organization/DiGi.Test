using DiGi.GIS.WebAPI.Classes;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the route templates of <see cref="OrtoAreaController"/> - six routes, <c>objects</c> carrying both the POST that creates a drawn polygon and the DELETE that removes one - to the frozen <c>gis/ortoarea/&lt;segment&gt;</c> wire contract.
        /// <para>The parameter names and route segments are relayed by hand by DiGi.GIS.WebAPI.UI, so a renamed or mistyped template breaks the relay silently: ASP.NET ignores an unknown route and the client never learns why. Each template is resolved through reflection here, so any rename fails the suite instead of production.</para>
        /// </summary>
        [Fact]
        public void OrtoAreaController_Routes()
        {
            Assert.Equal("gis/ortoarea/availableyearsbyboundingbox", DiGi.WebAPI.Query.Path<OrtoAreaController>(nameof(OrtoAreaController.AvailableYearsByBoundingBoxAsync)));
            Assert.Equal("gis/ortoarea/imagebyboundingbox", DiGi.WebAPI.Query.Path<OrtoAreaController>(nameof(OrtoAreaController.ImageByBoundingBoxAsync)));
            Assert.Equal("gis/ortoarea/areabyboundingbox", DiGi.WebAPI.Query.Path<OrtoAreaController>(nameof(OrtoAreaController.AreaByBoundingBoxAsync)));
            Assert.Equal("gis/ortoarea/objects", DiGi.WebAPI.Query.Path<OrtoAreaController>(nameof(OrtoAreaController.CreateObjectAsync)));
            Assert.Equal("gis/ortoarea/objects", DiGi.WebAPI.Query.Path<OrtoAreaController>(nameof(OrtoAreaController.RemoveObjectAsync)));
            Assert.Equal("gis/ortoarea/labelsbyboundingbox", DiGi.WebAPI.Query.Path<OrtoAreaController>(nameof(OrtoAreaController.LabelsByBoundingBoxAsync)));
            Assert.Equal("gis/ortoarea/label", DiGi.WebAPI.Query.Path<OrtoAreaController>(nameof(OrtoAreaController.SetLabelAsync)));
        }
    }
}
