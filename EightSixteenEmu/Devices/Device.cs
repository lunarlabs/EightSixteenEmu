using System.Reflection;
using System.Text.Json.Nodes;

namespace EightSixteenEmu.Devices
{
    public abstract class Device
    {
        public virtual void Init()
        {
            // Default implementation does nothing
        }

        public virtual void Reset()
        {
            // Default implementation does nothing
        }

        public virtual JsonObject ToJson()
        {
            JsonObject result = new()
            {
                { "type", GetType().Name },
                { "modulefile", Assembly.GetExecutingAssembly().Location },
                { "params", null }
            };
            return result;
        }
    }
}
