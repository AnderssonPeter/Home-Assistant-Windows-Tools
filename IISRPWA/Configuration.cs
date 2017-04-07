using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Web;
using System.Xml.Serialization;

namespace IISRPWA
{
    [XmlRoot(ElementName = "Configuration")]
    public class Configuration
    {
        static Configuration instance = null;

        private static string ConfigFilePath
        {
            get
            {
#if WEB
                return HttpContext.Current.Server.MapPath(@"~\Configuration.config");
#else
                return Path.Combine(Directory.GetParent(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)).FullName, "Configuration.config");
#endif
            }
        }

        public static Configuration Instance
        {
            get
            {
                if (instance == null)
                {
                    var ser = new XmlSerializer(typeof(Configuration), new Type[] { typeof(Users), typeof(User), typeof(PathExceptions), typeof(PathException) });
#if !WEB
                    if (!File.Exists(ConfigFilePath))
                    {
                        var config = new Configuration();
                        config.IPExceptions = new IPExceptions();
                        config.PathExceptions = new PathExceptions();
                        config.Users = new Users();
                        //config.IPExceptions.Add(new IPException() { Pattern = "192.168.0.*" });
                        config.PathExceptions.Add(new PathException() { Pattern = "~/.well-known/acme-challenge/*" });
                        config.PathExceptions.Add(new PathException() { Pattern = "~/manifest.json" });
                        config.PathExceptions.Add(new PathException() { Pattern = "~/static/*.png" });
                        config.PathExceptions.Add(new PathException() { Pattern = "~/static/*.ico" });
                        config.PathExceptions.Add(new PathException() { Pattern = "~/favicon.ico" });
                        config.SaveChanges();
                    }

                    using (var stream = File.OpenRead(ConfigFilePath))
                    {
                        instance = (Configuration)ser.Deserialize(stream);
                    }
#endif
                }
                return instance;
            }
        }

        public Users Users
        { get; set; }
        public PathExceptions PathExceptions
        { get; set; }
        public IPExceptions IPExceptions
        { get; set; }

#if !WEB
        public void SaveChanges()
        {
            var ser = new XmlSerializer(typeof(Configuration), new Type[] { typeof(Users), typeof(User), typeof(PathExceptions), typeof(PathException) });
            using (var stream = File.OpenWrite(ConfigFilePath))
            {
                ser.Serialize(stream, this);
                stream.SetLength(stream.Position);
                stream.Flush();
            }
        }
#endif
    }

    public class Users : Collection<User>
    {
    }

    public class User
    {
        [XmlAttribute]
        public string Username
        { get; set; }

        [XmlAttribute]
        public int HashIterations
        { get; set; }

        [XmlAttribute]
        public string PasswordHash
        { get; set; }

        [XmlAttribute]
        public string PasswordSalt
        { get; set; }

        public override string ToString()
        {
            return Username;
        }
    }

    public class PathExceptions : Collection<PathException>
    {
    }

    public class PathException
    {
        [XmlAttribute]
        public string Pattern
        { get; set; }

        public override string ToString()
        {
            return Pattern;
        }
    }

    public class IPExceptions : Collection<IPException>
    {
    }

    public class IPException
    {
        [XmlAttribute]
        public string Pattern
        { get; set; }

        public override string ToString()
        {
            return Pattern;
        }
    }
}