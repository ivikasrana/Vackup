using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Vackup
{
    [DataContract]
    public class AppSettings
    {
        [DataMember(Order = 1)]
        public List<string> Sources { get; set; } = new List<string>();

        [DataMember(Order = 2)]
        public string Destination { get; set; } = "";

        [DataMember(Order = 3)]
        public bool SkipUnchanged { get; set; } = true;

        [DataMember(Order = 4)]
        public bool DatedFolder { get; set; }

        [DataMember(Order = 5)]
        public string Theme { get; set; } = "";

        [DataMember(Order = 6)]
        public double SpeedLimitMb { get; set; }

        public static string FilePath
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vackup");
                return Path.Combine(dir, "settings.json");
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (var stream = File.OpenRead(FilePath))
                    {
                        var loaded = (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(stream);
                        if (loaded != null)
                        {
                            loaded.Sources = loaded.Sources ?? new List<string>();
                            loaded.Destination = loaded.Destination ?? "";
                            return loaded;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var temp = FilePath + ".tmp";
                using (var stream = File.Create(temp))
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, Encoding.UTF8, true, true, "  "))
                {
                    new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(writer, this);
                }
                if (File.Exists(FilePath))
                {
                    File.Replace(temp, FilePath, null);
                }
                else
                {
                    File.Move(temp, FilePath);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
