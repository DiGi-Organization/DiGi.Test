using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Geometry.Spatial.Classes;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a <see cref="BuildingModel"/> returned by <see cref="Core.Query.Clone{TSerializableObject}(TSerializableObject)"/> is isolated from its source (DiGi-Organization/DiGi.Core#14).
        /// <para>A 10 x 10 x 3 m box gets a 10 x 5 x 3 m neighbour against half of its east wall. Splitting the party walls of the clone gives the clone five walls; the source keeps its four, its spaces and its components. Before the fix <c>ParametrizedObject.Clone</c> was a <c>MemberwiseClone</c>, so the clone shared the source's relation cluster and the split reached the source.</para>
        /// </summary>
        [Fact]
        public void BuildingModel_Clone_Isolated()
        {
            BuildingModel buildingModel = new();
            Assert.NotNull(AddBoxSpace(buildingModel, new BoundingBox3D(new Point3D(0, 0, 0), new Point3D(10, 10, 3)), new Space(new Point3D(5, 5, 1.5), "Space"), null, null, null));

            BuildingModel buildingModel_Neighbour = new();
            Assert.NotNull(AddBoxSpace(buildingModel_Neighbour, new BoundingBox3D(new Point3D(10, 0, 0), new Point3D(20, 5, 3)), new Space(new Point3D(15, 2.5, 1.5), "Space Neighbour"), null, null, null));

            Assert.Equal(4, buildingModel.GetComponents<IWall>()!.Count);

            BuildingModel? buildingModel_Clone = Core.Query.Clone(buildingModel);
            Assert.NotNull(buildingModel_Clone);
            Assert.NotSame(buildingModel, buildingModel_Clone);
            Assert.Equal(buildingModel.Guid, buildingModel_Clone.Guid);

            List<PartyWallResult>? partyWallResults = Building.Query.PartyWalls(buildingModel_Clone, [buildingModel_Neighbour]);
            Assert.NotNull(partyWallResults);
            Assert.Single(partyWallResults);

            Assert.True(Building.Modify.SplitPartyWalls(buildingModel_Clone, partyWallResults));
            Assert.Equal(5, buildingModel_Clone.GetComponents<IWall>()!.Count);

            // A clone of the split model keeps the Guid of the neighbour on its party wall (an object member holding a Guid).
            BuildingModel? buildingModel_Clone_Split = Core.Query.Clone(buildingModel_Clone);
            Assert.NotNull(buildingModel_Clone_Split);
            List<Guid> guids_PartyWall = [];
            foreach (IWall wall in buildingModel_Clone_Split.GetComponents<IWall>()!)
            {
                if (wall is Component component && component.TryGetValue(new Core.Parameter.Classes.EnumParameterDefinition(Building.Enums.ComponentParameter.PartyWall), out Guid guid))
                {
                    guids_PartyWall.Add(guid);
                }
            }

            Assert.Equal([buildingModel_Neighbour.Guid], guids_PartyWall);

            // The source is untouched.
            Assert.Equal(4, buildingModel.GetComponents<IWall>()!.Count);
            Assert.Equal(6, buildingModel.GetComponents<IComponent>()!.Count);
            Assert.Single(buildingModel.GetSpaces<ISpace>()!);

            // The other way round: a space added to the source does not reach the clone.
            BuildingModel? buildingModel_Clone_2 = Core.Query.Clone(buildingModel);
            Assert.NotNull(buildingModel_Clone_2);
            Assert.NotNull(AddBoxSpace(buildingModel, new BoundingBox3D(new Point3D(0, 0, 3), new Point3D(10, 10, 6)), new Space(new Point3D(5, 5, 4.5), "Space 2"), null, null, null));
            Assert.Equal(2, buildingModel.GetSpaces<ISpace>()!.Count);
            Assert.Single(buildingModel_Clone_2.GetSpaces<ISpace>()!);
        }
    }
}
