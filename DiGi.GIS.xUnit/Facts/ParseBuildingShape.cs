using DiGi.GIS.Enums;
using System;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Parses a <see cref="BuildingShape"/> member name stored in the building shape sample fixture and fails the fact when the name is empty or is not a defined member of the enum.
        /// </summary>
        /// <param name="text">The member name to parse, for example <c>Square</c>.</param>
        /// <returns>The parsed <see cref="BuildingShape"/>.</returns>
        private static BuildingShape ParseBuildingShape(string? text)
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.True(Enum.TryParse(text, out BuildingShape buildingShape) && Enum.IsDefined(buildingShape), $"'{text}' is not a defined BuildingShape.");
            return buildingShape;
        }
    }
}
