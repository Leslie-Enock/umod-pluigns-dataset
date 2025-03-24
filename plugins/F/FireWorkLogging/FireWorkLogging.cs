using Newtonsoft.Json;
using System.Collections.Generic;
namespace Oxide.Plugins
{
    //https://discord.com/api/oauth2/authorize?client_id=923264306143985724&permissions=8&scope=bot%20applications.commands
    [Info("Firework Logging", "mrcameron999", "1.0.2")]
    [Description("Logs pattern fireworks to a discord bot")]
    public class FireWorkLogging : CovalencePlugin
    {
        private string connection = "https://fireworklogger.gameservertools.com/";

        private float timeout = 1000f;
        private string serverName = string.Empty;
        private Dictionary<string, string> headers = new Dictionary<string, string>(){};

        private void Init()
        {
            LoadConfigData();
        }

        private void LoadConfigData()
        {
            string guildId = Config["GuildId"].ToString();
            string password = Config["Password"].ToString();
            serverName = Config["ServerName"].ToString();

            headers.Add("Content-Type", "application/json");
            headers.Add("GuildId", guildId);
            headers.Add("Password", password);
        }
        protected override void LoadDefaultConfig()
        {
            Config["GuildId"] = 123456789123456789;
            Config["Password"] = "Get your password from the discord bot";
            Config["ServerName"] = "Your Server Name";
        }
        object OnEntityFlagsNetworkUpdate(PatternFirework entity)
        {
            if (!entity.IsOn() || entity.Design == null || entity.Design.stars == null) { return null; }

            List<FireworkPoint> points = new List<FireworkPoint>();

            foreach (ProtoBuf.PatternFirework.Star item in entity.Design.stars)
            {
                points.Add(new FireworkPoint() {x = item.position.x,y = item.position.y, colora = (int)(255.0f * item.color.a), colorr = (int)(255.0f * item.color.r),colorb = (int)(255.0f * item.color.b), colorg = (int)(255.0f * item.color.g) });
            }
            
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters.Add("userId", entity.OwnerID);
            parameters.Add("serverName", serverName);
            parameters.Add("points", points);

            string body = JsonConvert.SerializeObject(parameters);

            try
            {
                webrequest.Enqueue($"{connection}api/Rust/FireWork", body, (code, response) =>
                {
                }, this, Core.Libraries.RequestMethod.POST, headers, timeout);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"FireWorkLog failed {e}");
            }
            
            return null;
        }

        public class FireworkPoint
        {
            public float x { get; set; }
            public float y { get; set; }
            public int colora { get; set; }
            public int colorr { get; set; }
            public int colorg { get; set; }
            public int colorb { get; set; }
        }
    }
}
