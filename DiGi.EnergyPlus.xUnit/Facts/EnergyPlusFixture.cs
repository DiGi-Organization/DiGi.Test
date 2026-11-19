using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Energy.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.HVAC;
using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Classes;
using DiGi.Core.Parameter.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;

namespace DiGi.EnergyPlus.xUnit
{
    public partial class Facts
    {
        private static readonly double[] energyPlus_ExternalTemperatures = [-2.0, -1.0, 3.0, 8.0, 13.0, 16.0, 18.0, 18.0, 14.0, 9.0, 4.0, 0.0];

        /// <summary>
        /// Builds the in-code box of the IDF writer facts, after the heat-demand facts of DiGi.Analytical (DiGi-Organization/DiGi.Analytical#21): a 10 x 8 x 3 m storey with layered walls, roof and floor of declared U 0.30, 0.25 and 0.35, and two 1.2 x 1.5 m windows (Uw 1.3, g 0.6) in the south wall, one with a 1.0 x 1.3 m pane and one without.
        /// <para>Options: the north wall a party wall; the west wall a construction with a declared U of 0.5 and no layers; a third, five-sided window in the south wall; a second storey on top, whose floor bounds both spaces.</para>
        /// </summary>
        /// <param name="partyWall">Whether the north wall is a party wall.</param>
        /// <param name="declaredOnly">Whether the west wall has a construction with a declared U and no layers.</param>
        /// <param name="pentagon">Whether the south wall gets a five-sided window.</param>
        /// <param name="twoStorey">Whether a second storey is added on top.</param>
        /// <returns>The model and the identifiers of its named components.</returns>
        private static (BuildingModel BuildingModel, Dictionary<string, Guid> Guids) EnergyPlus_Model(bool partyWall, bool declaredOnly, bool pentagon, bool twoStorey)
        {
            OpaqueMaterial opaqueMaterial_Insulation = new("Insulation") { Conductivity = 0.04, Density = 30, SpecificHeat = 1400 };
            OpaqueMaterial opaqueMaterial_Brick = new("Brick") { Conductivity = 0.77, Density = 1800, SpecificHeat = 880 };
            OpaqueMaterial opaqueMaterial_Plaster = new("Plaster") { Conductivity = 0.7, Density = 1600, SpecificHeat = 1000 };
            OpaqueMaterial opaqueMaterial_Concrete = new("Concrete") { Conductivity = 2.0, Density = 2400, SpecificHeat = 1000 };

            WallConstruction wallConstruction = new() { Name = "Wall" };
            RoofConstruction roofConstruction = new() { Name = "Roof" };
            FloorConstruction floorConstruction = new() { Name = "Floor" };
            Assert.True(wallConstruction.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 0.30));
            Assert.True(roofConstruction.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 0.25));
            Assert.True(floorConstruction.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 0.35));

            ConstructionRelationCluster constructionRelationCluster = new([opaqueMaterial_Insulation, opaqueMaterial_Brick, opaqueMaterial_Plaster, opaqueMaterial_Concrete, wallConstruction, roofConstruction, floorConstruction]);
            Assert.NotNull(Analytical.Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Insulation, 0.1));
            Assert.NotNull(Analytical.Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Brick, 0.25));
            Assert.NotNull(Analytical.Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Plaster, 0.015));
            Assert.NotNull(Analytical.Building.Create.StructureLayer(constructionRelationCluster, roofConstruction, opaqueMaterial_Insulation, 0.2));
            Assert.NotNull(Analytical.Building.Create.StructureLayer(constructionRelationCluster, roofConstruction, opaqueMaterial_Concrete, 0.15));
            Assert.NotNull(Analytical.Building.Create.StructureLayer(constructionRelationCluster, floorConstruction, opaqueMaterial_Insulation, 0.1));
            Assert.NotNull(Analytical.Building.Create.StructureLayer(constructionRelationCluster, floorConstruction, opaqueMaterial_Concrete, 0.1));

            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Update(constructionRelationCluster, wallConstruction));
            Assert.True(buildingModel.Update(constructionRelationCluster, roofConstruction));
            Assert.True(buildingModel.Update(constructionRelationCluster, floorConstruction));

            WallConstruction wallConstruction_Declared = new() { Name = "Declared" };
            Assert.True(wallConstruction_Declared.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 0.5));
            Assert.True(buildingModel.Update(wallConstruction_Declared));

            Dictionary<string, Guid> guids = [];

            void Assign(List<IComponent> components, IList<Space> spaces, string suffix, bool skipRoof, bool sharedFloor)
            {
                string[] names = ["Floor", "Roof", "South", "East", "North", "West"];
                for (int i = 0; i < components.Count; i++)
                {
                    if (i == 1 && skipRoof)
                    {
                        continue;
                    }

                    IComponent component = components[i];
                    if (i == 4 && partyWall)
                    {
                        Assert.True(((Component)component).SetValue(ComponentParameter.PartyWall, Guid.NewGuid()));
                    }

                    Assert.True(i == 0 && sharedFloor ? buildingModel.Assign(component, spaces[0], spaces[1]) : buildingModel.Assign(component, spaces[^1]));
                    Assert.True(component switch
                    {
                        IFloor floor => buildingModel.Assign(floor, floorConstruction),
                        IRoof roof => buildingModel.Assign(roof, roofConstruction),
                        IWall wall => buildingModel.Assign(wall, i == 5 && declaredOnly ? wallConstruction_Declared : wallConstruction),
                        _ => false,
                    });

                    guids[names[i] + suffix] = component.Guid;
                }
            }

            Space space_Lower = new(new Point3D(5, 4, 1.5), "Lower");
            List<IComponent> components_Lower = EnergyPlus_Box(0, 0, 0, 10, 8, 3);
            Assign(components_Lower, [space_Lower], string.Empty, twoStorey, false);

            if (twoStorey)
            {
                Space space_Upper = new(new Point3D(5, 4, 4.5), "Upper");
                Assign(EnergyPlus_Box(0, 0, 3, 10, 8, 6), [space_Lower, space_Upper], " Upper", false, true);
            }

            WindowConstruction windowConstruction_Pane = new("Window with pane", EnergyPlus_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));
            WindowConstruction windowConstruction_NoPane = new("Window without panes", EnergyPlus_Rectangle(0, 0, 1.2, 1.5));
            PolygonalFace2D? polygonalFace2D_Pentagon = Geometry.Planar.Create.PolygonalFace2D(new Polygon2D([new Point2D(0, 0), new Point2D(1.2, 0), new Point2D(1.2, 1.0), new Point2D(0.6, 1.5), new Point2D(0, 1.0)]));
            Assert.NotNull(polygonalFace2D_Pentagon);

            WindowConstruction windowConstruction_Pentagon = new("Pentagon", polygonalFace2D_Pentagon);
            foreach (WindowConstruction windowConstruction in new[] { windowConstruction_Pane, windowConstruction_NoPane, windowConstruction_Pentagon })
            {
                Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.ThermalTransmittance, 1.3));
                Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.TotalSolarEnergyTransmittance, 0.6));
            }

            IPhysicalComponent physicalComponent_South = (IPhysicalComponent)components_Lower[2];
            Window window_Pane = new(EnergyPlus_Plane(2, 0, 0.8));
            Window window_NoPane = new(EnergyPlus_Plane(6, 0, 0.8));
            Assert.True(buildingModel.Assign(physicalComponent_South, window_Pane, windowConstruction_Pane));
            Assert.True(buildingModel.Assign(physicalComponent_South, window_NoPane, windowConstruction_NoPane));
            guids["Window Pane"] = window_Pane.Guid;
            guids["Window NoPane"] = window_NoPane.Guid;

            if (pentagon)
            {
                Window window_Pentagon = new(EnergyPlus_Plane(8.2, 0, 0.8));
                Assert.True(buildingModel.Assign(physicalComponent_South, window_Pentagon, windowConstruction_Pentagon));
                guids["Window Pentagon"] = window_Pentagon.Guid;
            }

            Analytical.Building.Classes.InternalCondition internalCondition = new("Test");

            Thermostat thermostat = new();
            thermostat[ThermostatProfileType.LowerLimit] = new Profile([20.0]);

            InternalGain internalGain = new();
            internalGain[InternalGainProfileType.TotalInternalGain] = new Profile([5.0]);
            internalGain[InternalGainProfileType.Infiltration] = new Profile([0.3]);

            Assert.True(internalCondition.SetThermostat(thermostat));
            Assert.True(internalCondition.SetInternalGain(internalGain));
            Assert.True(internalCondition.SetSystemProperties(new SystemProperties() { OutsideAirPerArea = 0.3 }));
            Assert.True(buildingModel.AssignInternalCondition(internalCondition));

            return (buildingModel, guids);
        }

        /// <summary>
        /// Builds the six faces of a box as components, in the order floor, roof, south, east, north, west.
        /// </summary>
        /// <param name="xMin">The minimum X.</param>
        /// <param name="yMin">The minimum Y.</param>
        /// <param name="zMin">The minimum Z.</param>
        /// <param name="xMax">The maximum X.</param>
        /// <param name="yMax">The maximum Y.</param>
        /// <param name="zMax">The maximum Z.</param>
        /// <returns>The components.</returns>
        private static List<IComponent> EnergyPlus_Box(double xMin, double yMin, double zMin, double xMax, double yMax, double zMax)
        {
            List<List<Point3D>> rings =
            [
                [new Point3D(xMin, yMin, zMin), new Point3D(xMin, yMax, zMin), new Point3D(xMax, yMax, zMin), new Point3D(xMax, yMin, zMin)],
                [new Point3D(xMin, yMin, zMax), new Point3D(xMax, yMin, zMax), new Point3D(xMax, yMax, zMax), new Point3D(xMin, yMax, zMax)],
                [new Point3D(xMin, yMin, zMin), new Point3D(xMax, yMin, zMin), new Point3D(xMax, yMin, zMax), new Point3D(xMin, yMin, zMax)],
                [new Point3D(xMax, yMin, zMin), new Point3D(xMax, yMax, zMin), new Point3D(xMax, yMax, zMax), new Point3D(xMax, yMin, zMax)],
                [new Point3D(xMax, yMax, zMin), new Point3D(xMin, yMax, zMin), new Point3D(xMin, yMax, zMax), new Point3D(xMax, yMax, zMax)],
                [new Point3D(xMin, yMax, zMin), new Point3D(xMin, yMin, zMin), new Point3D(xMin, yMin, zMax), new Point3D(xMin, yMax, zMax)]
            ];

            List<IComponent> result = [];
            for (int i = 0; i < rings.Count; i++)
            {
                Polygon3D? polygon3D = Geometry.Spatial.Create.Polygon3D(rings[i]);
                Assert.NotNull(polygon3D);

                PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(polygon3D);
                Assert.NotNull(polygonalFace3D);

                result.Add(i switch
                {
                    0 => new FaceFloor(polygonalFace3D),
                    1 => new SurfaceRoof(polygonalFace3D),
                    _ => new SurfaceWall(polygonalFace3D)
                });
            }

            return result;
        }

        /// <summary>
        /// Builds the plane of an opening in a wall facing south: X along the wall, Y up.
        /// </summary>
        /// <param name="x">The X of the insertion point.</param>
        /// <param name="y">The Y of the insertion point.</param>
        /// <param name="z">The Z of the insertion point.</param>
        /// <returns>The plane.</returns>
        private static Plane EnergyPlus_Plane(double x, double y, double z)
        {
            return new Plane(new Point3D(x, y, z), new Vector3D(1, 0, 0), new Vector3D(0, 0, 1));
        }

        /// <summary>
        /// Builds a rectangular opening outline, optionally with rectangular holes for the panes.
        /// </summary>
        /// <param name="minX">The minimum X.</param>
        /// <param name="minY">The minimum Y.</param>
        /// <param name="maxX">The maximum X.</param>
        /// <param name="maxY">The maximum Y.</param>
        /// <param name="holes">The panes.</param>
        /// <returns>The outline.</returns>
        private static PolygonalFace2D EnergyPlus_Rectangle(double minX, double minY, double maxX, double maxY, params (double MinX, double MinY, double MaxX, double MaxY)[] holes)
        {
            static Polygon2D CreatePolygon2D(double minX, double minY, double maxX, double maxY)
            {
                return new Polygon2D([new Point2D(minX, minY), new Point2D(maxX, minY), new Point2D(maxX, maxY), new Point2D(minX, maxY)]);
            }

            List<Geometry.Planar.Interfaces.IPolygonal2D> polygonal2Ds_Internal = [];
            foreach ((double MinX, double MinY, double MaxX, double MaxY) hole in holes)
            {
                polygonal2Ds_Internal.Add(CreatePolygon2D(hole.MinX, hole.MinY, hole.MaxX, hole.MaxY));
            }

            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(CreatePolygon2D(minX, minY, maxX, maxY), polygonal2Ds_Internal.Count == 0 ? null : polygonal2Ds_Internal);
            Assert.NotNull(polygonalFace2D);

            return polygonalFace2D;
        }

        /// <summary>
        /// Builds the monthly climate of the IDF writer facts: fixed monthly temperatures and no irradiation.
        /// </summary>
        /// <returns>The monthly climate.</returns>
        private static MonthlyClimate EnergyPlus_Climate()
        {
            return new MonthlyClimate(energyPlus_ExternalTemperatures, -20.0, 7.6, null, "test climate");
        }

        /// <summary>
        /// Splits an IDF into its objects: comments removed, each object its class name and its fields, every comma-separated value a field.
        /// </summary>
        /// <param name="text">The text of the IDF.</param>
        /// <returns>The objects in file order.</returns>
        private static List<(string ClassName, List<string> Fields)> EnergyPlus_Objects(string text)
        {
            System.Text.StringBuilder stringBuilder = new();
            foreach (string line in text.Split('\n'))
            {
                int index = line.IndexOf('!');
                stringBuilder.Append(index < 0 ? line : line[..index]).Append(' ');
            }

            List<(string ClassName, List<string> Fields)> result = [];
            foreach (string @object in stringBuilder.ToString().Split(';'))
            {
                if (string.IsNullOrWhiteSpace(@object))
                {
                    continue;
                }

                List<string> values = [.. @object.Split(',').Select(x => x.Trim())];
                result.Add((values[0], values.GetRange(1, values.Count - 1)));
            }

            return result;
        }

        /// <summary>
        /// Gets the fields of the object of a class whose first field is the given name.
        /// </summary>
        /// <param name="objects">The objects of an IDF.</param>
        /// <param name="className">The class name.</param>
        /// <param name="name">The name.</param>
        /// <returns>The fields.</returns>
        private static List<string> EnergyPlus_Object(List<(string ClassName, List<string> Fields)> objects, string className, string name)
        {
            List<string>? result = objects.Find(x => x.ClassName == className && x.Fields.Count > 0 && x.Fields[0] == name).Fields;
            Assert.NotNull(result);

            return result;
        }

        /// <summary>
        /// Gets the thermal resistance of the layers of a construction from the materials of an IDF: d/λ of a <c>Material</c>, R of a <c>Material:NoMass</c>.
        /// </summary>
        /// <param name="objects">The objects of an IDF.</param>
        /// <param name="constructionName">The name of the construction.</param>
        /// <returns>The resistance [m2K/W].</returns>
        private static double EnergyPlus_ThermalResistance(List<(string ClassName, List<string> Fields)> objects, string constructionName)
        {
            double result = 0.0;
            foreach (string materialName in EnergyPlus_Object(objects, "Construction", constructionName).Skip(1))
            {
                (string ClassName, List<string> Fields) material = objects.Find(x => (x.ClassName == "Material" || x.ClassName == "Material:NoMass") && x.Fields[0] == materialName);
                Assert.NotNull(material.Fields);

                result += material.ClassName == "Material" ? EnergyPlus_Double(material.Fields[2]) / EnergyPlus_Double(material.Fields[3]) : EnergyPlus_Double(material.Fields[2]);
            }

            return result;
        }

        /// <summary>
        /// Parses a numeric IDF field, which is always written in the invariant culture.
        /// </summary>
        /// <param name="value">The field.</param>
        /// <returns>The number.</returns>
        private static double EnergyPlus_Double(string value)
        {
            return double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
