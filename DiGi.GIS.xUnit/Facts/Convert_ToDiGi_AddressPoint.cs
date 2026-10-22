using DiGi.PRG.Geometry.Classes;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the conversion of PRG address points from the shared fixture to address points, against three hand-checked records.
        /// <para>The position is asserted as is: it is already easting first and must not be swapped again.</para>
        /// </summary>
        [Fact]
        public void Convert_ToDiGi_AddressPoint()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_PRG_AddressPoints.xml");
            Assert.NotNull(path);

            List<PunktAdresowy>? punktAdresowys = PRG.Geometry.Create.PunktyAdresowe(path);
            Assert.NotNull(punktAdresowys);
            Assert.NotEmpty(punktAdresowys);

            Dictionary<string, Classes.AddressPoint> dictionary = [];
            foreach (PunktAdresowy punktAdresowy in punktAdresowys)
            {
                Classes.AddressPoint? addressPoint = Convert.ToDiGi(punktAdresowy);
                if (addressPoint?.ReferenceId != null)
                {
                    dictionary[addressPoint.ReferenceId] = addressPoint;
                }
            }

            Classes.AddressPoint addressPoint_Square = dictionary["fad42bf4-a0e8-4fe6-85d7-a3b043b185ca"];
            Assert.Equal("Słowiański", addressPoint_Square.StreetName);
            Assert.Equal("20295", addressPoint_Square.ULIC);
            Assert.Equal("0954047", addressPoint_Square.SIMC);
            Assert.Equal("59-220", addressPoint_Square.PostalCode);
            Assert.Equal("7", addressPoint_Square.HouseNumber);
            Assert.NotNull(addressPoint_Square.Location);
            Assert.Equal(301858.38, addressPoint_Square.Location.X, 2);
            Assert.Equal(374887.73, addressPoint_Square.Location.Y, 2);

            Classes.AddressPoint addressPoint_Czysta = dictionary["8ba20226-6e94-4a81-a018-9cd4e3a5e0d7"];
            Assert.Equal("03513", addressPoint_Czysta.ULIC);
            Assert.Equal("2/4", addressPoint_Czysta.HouseNumber);

            Classes.AddressPoint addressPoint_Babin = dictionary["5632bb59-2978-44b1-b71f-fc2da86a0f03"];
            Assert.Null(addressPoint_Babin.StreetName);
            Assert.Equal("Babin", addressPoint_Babin.LocalityName);
            Assert.Equal("0364618", addressPoint_Babin.SIMC);
            Assert.Equal("59-223", addressPoint_Babin.PostalCode);
            Assert.Equal("6A", addressPoint_Babin.HouseNumber);
            Assert.Equal("Babin 6A", Convert.ToDiGi(addressPoint_Babin)?.Street);

            Assert.Null(Convert.ToDiGi((PunktAdresowy?)null));
        }
    }
}
