using DiGi.Core.Classes;
using DiGi.GIS.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>Verifies that RemoveAll clears every opened GIS model file without throwing while the backing dictionary is enumerated, by opening two on-disk files into a manager and asserting the manager is empty afterwards.</summary>
        [Fact]
        public void GISModelFileManager_RemoveAll()
        {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), string.Format(CultureInfo.CurrentCulture, "DiGi.GIS.xUnit_{0}", Guid.NewGuid().ToString("N")));
            System.IO.Directory.CreateDirectory(directory);

            try
            {
                string path_1 = System.IO.Path.Combine(directory, string.Format(CultureInfo.CurrentCulture, "model_1.{0}", Constants.FileExtension.GISModelFile));
                string path_2 = System.IO.Path.Combine(directory, string.Format(CultureInfo.CurrentCulture, "model_2.{0}", Constants.FileExtension.GISModelFile));

                CreateGISModelFile(path_1);
                CreateGISModelFile(path_2);

                GISModelFileManager gISModelFileManager = new();

                GuidExternalReference? guidExternalReference_1 = gISModelFileManager.Open(path_1);
                GuidExternalReference? guidExternalReference_2 = gISModelFileManager.Open(path_2);

                Assert.NotNull(guidExternalReference_1);
                Assert.NotNull(guidExternalReference_2);

                HashSet<GuidExternalReference>? guidExternalReferences = gISModelFileManager.GetGuidExternalReferences();
                Assert.NotNull(guidExternalReferences);
                Assert.Equal(2, guidExternalReferences.Count);

                bool removed = gISModelFileManager.RemoveAll();
                Assert.True(removed);

                HashSet<GuidExternalReference>? guidExternalReferences_Remaining = gISModelFileManager.GetGuidExternalReferences();
                Assert.True(guidExternalReferences_Remaining is null || guidExternalReferences_Remaining.Count == 0);
            }
            finally
            {
                if (System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.Delete(directory, true);
                }
            }
        }

        private static void CreateGISModelFile(string path)
        {
            using GISModelFile gISModelFile = new(path);
            gISModelFile.Value = new GISModel();
            gISModelFile.Save();
        }
    }
}