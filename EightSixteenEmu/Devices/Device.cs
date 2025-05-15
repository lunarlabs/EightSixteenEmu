using System.Reflection;
using System.Text.Json.Nodes;

namespace EightSixteenEmu.Devices
{
    /// <summary>
    /// Base class for all devices.
    /// </summary>
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
        /// <summary>
        /// Get the definition of the device.
        /// </summary>
        /// <returns>
        /// A JSON object containing the device definition, consisting of:
        /// guid: the device's GUID,
        /// type: the device's type name,
        /// modulefile: the assembly location of the device,
        /// params: the device's parameters.
        /// </returns>
        public JsonObject GetDefinition()
        {
            JsonObject result = new()
            {
                { "guid", guid.ToString() },
                { "type", GetType().FullName },
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
