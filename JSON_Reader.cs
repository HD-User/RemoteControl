using Newtonsoft.Json;

namespace RemoteControl
{
    internal class JSON_Reader
    {
        public int port { get; set; }
        public string PowerOffSchedulerPath { get; set; }

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("config.json"))
            {
                string json = await sr.ReadToEndAsync();
                JSONStructure data = JsonConvert.DeserializeObject<JSONStructure>(json)!;
                port = data.port;
                PowerOffSchedulerPath = data.PowerOffSchedulerPath;
            }
        }
        internal sealed class JSONStructure
        {
            public int port { get; set; }
            public string PowerOffSchedulerPath { set; get; }
        }
    }
}