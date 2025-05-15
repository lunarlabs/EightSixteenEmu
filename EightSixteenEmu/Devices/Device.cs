using System.Reflection;
using System.Text.Json.Nodes;

namespace EightSixteenEmu.Devices
{
    public abstract class Device
    {
        public readonly Guid guid;

        public Device(Guid? guid = null)
        {
            this.guid = guid ?? Guid.NewGuid();
        }

        public virtual void Init()
        {
            // Default implementation does nothing
        }

        public virtual void Reset()
        {
            // Default implementation does nothing
        }

        public virtual JsonObject? GetParams()
        {
            // Default implementation returns null
            return null;
        }
        public virtual void SetParams(JsonObject? json)
        {
            // Default implementation does nothing
        }

        public JsonObject GetDefinition()
        {
            JsonObject result = new()
            {
                { "guid", guid.ToString() },
                { "type", GetType().Name },
                { "modulefile", GetType().Assembly.Location },
                { "params", GetParams() }
            };
            return result;
        }

        public virtual JsonObject? GetState()
        {
            // Default implementation returns null
            return null;
        }

        public virtual void SetState(JsonObject? json)
        {
            // Default implementation does nothing
        }
    }
}
