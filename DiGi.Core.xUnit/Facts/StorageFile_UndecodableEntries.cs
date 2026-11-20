using DiGi.Core.Classes;
using DiGi.Core.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// A value entry whose name does not decode to a unique reference - one written in a former reference format - is reported by <see cref="IO.File.Classes.StorageFile{TSerializableObject}.GetUndecodableEntryNames"/>, and <see cref="IO.File.Classes.StorageFile{TSerializableObject}.Save"/> refuses the file, leaving it byte for byte unchanged (DiGi-Organization/DiGi.GIS.PostgreSQL#149).
        /// <para>A missing file reports null, a file of decodable entries reports none and saves.</para>
        /// </summary>
        [Fact]
        public void StorageFile_UndecodableEntries()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StorageFile_UndecodableEntries_" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using (IO.File.Classes.StorageFile<Address> storageFile = new(path))
                {
                    Assert.Null(storageFile.GetUndecodableEntryNames());
                    storageFile.Open();
                    Assert.NotNull(storageFile.AddValue(new Address("Street 1", "City 1", "Code 1", CountryCode.PL)));
                    Assert.True(storageFile.Save());
                    Assert.Empty(storageFile.GetUndecodableEntryNames()!);
                }

                // The key format the files of 2025 were written in, which Query.Decode no longer reads.
                string entryName = System.IO.Path.Combine(IO.Constants.EntryName.Values, "DiGi.Core.Classes.Address%2cDiGi.Core%3a%3a%22legacy%22");
                using (ZipArchive zipArchive = ZipFile.Open(path, ZipArchiveMode.Update))
                {
                    using StreamWriter streamWriter = new(zipArchive.CreateEntry(entryName).Open());
                    streamWriter.Write(Convert.ToSystem_String(new Address("Street 2", "City 2", "Code 2", CountryCode.PL)));
                }

                byte[] bytes = File.ReadAllBytes(path);
                using (IO.File.Classes.StorageFile<Address> storageFile = new(path))
                {
                    storageFile.Open();
                    List<string>? undecodableEntryNames = storageFile.GetUndecodableEntryNames();
                    Assert.Equal([entryName], undecodableEntryNames);
                    Assert.Equal(1, storageFile.Count);

                    Assert.NotNull(storageFile.AddValue(new Address("Street 3", "City 3", "Code 3", CountryCode.PL)));
                    Assert.False(storageFile.Save());
                }

                Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
