using EightSixteenEmu.Devices;
using System.Reflection;
using System.Text.Json.Nodes;

namespace EightSixteenEmu.Factories
{
    public static class DeviceFactory
    {
        public static Device CreateFromJson(JsonObject json)
        {
            string typeName = json["type"]?.ToString();
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("Type name is required.", nameof(json));
            string moduleFile = json["modulefile"]?.ToString();
            if (string.IsNullOrEmpty(moduleFile))
                throw new ArgumentException("Module file location is required.", nameof(json));
            if (!File.Exists(moduleFile))
                throw new FileNotFoundException($"Module file '{moduleFile}' not found.", moduleFile);
            string guidStr = json["guid"]?.ToString();
            JsonObject? paramsObj = json["params"]?.AsObject();

            Assembly assembly = Assembly.LoadFrom(moduleFile);
            Type deviceType = assembly.GetType(typeName) ?? throw new TypeLoadException($"Type '{typeName}' not found in assembly '{moduleFile}'.");
            if (!typeof(Device).IsAssignableFrom(deviceType))
                throw new ArgumentException($"Type '{typeName}' is not a valid Device type.", nameof(json));
            Device result = (Device)Activator.CreateInstance(deviceType, new object[] { paramsObj, Guid.TryParse(guidStr, out Guid guid) ? guid : (Guid?)null }) ?? throw new InvalidOperationException($"Failed to create instance of type '{typeName}'.");

            return result;
        }
    }
}
