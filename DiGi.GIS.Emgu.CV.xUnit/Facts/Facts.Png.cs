using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Encodes a synthetic image losslessly, so a fact controls every pixel value the code under test decodes.
        /// </summary>
        /// <param name="image">The image to encode.</param>
        /// <returns>The PNG bytes.</returns>
        private static byte[] Png(Image<Bgr, byte> image)
        {
            using VectorOfByte vectorOfByte = new();
            Assert.True(CvInvoke.Imencode(".png", image, vectorOfByte));
            return vectorOfByte.ToArray();
        }
    }
}
