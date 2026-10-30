using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Analytical.Building.Regulation.Enums;
using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the serialization and the copy constructors of OpeningSize, OpeningSizeSet and OpeningSizeLibrary with every member populated (DiGi-Organization/DiGi.GIS.Analytical#6).
        /// </summary>
        [Fact]
        public void OpeningSize_Serialization()
        {
            OpeningSize openingSize = new(OpeningKind.Window, RoomUse.LivingRoom, 1.20, 2.20, 0.80, true, 2, 0.12, 0.10, 0.55, "Size: DEFAULT test");
            Assert.Equal(OpeningKind.Window, openingSize.OpeningKind);
            Assert.Equal(RoomUse.LivingRoom, openingSize.RoomUse);
            Assert.Equal(1.20, openingSize.Width);
            Assert.Equal(2.20, openingSize.Height);
            Assert.Equal(0.80, openingSize.SillHeight);
            Assert.True(openingSize.Glazed);
            Assert.Equal(2, openingSize.Columns);
            Assert.Equal(0.12, openingSize.FrameWidth);
            Assert.Equal(0.10, openingSize.DividerWidth);
            Assert.Equal(0.55, openingSize.TopLightHeight);
            Assert.Equal("Size: DEFAULT test", openingSize.Source);

            Assert.Equal(Core.Convert.ToSystem_String(openingSize), Core.Convert.ToSystem_String(new OpeningSize(openingSize)));
            Assert.Equal(Core.Convert.ToSystem_String(openingSize), Core.Convert.ToSystem_String(Core.Convert.ToDiGi<OpeningSize>(Core.Convert.ToSystem_String(openingSize))?.FirstOrDefault()));
            Core.xUnit.Query.SerializationCheck(openingSize);

            OpeningSize openingSize_Door = new(OpeningKind.EntranceDoor, RoomUse.Other, 1.00, 2.10, null, false, 1, 0.05, 0.05, null, null);
            DateTimeOffset startDate = new(1967, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset endDate = new(1986, 1, 1, 0, 0, 0, TimeSpan.Zero);
            OpeningSizeSet openingSizeSet = new(BuildingType.ApartmentBlock, startDate, endDate, "W-70", true, 2.80, 0.60, [openingSize, openingSize_Door], [RoomUse.Bathroom], "System: PL-ITB test");
            Assert.Equal(BuildingType.ApartmentBlock, openingSizeSet.BuildingType);
            Assert.Equal(startDate, openingSizeSet.StartDate);
            Assert.Equal(endDate, openingSizeSet.EndDate);
            Assert.Equal("W-70", openingSizeSet.System);
            Assert.True(openingSizeSet.DefaultSystem);
            Assert.Equal(2.80, openingSizeSet.StoreyHeight);
            Assert.Equal(0.60, openingSizeSet.FacadeModule);
            Assert.Equal(2, openingSizeSet.GetOpeningSizes().Count);
            Assert.Equal([RoomUse.Bathroom], openingSizeSet.WindowlessRoomUses);
            Assert.Equal("System: PL-ITB test", openingSizeSet.Source);

            // Lookup by room use, falling back to the first size of the kind; a windowless room use has none.
            Assert.Equal(1.20, openingSizeSet.GetOpeningSize(OpeningKind.Window, RoomUse.Kitchen)?.Width);
            Assert.Null(openingSizeSet.GetOpeningSize(OpeningKind.Window, RoomUse.Bathroom));
            Assert.Null(openingSizeSet.GetOpeningSize(OpeningKind.GarageDoor, RoomUse.Other));
            Assert.True(openingSizeSet.Includes(startDate));
            Assert.False(openingSizeSet.Includes(endDate));

            Assert.Equal(Core.Convert.ToSystem_String(openingSizeSet), Core.Convert.ToSystem_String(new OpeningSizeSet(openingSizeSet)));
            Core.xUnit.Query.SerializationCheck(openingSizeSet);

            OpeningSizeLibrary openingSizeLibrary = new(Core.Create.Guid("Test.OpeningSizeLibrary"), "Test", "1.0.0", "Test library");
            Assert.True(openingSizeLibrary.Add(openingSizeSet));
            Assert.False(openingSizeLibrary.Add(null));
            Assert.Equal("Test", openingSizeLibrary.Name);
            Assert.Equal("1.0.0", openingSizeLibrary.Version);
            Assert.Equal("Test library", openingSizeLibrary.Description);
            Assert.Equal(Core.Convert.ToSystem_String(openingSizeLibrary), Core.Convert.ToSystem_String(new OpeningSizeLibrary(openingSizeLibrary)));
            Core.xUnit.Query.SerializationCheck(openingSizeLibrary);
        }

        /// <summary>
        /// Tests Building.Regulation.Query.Panes and the window and door constructions made from opening sizes: panes are inside the outline, never touch, and leave the frame and dividers as the remainder; an opaque door has none (DiGi-Organization/DiGi.GIS.Analytical#6).
        /// </summary>
        [Fact]
        public void OpeningSize_Panes()
        {
            const double tolerance = 1e-9;

            OpeningSize openingSize = new(OpeningKind.Window, RoomUse.LivingRoom, 1.20, 2.20, 0.80, true, 2, 0.12, 0.10, 0.55, null);
            List<Polygon2D>? panes = Building.Regulation.Query.Panes(openingSize);
            Assert.NotNull(panes);
            Assert.Equal(3, panes.Count);

            // Two sashes of (1.20 - 0.24 - 0.10) / 2 = 0.43 m, 2.20 - 0.24 - 0.55 - 0.10 = 1.31 m high, and a top light of 0.96 x 0.55 m.
            Assert.Equal(0.43 * 1.31, panes[0].GetArea(), tolerance);
            Assert.Equal(0.43 * 1.31, panes[1].GetArea(), tolerance);
            Assert.Equal(0.96 * 0.55, panes[2].GetArea(), tolerance);

            Building.Classes.WindowConstruction? windowConstruction = Building.Regulation.Create.WindowConstruction(openingSize);
            Assert.NotNull(windowConstruction);
            Assert.Equal("Window 1200x2200", windowConstruction.Name);

            OpeningSize openingSize_Door = new(OpeningKind.EntranceDoor, RoomUse.Other, 1.00, 2.10, null, false, 1, 0.05, 0.05, null, null);
            Assert.Empty(Building.Regulation.Query.Panes(openingSize_Door)!);
            Assert.NotNull(Building.Regulation.Create.DoorConstruction(openingSize_Door));
            Assert.Null(Building.Regulation.Query.Panes(null));

            // A layout leaving no positive pane gives none.
            Assert.Empty(Building.Regulation.Query.Panes(new OpeningSize(OpeningKind.Window, RoomUse.Other, 0.20, 0.20, 0.85, true, 2, 0.10, 0.10, null, null))!);
        }

        /// <summary>
        /// Tests Building.Regulation.Create.OpeningSizeLibrary_PL against the sources and the rules the opening generator applies (DiGi-Organization/DiGi.GIS.Analytical#6).
        /// <para>Completeness: every building type has a set for every date, every set has a living-room, kitchen and other window, a bathroom window or a documented "none", an entrance door, and every multi-family set from 1946 a balcony door. Rules, for every window: sill + height at most the storey height less a 0.30 m head clearance (slab and lintel), and at least 0.8 m between windows of consecutive storeys (WT § 223) - balcony doors are exempt, as the balcony slab is the projection the paragraph allows. Entrance doors of sets from 2003 have at least 0.9 x 2.0 m in the clear (WT § 62), the clear height measured under the head of the frame. Panel sets: window widths plus the 35 mm catalogue gap are multiples of the system's module.</para>
        /// <para>Lookup: a 1975 apartment block gives the W-70 set, a named system its own set; a 1910 multi-family building gets a tenement window of at least 2.0 m in a storey of at least 3.40 m and a 1935 one the inter-war set; 1917 and 1918 fall on either side of the split. Every source item is tagged.</para>
        /// </summary>
        [Fact]
        public void OpeningSizeLibrary_PL()
        {
            const double tolerance = 1e-9;

            OpeningSizeLibrary openingSizeLibrary = Building.Regulation.Create.OpeningSizeLibrary_PL();
            Assert.Equal(Core.Convert.ToSystem_String(openingSizeLibrary), Core.Convert.ToSystem_String(Building.Regulation.Create.OpeningSizeLibrary_PL()));

            static DateTimeOffset Date(int year)
            {
                return new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
            }

            string[] tags = ["PL-BN85", "PL-ITB", "PL-OWT", "PL-WAW", "PL-ROG", "PL-1928", "PL-WT", "DEFAULT"];
            void AssertTagged(string? source)
            {
                Assert.False(string.IsNullOrWhiteSpace(source));
                foreach (string item in source!.Split(["; "], StringSplitOptions.None))
                {
                    int index = item.IndexOf(": ", StringComparison.Ordinal);
                    Assert.True(index > 0, item);
                    string value = item.Substring(index + 2);
                    Assert.True(value == "none" || tags.Any(x => value.StartsWith(x, StringComparison.Ordinal)), item);
                }
            }

            List<OpeningSizeSet> openingSizeSets = openingSizeLibrary.GetOpeningSizeSets();
            Assert.Equal((8 * 4) - 1 + 6, openingSizeSets.Count);

            foreach (OpeningSizeSet openingSizeSet in openingSizeSets)
            {
                string key = openingSizeSet.BuildingType + " " + openingSizeSet.StartDate.Year + " " + openingSizeSet.System;
                AssertTagged(openingSizeSet.Source);

                List<OpeningSize> openingSizes = openingSizeSet.GetOpeningSizes();
                foreach (RoomUse roomUse in new RoomUse[] { RoomUse.LivingRoom, RoomUse.Kitchen, RoomUse.Other })
                {
                    Assert.True(openingSizes.Any(x => x.OpeningKind == OpeningKind.Window && x.RoomUse == roomUse) || (roomUse == RoomUse.Other && openingSizes.Count(x => x.OpeningKind == OpeningKind.Window) >= 2), key + " " + roomUse);
                }

                Assert.True(openingSizes.Any(x => x.OpeningKind == OpeningKind.Window && x.RoomUse == RoomUse.Bathroom) ^ openingSizeSet.WindowlessRoomUses.Contains(RoomUse.Bathroom), key + " bathroom");
                Assert.Contains(openingSizes, x => x.OpeningKind == OpeningKind.EntranceDoor || x.OpeningKind == OpeningKind.StaircaseEntranceDoor);

                bool multiFamily = openingSizeSet.BuildingType == BuildingType.MultiFamily || openingSizeSet.BuildingType == BuildingType.ApartmentBlock;
                if (multiFamily && openingSizeSet.StartDate >= Date(1946))
                {
                    Assert.Contains(openingSizes, x => x.OpeningKind == OpeningKind.BalconyDoor);
                }

                foreach (OpeningSize openingSize in openingSizes)
                {
                    string key_Size = key + " " + openingSize.OpeningKind + " " + openingSize.RoomUse;
                    AssertTagged(openingSize.Source);
                    Assert.True(openingSize.Width > 0 && openingSize.Height > 0, key_Size);

                    if (openingSize.Glazed)
                    {
                        List<Polygon2D>? panes = Building.Regulation.Query.Panes(openingSize);
                        Assert.NotNull(panes);
                        Assert.NotEmpty(panes);
                    }

                    if (openingSize.OpeningKind == OpeningKind.Window || openingSize.OpeningKind == OpeningKind.StaircaseWindow || openingSize.OpeningKind == OpeningKind.RoofWindow || openingSize.OpeningKind == OpeningKind.BalconyDoor)
                    {
                        Assert.NotNull(Building.Regulation.Create.WindowConstruction(openingSize));
                    }
                    else
                    {
                        Assert.NotNull(Building.Regulation.Create.DoorConstruction(openingSize));
                    }

                    if (openingSize.OpeningKind == OpeningKind.RoofWindow)
                    {
                        continue;
                    }

                    double top = (openingSize.SillHeight ?? 0.0) + openingSize.Height;
                    Assert.True(top <= openingSizeSet.StoreyHeight - 0.30 + tolerance, key_Size + " head");

                    if (openingSize.OpeningKind == OpeningKind.Window || openingSize.OpeningKind == OpeningKind.StaircaseWindow)
                    {
                        Assert.NotNull(openingSize.SillHeight);
                        Assert.True(openingSizeSet.StoreyHeight - openingSize.Height >= 0.8 - tolerance, key_Size + " strip");

                        if (openingSizeSet.FacadeModule is double facadeModule && openingSize.OpeningKind == OpeningKind.Window)
                        {
                            double modules = (openingSize.Width + 0.035) / facadeModule;
                            Assert.True(System.Math.Abs(modules - System.Math.Round(modules)) < 1e-6, key_Size + " module");
                        }
                    }

                    bool entrance = openingSize.OpeningKind == OpeningKind.EntranceDoor || openingSize.OpeningKind == OpeningKind.StaircaseEntranceDoor;
                    if (entrance && openingSizeSet.StartDate >= Date(2003))
                    {
                        double width_Clear = (openingSize.Width - (2 * openingSize.FrameWidth) - ((openingSize.Columns - 1) * openingSize.DividerWidth)) / openingSize.Columns;
                        Assert.True(width_Clear >= 0.9 - tolerance, key_Size + " clear width");
                        Assert.True(openingSize.Height - openingSize.FrameWidth >= 2.0 - tolerance, key_Size + " clear height");
                    }
                }
            }

            // Every building type has a set for every year.
            foreach (BuildingType buildingType in new BuildingType[] { BuildingType.SingleFamily, BuildingType.Terraced, BuildingType.MultiFamily, BuildingType.ApartmentBlock })
            {
                foreach (int year in new int[] { 1850, 1917, 1918, 1945, 1946, 1966, 1967, 1985, 1986, 1992, 1993, 2002, 2003, 2008, 2009, 2025 })
                {
                    Assert.NotNull(openingSizeLibrary.GetOpeningSizeSet(Date(year), buildingType));
                }
            }

            // 1975 apartment block: W-70 by default, a named system on request.
            OpeningSizeSet? openingSizeSet_W70 = openingSizeLibrary.GetOpeningSizeSet(Date(1975), BuildingType.ApartmentBlock);
            Assert.Equal("W-70", openingSizeSet_W70?.System);
            Assert.Equal(2.80, openingSizeSet_W70!.StoreyHeight, tolerance);
            Assert.Equal(0.60, openingSizeSet_W70.FacadeModule);
            Assert.Equal(1.765, openingSizeSet_W70.GetOpeningSize(OpeningKind.Window, RoomUse.LivingRoom)!.Width, tolerance);
            Assert.Null(openingSizeSet_W70.GetOpeningSize(OpeningKind.Window, RoomUse.Bathroom));
            Assert.StartsWith("System: PL-ITB", openingSizeSet_W70.Source);

            OpeningSizeSet? openingSizeSet_OWT67 = openingSizeLibrary.GetOpeningSizeSet(Date(1975), BuildingType.ApartmentBlock, "owt-67");
            Assert.Equal("OWT-67", openingSizeSet_OWT67?.System);
            Assert.Equal(2.70, openingSizeSet_OWT67!.StoreyHeight, tolerance);
            Assert.Equal(1.50, openingSizeSet_OWT67.GetOpeningSize(OpeningKind.Window, RoomUse.LivingRoom)!.Height + 0.065, tolerance);
            Assert.Null(openingSizeLibrary.GetOpeningSizeSet(Date(1975), BuildingType.ApartmentBlock, "Unknown"));
            Assert.Null(openingSizeLibrary.GetOpeningSizeSet(Date(1990), BuildingType.ApartmentBlock, "W-70"));
            Assert.Null(openingSizeLibrary.GetOpeningSizeSet(Date(1975), BuildingType.MultiFamily)?.System);

            // Tenement and inter-war multi-family buildings.
            OpeningSizeSet? openingSizeSet_Tenement = openingSizeLibrary.GetOpeningSizeSet(Date(1910), BuildingType.MultiFamily);
            Assert.True(openingSizeSet_Tenement!.StoreyHeight >= 3.40 - tolerance);
            Assert.True(openingSizeSet_Tenement.GetOpeningSize(OpeningKind.Window, RoomUse.LivingRoom)!.Height >= 2.0);
            Assert.NotNull(openingSizeSet_Tenement.GetOpeningSize(OpeningKind.Window, RoomUse.LivingRoom)!.TopLightHeight);
            Assert.StartsWith("StoreyHeight: PL-WAW", openingSizeSet_Tenement.Source);

            Assert.Equal(1918, openingSizeLibrary.GetOpeningSizeSet(Date(1935), BuildingType.MultiFamily)!.StartDate.Year);
            Assert.Equal(3.40, openingSizeLibrary.GetOpeningSizeSet(Date(1917), BuildingType.MultiFamily)!.StoreyHeight, tolerance);
            Assert.Equal(3.10, openingSizeLibrary.GetOpeningSizeSet(Date(1918), BuildingType.MultiFamily)!.StoreyHeight, tolerance);
        }

        /// <summary>
        /// Writes the sizes of Building.Regulation.Create.OpeningSizeLibrary_PL to reports/OpeningSizes.md for the Opening-Sizes wiki page, with each window's frame fraction 1 - Σ(pane area) / outline area against the F_f = 0.3 TABULA assumes for every Polish building (DiGi-Organization/DiGi.GIS.Analytical#6).
        /// </summary>
        [Fact]
        public void OpeningSizeLibrary_PL_Report()
        {
            OpeningSizeLibrary openingSizeLibrary = Building.Regulation.Create.OpeningSizeLibrary_PL();

            static string Text(double? value)
            {
                return value is double value_Temp ? value_Temp.ToString("0.###", CultureInfo.InvariantCulture) : "-";
            }

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("| Type | From | To | System | Storey [m] | Module [m] | Opening | Room | W x H [m] | Sill [m] | Sashes | Frame fraction | Sources |");
            stringBuilder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            List<double> frameFractions = [];
            foreach (OpeningSizeSet openingSizeSet in openingSizeLibrary.GetOpeningSizeSets().OrderBy(x => x.BuildingType).ThenBy(x => x.StartDate).ThenBy(x => x.System))
            {
                string from = openingSizeSet.StartDate == DateTimeOffset.MinValue ? "-" : openingSizeSet.StartDate.Year.ToString(CultureInfo.InvariantCulture);
                string to = openingSizeSet.EndDate is DateTimeOffset endDate ? (endDate.Year - 1).ToString(CultureInfo.InvariantCulture) : "-";
                string system = openingSizeSet.System is null ? "-" : openingSizeSet.System + (openingSizeSet.DefaultSystem ? " (default)" : string.Empty);

                foreach (OpeningSize openingSize in openingSizeSet.GetOpeningSizes())
                {
                    string frameFraction = "-";
                    List<Polygon2D>? panes = Building.Regulation.Query.Panes(openingSize);
                    if (panes is not null && panes.Count != 0)
                    {
                        double fraction = 1.0 - (panes.Sum(x => x.GetArea()) / (openingSize.Width * openingSize.Height));
                        frameFraction = fraction.ToString("0.00", CultureInfo.InvariantCulture);
                        if (openingSize.OpeningKind == OpeningKind.Window)
                        {
                            frameFractions.Add(fraction);
                        }
                    }

                    stringBuilder.AppendLine("| " + openingSizeSet.BuildingType + " | " + from + " | " + to + " | " + system + " | " + Text(openingSizeSet.StoreyHeight) + " | " + Text(openingSizeSet.FacadeModule) + " | " + openingSize.OpeningKind + " | " + openingSize.RoomUse + " | " + Text(openingSize.Width) + " x " + Text(openingSize.Height) + " | " + Text(openingSize.SillHeight) + " | " + openingSize.Columns + (openingSize.TopLightHeight is null ? string.Empty : " + top light") + " | " + frameFraction + " | " + openingSize.Source + " |");
                }
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Window frame fraction: mean " + frameFractions.Average().ToString("0.00", CultureInfo.InvariantCulture) + ", min " + frameFractions.Min().ToString("0.00", CultureInfo.InvariantCulture) + ", max " + frameFractions.Max().ToString("0.00", CultureInfo.InvariantCulture) + " (TABULA assumes 0.30).");

            string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(directory);

            string path = Path.Combine(directory, "OpeningSizes.md");
            File.WriteAllText(path, stringBuilder.ToString());
            Assert.True(File.Exists(path));
        }
    }
}
