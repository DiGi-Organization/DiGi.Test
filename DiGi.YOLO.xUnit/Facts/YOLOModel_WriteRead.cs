using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DiGi.YOLO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a dataset written by <see cref="Modify.Write(Classes.YOLOModel?)"/> reads back through <see cref="Modify.Read(string?)"/> with its images, categories, label 0 "Building", bounding boxes and an absolute "path:".
        /// <para>Before DiGi-Organization/DiGi.YOLO#16 Write wrote conf.yaml and then called WriteScripts, whose template conf.yaml replaced it: every dataset written this way ended up with no labels and "path: training", so training from it failed or read the wrong directory. The dataset directory sits under a folder with a space, which the old "%20" encoding also broke.</para>
        /// </summary>
        [Fact]
        public void YOLOModel_WriteRead()
        {
            string directory = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_Test_" + Path.GetRandomFileName());

            try
            {
                string directory_Source = Path.Combine(directory, "source");
                Directory.CreateDirectory(directory_Source);

                string directory_Dataset = Path.Combine(directory, "user files", "training");

                Classes.YOLOModel yOLOModel = new(directory_Dataset);
                Assert.True(yOLOModel.Add("Building"));

                Enums.Category[] categories = [Enums.Category.Train, Enums.Category.Train, Enums.Category.Validate, Enums.Category.Test];
                for (int i = 0; i < categories.Length; i++)
                {
                    string path_Image = Path.Combine(directory_Source, string.Format(CultureInfo.CurrentCulture, "image{0}.jpeg", i));
                    File.WriteAllBytes(path_Image, [0xFF, 0xD8, 0xFF, 0xD9]);

                    Assert.True(yOLOModel.Add(path_Image, categories[i]));
                    Assert.True(yOLOModel.Add(path_Image, "Building", new Classes.BoundingBox(0.5, 0.5, 0.25, 0.125)));
                }

                Assert.True(Modify.Write(yOLOModel));

                string path_Configuration = Path.Combine(directory_Dataset, "conf.yaml");
                string configuration = File.ReadAllText(path_Configuration);
                Assert.Contains(string.Concat("path: ", directory_Dataset.Replace('\\', '/')), configuration);
                Assert.Contains("0: Building", configuration);
                Assert.DoesNotContain("path: training", configuration);

                //The scripts are laid down beside the dataset, and writing them again keeps the dataset's conf.yaml
                Assert.True(File.Exists(Path.Combine(directory_Dataset, "train.py")));
                Assert.True(Modify.WriteScripts(directory_Dataset));
                Assert.Equal(configuration, File.ReadAllText(path_Configuration));

                Classes.YOLOModel? yOLOModel_Read = Modify.Read(path_Configuration);
                Assert.NotNull(yOLOModel_Read);
                Assert.Null(yOLOModel_Read!.Messages);
                Assert.Equal(directory_Dataset, yOLOModel_Read.Directory);

                Classes.Label? label = yOLOModel_Read.GetLabel(0);
                Assert.Equal("Building", label?.Name);

                List<Classes.Image> images_Train = [.. yOLOModel_Read.GetImages(Enums.Category.Train)];
                List<Classes.Image> images_Validate = [.. yOLOModel_Read.GetImages(Enums.Category.Validate)];
                List<Classes.Image> images_Test = [.. yOLOModel_Read.GetImages(Enums.Category.Test)];
                Assert.Equal(2, images_Train.Count);
                Assert.Single(images_Validate);
                Assert.Single(images_Test);

                Classes.LabelFile? labelFile = yOLOModel_Read.GetLabelFile(images_Train[0].Path);
                Assert.NotNull(labelFile);
                Classes.BoundingBox boundingBox = labelFile!.GetBoundingBoxes(0).Single();
                Assert.Equal(0.5, boundingBox.X);
                Assert.Equal(0.125, boundingBox.Height);

                //Writing a dataset whose images are already in place - here under an upper-cased spelling of the same paths - must not copy a file onto itself
                Classes.YOLOModel yOLOModel_InPlace = new(directory_Dataset);
                yOLOModel_InPlace.Add("Building");
                foreach (Classes.Image image in images_Train)
                {
                    string path_InPlace = image.Path!.ToUpperInvariant();
                    yOLOModel_InPlace.Add(path_InPlace, Enums.Category.Train);
                    yOLOModel_InPlace.Add(path_InPlace, "Building", new Classes.BoundingBox(0.5, 0.5, 0.25, 0.125));
                }

                Assert.True(Modify.Write(yOLOModel_InPlace));
                Assert.Equal(2, Modify.Read(path_Configuration)?.GetImages(Enums.Category.Train).Count());
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
